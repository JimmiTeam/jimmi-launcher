using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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

    private Thread? _pollThread;
    private volatile bool _running;
    public bool IsListening { get; set; }

    public int ListenAxisThreshold { get; set; } = 16000;

    public string? NativeSdlSearchPath { get; set; }

    public void Start()
    {
        if (_running) return;

        if (!string.IsNullOrEmpty(NativeSdlSearchPath) && Directory.Exists(NativeSdlSearchPath))
        {
            var currentPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            if (!currentPath.Contains(NativeSdlSearchPath, StringComparison.OrdinalIgnoreCase))
            {
                Environment.SetEnvironmentVariable("PATH", NativeSdlSearchPath + ";" + currentPath);
                Debug.WriteLine($"[GamepadService] Prepended to PATH: {NativeSdlSearchPath}");
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

    private void PollLoop()
    {
        try
        {
            if (SDL_Init(SDL_INIT_JOYSTICK | SDL_INIT_GAMECONTROLLER) < 0)
            {
                Debug.WriteLine($"[GamepadService] SDL_Init failed: {SDL_GetError()}");
                return;
            }

            SDL_JoystickEventState(SDL_ENABLE);

            var openJoysticks = new Dictionary<int, IntPtr>();
            for (int i = 0; i < SDL_NumJoysticks(); i++)
            {
                var js = SDL_JoystickOpen(i);
                if (js != IntPtr.Zero)
                {
                    var id = SDL_JoystickInstanceID(js);
                    openJoysticks[id] = js;
                }
            }

            while (_running)
            {
                while (SDL_PollEvent(out var e) != 0)
                {
                    HandleEvent(e, openJoysticks);
                }

                if (openJoysticks.Count > 0)
                {
                    IntPtr firstJs = IntPtr.Zero;
                    foreach (var kv in openJoysticks)
                    {
                        firstJs = kv.Value;
                        break;
                    }

                    if (firstJs != IntPtr.Zero)
                    {
                        int numAxes = SDL_JoystickNumAxes(firstJs);
                        short lx = numAxes > 0 ? SDL_JoystickGetAxis(firstJs, 0) : (short)0;
                        short ly = numAxes > 1 ? SDL_JoystickGetAxis(firstJs, 1) : (short)0;
                        short rx = numAxes > 2 ? SDL_JoystickGetAxis(firstJs, 2) : (short)0;
                        short ry = numAxes > 3 ? SDL_JoystickGetAxis(firstJs, 3) : (short)0;

                        AxisValuesUpdated?.Invoke(lx, ly, rx, ry);
                    }
                }

                Thread.Sleep(16);
            }

            foreach (var kv in openJoysticks)
            {
                SDL_JoystickClose(kv.Value);
            }

            SDL_QuitSubSystem(SDL_INIT_JOYSTICK | SDL_INIT_GAMECONTROLLER);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GamepadService] PollLoop exception: {ex}");
        }
    }


    private void HandleEvent(SDL_Event e, Dictionary<int, IntPtr> openJoysticks)
    {
        switch (e.type)
        {
            case SDL_EventType.SDL_JOYDEVICEADDED:
            {
                int deviceIndex = e.jdevice.which;
                var js = SDL_JoystickOpen(deviceIndex);
                if (js != IntPtr.Zero)
                {
                    var id = SDL_JoystickInstanceID(js);
                    openJoysticks[id] = js;
                    Debug.WriteLine($"[GamepadService] Joystick added: index={deviceIndex}, id={id}");
                }
                DevicesChanged?.Invoke();
                break;
            }

            case SDL_EventType.SDL_JOYDEVICEREMOVED:
            {
                int instanceId = e.jdevice.which;
                if (openJoysticks.TryGetValue(instanceId, out var js))
                {
                    SDL_JoystickClose(js);
                    openJoysticks.Remove(instanceId);
                    Debug.WriteLine($"[GamepadService] Joystick removed: id={instanceId}");
                }
                DevicesChanged?.Invoke();
                break;
            }

            case SDL_EventType.SDL_JOYBUTTONDOWN when IsListening:
            {
                int button = e.jbutton.button;
                var sdlStr = $"button({button})";
                InputDetected?.Invoke(new DetectedInput(
                    e.jbutton.which,
                    sdlStr,
                    $"Button {button}"));
                break;
            }

            case SDL_EventType.SDL_JOYAXISMOTION when IsListening:
            {
                short value = e.jaxis.axisValue;
                if (Math.Abs(value) > ListenAxisThreshold)
                {
                    int axis = e.jaxis.axis;
                    string sign = value > 0 ? "+" : "-";
                    var sdlStr = $"axis({axis}{sign})";
                    InputDetected?.Invoke(new DetectedInput(
                        e.jaxis.which,
                        sdlStr,
                        $"Axis {axis}{sign}"));
                }
                break;
            }

            case SDL_EventType.SDL_JOYHATMOTION when IsListening:
            {
                byte hatValue = e.jhat.hatValue;
                if (hatValue != SDL_HAT_CENTERED)
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
                break;
            }
        }
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
