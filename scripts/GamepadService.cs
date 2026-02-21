using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using SDL;
using static SDL.SDL3;

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

public sealed unsafe class GamepadService : IDisposable
{
    public event Action<DetectedInput>? InputDetected;
    public event Action<short, short, short, short>? AxisValuesUpdated;

    public event Action? DevicesChanged;
    public event Action<string>? DiagnosticMessage;

    private Thread? _pollThread;
    private volatile bool _running;
    private volatile bool _sdlReady;
    private volatile bool _isListening;
    public bool IsListening
    {
        get => _isListening;
        set => _isListening = value;
    }

    public int ListenAxisThreshold { get; set; } = 16000;

    public string? NativeSdlSearchPath { get; set; }

    private readonly Dictionary<SDL_JoystickID, nint> _openGamepads = new();
    private readonly Dictionary<SDL_JoystickID, nint> _openJoysticks = new();

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
            Name = "SDL3-GamepadPoll"
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

    private void OpenDevice(SDL_JoystickID instanceId)
    {
        if (SDL_IsGamepad(instanceId))
        {
            var gp = SDL_OpenGamepad(instanceId);
            if (gp != null)
            {
                var id = SDL_GetGamepadID(gp);

                if (_openGamepads.ContainsKey(id))
                {
                    SDL_CloseGamepad(gp);
                    Log($"Gamepad already open: id={(uint)id} (skipped)");
                    return;
                }

                _openGamepads[id] = (nint)gp;
                string name = SDL_GetGamepadName(gp) ?? "(unknown)";
                Log($"Opened Gamepad: id={(uint)id}, name='{name}'");
            }
            else
            {
                Log($"SDL_OpenGamepad({(uint)instanceId}) FAILED: {SDL_GetError()}");
            }
        }
        else
        {
            var js = SDL_OpenJoystick(instanceId);
            if (js != null)
            {
                var id = SDL_GetJoystickID(js);

                if (_openJoysticks.ContainsKey(id) || _openGamepads.ContainsKey(id))
                {
                    SDL_CloseJoystick(js);
                    Log($"Joystick already open: id={(uint)id} (skipped)");
                    return;
                }

                int axes = SDL_GetNumJoystickAxes(js);
                int buttons = SDL_GetNumJoystickButtons(js);
                int hats = SDL_GetNumJoystickHats(js);
                string name = SDL_GetJoystickName(js) ?? "(unknown)";
                _openJoysticks[id] = (nint)js;
                Log($"Opened Joystick: id={(uint)id}, name='{name}', axes={axes}, buttons={buttons}, hats={hats}");
            }
            else
            {
                Log($"SDL_OpenJoystick({(uint)instanceId}) FAILED: {SDL_GetError()}");
            }
        }
    }

    private void PollLoop()
    {
        try
        {
            if (!SDL_Init(SDL_InitFlags.SDL_INIT_JOYSTICK | SDL_InitFlags.SDL_INIT_GAMEPAD))
            {
                Log($"SDL_Init FAILED: {SDL_GetError()}");
                return;
            }

            int jsCount;
            using (var joystickIds = SDL3.SDL_GetJoysticks())
            {
                jsCount = joystickIds?.Count ?? 0;
                Log($"SDL_Init OK. Joysticks detected: {jsCount}");

                SDL_SetJoystickEventsEnabled(true);
                SDL_SetGamepadEventsEnabled(true);
                _sdlReady = true;

                if (joystickIds != null)
                {
                    for (int i = 0; i < joystickIds.Count; i++)
                        OpenDevice(joystickIds[i]);
                }
            }

            // if (_openGamepads.Count == 0 && _openJoysticks.Count == 0)
            // {
            //     Log("WARNING: No devices could be opened. Inputs will not work.");
            // }

            int frameCount = 0;
            while (_running)
            {
                try
                {
                    SDL_Event e;
                    while (SDL_PollEvent(&e))
                    {
                        HandleEvent(e);
                    }

                    short lx = 0, ly = 0, rx = 0, ry = 0;
                    bool gotAxes = false;

                    if (_openGamepads.Count > 0)
                    {
                        var firstGp = (SDL_Gamepad*)_openGamepads.Values.First();
                        lx = SDL_GetGamepadAxis(firstGp, SDL_GamepadAxis.SDL_GAMEPAD_AXIS_LEFTX);
                        ly = SDL_GetGamepadAxis(firstGp, SDL_GamepadAxis.SDL_GAMEPAD_AXIS_LEFTY);
                        rx = SDL_GetGamepadAxis(firstGp, SDL_GamepadAxis.SDL_GAMEPAD_AXIS_RIGHTX);
                        ry = SDL_GetGamepadAxis(firstGp, SDL_GamepadAxis.SDL_GAMEPAD_AXIS_RIGHTY);
                        gotAxes = true;
                    }
                    else if (_openJoysticks.Count > 0)
                    {
                        var firstJs = (SDL_Joystick*)_openJoysticks.Values.First();
                        int numAxes = SDL_GetNumJoystickAxes(firstJs);
                        lx = numAxes > 0 ? SDL_GetJoystickAxis(firstJs, 0) : (short)0;
                        ly = numAxes > 1 ? SDL_GetJoystickAxis(firstJs, 1) : (short)0;
                        rx = numAxes > 2 ? SDL_GetJoystickAxis(firstJs, 2) : (short)0;
                        ry = numAxes > 3 ? SDL_GetJoystickAxis(firstJs, 3) : (short)0;
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

            foreach (var kv in _openGamepads)
                SDL_CloseGamepad((SDL_Gamepad*)kv.Value);
            _openGamepads.Clear();

            foreach (var kv in _openJoysticks)
                SDL_CloseJoystick((SDL_Joystick*)kv.Value);
            _openJoysticks.Clear();

            _sdlReady = false;
            SDL_QuitSubSystem(SDL_InitFlags.SDL_INIT_JOYSTICK | SDL_InitFlags.SDL_INIT_GAMEPAD);
        }
        catch (Exception ex)
        {
            Log($"PollLoop CRASHED: {ex}");
        }
    }


    private void HandleEvent(SDL_Event e)
    {
        switch ((SDL_EventType)e.type)
        {
            // ── Device hotplug ──
            case SDL_EventType.SDL_EVENT_JOYSTICK_ADDED:
            {
                OpenDevice(e.jdevice.which);
                DevicesChanged?.Invoke();
                break;
            }
            case SDL_EventType.SDL_EVENT_JOYSTICK_REMOVED:
            {
                var id = e.jdevice.which;
                if (_openGamepads.TryGetValue(id, out var gpPtr))
                {
                    SDL_CloseGamepad((SDL_Gamepad*)gpPtr);
                    _openGamepads.Remove(id);
                    Console.WriteLine($"[GamepadService] Gamepad removed: id={(uint)id}");
                }
                else if (_openJoysticks.TryGetValue(id, out var jsPtr))
                {
                    SDL_CloseJoystick((SDL_Joystick*)jsPtr);
                    _openJoysticks.Remove(id);
                    Console.WriteLine($"[GamepadService] Joystick removed: id={(uint)id}");
                }
                DevicesChanged?.Invoke();
                break;
            }
            case SDL_EventType.SDL_EVENT_JOYSTICK_BUTTON_DOWN:
            {
                int button = e.jbutton.button;
                Console.WriteLine($"[GamepadService] BUTTON DOWN: button={button}, IsListening={IsListening}");
                if (IsListening)
                {
                    var sdlStr = $"button({button})";
                    InputDetected?.Invoke(new DetectedInput(
                        (int)(uint)e.jbutton.which,
                        sdlStr,
                        $"Button {button}"));
                }
                break;
            }

            case SDL_EventType.SDL_EVENT_JOYSTICK_AXIS_MOTION:
            {
                short value = e.jaxis.value;
                if (Math.Abs((int)value) > ListenAxisThreshold)
                {
                    int axis = e.jaxis.axis;
                    string sign = value > 0 ? "+" : "-";
                    Console.WriteLine($"[GamepadService] AXIS MOTION: axis={axis}{sign} val={value}, IsListening={IsListening}");
                    if (IsListening)
                    {
                        var sdlStr = $"axis({axis}{sign})";
                        InputDetected?.Invoke(new DetectedInput(
                            (int)(uint)e.jaxis.which,
                            sdlStr,
                            $"Axis {axis}{sign}"));
                    }
                }
                break;
            }

            case SDL_EventType.SDL_EVENT_JOYSTICK_HAT_MOTION:
            {
                uint hatValue = e.jhat.value;
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
                            (int)(uint)e.jhat.which,
                            sdlStr,
                            $"Hat {hat} {dir}"));
                    }
                }
                break;
            }
        }
    }

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
            using var joysticks = SDL3.SDL_GetJoysticks();
            if (joysticks != null)
            {
                for (int i = 0; i < joysticks.Count; i++)
                {
                    string name = SDL_GetJoystickNameForID(joysticks[i]) ?? $"Joystick {i}";
                    devices.Add((i, name));
                }
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
        using var joysticks = SDL3.SDL_GetJoysticks();
        if (joysticks != null)
        {
            for (int i = 0; i < joysticks.Count; i++)
            {
                string name = SDL_GetJoystickNameForID(joysticks[i]) ?? $"Joystick {i}";
                devices.Add((i, name));
            }
        }
        return devices;
    }
}
