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

/// <summary>
/// Represents a single N64 button/axis mapped to an SDL input string (e.g. "button(0)", "axis(2+)").
/// </summary>
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

public partial class ControllerSetupViewModel : MenuViewModelBase
{
    // ── Navigation ──────────────────────────────────────────────────────
    public override bool CanNavigateReplays { get => false; protected set => throw new InvalidOperationException(); }
    public override bool CanNavigateMain { get => true; protected set => throw new InvalidOperationException(); }
    public override bool CanNavigateOnline { get => false; protected set => throw new InvalidOperationException(); }
    public override bool CanNavigateOffline { get => false; protected set => throw new InvalidOperationException(); }

    private readonly Action<string>? _onNavigateRequested;

    // ── Config path ─────────────────────────────────────────────────────
    private static readonly string CfgPath = Path.Combine(Directory.GetCurrentDirectory(), "mupen64plus.cfg");

    // ── Port selection ──────────────────────────────────────────────────
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

    partial void OnSelectedPortIndexChanged(int value)
    {
        LoadPortBindings();
    }

    private string CurrentSectionName => $"Input-SDL-Control{_selectedPortIndex + 1}";

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

    /// <summary>Maps UI index to the cfg plugin integer (1, 2, 4, 5).</summary>
    private static readonly int[] ExpansionPakValues = { 1, 2, 4, 5 };

    // ── Device info ─────────────────────────────────────────────────────
    [ObservableProperty]
    private string _deviceName = string.Empty;

    [ObservableProperty]
    private int _deviceIndex = -1;

    // ── Current bindings ────────────────────────────────────────────────
    public ObservableCollection<ControllerBinding> Bindings { get; } = new();

    /// <summary>The ordered list of N64 input names as they appear in the cfg.</summary>
    private static readonly (string Key, string Label)[] N64Inputs =
    {
        ("DPad R",      "D-Pad Right"),
        ("DPad L",      "D-Pad Left"),
        ("DPad D",      "D-Pad Down"),
        ("DPad U",      "D-Pad Up"),
        ("Start",       "Start"),
        ("Z Trig",      "Z"),
        ("B Button",    "B"),
        ("A Button",    "A"),
        ("C Button R",  "C-Right"),
        ("C Button L",  "C-Left"),
        ("C Button D",  "C-Down"),
        ("C Button U",  "C-Up"),
        ("R Trig",      "R"),
        ("L Trig",      "L"),
        ("Mempak switch",  "Mempak Switch"),
        ("Rumblepak switch","Rumblepak Switch"),
        ("X Axis",      "Analog X"),
        ("Y Axis",      "Analog Y"),
    };

    // ── Presets ──────────────────────────────────────────────────────────
    public ObservableCollection<ControllerPreset> Presets { get; } = new();

    [ObservableProperty]
    private ControllerPreset? _selectedPreset;

    // ── Status ──────────────────────────────────────────────────────────
    [ObservableProperty]
    private string _statusMessage = string.Empty;

    // ── Ctor ────────────────────────────────────────────────────────────
    public ControllerSetupViewModel(Action<string>? onNavigateRequested = null)
    {
        _onNavigateRequested = onNavigateRequested;

        InitializePresets();
        InitializeBindings();
        LoadPortBindings();
    }

    // ── Initialization helpers ──────────────────────────────────────────

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
        // Default Xbox / XInput preset (matches the typical SDL mapping for Xbox controllers)
        Presets.Add(new ControllerPreset("Xbox / XInput Default", new Dictionary<string, string>
        {
            ["DPad R"]      = "hat(0 Right)",
            ["DPad L"]      = "hat(0 Left)",
            ["DPad D"]      = "hat(0 Down)",
            ["DPad U"]      = "hat(0 Up)",
            ["Start"]       = "button(7)",
            ["Z Trig"]      = "button(6) axis(4+)",
            ["B Button"]    = "button(2)",
            ["A Button"]    = "button(0)",
            ["C Button R"]  = "axis(2+)",
            ["C Button L"]  = "axis(2-) button(3)",
            ["C Button D"]  = "axis(3+) button(1)",
            ["C Button U"]  = "axis(3-)",
            ["R Trig"]      = "button(5) axis(5+)",
            ["L Trig"]      = "button(4)",
            ["Mempak switch"]  = "",
            ["Rumblepak switch"] = "",
            ["X Axis"]      = "axis(0-,0+)",
            ["Y Axis"]      = "axis(1-,1+)",
        }));

        // Alt layout: right stick for C-buttons, triggers for Z/R
        Presets.Add(new ControllerPreset("Xbox Alt (C on Right Stick)", new Dictionary<string, string>
        {
            ["DPad R"]      = "hat(0 Right)",
            ["DPad L"]      = "hat(0 Left)",
            ["DPad D"]      = "hat(0 Down)",
            ["DPad U"]      = "hat(0 Up)",
            ["Start"]       = "button(7)",
            ["Z Trig"]      = "axis(4+)",
            ["B Button"]    = "button(2)",
            ["A Button"]    = "button(0)",
            ["C Button R"]  = "axis(2+)",
            ["C Button L"]  = "axis(2-)",
            ["C Button D"]  = "axis(3+)",
            ["C Button U"]  = "axis(3-)",
            ["R Trig"]      = "button(5) axis(5+)",
            ["L Trig"]      = "button(4)",
            ["Mempak switch"]  = "",
            ["Rumblepak switch"] = "",
            ["X Axis"]      = "axis(0-,0+)",
            ["Y Axis"]      = "axis(1-,1+)",
        }));

        // Keyboard-style: face buttons for C
        Presets.Add(new ControllerPreset("Xbox Face Buttons as C", new Dictionary<string, string>
        {
            ["DPad R"]      = "hat(0 Right)",
            ["DPad L"]      = "hat(0 Left)",
            ["DPad D"]      = "hat(0 Down)",
            ["DPad U"]      = "hat(0 Up)",
            ["Start"]       = "button(7)",
            ["Z Trig"]      = "axis(4+)",
            ["B Button"]    = "button(6)",
            ["A Button"]    = "button(0)",
            ["C Button R"]  = "button(1)",
            ["C Button L"]  = "button(2)",
            ["C Button D"]  = "button(0)",
            ["C Button U"]  = "button(3)",
            ["R Trig"]      = "button(5) axis(5+)",
            ["L Trig"]      = "button(4)",
            ["Mempak switch"]  = "",
            ["Rumblepak switch"] = "",
            ["X Axis"]      = "axis(0-,0+)",
            ["Y Axis"]      = "axis(1-,1+)",
        }));

        // Clear all bindings
        Presets.Add(new ControllerPreset("Clear All", new Dictionary<string, string>(
            N64Inputs.Select(i => new KeyValuePair<string, string>(i.Key, ""))
        )));
    }

    // ── CFG Parsing ─────────────────────────────────────────────────────

    /// <summary>
    /// Reads all lines of the cfg, extracts key-value pairs from the
    /// currently selected Input-SDL-Control section, and populates bindings.
    /// </summary>
    private void LoadPortBindings()
    {
        if (!File.Exists(CfgPath))
        {
            StatusMessage = "mupen64plus.cfg not found.";
            return;
        }

        try
        {
            var lines = File.ReadAllLines(CfgPath);
            var sectionData = ParseSection(lines, CurrentSectionName);

            // Plug state
            IsPluggedIn = GetBool(sectionData, "plugged", true);

            // Device info
            DeviceName = GetString(sectionData, "name", "");
            if (int.TryParse(GetString(sectionData, "device", "-1"), out int dev))
                DeviceIndex = dev;

            // Deadzone / peak
            var dzParts = GetString(sectionData, "AnalogDeadzone", "4096,4096").Trim('"').Split(',');
            if (dzParts.Length > 0 && int.TryParse(dzParts[0], out int dz))
                AnalogDeadzone = dz;

            var pkParts = GetString(sectionData, "AnalogPeak", "32768,32768").Trim('"').Split(',');
            if (pkParts.Length > 0 && int.TryParse(pkParts[0], out int pk))
                AnalogPeak = pk;

            // Expansion pak
            if (int.TryParse(GetString(sectionData, "plugin", "2"), out int plugin))
            {
                int idx = Array.IndexOf(ExpansionPakValues, plugin);
                SelectedExpansionPakIndex = idx >= 0 ? idx : 1; // default Mem Pak
            }

            // Button/axis bindings
            foreach (var binding in Bindings)
            {
                binding.BoundValue = GetString(sectionData, binding.N64InputName, "");
            }

            StatusMessage = $"Loaded {CurrentSectionName}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading config: {ex.Message}";
            Debug.WriteLine($"ControllerSetup LoadPortBindings error: {ex}");
        }
    }

    /// <summary>
    /// Writes the current bindings and settings back into the cfg file,
    /// replacing only the relevant section's values.
    /// </summary>
    [RelayCommand]
    private void SaveBindings()
    {
        if (!File.Exists(CfgPath))
        {
            StatusMessage = "mupen64plus.cfg not found — cannot save.";
            return;
        }

        try
        {
            var lines = File.ReadAllLines(CfgPath).ToList();
            var (start, end) = FindSectionRange(lines, CurrentSectionName);
            if (start < 0)
            {
                StatusMessage = $"Section [{CurrentSectionName}] not found in cfg.";
                return;
            }

            // Build a lookup of new values to write
            var newValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // Controller settings
            newValues["plugged"] = IsPluggedIn ? "True" : "False";
            newValues["device"] = DeviceIndex.ToString();
            newValues["name"] = $"\"{DeviceName}\"";
            newValues["plugin"] = ExpansionPakValues[SelectedExpansionPakIndex].ToString();
            newValues["AnalogDeadzone"] = $"\"{AnalogDeadzone},{AnalogDeadzone}\"";
            newValues["AnalogPeak"] = $"\"{AnalogPeak},{AnalogPeak}\"";
            newValues["mode"] = "2"; // fully automatic

            // Button/axis bindings
            foreach (var binding in Bindings)
            {
                newValues[binding.N64InputName] = $"\"{binding.BoundValue}\"";
            }

            // Replace values in-place within the section
            for (int i = start + 1; i <= end && i < lines.Count; i++)
            {
                var line = lines[i];
                // Skip comments and blank lines
                if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith('#'))
                    continue;

                var eqIdx = line.IndexOf('=');
                if (eqIdx < 0) continue;

                var key = line.Substring(0, eqIdx).Trim();
                if (newValues.TryGetValue(key, out var newVal))
                {
                    lines[i] = $"{key} = {newVal}";
                    newValues.Remove(key); // consumed
                }
            }

            File.WriteAllLines(CfgPath, lines);
            StatusMessage = $"Saved {CurrentSectionName} successfully.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error saving config: {ex.Message}";
            Debug.WriteLine($"ControllerSetup SaveBindings error: {ex}");
        }
    }

    // ── Preset application ──────────────────────────────────────────────

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

    // ── Per-button clear ────────────────────────────────────────────────

    [RelayCommand]
    private void ClearBinding(ControllerBinding? binding)
    {
        if (binding == null) return;
        binding.BoundValue = string.Empty;
    }

    // ── Reset port to defaults ──────────────────────────────────────────

    [RelayCommand]
    private void ResetToDefaults()
    {
        // Apply the first preset (Xbox default) and then save
        if (Presets.Count > 0)
        {
            SelectedPreset = Presets[0];
            ApplyPreset();
            IsPluggedIn = _selectedPortIndex == 0; // only port 1 plugged by default
            DeviceIndex = _selectedPortIndex == 0 ? 0 : -1;
            AnalogDeadzone = 4096;
            AnalogPeak = 32768;
            SelectedExpansionPakIndex = 1; // Mem Pak
            StatusMessage = "Reset to XInput defaults (unsaved).";
        }
    }

    // ── Reload from disk ────────────────────────────────────────────────

    [RelayCommand]
    private void ReloadBindings()
    {
        LoadPortBindings();
    }

    // ── Navigation ──────────────────────────────────────────────────────

    [RelayCommand]
    private void NavigateToMain()
    {
        _onNavigateRequested?.Invoke("Main");
    }

    [RelayCommand]
    private void NavigateToSettings()
    {
        _onNavigateRequested?.Invoke("Settings");
    }

    // ── CFG helpers ─────────────────────────────────────────────────────

    /// <summary>
    /// Parses all key = value pairs from the named INI-style section.
    /// </summary>
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
                    break; // left the section
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

    /// <summary>
    /// Returns the line range (inclusive) of a section: header line and last data line before the next section.
    /// </summary>
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
                    // We've found the next section — end is the previous line
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
            end = lines.Count - 1; // section runs to EOF

        return (start, end);
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
