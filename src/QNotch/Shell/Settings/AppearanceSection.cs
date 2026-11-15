using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using QNotch.Core;
using QNotch.Theme;

namespace QNotch.Shell.Settings;

public static class AppearanceSection
{
    // Mono first (the default, follows the theme), then a few restrained colors.
    static readonly string[] Accents = [ThemeManager.Mono, "#4C8DFF", "#2FB67C", "#F2A33A", "#F06A6A", "#E879A8", "#A78BFA", "#38BDB0"];

    public static FrameworkElement Create(GeneralSettings gs, CardLayout layout)
    {
        var page = UiKit.Page("Appearance");

        // Theme: segmented control
        var seg = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var mode in Enum.GetValues<ThemeChoice>())
        {
            var m = mode;
            var rb = new RadioButton { Style = (Style)Application.Current.FindResource("SegmentButton"), Content = m.ToString(), GroupName = "theme", IsChecked = gs.Theme == m };
            rb.Click += (_, _) => QNotch.Core.Analytics.Track("settings", "change", target: "theme");
            rb.Checked += (_, _) => gs.Theme = m;
            seg.Children.Add(rb);
        }
        var segBox = new Border { CornerRadius = new CornerRadius(8), Child = seg };
        segBox.SetResourceReference(Border.BackgroundProperty, "ControlBrush");
        page.Children.Add(UiKit.Row("Theme", "Follow Windows, or force dark or light.", segBox));

        // Materials (Theme/Materials.cs). Window backdrops are real Windows materials; the notch's are simulated.
        var windowHint = Materials.BackdropsSupported
            ? "A real Windows backdrop for this window. Mica tints with your wallpaper; Acrylic blurs what is behind the window."
            : "Mica and Acrylic need Windows 11 version 22H2 or later. This window stays solid.";
        var windowSeg = Segmented("window-material", gs.WindowMaterial, v => gs.WindowMaterial = v,
            [(WindowMaterial.Solid, "Solid"), (WindowMaterial.Mica, "Mica"), (WindowMaterial.MicaAlt, "Mica Alt"), (WindowMaterial.Acrylic, "Acrylic")]);
        windowSeg.IsEnabled = Materials.BackdropsSupported;
        page.Children.Add(UiKit.Row("Window material", windowHint, windowSeg));

        page.Children.Add(UiKit.Row("Notch material",
            "Glass and Clear are simulated with tint, sheen and a lit rim: Windows cannot blur the notch's transparent window.",
            Segmented("island-material", gs.IslandMaterial, v => gs.IslandMaterial = v,
                [(IslandMaterial.Solid, "Solid"), (IslandMaterial.Glass, "Glass"), (IslandMaterial.Clear, "Clear")])));
        var opacity = Percent("Notch opacity", gs.IslandOpacity, 50, 100, v => gs.IslandOpacity = v);
        var highlight = Percent("Glass highlight", gs.GlassHighlight, 0, 100, v => gs.GlassHighlight = v);
        var opacityRow = UiKit.Row("Notch opacity", "Lower shows more of the desktop. Text stays readable down to 50%.", opacity);
        var highlightRow = UiKit.Row("Glass highlight", "The sheen and the lit lower rim of the Glass material.", highlight);
        void Enable() { opacityRow.IsEnabled = gs.IslandMaterial != IslandMaterial.Solid; highlightRow.IsEnabled = gs.IslandMaterial == IslandMaterial.Glass; }
        Enable();
        System.ComponentModel.PropertyChangedEventHandler onMaterial = (_, e) => { if (e.PropertyName == nameof(GeneralSettings.IslandMaterial)) Enable(); };
        // Settings pages are rebuilt with each window; follow the page's lifetime instead of holding it from GeneralSettings.
        page.Loaded += (_, _) => { gs.PropertyChanged -= onMaterial; gs.PropertyChanged += onMaterial; Enable(); };
        page.Unloaded += (_, _) => gs.PropertyChanged -= onMaterial;
        page.Children.Add(opacityRow);
        page.Children.Add(highlightRow);

        // Accent swatches
        var sw = new StackPanel { Orientation = Orientation.Horizontal };
        var rings = new List<(Border ring, string hex)>();
        void Mark() { foreach (var (r, h) in rings) { if (string.Equals(h, gs.AccentColor, StringComparison.OrdinalIgnoreCase)) r.SetResourceReference(Border.BorderBrushProperty, "TextPrimaryBrush"); else r.BorderBrush = Brushes.Transparent; } }
        foreach (var hex in Accents)
        {
            var h = hex;
            var ring = new Border
            {
                Width = 26, Height = 26, CornerRadius = new CornerRadius(13), BorderThickness = new Thickness(2), Margin = new Thickness(0, 0, 6, 0), Cursor = Cursors.Hand,
                Background = Brushes.Transparent, ToolTip = h == ThemeManager.Mono ? "Mono" : h,
                Child = new Border { CornerRadius = new CornerRadius(10), Margin = new Thickness(2), Background = Swatch(h) },
            };
            ring.MouseEnter += (_, _) => { if (!string.Equals(h, gs.AccentColor, StringComparison.OrdinalIgnoreCase)) ring.SetResourceReference(Border.BorderBrushProperty, "StrokeStrongBrush"); };
            ring.MouseLeave += (_, _) => Mark();
            ring.MouseLeftButtonUp += (_, _) => { Analytics.Track("settings", "change", target: "accent"); gs.AccentColor = h; Mark(); };
            rings.Add((ring, h));
            sw.Children.Add(ring);
        }
        Mark();
        page.Children.Add(UiKit.Row("Accent color", null, sw));

        page.Children.Add(UiKit.Row("Reduce motion", "Skips animations. Always on when Windows animation effects are off.", UiKit.Toggle(gs.ReduceMotion, v => { QNotch.Core.Analytics.Track("settings", "change", target: "reduce_motion", enabled: v); gs.ReduceMotion = v; })));

        // Profile
        var name = new TextBox { Width = 200, Text = gs.ProfileName };
        var committedName = gs.ProfileName;
        name.TextChanged += (_, _) => gs.ProfileName = name.Text;
        name.LostKeyboardFocus += (_, _) => { if (committedName == name.Text) return; Analytics.Track("settings", "change", target: "profile_name"); committedName = name.Text; };
        page.Children.Add(UiKit.Row("Display name", "Shown in the panel header.", name));

        var pick = new Button { Content = "Choose image" };
        var clear = new Button { Content = "Remove", Margin = new Thickness(8, 0, 0, 0) };
        pick.Click += (_, _) =>
        { QNotch.Core.Analytics.Track("settings", "choose_photo", "ui");
            var dlg = new OpenFileDialog { Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.webp|All files|*.*" };
            if (dlg.ShowDialog() == true) gs.ProfileImagePath = dlg.FileName;
        };
        clear.Click += (_, _) => { QNotch.Core.Analytics.Track("settings", "remove_photo", "ui"); gs.ProfileImagePath = ""; };
        var pp = new StackPanel { Orientation = Orientation.Horizontal };
        pp.Children.Add(pick);
        pp.Children.Add(clear);
        page.Children.Add(UiKit.Row("Profile image", "A square image works best.", pp));

        // Cards
        var cards = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
        cards.Children.Add(new TextBlock { Text = "Home cards", Style = (Style)Application.Current.FindResource("Title"), Margin = new Thickness(0, 0, 0, 4) });
        cards.Children.Add(new TextBlock { Text = "Hidden cards keep their slot and return to it when shown again.", Style = (Style)Application.Current.FindResource("Muted"), Margin = new Thickness(0, 0, 0, 12) });
        foreach (var c in layout.Ordered)
        {
            var id = c.Id;
            cards.Children.Add(UiKit.Row(c.Title, null, UiKit.Toggle(layout.IsVisible(id), v => { QNotch.Core.Analytics.Track("settings", "change", target: "card_visibility", enabled: v); layout.SetVisible(id, v); })));
        }
        page.Children.Add(cards);
        return page;
    }

    /// <summary>A segmented control (the Theme row's style) over enum values; checking one sets it.</summary>
    static Border Segmented<T>(string group, T current, Action<T> set, (T Value, string Label)[] options) where T : struct, Enum
    {
        var seg = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var (value, label) in options)
        {
            var v = value;
            var rb = new RadioButton { Style = (Style)Application.Current.FindResource("SegmentButton"), Content = label, GroupName = group, IsChecked = current.Equals(v) };
            System.Windows.Automation.AutomationProperties.SetAutomationId(rb, $"{group}-{v.ToString().ToLowerInvariant()}");
            rb.Checked += (_, _) => { Analytics.Track("settings", "change", target: group); set(v); };
            seg.Children.Add(rb);
        }
        var box = new Border { CornerRadius = new CornerRadius(8), Child = seg };
        box.SetResourceReference(Border.BackgroundProperty, "ControlBrush");
        return box;
    }

    /// <summary>A 5% step slider for a 0..1 setting shown as a percentage, with its value beside it.</summary>
    static FrameworkElement Percent(string name, double value, int min, int max, Action<double> set)
    {
        var slider = new Slider { Minimum = min, Maximum = max, Value = Math.Clamp(Math.Round(value * 100), min, max), Width = 180,
            SmallChange = 5, LargeChange = 10, TickFrequency = 5, IsSnapToTickEnabled = true, VerticalAlignment = VerticalAlignment.Center };
        System.Windows.Automation.AutomationProperties.SetName(slider, name);
        var label = new TextBlock { Width = 44, TextAlignment = TextAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Text = $"{slider.Value:0}%" };
        slider.ValueChanged += (_, e) => { label.Text = $"{e.NewValue:0}%"; set(e.NewValue / 100); };
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(slider);
        row.Children.Add(label);
        return row;
    }

    /// <summary>A swatch fill: the hex color, or for Mono a diagonal half white, half black.</summary>
    static Brush Swatch(string value)
    {
        if (value != ThemeManager.Mono) return new SolidColorBrush((Color)ColorConverter.ConvertFromString(value));
        var b = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
        b.GradientStops.Add(new GradientStop(Color.FromRgb(0xF4, 0xF4, 0xF5), 0.5));
        b.GradientStops.Add(new GradientStop(Color.FromRgb(0x1C, 0x1C, 0x1E), 0.5));
        b.Freeze();
        return b;
    }
}
