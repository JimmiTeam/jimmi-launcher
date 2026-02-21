using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace JimmiLauncher.ViewModels;

public partial class ControllerBinding : ObservableObject
{
    public string N64InputName { get; }
    public string DisplayLabel { get; }

    [ObservableProperty]
    private string _boundValue = string.Empty;

    [ObservableProperty]
    private bool _isListening;

    public ControllerBinding(string n64InputName, string displayLabel, string boundValue = "")
    {
        N64InputName = n64InputName;
        DisplayLabel = displayLabel;
        BoundValue = boundValue;
    }
}

public class ControllerPreset
{
    public string Name { get; }
    public Dictionary<string, string> Mappings { get; }

    public ControllerPreset(string name, Dictionary<string, string> mappings)
    {
        Name = name;
        Mappings = mappings;
    }

    public override string ToString() => Name;
}

public partial class ControllerSetupViewModel : MenuViewModelBase, IDisposable
{
    public override bool CanNavigateReplays { get => false; protected set => throw new InvalidOperationException(); }
    public override bool CanNavigateMain { get => true; protected set => throw new InvalidOperationException(); }
    public override bool CanNavigateOnline { get => false; protected set => throw new InvalidOperationException(); }
    public override bool CanNavigateOffline { get => false; protected set => throw new InvalidOperationException(); }

    private readonly Action<string>? _onNavigateRequested;

    // private static readonly string CfgPath = Path.Combine(Directory.GetCurrentDirectory(), "mupen64plus.cfg");
    // private static readonly string CfgPath = "E:/Jimmi/JimmiLauncher/mupen64plus.cfg";
    // private static readonly string AutoCfgPath = "E:/Jimmi/JimmiLauncher/InputAutoCfg.ini";
    private static readonly string CfgPath = "./mupen/mupen64plus.cfg";
    private static readonly string AutoCfgPath = "./mupen/InputAutoCfg.ini";

    public ObservableCollection<string> ControllerPorts { get; } = new()
    {
        "Controller 1",
        "Controller 2",
        "Controller 3",
        "Controller 4"
    };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPluggedIn))]
    private int _selectedPortIndex;

    
    [ObservableProperty]
    private bool _usingRaphnet = Globals.UsingRaphnet;
    partial void OnUsingRaphnetChanged(bool value)
    {
        DatabaseHandler.UpdateRaphnetUsage(value);
    }

    partial void OnSelectedPortIndexChanged(int value)
    {
        LoadPortBindings();
    }

    private string CurrentSectionName => $"Input-SDL-Control{SelectedPortIndex + 1}";

    [ObservableProperty]
    private bool _isPluggedIn = true;

    [ObservableProperty]
    private int _analogDeadzone = 4096;

    [ObservableProperty]
    private int _analogPeak = 32768;

    public ObservableCollection<string> ExpansionPakOptions { get; } = new()
    {
        "None",
        "Mem Pak",
        "Transfer Pak",
        "Rumble Pak"
    };

    [ObservableProperty]
    private int _selectedExpansionPakIndex;

    private static readonly int[] ExpansionPakValues = { 1, 2, 4, 5 };

    [ObservableProperty]
    private string _deviceName = string.Empty;

    [ObservableProperty]
    private int _deviceIndex = -1;

    public ObservableCollection<string> DetectedDevices { get; } = new();

    [ObservableProperty]
    private int _selectedDetectedDeviceIndex = -1;

    partial void OnSelectedDetectedDeviceIndexChanged(int value)
    {
        if (value >= 0 && value < DetectedDevices.Count)
        {
            var entry = DetectedDevices[value];
            var colonIdx = entry.IndexOf(':');
            if (colonIdx >= 0)
            {
                if (int.TryParse(entry[..colonIdx].Trim(), out int idx))
                    DeviceIndex = idx;

                DeviceName = entry[(colonIdx + 1)..].Trim();
            }

            // Load bindings from the matching AutoCfg section
            if (File.Exists(AutoCfgPath))
            {
                var bindingData = ParseAutoCfgSectionByName(File.ReadAllLines(AutoCfgPath), DeviceName);
                if (bindingData.Count > 0)
                {
                    foreach (var binding in Bindings)
                    {
                        if (AxisSplitMap.TryGetValue(binding.N64InputName, out var splitInfo))
                        {
                            var combined = GetString(bindingData, splitInfo.CfgKey, "");
                            binding.BoundValue = SplitAxisValue(combined, splitInfo.IsPositive);
                        }
                        else
                        {
                            binding.BoundValue = GetString(bindingData, binding.N64InputName, "");
                        }
                    }
                    StatusMessage = $"Loaded bindings for: {DeviceName}";
                }
            }
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LeftStickCanvasX))]
    private double _leftStickX = 0.5;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LeftStickCanvasY))]
    private double _leftStickY = 0.5;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RightStickCanvasX))]
    private double _rightStickX = 0.5;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RightStickCanvasY))]
    private double _rightStickY = 0.5;

    private const double StickCanvasSize = 100.0;
    private const double DotRadius = 50.0;

    public double LeftStickCanvasX => LeftStickX * (StickCanvasSize - DotRadius);
    public double LeftStickCanvasY => LeftStickY * (StickCanvasSize - DotRadius);
    public double RightStickCanvasX => RightStickX * (StickCanvasSize - DotRadius);
    public double RightStickCanvasY => RightStickY * (StickCanvasSize - DotRadius);

    private GamepadService? _gamepadService;
    private ControllerBinding? _listeningBinding;

    public ObservableCollection<ControllerBinding> Bindings { get; } = new();

    private static readonly (string Key, string Label)[] N64Inputs =
    {
        ("DPad R", "D-Pad Right"),
        ("DPad L", "D-Pad Left"),
        ("DPad D","D-Pad Down"),
        ("DPad U", "D-Pad Up"),
        ("Start", "Start"),
        ("Z Trig", "Z"),
        ("B Button", "B"),
        ("A Button", "A"),
        ("C Button R", "C-Right"),
        ("C Button L", "C-Left"),
        ("C Button D", "C-Down"),
        ("C Button U", "C-Up"),
        ("R Trig", "R"),
        ("L Trig", "L"),
        ("X Axis L", "Analog Left"),
        ("X Axis R", "Analog Right"),
        ("Y Axis U", "Analog Up"),
        ("Y Axis D", "Analog Down"),
    };

    private static readonly Dictionary<string, (string CfgKey, bool IsPositive)> AxisSplitMap = new()
    {
        ["X Axis L"] = ("X Axis", false),
        ["X Axis R"] = ("X Axis", true),
        ["Y Axis U"] = ("Y Axis", false),
        ["Y Axis D"] = ("Y Axis", true),
    };

    public ObservableCollection<ControllerPreset> Presets { get; } = new();

    [ObservableProperty]
    private ControllerPreset? _selectedPreset;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public ControllerSetupViewModel(Action<string>? onNavigateRequested = null)
    {
        _onNavigateRequested = onNavigateRequested;

        // InitializePresets();
        InitializeBindings();
        LoadPortBindings();
        InitializeGamepadService();
    }


    private void InitializeBindings()
    {
        Bindings.Clear();
        foreach (var (key, label) in N64Inputs)
        {
            Bindings.Add(new ControllerBinding(key, label));
        }
    }

    private void InitializePresets()
    {
        Presets.Add(new ControllerPreset("Xbox / XInput Default", new Dictionary<string, string>
        {
            ["DPad R"] = "hat(0 Right)",
            ["DPad L"] = "hat(0 Left)",
            ["DPad D"] = "hat(0 Down)",
            ["DPad U"] = "hat(0 Up)",
            ["Start"] = "button(7)",
            ["Z Trig"]  = "axis(2-)",
            ["B Button"] = "button(2)",
            ["A Button"] = "button(0)",
            ["C Button R"] = "axis(3+)",
            ["C Button L"] = "axis(3-) button(3)",
            ["C Button D"] = "axis(4+) button(1)",
            ["C Button U"] = "axis(4-)",
            ["R Trig"] = "button(5) axis(5+)",
            ["L Trig"] = "button(4)",
            ["X Axis L"] = "axis(0-)",
            ["X Axis R"] = "axis(0+)",
            ["Y Axis U"] = "axis(1-)",
            ["Y Axis D"] = "axis(1+)",
            
        }));

        Presets.Add(new ControllerPreset("Clear All", new Dictionary<string, string>(
            N64Inputs.Select(i => new KeyValuePair<string, string>(i.Key, ""))
        )));
    }

    private void LoadPortBindings()
    {
        if (!File.Exists(CfgPath))
        {
            StatusMessage = "mupen64plus.cfg not found.";
            return;
        }

        try
        {
            var cfgLines = File.ReadAllLines(CfgPath);
            var sectionData = ParseSection(cfgLines, CurrentSectionName);

            IsPluggedIn = GetBool(sectionData, "plugged", true);

            DeviceName = GetString(sectionData, "name", "");
            if (int.TryParse(GetString(sectionData, "device", "-1"), out int dev))
                DeviceIndex = dev;

            var dzParts = GetString(sectionData, "AnalogDeadzone", "4096,4096").Trim('"').Split(',');
            if (dzParts.Length > 0 && int.TryParse(dzParts[0], out int dz))
                AnalogDeadzone = dz;

            var pkParts = GetString(sectionData, "AnalogPeak", "32768,32768").Trim('"').Split(',');
            if (pkParts.Length > 0 && int.TryParse(pkParts[0], out int pk))
                AnalogPeak = pk;

            if (int.TryParse(GetString(sectionData, "plugin", "2"), out int plugin))
            {
                int idx = Array.IndexOf(ExpansionPakValues, plugin);
                SelectedExpansionPakIndex = idx >= 0 ? idx : 1;
            }

            var bindingData = File.Exists(AutoCfgPath)
                ? ParseAutoCfgSectionByName(File.ReadAllLines(AutoCfgPath), DeviceName)
                : sectionData;

            // Fall back to cfg data if no matching section found in AutoCfg
            if (bindingData.Count == 0)
                bindingData = sectionData;

            foreach (var binding in Bindings)
            {
                if (AxisSplitMap.TryGetValue(binding.N64InputName, out var splitInfo))
                {
                    var combined = GetString(bindingData, splitInfo.CfgKey, "");
                    binding.BoundValue = SplitAxisValue(combined, splitInfo.IsPositive);
                }
                else
                {
                    binding.BoundValue = GetString(bindingData, binding.N64InputName, "");
                }
            }

            StatusMessage = $"Loaded {CurrentSectionName}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading config: {ex.Message}";
            Console.WriteLine($"ControllerSetup LoadPortBindings error: {ex}");
        }
    }

    [RelayCommand]
    private void SaveBindings()
    {
        if (!File.Exists(CfgPath))
        {
            StatusMessage = "mupen64plus.cfg not found.";
            return;
        }

        try
        {
            var lines = File.ReadAllLines(CfgPath).ToList();
            var (start, end) = FindSectionRange(lines, CurrentSectionName);
            if (start < 0)
            {
                // StatusMessage = $"Section [{CurrentSectionName}] not found in cfg.";
                return;
            }

            var newValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            newValues["plugged"] = IsPluggedIn ? "True" : "False";
            newValues["device"] = DeviceIndex.ToString();
            newValues["name"] = $"\"{DeviceName}\"";
            newValues["plugin"] = ExpansionPakValues[SelectedExpansionPakIndex].ToString();
            newValues["AnalogDeadzone"] = $"\"{AnalogDeadzone},{AnalogDeadzone}\"";
            newValues["AnalogPeak"] = $"\"{AnalogPeak},{AnalogPeak}\"";
            newValues["mode"] = "2";

            var bindingValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var axisParts = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            foreach (var binding in Bindings)
            {
                if (AxisSplitMap.TryGetValue(binding.N64InputName, out var splitInfo))
                {
                    if (!axisParts.ContainsKey(splitInfo.CfgKey))
                        axisParts[splitInfo.CfgKey] = new string[2];

                    axisParts[splitInfo.CfgKey][splitInfo.IsPositive ? 1 : 0] = binding.BoundValue;
                }
                else
                {
                    newValues[binding.N64InputName] = $"\"{binding.BoundValue}\"";
                    bindingValues[binding.N64InputName] = binding.BoundValue;
                }
            }

            foreach (var (cfgKey, parts) in axisParts)
            {
                var neg = parts[0] ?? "";
                var pos = parts[1] ?? "";
                var combined = (neg, pos) switch
                {
                    ("", "") => "",
                    _ => $"{ExtractAxisInner(neg)},{ExtractAxisInner(pos)}"
                };
                if (!string.IsNullOrEmpty(combined))
                    combined = $"axis({combined})";
                newValues[cfgKey] = $"\"{combined}\"";
                bindingValues[cfgKey] = combined;
            }

            for (int i = start + 1; i <= end && i < lines.Count; i++)
            {
                var line = lines[i];
                if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith('#'))
                    continue;

                var eqIdx = line.IndexOf('=');
                if (eqIdx < 0) continue;

                var key = line.Substring(0, eqIdx).Trim();
                if (newValues.TryGetValue(key, out var newVal))
                {
                    lines[i] = $"{key} = {newVal}";
                    newValues.Remove(key);
                }
            }

            File.WriteAllLines(CfgPath, lines);

            SaveToAutoCfg(DeviceName, bindingValues);

            StatusMessage = $"Save successful.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error saving config: {ex.Message}";
            Console.WriteLine($"ControllerSetup SaveBindings error: {ex}");
        }
    }

    private static void SaveToAutoCfg(string deviceName, Dictionary<string, string> bindingValues)
    {
        if (!File.Exists(AutoCfgPath)) return;
        if (string.IsNullOrWhiteSpace(deviceName)) return;

        var lines = File.ReadAllLines(AutoCfgPath).ToList();
        var (dataStart, dataEnd) = FindAutoCfgSectionByName(lines, deviceName);
        if (dataStart < 0) return;

        for (int i = dataStart; i <= dataEnd && i < lines.Count; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith(';'))
                continue;
            if (line.TrimStart().StartsWith('['))
                break;

            var eqIdx = line.IndexOf('=');
            if (eqIdx < 0) continue;

            var key = line.Substring(0, eqIdx).Trim();
            if (bindingValues.TryGetValue(key, out var newVal))
            {
                lines[i] = $"{key} = {newVal}";
            }
        }

        File.WriteAllLines(AutoCfgPath, lines);
    }


    [RelayCommand]
    private void ApplyPreset()
    {
        if (SelectedPreset == null) return;

        foreach (var binding in Bindings)
        {
            if (SelectedPreset.Mappings.TryGetValue(binding.N64InputName, out var value))
            {
                binding.BoundValue = value;
            }
        }

        StatusMessage = $"Applied preset: {SelectedPreset.Name}";
    }


    [RelayCommand]
    private void ClearBinding(ControllerBinding? binding)
    {
        if (binding == null) return;
        binding.BoundValue = string.Empty;
    }


    [RelayCommand]
    private void ResetToDefaults()
    {
        if (Presets.Count > 0)
        {
            SelectedPreset = Presets[0];
            // ApplyPreset();
            IsPluggedIn = SelectedPortIndex == 0;
            DeviceIndex = SelectedPortIndex == 0 ? 0 : -1;
            AnalogDeadzone = 4096;
            AnalogPeak = 32768;
            SelectedExpansionPakIndex = 1;
            StatusMessage = "Reset to XInput defaults (unsaved).";
        }
    }


    [RelayCommand]
    private void ReloadBindings()
    {
        LoadPortBindings();
    }

    private void InitializeGamepadService()
    {
        try
        {
            _gamepadService = new GamepadService();
            var mupenFullPath = Path.GetFullPath(Globals.MupenExecutablePath);
            if (File.Exists(mupenFullPath))
            {
                _gamepadService.NativeSdlSearchPath = Path.GetDirectoryName(mupenFullPath);
            }

            _gamepadService.AxisValuesUpdated += OnAxisValuesUpdated;
            _gamepadService.InputDetected += OnInputDetected;
            _gamepadService.DevicesChanged += OnDevicesChanged;
            _gamepadService.DiagnosticMessage += OnDiagnosticMessage;
            _gamepadService.Start();

            Dispatcher.UIThread.InvokeAsync(RefreshDevices, DispatcherPriority.Background);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ControllerSetup] Failed to start GamepadService: {ex.Message}");
            StatusMessage = "Gamepad service unavailable.";
        }
    }

    private void OnDiagnosticMessage(string message)
    {
        Console.WriteLine($"[GamepadDiag] {message}");
        Dispatcher.UIThread.Post(() =>
        {
            StatusMessage = message;
        });
    }

    private void OnAxisValuesUpdated(short lx, short ly, short rx, short ry)
    {
        Dispatcher.UIThread.Post(() =>
        {
            LeftStickX = (lx + 32768.0) / 65535.0;
            LeftStickY = (ly + 32768.0) / 65535.0;
            RightStickX = (rx + 32768.0) / 65535.0;
            RightStickY = (ry + 32768.0) / 65535.0;
        });
    }

    private void OnInputDetected(DetectedInput input)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_listeningBinding != null)
            {
                _listeningBinding.BoundValue = input.SdlString;
                _listeningBinding.IsListening = false;
                _listeningBinding = null;

                if (_gamepadService != null)
                    _gamepadService.IsListening = false;

                StatusMessage = $"Bound: {input.DisplayLabel}";
            }
        });
    }

    private void OnDevicesChanged()
    {
        Dispatcher.UIThread.Post(RefreshDevices);
    }

    [RelayCommand]
    private void ListenForInput(ControllerBinding? binding)
    {
        if (binding == null || _gamepadService == null) return;

        if (_listeningBinding != null)
            _listeningBinding.IsListening = false;

        _listeningBinding = binding;
        binding.IsListening = true;
        _gamepadService.IsListening = true;
        StatusMessage = $"Press a button/axis for: {binding.DisplayLabel}";
    }

    [RelayCommand]
    private void CancelListen()
    {
        if (_listeningBinding != null)
        {
            _listeningBinding.IsListening = false;
            _listeningBinding = null;
        }
        if (_gamepadService != null)
            _gamepadService.IsListening = false;
        StatusMessage = string.Empty;
    }

    [RelayCommand]
    private void RefreshDevices()
    {
        DetectedDevices.Clear();
        try
        {
            var devices = _gamepadService?.GetConnectedDevicesSafe()
                          ?? GamepadService.GetConnectedDevices();
            foreach (var (index, name) in devices)
            {
                DetectedDevices.Add($"{index}: {name}");
            }
            if (DetectedDevices.Count > 0)
                SelectedDetectedDeviceIndex = 0;

            StatusMessage = $"Found {devices.Count} device(s).";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ControllerSetup] RefreshDevices error: {ex.Message}");
            StatusMessage = $"Refresh error: {ex.Message}";
        }
    }

    [RelayCommand]
    private void ToggleRaphnet()
    {
        UsingRaphnet = !UsingRaphnet;
        Globals.UsingRaphnet = UsingRaphnet;
    }

    [RelayCommand]
    private void NavigateToMain()
    {
        Dispose();
        _onNavigateRequested?.Invoke("Main");
    }

    [RelayCommand]
    private void NavigateToSettings()
    {
        Dispose();
        _onNavigateRequested?.Invoke("Settings");
    }

    public void Dispose()
    {
        if (_gamepadService != null)
        {
            _gamepadService.AxisValuesUpdated -= OnAxisValuesUpdated;
            _gamepadService.InputDetected -= OnInputDetected;
            _gamepadService.DevicesChanged -= OnDevicesChanged;
            _gamepadService.DiagnosticMessage -= OnDiagnosticMessage;
            _gamepadService.Dispose();
            _gamepadService = null;
        }
    }

    private static Dictionary<string, string> ParseAutoCfgSectionByName(string[] lines, string deviceName)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(deviceName)) return result;

        deviceName = deviceName.Trim('"');
        var (dataStart, dataEnd) = FindAutoCfgSectionByName(lines, deviceName);
        if (dataStart < 0) return result;

        for (int i = dataStart; i <= dataEnd; i++)
        {
            var trimmed = lines[i].Trim();
            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith(';'))
                continue;
            if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
                break;

            var eqIdx = trimmed.IndexOf('=');
            if (eqIdx < 0) continue;

            var key = trimmed[..eqIdx].Trim();
            var val = trimmed[(eqIdx + 1)..].Trim().Trim('"');
            result[key] = val;
        }

        return result;
    }

    /// <summary>
    /// Finds the data line range (start, end) for the section in InputAutoCfg.ini
    /// whose header matches the given device name. Handles consecutive headers
    /// that share the same data block.
    /// </summary>
    private static (int DataStart, int DataEnd) FindAutoCfgSectionByName(IReadOnlyList<string> lines, string deviceName)
    {
        if (string.IsNullOrWhiteSpace(deviceName)) return (-1, -1);

        deviceName = deviceName.Trim('"');
        int dataStart = -1;
        bool currentGroupMatches = false;
        bool readingData = false;

        for (int i = 0; i < lines.Count; i++)
        {
            var trimmed = lines[i].Trim();
            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith(';'))
                continue;

            if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
            {
                if (readingData)
                {
                    if (currentGroupMatches)
                        return (dataStart, i - 1);

                    currentGroupMatches = false;
                    readingData = false;
                }

                var sectionName = trimmed[1..^1].Trim();
                if (MatchesDeviceName(sectionName, deviceName))
                    currentGroupMatches = true;
            }
            else
            {
                if (!readingData)
                    readingData = true;

                if (currentGroupMatches && dataStart < 0)
                    dataStart = i;
            }
        }

        if (currentGroupMatches && dataStart >= 0)
            return (dataStart, lines.Count - 1);

        return (-1, -1);
    }

    /// <summary>
    /// Checks whether a section header name matches the target device name.
    /// Uses "starts with" so that e.g. device "raphnet technologies N64 to USB v3.6"
    /// matches section "[raphnet technologies N64 to USB]".
    /// Also strips common platform prefixes before comparing.
    /// </summary>
    private static bool MatchesDeviceName(string sectionName, string deviceName)
    {
        if (deviceName.StartsWith(sectionName, StringComparison.OrdinalIgnoreCase))
            return true;

        // Try stripping common platform prefixes from section name
        string[] prefixes = { "Win32: ", "Linux: ", "XInput: " };
        foreach (var prefix in prefixes)
        {
            if (sectionName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                var stripped = sectionName[prefix.Length..];
                if (deviceName.StartsWith(stripped, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        return false;
    }

    private static Dictionary<string, string> ParseSection(string[] lines, string sectionName)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        bool inSection = false;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
            {
                var name = trimmed[1..^1];
                if (string.Equals(name, sectionName, StringComparison.OrdinalIgnoreCase))
                {
                    inSection = true;
                    continue;
                }
                else if (inSection)
                {
                    break;
                }
            }

            if (!inSection) continue;
            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith('#')) continue;

            var eqIdx = trimmed.IndexOf('=');
            if (eqIdx < 0) continue;

            var key = trimmed[..eqIdx].Trim();
            var val = trimmed[(eqIdx + 1)..].Trim().Trim('"');
            result[key] = val;
        }

        return result;
    }

    private static (int Start, int End) FindSectionRange(List<string> lines, string sectionName)
    {
        int start = -1;
        int end = -1;

        for (int i = 0; i < lines.Count; i++)
        {
            var trimmed = lines[i].Trim();
            if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
            {
                var name = trimmed[1..^1];
                if (start >= 0)
                {
                    end = i - 1;
                    break;
                }
                if (string.Equals(name, sectionName, StringComparison.OrdinalIgnoreCase))
                {
                    start = i;
                }
            }
        }

        if (start >= 0 && end < 0)
            end = lines.Count - 1;

        return (start, end);
    }

    private static string SplitAxisValue(string combined, bool isPositive)
    {
        if (string.IsNullOrWhiteSpace(combined))
            return "";

        // Unwrap axis() wrapper first, e.g. "axis(0-,0+)" → "0-,0+"
        var inner = combined.Trim();
        if (inner.StartsWith("axis(", StringComparison.OrdinalIgnoreCase) && inner.EndsWith(")"))
            inner = inner[5..^1];

        var commaIdx = inner.IndexOf(',');
        if (commaIdx < 0)
        {
            // Single value — assign to negative side only
            return isPositive ? "" : $"axis({inner})";
        }

        var part = isPositive
            ? inner[(commaIdx + 1)..].Trim()
            : inner[..commaIdx].Trim();

        return string.IsNullOrEmpty(part) ? "" : $"axis({part})";
    }

    /// <summary>
    /// Extracts the inner value from an axis string, e.g. "axis(0-)" → "0-".
    /// Returns the original string if it doesn't match the axis() pattern.
    /// </summary>
    private static string ExtractAxisInner(string value)
    {
        value = value.Trim();
        if (value.StartsWith("axis(", StringComparison.OrdinalIgnoreCase) && value.EndsWith(")"))
            return value[5..^1];
        return value;
    }

    private static string GetString(Dictionary<string, string> data, string key, string defaultValue)
        => data.TryGetValue(key, out var v) ? v : defaultValue;

    private static bool GetBool(Dictionary<string, string> data, string key, bool defaultValue)
    {
        if (data.TryGetValue(key, out var v))
            return v.Equals("True", StringComparison.OrdinalIgnoreCase);
        return defaultValue;
    }
}
