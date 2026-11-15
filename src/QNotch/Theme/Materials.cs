using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using QNotch.Core;
using QNotch.Interop;

namespace QNotch.Theme;

/// <summary>
/// The material engine. Normal windows get a real Windows system backdrop (Mica, Mica Alt, Acrylic) through DWM where the OS
/// supports it; the notch, a layered window that Windows cannot blur, gets a simulated glass made of frozen brushes. Both are
/// resource overrides on the window, so every DynamicResource below them follows, and both re-apply when the theme changes.
/// Nothing here animates or redraws on its own: a material costs nothing while idle.
/// </summary>
public static class Materials
{
    const int DwmwaSystemBackdropType = 38; // DWMWA_SYSTEMBACKDROP_TYPE, Windows 11 22H2 (build 22621) and later
    const int DwmwaCaptionColor = 35, DwmwaBorderColor = 34, DwmwaDarkMode = 20;
    const int DwmColorDefault = unchecked((int)0xFFFFFFFF);

    /// <summary>True when Windows offers DWMWA_SYSTEMBACKDROP_TYPE. Earlier builds get the solid fallback.</summary>
    public static bool BackdropsSupported { get; } = Environment.OSVersion.Version.Build >= 22621;

    /// <summary>The backdrop type DWM expects for a material (DWM_SYSTEMBACKDROP_TYPE): none 1, Mica 2, Acrylic 3, Mica Alt 4.</summary>
    public static int BackdropType(WindowMaterial m) => m switch
    {
        WindowMaterial.Mica => 2, WindowMaterial.Acrylic => 3, WindowMaterial.MicaAlt => 4, _ => 1,
    };

    /// <summary>The material a request actually gets on this system (solid where backdrops are unsupported).</summary>
    public static WindowMaterial Effective(WindowMaterial wanted) => BackdropsSupported ? wanted : WindowMaterial.Solid;

    static readonly string[] WindowKeys = ["WindowBrush", "SurfaceRaisedBrush", "CardBrush", "CardHoverBrush"];

    /// <summary>
    /// Applies <paramref name="wanted"/> to a normal (not layered) window after its handle exists, and returns what was applied.
    /// Any failing DWM call falls back to solid, so the window is never left half transparent.
    /// </summary>
    public static WindowMaterial ApplyWindow(Window window, WindowMaterial wanted)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == 0 || HwndSource.FromHwnd(hwnd) is not { CompositionTarget: { } target }) return WindowMaterial.Solid;
        var dark = ThemeManager.IsDark;
        var applied = Effective(wanted);
        if (applied != WindowMaterial.Solid)
        {
            var margins = new MARGINS(-1);
            var type = BackdropType(applied);
            var hr = Native.DwmExtendFrameIntoClientArea(hwnd, ref margins);
            if (hr >= 0) hr = Native.DwmSetWindowAttribute(hwnd, DwmwaSystemBackdropType, ref type, sizeof(int));
            if (hr < 0)
            {
                Log.Warn($"The {applied} backdrop is unavailable (HRESULT 0x{hr:X8}); using a solid window.");
                applied = WindowMaterial.Solid;
            }
        }

        int darkMode = dark ? 1 : 0;
        Native.DwmSetWindowAttribute(hwnd, DwmwaDarkMode, ref darkMode, sizeof(int));
        if (applied == WindowMaterial.Solid)
        {
            if (BackdropsSupported) { var none = BackdropType(WindowMaterial.Solid); Native.DwmSetWindowAttribute(hwnd, DwmwaSystemBackdropType, ref none, sizeof(int)); }
            var zero = new MARGINS(0);
            Native.DwmExtendFrameIntoClientArea(hwnd, ref zero);
            foreach (var key in WindowKeys) window.Resources.Remove(key);
            target.BackgroundColor = SystemColors.WindowColor;
            window.SetResourceReference(Window.BackgroundProperty, "WindowBrush");
            // A caption and border in the window color, so the solid window reads as one surface.
            if (window.TryFindResource("WindowBrush") is SolidColorBrush b)
            {
                var bgr = b.Color.R | (b.Color.G << 8) | (b.Color.B << 16);
                Native.DwmSetWindowAttribute(hwnd, DwmwaCaptionColor, ref bgr, sizeof(int));
                Native.DwmSetWindowAttribute(hwnd, DwmwaBorderColor, ref bgr, sizeof(int));
            }
            return applied;
        }

        // The backdrop shows through a transparent client area; panels and cards become tints over it, like Windows Settings.
        target.BackgroundColor = Colors.Transparent;
        // Acrylic samples whatever is behind the window and can come out far lighter (or darker) than the theme, which washed out
        // hint text in testing. A theme-colored tint keeps contrast close to the solid theme; Mica is already theme-tinted by Windows.
        window.Resources["WindowBrush"] = Frozen(applied == WindowMaterial.Acrylic
            ? (dark ? Color.FromArgb(0x9E, 0x10, 0x10, 0x11) : Color.FromArgb(0x9E, 0xF4, 0xF4, 0xF5))
            : Colors.Transparent);
        window.SetResourceReference(Window.BackgroundProperty, "WindowBrush");
        window.Resources["SurfaceRaisedBrush"] = Frozen(dark ? Color.FromArgb(0x1A, 0, 0, 0) : Color.FromArgb(0x0A, 0, 0, 0));
        window.Resources["CardBrush"] = Frozen(dark ? Color.FromArgb(0x10, 0xFF, 0xFF, 0xFF) : Color.FromArgb(0xB3, 0xFF, 0xFF, 0xFF));
        window.Resources["CardHoverBrush"] = Frozen(dark ? Color.FromArgb(0x1A, 0xFF, 0xFF, 0xFF) : Color.FromArgb(0xE0, 0xFF, 0xFF, 0xFF));
        var auto = DwmColorDefault; // the caption takes the backdrop too
        Native.DwmSetWindowAttribute(hwnd, DwmwaCaptionColor, ref auto, sizeof(int));
        Native.DwmSetWindowAttribute(hwnd, DwmwaBorderColor, ref auto, sizeof(int));
        return applied;
    }

    static readonly string[] IslandKeys = ["SurfaceBrush", "CardBrush", "CardHoverBrush", "IslandRimBrush"];

    /// <summary>
    /// Applies the notch surface material to <paramref name="root"/> (the notch window's root). Solid removes the overrides.
    /// Glass: a translucent tint with a soft top sheen and a lit lower rim; Clear: a plainer, more transparent tint.
    /// <paramref name="opacity"/> is clamped to 0.5..1 so text stays readable over any wallpaper.
    /// </summary>
    public static void ApplyIsland(FrameworkElement root, IslandMaterial material, double opacity, double highlight)
    {
        foreach (var key in IslandKeys) root.Resources.Remove(key);
        if (material == IslandMaterial.Solid) return;
        foreach (var (key, brush) in IslandBrushes(material, opacity, highlight, ThemeManager.IsDark)) root.Resources[key] = brush;
    }

    /// <summary>The brushes for a simulated island material (also used by the Appearance preview).</summary>
    public static IEnumerable<(string Key, Brush Brush)> IslandBrushes(IslandMaterial material, double opacity, double highlight, bool dark)
    {
        var a = (byte)Math.Round(255 * Math.Clamp(opacity, 0.5, 1));
        var h = Math.Clamp(highlight, 0, 1);
        var glass = material == IslandMaterial.Glass;
        var baseColor = dark ? Color.FromRgb(0x0A, 0x0A, 0x0C) : Color.FromRgb(0xF7, 0xF7, 0xF8);
        var lift = Colors.White;

        // Body: the tint, a little lighter toward the bottom where light would catch a glass edge.
        var body = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
        body.GradientStops.Add(new GradientStop(Color.FromArgb(a, baseColor.R, baseColor.G, baseColor.B), 0));
        var bottom = glass ? Blend(baseColor, lift, dark ? 0.06 * h : 0.4 * h) : baseColor;
        body.GradientStops.Add(new GradientStop(Color.FromArgb(a, bottom.R, bottom.G, bottom.B), 1));
        body.Freeze();
        yield return ("SurfaceBrush", body);

        // Cards become a lighter layer of the same glass instead of opaque blocks.
        byte card = (byte)(dark ? (glass ? 0x14 : 0x0E) : (glass ? 0x8C : 0x66));
        yield return ("CardBrush", Frozen(dark ? Color.FromArgb(card, 0xFF, 0xFF, 0xFF) : Color.FromArgb(card, 0xFF, 0xFF, 0xFF)));
        yield return ("CardHoverBrush", Frozen(dark ? Color.FromArgb((byte)(card + 0x0C), 0xFF, 0xFF, 0xFF) : Color.FromArgb((byte)Math.Min(255, card + 0x30), 0xFF, 0xFF, 0xFF)));

        // Rim: transparent where the notch meets the screen edge, lit along the lower edge (the specular highlight).
        var rim = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
        var rimAlpha = (byte)Math.Round((glass ? 0x55 : 0x26) * h + (dark ? 0x10 : 0x18));
        var rimColor = dark ? Colors.White : Colors.Black;
        rim.GradientStops.Add(new GradientStop(Color.FromArgb(0, rimColor.R, rimColor.G, rimColor.B), 0));
        rim.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(rimAlpha / 3), rimColor.R, rimColor.G, rimColor.B), 0.5));
        rim.GradientStops.Add(new GradientStop(Color.FromArgb(rimAlpha, rimColor.R, rimColor.G, rimColor.B), 1));
        rim.Freeze();
        yield return ("IslandRimBrush", rim);
    }

    static SolidColorBrush Frozen(Color c) { var b = new SolidColorBrush(c); b.Freeze(); return b; }

    static Color Blend(Color a, Color b, double t) => Color.FromRgb(
        (byte)(a.R + (b.R - a.R) * t), (byte)(a.G + (b.G - a.G) * t), (byte)(a.B + (b.B - a.B) * t));
}
