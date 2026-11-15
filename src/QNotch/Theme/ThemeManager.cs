using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using QNotch.Core;

namespace QNotch.Theme;

/// <summary>
/// Swaps the dark/light dictionary, layers the active appearance preset's palette over it, and (re)creates the accent brushes.
/// All styles use DynamicResource, so every change is live: notch, panel, cards, settings, popups and tooltips follow at once.
/// Merged dictionaries: [0] stock Theme.Dark/Light.xaml, [1] the preset layer (palette overrides), then Styles.xaml.
/// </summary>
public static class ThemeManager
{
    static bool? _dark;
    static ResourceDictionary? _presetLayer;
    public static bool IsDark => _dark ?? true;

    /// <summary>Raised after every Apply (theme, preset, accent or system color change), so materials can re-derive their brushes.</summary>
    public static event Action? Changed;

    /// <summary>The user's custom presets (appearance-presets.json), set at startup and by Settings, Appearance.</summary>
    public static IReadOnlyList<ThemePreset> CustomPresets { get; set; } = [];

    /// <summary>The preset in effect: the chosen one, or Orbit when it is unknown (a deleted custom preset).</summary>
    public static ThemePreset ActivePreset(GeneralSettings gs) =>
        AppearancePresets.Find(gs.ThemePreset, CustomPresets) ?? AppearancePresets.BuiltIns[0];

    /// <summary>Whether a preset renders dark: its own mode, or the Theme setting for presets that follow it.</summary>
    public static bool IsDarkFor(ThemePreset preset, GeneralSettings gs) =>
        preset.Dark ?? gs.Theme switch { ThemeChoice.Dark => true, ThemeChoice.Light => false, _ => !SystemUsesLightTheme() };

    public static void Apply(GeneralSettings gs)
    {
        var res = Application.Current.Resources;
        var preset = ActivePreset(gs);
        var dark = IsDarkFor(preset, gs);
        if (_dark != dark)
        {
            _dark = dark;
            var uri = new Uri($"pack://application:,,,/Theme/Theme.{(dark ? "Dark" : "Light")}.xaml");
            var dict = new ResourceDictionary { Source = uri };
            if (res.MergedDictionaries.Count > 0 && res.MergedDictionaries[0].Source?.OriginalString.Contains("Theme.") == true)
                res.MergedDictionaries[0] = dict;
            else res.MergedDictionaries.Insert(0, dict);
        }

        // The preset layer sits right after the stock palette, so it wins over it and loses to nothing else.
        var layer = PaletteBrushes(preset, dark);
        if (_presetLayer is not null && res.MergedDictionaries.IndexOf(_presetLayer) is var at and >= 0) res.MergedDictionaries[at] = layer;
        else res.MergedDictionaries.Insert(1, layer);
        _presetLayer = layer;

        foreach (var (key, brush) in AccentBrushes(AccentFor(gs.AccentColor, dark), dark)) res[key] = brush;
        Changed?.Invoke();
    }

    /// <summary>The palette brushes of a preset ("SurfaceBrush", ...). Empty for presets that keep the stock palette.</summary>
    public static ResourceDictionary PaletteBrushes(ThemePreset preset, bool dark)
    {
        var layer = new ResourceDictionary();
        if (preset.Colors.Count == 0) return layer;
        foreach (var (key, hex) in AppearancePresets.Palette(preset, dark))
            if (AppearancePresets.TryParseColor(hex, out var a, out var r, out var g, out var b)) layer[key + "Brush"] = Frozen(Color.FromArgb(a, r, g, b));
        return layer;
    }

    /// <summary>The accent brushes for an accent color in a mode (also used by the Appearance preview).</summary>
    public static IEnumerable<(string Key, Brush Brush)> AccentBrushes(Color accent, bool dark)
    {
        var lum = (0.299 * accent.R + 0.587 * accent.G + 0.114 * accent.B) / 255;
        var onAccent = lum > 0.55 ? Color.FromRgb(0x0B, 0x0B, 0x0D) : Colors.White;
        yield return ("AccentBrush", Frozen(accent));
        yield return ("AccentHoverBrush", Frozen(Blend(accent, dark ? Colors.White : Colors.Black, 0.18)));
        yield return ("AccentSoftBrush", Frozen(Color.FromArgb(0x38, accent.R, accent.G, accent.B)));
        yield return ("OnAccentBrush", Frozen(onAccent));
        yield return ("ToggleKnobOnBrush", Frozen(dark ? onAccent : Colors.White)); // Windows 11: dark knob in dark mode, white in light
    }

    /// <summary>The "mono" accent (default) follows the theme: near-white on dark, near-black on light. Anything else is a hex color.</summary>
    public const string Mono = "mono";

    public static Color AccentFor(string? value, bool dark)
    {
        if (string.Equals(value, Mono, StringComparison.OrdinalIgnoreCase))
            return dark ? Color.FromRgb(0xF4, 0xF4, 0xF5) : Color.FromRgb(0x1C, 0x1C, 0x1E);
        try { return (Color)ColorConverter.ConvertFromString(value); }
        catch { return dark ? Color.FromRgb(0xF4, 0xF4, 0xF5) : Color.FromRgb(0x1C, 0x1C, 0x1E); }
    }

    /// <summary>A palette color of the app as it is now (preset layer over the stock palette), for materials that tint with it.</summary>
    public static Color? Current(string brushKey) =>
        Application.Current?.TryFindResource(brushKey) is SolidColorBrush b ? b.Color : null;

    static SolidColorBrush Frozen(Color c) { var b = new SolidColorBrush(c); b.Freeze(); return b; }

    static Color Blend(Color a, Color b, double t) => Color.FromRgb(
        (byte)(a.R + (b.R - a.R) * t), (byte)(a.G + (b.G - a.G) * t), (byte)(a.B + (b.B - a.B) * t));

    static bool SystemUsesLightTheme()
    {
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return k?.GetValue("AppsUseLightTheme") is int v && v != 0;
        }
        catch { return false; }
    }
}
