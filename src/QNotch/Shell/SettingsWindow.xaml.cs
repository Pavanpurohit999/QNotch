using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using QNotch.Core;
using QNotch.Interop;
using QNotch.Modules;
using QNotch.Theme;

namespace QNotch.Shell;

/// <summary>Normal activatable window. Left nav lists every registered settings section; section views are created lazily, once.</summary>
public partial class SettingsWindow : Window
{
    static SettingsWindow? _instance;
    readonly Dictionary<string, FrameworkElement> _views = new();
    readonly Dictionary<string, RadioButton> _buttons = new();
    readonly Registry<SettingsSectionDescriptor> _sections;
    bool _noAnim;

    /// <summary>The open settings window, if any (debug channel).</summary>
    internal static SettingsWindow? Current => _instance;

    SettingsWindow(Registry<SettingsSectionDescriptor> sections)
    {
        _sections = sections;
        InitializeComponent();
        Icon = AppIcon.Render(32);
        foreach (var s in sections.Items)
        {
            var id = s.Id;
            if (id == "about")
            {
                var separator = new Border { Height = 1, Margin = new Thickness(12, 12, 12, 12) };
                separator.SetResourceReference(Border.BackgroundProperty, "StrokeBrush");
                Nav.Children.Add(separator);
            }
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            var glyph = UiKit.Glyph(s.Glyph, 15, "TextSecondaryBrush");
            glyph.Margin = new Thickness(0, 0, 12, 0);
            glyph.VerticalAlignment = VerticalAlignment.Center;
            row.Children.Add(glyph);
            row.Children.Add(new TextBlock { Text = s.Title, FontSize = 13, VerticalAlignment = VerticalAlignment.Center });
            var rb = new RadioButton { Style = (Style)FindResource("NavButton"), Content = row, GroupName = "nav", Margin = new Thickness(0, 0, 0, 2) };
            System.Windows.Automation.AutomationProperties.SetAutomationId(rb, "settings-nav-" + id);
            rb.Click += (_, _) => QNotch.Core.Analytics.Track("settings", "select_section", target: id);
            rb.Checked += (_, _) => { Select(id); rb.BringIntoView(); };
            Nav.Children.Add(rb);
            _buttons[id] = rb;
        }
        SourceInitialized += (_, _) => ApplyTitleBar();
    }

    /// <summary>Shows the single settings window (creating it on first use) and optionally jumps to a section.</summary>
    public static void Show(Registry<SettingsSectionDescriptor> sections, GeneralSettings gs, string? sectionId)
    {
        if (sectionId == "updates") sectionId = "about"; // Older diagnostic links still reach update controls.
        _material = gs.WindowMaterial;
        if (_instance is null)
        {
            var w = new SettingsWindow(sections);
            PropertyChangedEventHandler onTheme = (_, e) =>
            {
                if (e.PropertyName == nameof(GeneralSettings.WindowMaterial)) { _material = gs.WindowMaterial; w.ApplyTitleBar(); }
            };
            Action onApplied = w.ApplyTitleBar; // theme, accent or Windows color changes
            gs.PropertyChanged += onTheme;
            ThemeManager.Changed += onApplied;
            w.Closed += (_, _) => { gs.PropertyChanged -= onTheme; ThemeManager.Changed -= onApplied; _instance = null; };
            _instance = w;
        }
        var win = _instance;
        ((Window)win).Show();
        win.WindowState = WindowState.Normal;
        win.Activate();
        var first = sectionId is not null && win._buttons.ContainsKey(sectionId) ? sectionId : win._sections.Items.FirstOrDefault()?.Id;
        if (first is not null) win._buttons[first].IsChecked = true;
    }

    void Select(string id)
    {
        if (!_views.TryGetValue(id, out var v))
        {
            var d = _sections.Find(id)!;
            try { v = d.Factory(); }
            catch (Exception ex)
            {
                Log.Error($"Settings section '{id}' failed", ex);
                v = Placeholder.Create(Glyphs.Warning, d.Title, "This section failed to load. See logs.");
            }
            _views[id] = v;
        }
        Scroll.Content = v;
        Scroll.ScrollToTop();
        if (IsLoaded && Motion.Enabled && !_noAnim)
            v.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(140)) { FillBehavior = FillBehavior.Stop });
    }

    /// <summary>The material for the live window (General settings). Off-screen snapshots stay solid so their PNGs are opaque.</summary>
    static WindowMaterial _material = WindowMaterial.Solid;

    /// <summary>Backdrop, caption and border for the theme and material (Theme/Materials.cs); re-applied when either changes.</summary>
    void ApplyTitleBar() => Materials.ApplyWindow(this, _noAnim ? WindowMaterial.Solid : _material);

    /// <summary>Snapshot tool: renders off-screen, through <paramref name="save"/>, every section <paramref name="wants"/> by id (and
    /// <c>&lt;id&gt;-end</c>, the bottom of a page taller than the window).</summary>
    internal static void Snapshot(Registry<SettingsSectionDescriptor> sections, Func<string, bool> wants, Action<FrameworkElement, string> save)
    {
        if (!sections.Items.Any(s => wants(s.Id) || wants(s.Id + "-end"))) return;
        var w = new SettingsWindow(sections) { WindowStartupLocation = WindowStartupLocation.Manual, Left = -20000, Top = 0, ShowActivated = false, _noAnim = true };
        w.Show();
        try
        {
            foreach (var s in sections.Items)
            {
                if (!wants(s.Id) && !wants(s.Id + "-end")) continue;
                w._buttons[s.Id].IsChecked = true;
                w.UpdateLayout();
                if (wants(s.Id)) save((FrameworkElement)w.Content, s.Id);
                if (w.Scroll.ScrollableHeight <= 0 || !wants(s.Id + "-end")) continue;
                w.Scroll.ScrollToEnd(); // tall pages (Game mode segments): a second shot of the bottom
                w.UpdateLayout();
                save((FrameworkElement)w.Content, s.Id + "-end");
                w.Scroll.ScrollToHome();
            }
        }
        finally { w.Close(); }
    }
}
