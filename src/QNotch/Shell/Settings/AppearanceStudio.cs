using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using QNotch.Core;
using QNotch.Theme;

namespace QNotch.Shell.Settings;

/// <summary>
/// The top of Settings, Appearance: a live preview, the preset gallery (hover to preview, click to apply) and the preset manager
/// (save, rename, duplicate, delete, import, export, restore). Presets are Core/AppearancePresets.cs; custom ones are stored in
/// appearance-presets.json. The preview is built from the same styles as the app, with the previewed preset's brushes as local
/// resources, so it shows real controls without touching the live theme.
/// </summary>
internal sealed class AppearanceStudio
{
    readonly GeneralSettings _gs;
    readonly SettingsStore _store;
    readonly StackPanel _root = new();
    readonly Border _previewHost = new();
    readonly WrapPanel _gallery = new() { Margin = new Thickness(0, 12, 0, 4) };
    readonly TextBox _name = new() { Width = 200, MaxLength = AppearancePresets.MaxName };
    readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) };
    readonly Button _rename = new() { Content = "Rename" }, _delete = new() { Content = "Delete" };
    DispatcherTimer? _confirmDelete;
    ThemePreset? _hovered;

    public static FrameworkElement Create(GeneralSettings gs, SettingsStore store) => new AppearanceStudio(gs, store)._root;

    AppearanceStudio(GeneralSettings gs, SettingsStore store)
    {
        _gs = gs; _store = store;
        _status.SetResourceReference(TextBlock.StyleProperty, "Muted");
        AutomationProperties.SetAutomationId(_status, "appearance-preset-status");

        var title = UiKit.Text("Presets", "Title");
        title.Margin = new Thickness(0, 0, 0, 4);
        _root.Children.Add(title);
        var hint = UiKit.Text("Hover a preset to preview it here, click to apply. Accent and materials stay adjustable below.", "Muted");
        hint.TextWrapping = TextWrapping.Wrap;
        _root.Children.Add(hint);
        _previewHost.Margin = new Thickness(0, 12, 0, 0);
        AutomationProperties.SetAutomationId(_previewHost, "appearance-preview");
        _root.Children.Add(_previewHost);
        _root.Children.Add(_gallery);
        _root.Children.Add(Manager());
        _root.Children.Add(_status);
        _root.Margin = new Thickness(0, 0, 0, 20);

        Rebuild();
        // The settings page outlives no window: follow its lifetime rather than hold it from GeneralSettings.
        System.ComponentModel.PropertyChangedEventHandler onChange = (_, e) =>
        {
            if (e.PropertyName is nameof(GeneralSettings.ThemePreset) or nameof(GeneralSettings.AccentColor) or nameof(GeneralSettings.Theme)
                or nameof(GeneralSettings.IslandMaterial) or nameof(GeneralSettings.IslandOpacity) or nameof(GeneralSettings.GlassHighlight)) Rebuild();
        };
        _root.Loaded += (_, _) => { _gs.PropertyChanged -= onChange; _gs.PropertyChanged += onChange; };
        _root.Unloaded += (_, _) => _gs.PropertyChanged -= onChange;
    }

    IEnumerable<ThemePreset> All => AppearancePresets.BuiltIns.Concat(ThemeManager.CustomPresets);
    ThemePreset Active => ThemeManager.ActivePreset(_gs);

    void Rebuild()
    {
        ShowPreview(_hovered);
        _gallery.Children.Clear();
        foreach (var p in All) _gallery.Children.Add(Tile(p));
        var custom = !Active.BuiltIn;
        _rename.IsEnabled = custom;
        _delete.IsEnabled = custom;
        // Empty for built-ins (Save as new then uses "My preset"); a custom preset's name, ready to rename.
        if (!_name.IsKeyboardFocusWithin) _name.Text = custom ? Active.Name : "";
    }

    // ---------- preview ----------

    void ShowPreview(ThemePreset? preset)
    {
        var p = preset ?? Active;
        var current = preset is null || preset.Id == Active.Id;
        var dark = ThemeManager.IsDarkFor(p, _gs);
        // Previewing the active preset shows the live settings; another preset shows what applying it would give.
        var accent = current ? _gs.AccentColor : p.Accent;
        var material = current ? _gs.IslandMaterial : p.IslandMaterial;
        var opacity = current ? _gs.IslandOpacity : p.IslandOpacity;
        var highlight = current ? _gs.GlassHighlight : p.GlassHighlight;
        _previewHost.Child = Preview(p, dark, accent, material, opacity, highlight, current ? "Current appearance" : "Preview: " + p.Name);
    }

    /// <summary>A preset rendered with real controls: a desktop backdrop, the notch pill and a panel card.</summary>
    internal static FrameworkElement Preview(ThemePreset preset, bool dark, string accent, IslandMaterial material, double opacity, double highlight, string caption)
    {
        var resources = ThemeManager.PaletteBrushes(preset, dark);
        if (resources.Count == 0) // Orbit: the stock palette of the mode
            foreach (var (k, hex) in AppearancePresets.Palette(preset, dark))
                if (AppearancePresets.TryParseColor(hex, out var a, out var r, out var g, out var b)) resources[k + "Brush"] = Frozen(Color.FromArgb(a, r, g, b));
        foreach (var (k, brush) in ThemeManager.AccentBrushes(ThemeManager.AccentFor(accent, dark), dark)) resources[k] = brush;
        var surface = resources["SurfaceBrush"] is SolidColorBrush sb ? sb.Color : (Color?)null;
        if (material != IslandMaterial.Solid)
            foreach (var (k, brush) in Materials.IslandBrushes(material, opacity, highlight, dark, surface)) resources[k] = brush;

        // A soft "wallpaper" so glass presets show what they let through.
        var wallpaper = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
        wallpaper.GradientStops.Add(new GradientStop(dark ? Color.FromRgb(0x2A, 0x3A, 0x5C) : Color.FromRgb(0xBF, 0xD4, 0xF2), 0));
        wallpaper.GradientStops.Add(new GradientStop(dark ? Color.FromRgb(0x4A, 0x2E, 0x4F) : Color.FromRgb(0xF2, 0xD1, 0xC4), 1));
        wallpaper.Freeze();

        var desk = new Grid { Height = 200, ClipToBounds = true };
        desk.Resources = resources;

        var pill = new Border { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top, Height = 30,
            CornerRadius = new CornerRadius(0, 0, 14, 14), BorderThickness = new Thickness(1, 0, 1, 1), Padding = new Thickness(14, 0, 14, 0) };
        pill.SetResourceReference(Border.BackgroundProperty, "SurfaceBrush");
        pill.SetResourceReference(Border.BorderBrushProperty, "IslandRimBrush");
        var pillRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        pillRow.Children.Add(Label("♫", "AccentBrush", 12, new Thickness(0, 0, 8, 0)));
        pillRow.Children.Add(Label("Midnight City", "TextPrimaryBrush", 12, new Thickness(0, 0, 6, 0), FontWeights.SemiBold));
        pillRow.Children.Add(Label("M83", "TextSecondaryBrush", 12, new Thickness(0, 0, 18, 0)));
        pillRow.Children.Add(Label("CPU 12%", "TextSecondaryBrush", 11, new Thickness(0, 0, 14, 0)));
        pillRow.Children.Add(Label("21:30", "TextPrimaryBrush", 12, default, FontWeights.SemiBold));
        pill.Child = pillRow;

        var panel = new Border { Margin = new Thickness(0, 40, 0, 12), Width = 400, HorizontalAlignment = HorizontalAlignment.Center,
            CornerRadius = new CornerRadius(18), BorderThickness = new Thickness(1), Padding = new Thickness(10) };
        panel.SetResourceReference(Border.BackgroundProperty, "SurfaceBrush");
        panel.SetResourceReference(Border.BorderBrushProperty, "IslandRimBrush");
        var card = new Border { Padding = new Thickness(14, 10, 14, 10), CornerRadius = new CornerRadius(12) };
        card.SetResourceReference(Border.BackgroundProperty, "CardBrush");
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var text = new StackPanel();
        text.Children.Add(Label("Focus mode", "TextPrimaryBrush", 13, default, FontWeights.SemiBold));
        text.Children.Add(Label("Silences notifications for an hour.", "TextTertiaryBrush", 11, new Thickness(0, 2, 0, 8)));
        var slider = new Slider { Minimum = 0, Maximum = 100, Value = 62, Width = 200, IsHitTestVisible = false, Focusable = false, HorizontalAlignment = HorizontalAlignment.Left };
        // Illustrations, not settings: named so a screen reader says what they are (the smoke test's accessible-names check).
        AutomationProperties.SetName(slider, "Example slider in the preview");
        text.Children.Add(slider);
        grid.Children.Add(text);
        var controls = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        var toggle = new CheckBox { IsChecked = true, IsHitTestVisible = false, Focusable = false, HorizontalAlignment = HorizontalAlignment.Right };
        toggle.SetResourceReference(FrameworkElement.StyleProperty, "ToggleSwitch");
        AutomationProperties.SetName(toggle, "Example switch in the preview");
        controls.Children.Add(toggle);
        var button = new Button { Content = "Apply", Margin = new Thickness(0, 10, 0, 0), IsHitTestVisible = false, Focusable = false };
        button.SetResourceReference(FrameworkElement.StyleProperty, "AccentButton");
        controls.Children.Add(button);
        Grid.SetColumn(controls, 1);
        grid.Children.Add(controls);
        card.Child = grid;
        panel.Child = card;

        desk.Children.Add(pill);
        desk.Children.Add(panel);
        var frame = new Border { CornerRadius = new CornerRadius(14), Background = wallpaper, Child = desk, ClipToBounds = true };
        AutomationProperties.SetName(frame, caption);

        var box = new StackPanel();
        box.Children.Add(frame);
        var label = UiKit.Text(caption, "Muted");
        label.Margin = new Thickness(2, 6, 0, 0);
        box.Children.Add(label);
        return box;
    }

    static TextBlock Label(string text, string brush, double size, Thickness margin, FontWeight? weight = null)
    {
        var t = new TextBlock { Text = text, FontSize = size, Margin = margin, VerticalAlignment = VerticalAlignment.Center, FontWeight = weight ?? FontWeights.Normal };
        t.SetResourceReference(TextBlock.ForegroundProperty, brush);
        return t;
    }

    // ---------- gallery ----------

    FrameworkElement Tile(ThemePreset p)
    {
        var dark = ThemeManager.IsDarkFor(p, _gs);
        var palette = AppearancePresets.Palette(p, dark);
        Brush B(string key) => Frozen(Parse(palette[key]));
        var accent = Frozen(ThemeManager.AccentFor(p.Accent, dark));

        var swatch = new Grid { Width = 134, Height = 70 };
        // Outlined in the page's own stroke color, so a light preset stays visible on a light page (and dark on dark).
        var backdrop = new Border { Background = B("Window"), CornerRadius = new CornerRadius(10), BorderThickness = new Thickness(1) };
        backdrop.SetResourceReference(Border.BorderBrushProperty, "StrokeStrongBrush");
        swatch.Children.Add(backdrop);
        var pill = new Border { Background = B("Surface"), BorderBrush = B("IslandRim"), BorderThickness = new Thickness(1, 0, 1, 1), Width = 68, Height = 12,
            CornerRadius = new CornerRadius(0, 0, 6, 6), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top };
        swatch.Children.Add(pill);
        var card = new Border { Background = B("Card"), BorderBrush = B("Stroke"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(7),
            Margin = new Thickness(12, 22, 12, 10), Padding = new Thickness(8, 6, 8, 6) };
        var lines = new StackPanel();
        lines.Children.Add(new Border { Background = B("TextPrimary"), Height = 4, Width = 52, CornerRadius = new CornerRadius(2), HorizontalAlignment = HorizontalAlignment.Left });
        lines.Children.Add(new Border { Background = B("TextTertiary"), Height = 4, Width = 64, CornerRadius = new CornerRadius(2), Margin = new Thickness(0, 5, 0, 0), HorizontalAlignment = HorizontalAlignment.Left });
        var dot = new Border { Background = accent, Width = 18, Height = 9, CornerRadius = new CornerRadius(4.5), HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, -14, 0, 0) };
        lines.Children.Add(dot);
        card.Child = lines;
        swatch.Children.Add(card);

        var content = new StackPanel();
        content.Children.Add(swatch);
        var name = new TextBlock { Text = p.Name, FontSize = 12, Margin = new Thickness(2, 6, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 134 };
        content.Children.Add(name);
        var tag = UiKit.Text(p.BuiltIn ? (p.Dark is null ? "Follows Theme" : p.Dark == true ? "Dark" : "Light") : "Custom", "Muted");
        tag.Margin = new Thickness(2, 1, 0, 0);
        content.Children.Add(tag);

        var active = p.Id == Active.Id;
        var ring = new Border { Child = content, Padding = new Thickness(6), CornerRadius = new CornerRadius(14), BorderThickness = new Thickness(2),
            Background = Brushes.Transparent };
        if (active) ring.SetResourceReference(Border.BorderBrushProperty, "TextPrimaryBrush");
        else ring.BorderBrush = Brushes.Transparent;

        // A Button so presets are reachable and appliable with the keyboard; focus previews like hover does.
        var button = new Button { Content = ring, Padding = new Thickness(0), Margin = new Thickness(0, 0, 8, 8), Background = Brushes.Transparent,
            BorderThickness = new Thickness(0), Cursor = System.Windows.Input.Cursors.Hand, ToolTip = p.Name };
        button.Template = BareTemplate();
        AutomationProperties.SetName(button, p.Name + (active ? ", applied" : ""));
        AutomationProperties.SetAutomationId(button, "appearance-preset-" + p.Id);
        void Enter() { _hovered = p; ShowPreview(p); if (!active) ring.SetResourceReference(Border.BorderBrushProperty, "StrokeStrongBrush"); }
        void Leave() { _hovered = null; ShowPreview(null); if (!active) ring.BorderBrush = Brushes.Transparent; }
        button.MouseEnter += (_, _) => Enter();
        button.MouseLeave += (_, _) => Leave();
        button.GotKeyboardFocus += (_, _) => Enter();
        button.LostKeyboardFocus += (_, _) => Leave();
        button.Click += (_, _) => { Analytics.Track("settings", "change", target: "preset"); ApplyPreset(p); };
        return button;
    }

    static ControlTemplate BareTemplate()
    {
        var t = new ControlTemplate(typeof(Button));
        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        t.VisualTree = presenter;
        return t;
    }

    /// <summary>Applies a preset: its accent and materials once, then the palette.</summary>
    void ApplyPreset(ThemePreset p)
    {
        _hovered = null;
        _gs.AccentColor = p.Accent;
        _gs.WindowMaterial = p.WindowMaterial;
        _gs.IslandMaterial = p.IslandMaterial;
        _gs.IslandOpacity = p.IslandOpacity;
        _gs.GlassHighlight = p.GlassHighlight;
        _gs.ThemePreset = p.Id;
        Say($"Applied {p.Name}.");
    }

    // ---------- manager ----------

    FrameworkElement Manager()
    {
        var wrap = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
        AutomationProperties.SetName(_name, "Preset name");
        AutomationProperties.SetAutomationId(_name, "appearance-preset-name");
        _name.Margin = new Thickness(0, 0, 8, 8);
        _name.VerticalContentAlignment = VerticalAlignment.Center;
        _name.ToolTip = "Name for Save as new or Rename. Empty saves as \"My preset\".";
        // Like a Windows rename box: focusing selects the name, so typing replaces it. After the click, or the click would undo it.
        _name.GotKeyboardFocus += (_, _) => _name.Dispatcher.BeginInvoke(DispatcherPriority.Input, _name.SelectAll);
        wrap.Children.Add(_name);
        wrap.Children.Add(Action("Save as new", "save", SaveAsNew));
        wrap.Children.Add(Action(_rename, "rename", Rename));
        wrap.Children.Add(Action("Duplicate", "duplicate", Duplicate));
        wrap.Children.Add(Action(_delete, "delete", Delete));
        wrap.Children.Add(Action("Import…", "import", Import));
        wrap.Children.Add(Action("Export…", "export", Export));
        wrap.Children.Add(Action("Restore default", "restore", RestoreDefault));
        return wrap;
    }

    static Button Action(string text, string id, Action run) => Action(new Button { Content = text }, id, run);

    static Button Action(Button b, string id, Action run)
    {
        b.Margin = new Thickness(0, 0, 8, 8);
        b.Padding = new Thickness(12, 5, 12, 5);
        AutomationProperties.SetAutomationId(b, "appearance-preset-" + id);
        b.Click += (_, _) => { Analytics.Track("settings", "preset_" + id); run(); };
        return b;
    }

    string NameOrDefault(string fallback) => _name.Text.Trim() is { Length: > 0 } n ? n : fallback;

    /// <summary>A custom copy of a preset as it renders now: its full palette and mode, with the given settings.</summary>
    ThemePreset Capture(ThemePreset from, string name, bool useLiveSettings)
    {
        var dark = ThemeManager.IsDarkFor(from, _gs);
        return new ThemePreset
        {
            Id = AppearancePresets.NewId(name, ThemeManager.CustomPresets), Name = name, Dark = dark,
            Colors = AppearancePresets.Palette(from, dark),
            Accent = useLiveSettings ? _gs.AccentColor : from.Accent,
            WindowMaterial = useLiveSettings ? _gs.WindowMaterial : from.WindowMaterial,
            IslandMaterial = useLiveSettings ? _gs.IslandMaterial : from.IslandMaterial,
            IslandOpacity = useLiveSettings ? _gs.IslandOpacity : from.IslandOpacity,
            GlassHighlight = useLiveSettings ? _gs.GlassHighlight : from.GlassHighlight,
        };
    }

    void SaveAsNew()
    {
        if (!HasRoom()) return;
        var preset = Capture(Active, NameOrDefault("My preset"), useLiveSettings: true);
        Store([.. ThemeManager.CustomPresets, preset]);
        _gs.ThemePreset = preset.Id;
        Say($"Saved {preset.Name}.");
    }

    void Rename()
    {
        if (Active.BuiltIn) return;
        var name = NameOrDefault(Active.Name);
        Store(ThemeManager.CustomPresets.Select(p => p.Id == Active.Id ? p with { Name = name } : p).ToList());
        Rebuild();
        Say($"Renamed to {name}.");
    }

    void Duplicate()
    {
        if (!HasRoom()) return;
        var copy = Capture(Active, Trim(Active.Name + " copy"), useLiveSettings: !Active.BuiltIn);
        Store([.. ThemeManager.CustomPresets, copy]);
        ApplyPreset(copy);
        Say($"Created {copy.Name}.");
    }

    void Delete()
    {
        if (Active.BuiltIn) return;
        // Two steps: the first press arms the button for four seconds.
        if (_confirmDelete is null)
        {
            _delete.Content = "Confirm delete";
            _confirmDelete = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
            _confirmDelete.Tick += (_, _) => Disarm();
            _confirmDelete.Start();
            Say($"Press Confirm delete to remove {Active.Name}. Nothing else changes.");
            return;
        }
        Disarm();
        var name = Active.Name;
        var id = Active.Id;
        Store(ThemeManager.CustomPresets.Where(p => p.Id != id).ToList());
        _gs.ThemePreset = AppearancePresets.DefaultId; // the palette falls back to Orbit; accent and materials stay
        Say($"Deleted {name}.");
    }

    void Disarm()
    {
        _confirmDelete?.Stop();
        _confirmDelete = null;
        _delete.Content = "Delete";
    }

    void Import()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "Orbit Desk presets (*.json)|*.json", Title = "Import appearance presets" };
        if (dialog.ShowDialog(Window.GetWindow(_root)) != true) return;
        try
        {
            if (new FileInfo(dialog.FileName).Length > 1_000_000) throw new InvalidDataException("The presets file is too large.");
            var incoming = AppearancePresets.Read(File.ReadAllText(dialog.FileName));
            var list = ThemeManager.CustomPresets.ToList();
            if (list.Count + incoming.Count > AppearancePresets.MaxCustom)
                throw new InvalidDataException($"That would make more than {AppearancePresets.MaxCustom} custom presets.");
            // An id already in use gets a fresh one, so importing never overwrites a preset.
            foreach (var p in incoming) list.Add(list.Any(x => x.Id == p.Id) ? p with { Id = AppearancePresets.NewId(p.Name, list) } : p);
            Store(list);
            ApplyPreset(list[^1]);
            Say(incoming.Count == 1 ? $"Imported and applied {incoming[0].Name}." : $"Imported {incoming.Count} presets and applied {list[^1].Name}.");
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            Say("Could not import: " + ex.Message);
        }
    }

    void Export()
    {
        var preset = Active.BuiltIn ? Capture(Active, Active.Name, useLiveSettings: true) : Active with { Accent = _gs.AccentColor,
            WindowMaterial = _gs.WindowMaterial, IslandMaterial = _gs.IslandMaterial, IslandOpacity = _gs.IslandOpacity, GlassHighlight = _gs.GlassHighlight };
        var safe = new string(preset.Name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '-' : c).ToArray());
        var dialog = new Microsoft.Win32.SaveFileDialog { Filter = "Orbit Desk presets (*.json)|*.json", FileName = safe + ".orbit-preset.json", Title = "Export appearance preset" };
        if (dialog.ShowDialog(Window.GetWindow(_root)) != true) return;
        try
        {
            File.WriteAllText(dialog.FileName, AppearancePresets.Write([preset]));
            Say($"Exported {preset.Name} to {Path.GetFileName(dialog.FileName)}.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Say("Could not export: " + ex.Message); }
    }

    void RestoreDefault()
    {
        _gs.Theme = ThemeChoice.System;
        ApplyPreset(AppearancePresets.BuiltIns[0]);
        _gs.WindowMaterial = WindowMaterial.Mica;
        Say("Restored the default appearance.");
    }

    bool HasRoom()
    {
        if (ThemeManager.CustomPresets.Count < AppearancePresets.MaxCustom) return true;
        Say($"You have {AppearancePresets.MaxCustom} custom presets, the most there can be. Delete one first.");
        return false;
    }

    void Store(List<ThemePreset> presets)
    {
        ThemeManager.CustomPresets = presets;
        _store.Save(AppearancePresets.StoreId, new PresetFile { Presets = presets });
        Rebuild();
    }

    void Say(string text) => _status.Text = text;

    static string Trim(string name) => name.Length <= AppearancePresets.MaxName ? name : name[..AppearancePresets.MaxName];

    static Color Parse(string hex)
    {
        AppearancePresets.TryParseColor(hex, out var a, out var r, out var g, out var b);
        return Color.FromArgb(a, r, g, b);
    }

    static SolidColorBrush Frozen(Color c) { var b = new SolidColorBrush(c); b.Freeze(); return b; }
}
