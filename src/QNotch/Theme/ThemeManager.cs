using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using QNotch.Core;

namespace QNotch.Theme;

/// <summary>Swaps the dark/light dictionary and (re)creates the accent brushes. All styles use DynamicResource so the swap is live.</summary>
public static class ThemeManager
{
    static bool? _dark;
    public static bool IsDark => _dark ?? true;

    /// <summary>Raised after every Apply (theme, accent or system color change), so materials can re-derive their brushes.</summary>
    public static event Action? Changed;

    public static void Apply(GeneralSettings gs)
    {
        var res = Application.Current.Resources;
        var dark = gs.Theme switch { ThemeChoice.Dark => true, ThemeChoice.Light => false, _ => !SystemUsesLightTheme() };
        if (_dark != dark)
        {
            _dark = dark;
            var uri = new Uri($"pack://application:,,,/Theme/Theme.{(dark ? "Dark" : "Light")}.xaml");
            var dict = new ResourceDictionary { Source = uri };
            if (res.MergedDictionaries.Count > 0 && res.MergedDictionaries[0].Source?.OriginalString.Contains("Theme.") == true)
                res.MergedDictionaries[0] = dict;
            else res.MergedDictionaries.Insert(0, dict);
        }

        var accent = AccentFor(gs.AccentColor, dark);
        var lum = (0.299 * accent.R + 0.587 * accent.G + 0.114 * accent.B) / 255;
        res["AccentBrush"] = Frozen(accent);
        res["AccentHoverBrush"] = Frozen(Blend(accent, dark ? Colors.White : Colors.Black, 0.18));
        res["AccentSoftBrush"] = Frozen(Color.FromArgb(0x38, accent.R, accent.G, accent.B));
        var onAccent = lum > 0.55 ? Color.FromRgb(0x0B, 0x0B, 0x0D) : Colors.White;
        res["OnAccentBrush"] = Frozen(onAccent);
        res["ToggleKnobOnBrush"] = Frozen(dark ? onAccent : Colors.White); // Windows 11: dark knob in dark mode, white in light
        Changed?.Invoke();
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
