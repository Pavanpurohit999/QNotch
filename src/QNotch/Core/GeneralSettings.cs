using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace QNotch.Core;

public enum ThemeChoice { System, Dark, Light }
/// <summary>A real Windows system backdrop for normal windows (DWMWA_SYSTEMBACKDROP_TYPE, Windows 11 22H2+), or solid.</summary>
public enum WindowMaterial { Solid, Mica, MicaAlt, Acrylic }
/// <summary>The notch surface. Glass and Clear are simulated (translucent tint, sheen, rim): Windows cannot blur a layered window.</summary>
public enum IslandMaterial { Solid, Glass, Clear }

/// <summary>Shell settings (general.json). Live object: bind to it, mutate it; the shell persists changes automatically.</summary>
public sealed partial class GeneralSettings : ObservableObject
{
    /// <summary>Index into <c>Monitors.List()</c> (primary is always 0).</summary>
    [ObservableProperty] int _monitorIndex;
    [ObservableProperty] int _hoverDwellMs = 120;
    [ObservableProperty] int _leaveDelayMs = 400;
    [ObservableProperty] bool _pinned;
    // Only the defaults of the "toggle" and "gamemode" shortcuts now (a value saved here before hotkeys.json existed still counts).
    [ObservableProperty] string _toggleHotkey = "Ctrl+Alt+N";
    [ObservableProperty] string _gameModeHotkey = "Ctrl+Alt+G";
    [ObservableProperty] string _accentColor = "mono"; // ThemeManager.Mono: follows the theme (white on dark, black on light)
    [ObservableProperty] ThemeChoice _theme = ThemeChoice.System;
    /// <summary>The appearance preset id (Core/AppearancePresets.cs): a built-in or a custom preset; unknown ids fall back to Orbit.</summary>
    [ObservableProperty] string _themePreset = AppearancePresets.DefaultId;
    [ObservableProperty] bool _reduceMotion;
    /// <summary>Settings window backdrop (Theme/Materials.cs): a real Windows material where supported, else solid.</summary>
    [ObservableProperty] WindowMaterial _windowMaterial = WindowMaterial.Mica;
    /// <summary>The notch's surface. Simulated: the notch is a layered window, which Windows cannot blur.</summary>
    [ObservableProperty] IslandMaterial _islandMaterial = IslandMaterial.Solid;
    /// <summary>Notch surface opacity for Glass and Clear, 0.5 to 1.</summary>
    [ObservableProperty] double _islandOpacity = 0.82;
    /// <summary>Glass rim and sheen strength, 0 to 1.</summary>
    [ObservableProperty] double _glassHighlight = 0.6;
    [ObservableProperty] string _profileName = Environment.UserName;
    [ObservableProperty] string _profileImagePath = "";
    [ObservableProperty] bool _startWithWindows;
    // Local preferences only: setup codes never carry update choices or notification history.
    [ObservableProperty] bool _shareUsageData = false; // Orbit Desk: no telemetry by default (and no analytics key is shipped)
    [ObservableProperty] bool _checkForUpdates = true;
    [ObservableProperty] bool _updateChecksChoiceMade;
    [ObservableProperty] string _lastNotifiedUpdateVersion = "";
    /// <summary>The welcome tour finished, was skipped or closed (or the settings predate it). A local preference: never in setup codes.</summary>
    [ObservableProperty] bool _onboardingDone;
    [ObservableProperty] string _lastTab = "home";

    /// <summary>Ids of modules the user turned off (Settings, Features). They are never created; a change applies on restart.</summary>
    public List<string> DisabledModules { get; set; } = new();
    /// <summary>Explicit local opt-ins for default-off modules. Never included in setup codes.</summary>
    [ObservableProperty] List<string> _localEnabledModules = new();

    /// <summary>Card ids in display order, including hidden ones (hidden cards keep their slot).</summary>
    public List<string> CardOrder { get; set; } = new();

    /// <summary>Tab ids in strip order (Edit mode). Tabs not listed follow in their default order; ids of tabs that are gone keep their slot.</summary>
    public List<string> TabOrder { get; set; } = new();

    /// <summary>Where the notch sits while an app is in front: process name ("chrome.exe") to DIPs from the monitor center
    /// (+-10000 means flush with that edge). Missing means centered. Saved by NotchNudge, not through PropertyChanged.</summary>
    public Dictionary<string, double> NotchSpots { get; set; } = new();

    /// <summary>Explicit visibility overrides by card id; missing means the descriptor default.</summary>
    public Dictionary<string, bool> CardVisible { get; set; } = new();

    /// <summary>Home card size names by id. Unknown or unsupported sizes fall back to the descriptor default.</summary>
    public Dictionary<string, string> CardSizes { get; set; } = new();
}
