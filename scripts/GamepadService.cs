using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using static SDL2.SDL;

namespace JimmiLauncher;

public readonly struct DetectedInput
{
    public int DeviceIndex { get; }

    public string SdlString { get; }
    public string DisplayLabel { get; }

    public DetectedInput(int deviceIndex, string sdlString, string displayLabel)
    {
        DeviceIndex = deviceIndex;
        SdlString = sdlString;
        DisplayLabel = displayLabel;
    }
}

public sealed class GamepadService : IDisposable
{
    public event Action<DetectedInput>? InputDetected;
    public event Action<short, short, short, short>? AxisValuesUpdated;

    public event Action? DevicesChanged;
    public event Action<string>? DiagnosticMessage;

    private Thread? _pollThread;
    private volatile bool _running;
    private volatile bool _sdlReady;
    public bool IsListening { get; set; }

    public int ListenAxisThreshold { get; set; } = 16000;

    public string? NativeSdlSearchPath { get; set; }

    // Track game controllers (Xbox/XInput) separately from plain joysticks
    private readonly Dictionary<int, IntPtr> _openControllers = new(); // instanceId -> SDL_GameController*
    private readonly Dictionary<int, IntPtr> _openJoysticks = new();   // instanceId -> SDL_Joystick* (non-gamecontroller only)

    private void Log(string msg)
    {
        Console.WriteLine($"[GamepadService] {msg}");
        DiagnosticMessage?.Invoke(msg);
    }

    public void Start()
    {
        if (_running) return;

        if (!string.IsNullOrEmpty(NativeSdlSearchPath) && Directory.Exists(NativeSdlSearchPath))
        {
            var currentPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            if (!currentPath.Contains(NativeSdlSearchPath, StringComparison.OrdinalIgnoreCase))
            {
                Environment.SetEnvironmentVariable("PATH", NativeSdlSearchPath + ";" + currentPath);
                Console.WriteLine($"[GamepadService] Prepended to PATH: {NativeSdlSearchPath}");
            }
        }

        _running = true;
        _pollThread = new Thread(PollLoop)
        {
            IsBackground = true,
            Name = "SDL2-GamepadPoll"
        };
        _pollThread.Start();
    }

    public void Stop()
    {
        _running = false;
        _pollThread?.Join(timeout: TimeSpan.FromSeconds(2));
        _pollThread = null;
    }

    public void Dispose() => Stop();

    private void OpenDevice(int deviceIndex)
    {
        if (SDL_IsGameController(deviceIndex) == SDL_bool.SDL_TRUE)
        {
            var gc = SDL_GameControllerOpen(deviceIndex);
            if (gc != IntPtr.Zero)
            {
                var js = SDL_GameControllerGetJoystick(gc);
                var id = SDL_JoystickInstanceID(js);

                if (_openControllers.ContainsKey(id))
                {
                    SDL_GameControllerClose(gc);
                    Log($"GameController already open: index={deviceIndex}, id={id} (skipped)");
                    return;
                }

                _openControllers[id] = gc;
                string name = SDL_GameControllerName(gc) ?? "(unknown)";
                Log($"Opened GameController {deviceIndex}: id={id}, name='{name}'");
            }
            else
            {
                Log($"SDL_GameControllerOpen({deviceIndex}) FAILED: {SDL_GetError()}");
            }
        }
        else
        {
            var js = SDL_JoystickOpen(deviceIndex);
            if (js != IntPtr.Zero)
            {
                var id = SDL_JoystickInstanceID(js);

                if (_openJoysticks.ContainsKey(id) || _openControllers.ContainsKey(id))
                {
                    SDL_JoystickClose(js);
                    Log($"Joystick already open: index={deviceIndex}, id={id} (skipped)");
                    return;
                }

                int axes = SDL_JoystickNumAxes(js);
                int buttons = SDL_JoystickNumButtons(js);
                int hats = SDL_JoystickNumHats(js);
                string name = SDL_JoystickName(js) ?? "(unknown)";
                _openJoysticks[id] = js;
                Log($"Opened Joystick {deviceIndex}: id={id}, name='{name}', axes={axes}, buttons={buttons}, hats={hats}");
            }
            else
            {
                Log($"SDL_JoystickOpen({deviceIndex}) FAILED: {SDL_GetError()}");
            }
        }
    }

    private void PollLoop()
    {
        try
        {
            int initResult = SDL_Init(SDL_INIT_JOYSTICK | SDL_INIT_GAMECONTROLLER);
            if (initResult < 0)
            {
                Log($"SDL_Init FAILED ({initResult}): {SDL_GetError()}");
                return;
            }
            Log($"SDL_Init OK. Joysticks detected: {SDL_NumJoysticks()}");

            SDL_JoystickEventState(SDL_ENABLE);
            SDL_GameControllerEventState(SDL_ENABLE);
            _sdlReady = true;

            int jsCount = SDL_NumJoysticks();
            for (int i = 0; i < jsCount; i++)
            {
                OpenDevice(i);
            }

            if (_openControllers.Count == 0 && _openJoysticks.Count == 0)
            {
                Log("WARNING: No devices could be opened. Inputs will not work.");
            }

            int frameCount = 0;
            while (_running)
            {
                try
                {
                    while (SDL_PollEvent(out var e) != 0)
                    {
                        HandleEvent(e);
                    }

                    // Read axes from the first available device
                    short lx = 0, ly = 0, rx = 0, ry = 0;
                    bool gotAxes = false;

                    if (_openControllers.Count > 0)
                    {
                        var firstGc = _openControllers.Values.First();
                        lx = SDL_GameControllerGetAxis(firstGc, SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_LEFTX);
                        ly = SDL_GameControllerGetAxis(firstGc, SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_LEFTY);
                        rx = SDL_GameControllerGetAxis(firstGc, SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_RIGHTX);
                        ry = SDL_GameControllerGetAxis(firstGc, SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_RIGHTY);
                        gotAxes = true;
                    }
                    else if (_openJoysticks.Count > 0)
                    {
                        var firstJs = _openJoysticks.Values.First();
                        int numAxes = SDL_JoystickNumAxes(firstJs);
                        lx = numAxes > 0 ? SDL_JoystickGetAxis(firstJs, 0) : (short)0;
                        ly = numAxes > 1 ? SDL_JoystickGetAxis(firstJs, 1) : (short)0;
                        rx = numAxes > 2 ? SDL_JoystickGetAxis(firstJs, 2) : (short)0;
                        ry = numAxes > 3 ? SDL_JoystickGetAxis(firstJs, 3) : (short)0;
                        gotAxes = true;
                    }

                    if (gotAxes)
                    {
                        if (frameCount % 60 == 0)
                        {
                            Console.WriteLine($"[GamepadService] Axes: lx={lx} ly={ly} rx={rx} ry={ry} | subscribers={AxisValuesUpdated != null}");
                        }
                        AxisValuesUpdated?.Invoke(lx, ly, rx, ry);
                    }
                }
                catch (Exception innerEx)
                {
                    Log($"PollLoop iteration error: {innerEx.Message}");
                }

                frameCount++;
                Thread.Sleep(16);
            }

            foreach (var kv in _openControllers)
                SDL_GameControllerClose(kv.Value);
            _openControllers.Clear();

            foreach (var kv in _openJoysticks)
                SDL_JoystickClose(kv.Value);
            _openJoysticks.Clear();

            _sdlReady = false;
            SDL_QuitSubSystem(SDL_INIT_JOYSTICK | SDL_INIT_GAMECONTROLLER);
        }
        catch (Exception ex)
        {
            Log($"PollLoop CRASHED: {ex}");
        }
    }


    private void HandleEvent(SDL_Event e)
    {
        switch (e.type)
        {
            // ── Device hotplug ──
            case SDL_EventType.SDL_JOYDEVICEADDED:
            {
                int deviceIndex = e.jdevice.which;
                OpenDevice(deviceIndex);
                DevicesChanged?.Invoke();
                break;
            }

            case SDL_EventType.SDL_JOYDEVICEREMOVED:
            {
                int instanceId = e.jdevice.which;
                if (_openControllers.TryGetValue(instanceId, out var gc))
                {
                    SDL_GameControllerClose(gc);
                    _openControllers.Remove(instanceId);
                    Console.WriteLine($"[GamepadService] GameController removed: id={instanceId}");
                }
                else if (_openJoysticks.TryGetValue(instanceId, out var js))
                {
                    SDL_JoystickClose(js);
                    _openJoysticks.Remove(instanceId);
                    Console.WriteLine($"[GamepadService] Joystick removed: id={instanceId}");
                }
                DevicesChanged?.Invoke();
                break;
            }

            // ── GameController events (Xbox/XInput) ──
            case SDL_EventType.SDL_CONTROLLERBUTTONDOWN:
            {
                int button = e.cbutton.button;
                // Map SDL GameController button to mupen "button(N)" format using the underlying joystick button
                var gc = IntPtr.Zero;
                if (_openControllers.TryGetValue(e.cbutton.which, out gc))
                {
                    // Get the underlying joystick button index for this controller button
                    var bind = SDL_GameControllerGetBindForButton(gc, (SDL_GameControllerButton)button);
                    int joyButton = bind.bindType == SDL_GameControllerBindType.SDL_CONTROLLER_BINDTYPE_BUTTON
                        ? bind.value.button
                        : button;

                    Console.WriteLine($"[GamepadService] CONTROLLER BUTTON DOWN: gcBtn={button}, joyBtn={joyButton}, IsListening={IsListening}");
                    if (IsListening)
                    {
                        var sdlStr = $"button({joyButton})";
                        InputDetected?.Invoke(new DetectedInput(
                            e.cbutton.which,
                            sdlStr,
                            $"Button {joyButton}"));
                    }
                }
                break;
            }

            case SDL_EventType.SDL_CONTROLLERAXISMOTION:
            {
                short value = e.caxis.axisValue;
                if (Math.Abs(value) > ListenAxisThreshold)
                {
                    int axis = e.caxis.axis;
                    // Map SDL GameController axis to mupen "axis(N+/-)" format using the underlying joystick axis
                    var gc = IntPtr.Zero;
                    if (_openControllers.TryGetValue(e.caxis.which, out gc))
                    {
                        var bind = SDL_GameControllerGetBindForAxis(gc, (SDL_GameControllerAxis)axis);
                        int joyAxis = bind.bindType == SDL_GameControllerBindType.SDL_CONTROLLER_BINDTYPE_AXIS
                            ? bind.value.axis
                            : axis;

                        string sign = value > 0 ? "+" : "-";
                        Console.WriteLine($"[GamepadService] CONTROLLER AXIS: gcAxis={axis}, joyAxis={joyAxis}{sign} val={value}, IsListening={IsListening}");
                        if (IsListening)
                        {
                            var sdlStr = $"axis({joyAxis}{sign})";
                            InputDetected?.Invoke(new DetectedInput(
                                e.caxis.which,
                                sdlStr,
                                $"Axis {joyAxis}{sign}"));
                        }
                    }
                }
                break;
            }

            // ── Plain Joystick events (non-Xbox devices) ──
            case SDL_EventType.SDL_JOYBUTTONDOWN:
            {
                // Skip if this joystick is managed as a game controller
                if (_openControllers.ContainsKey(e.jbutton.which))
                    break;

                int button = e.jbutton.button;
                Console.WriteLine($"[GamepadService] BUTTON DOWN: button={button}, IsListening={IsListening}");
                if (IsListening)
                {
                    var sdlStr = $"button({button})";
                    InputDetected?.Invoke(new DetectedInput(
                        e.jbutton.which,
                        sdlStr,
                        $"Button {button}"));
                }
                break;
            }

            case SDL_EventType.SDL_JOYAXISMOTION:
            {
                if (_openControllers.ContainsKey(e.jaxis.which))
                    break;

                short value = e.jaxis.axisValue;
                if (Math.Abs(value) > ListenAxisThreshold)
                {
                    int axis = e.jaxis.axis;
                    string sign = value > 0 ? "+" : "-";
                    Console.WriteLine($"[GamepadService] AXIS MOTION: axis={axis}{sign} val={value}, IsListening={IsListening}");
                    if (IsListening)
                    {
                        var sdlStr = $"axis({axis}{sign})";
                        InputDetected?.Invoke(new DetectedInput(
                            e.jaxis.which,
                            sdlStr,
                            $"Axis {axis}{sign}"));
                    }
                }
                break;
            }

            case SDL_EventType.SDL_JOYHATMOTION:
            {
                if (_openControllers.ContainsKey(e.jhat.which))
                    break;

                byte hatValue = e.jhat.hatValue;
                if (hatValue != SDL_HAT_CENTERED)
                {
                    Console.WriteLine($"[GamepadService] HAT MOTION: hat={e.jhat.hat} val={hatValue}, IsListening={IsListening}");
                    if (IsListening)
                    {
                        int hat = e.jhat.hat;
                        string dir = hatValue switch
                        {
                            SDL_HAT_UP => "Up",
                            SDL_HAT_DOWN => "Down",
                            SDL_HAT_LEFT => "Left",
                            SDL_HAT_RIGHT => "Right",
                            SDL_HAT_RIGHTUP => "Right",
                            SDL_HAT_RIGHTDOWN => "Right",
                            SDL_HAT_LEFTUP => "Left",
                            SDL_HAT_LEFTDOWN => "Left",
                            _ => "Up"
                        };
                        var sdlStr = $"hat({hat} {dir})";
                        InputDetected?.Invoke(new DetectedInput(
                            e.jhat.which,
                            sdlStr,
                            $"Hat {hat} {dir}"));
                    }
                }
                break;
            }
        }
    }

    /// <summary>
    /// Query connected devices. Should be called from the poll thread for thread safety,
    /// but works from other threads in practice for simple queries.
    /// </summary>
    public List<(int Index, string Name)> GetConnectedDevicesSafe()
    {
        var devices = new List<(int, string)>();
        if (!_sdlReady)
        {
            Console.WriteLine("[GamepadService] GetConnectedDevicesSafe called before SDL ready");
            return devices;
        }
        try
        {
            int count = SDL_NumJoysticks();
            for (int i = 0; i < count; i++)
            {
                string name = SDL_JoystickNameForIndex(i) ?? $"Joystick {i}";
                devices.Add((i, name));
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GamepadService] GetConnectedDevicesSafe error: {ex.Message}");
        }
        return devices;
    }

    public static List<(int Index, string Name)> GetConnectedDevices()
    {
        var devices = new List<(int, string)>();
        int count = SDL_NumJoysticks();
        for (int i = 0; i < count; i++)
        {
            string name = SDL_JoystickNameForIndex(i) ?? $"Joystick {i}";
            devices.Add((i, name));
        }
        return devices;
    }
}
