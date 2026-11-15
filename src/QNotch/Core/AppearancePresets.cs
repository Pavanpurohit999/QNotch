using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace QNotch.Core;

/// <summary>
/// An appearance preset: a palette (the theme color tokens, as hex) plus the appearance settings it implies. Applying a preset
/// writes those settings once (Settings, Appearance); the user can still change accent and materials afterwards. A preset with
/// <see cref="Dark"/> null follows the Theme setting and the stock palettes (the default Orbit preset). No WPF types here, so
/// validation is testable on its own.
/// </summary>
public sealed record ThemePreset
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    /// <summary>True dark, false light, null follows Settings, Theme.</summary>
    public bool? Dark { get; init; }
    /// <summary>Palette overrides by token (see <see cref="AppearancePresets.PaletteKeys"/>), "#RRGGBB" or "#AARRGGBB".</summary>
    public Dictionary<string, string> Colors { get; init; } = new();
    public string Accent { get; init; } = "mono";
    public WindowMaterial WindowMaterial { get; init; } = WindowMaterial.Mica;
    public IslandMaterial IslandMaterial { get; init; } = IslandMaterial.Solid;
    public double IslandOpacity { get; init; } = 0.82;
    public double GlassHighlight { get; init; } = 0.6;
    [JsonIgnore] public bool BuiltIn { get; init; }
}

/// <summary>The custom presets file (appearance-presets.json) and the export format, both versioned.</summary>
public sealed class PresetFile
{
    public int Version { get; set; } = AppearancePresets.FormatVersion;
    public List<ThemePreset> Presets { get; set; } = new();
}

public static class AppearancePresets
{
    public const int FormatVersion = 1;
    public const string DefaultId = "orbit";
    public const int MaxCustom = 50, MaxName = 40;
    /// <summary>The settings file id of the custom presets (appearance-presets.json).</summary>
    public const string StoreId = "appearance-presets";

    /// <summary>Every palette token a preset may set (the brushes of Theme.Dark/Light.xaml without the "Brush" suffix).</summary>
    public static readonly string[] PaletteKeys =
    [
        "Surface", "SurfaceRaised", "Window", "Card", "CardHover", "Stroke", "StrokeStrong", "Control", "ControlHover", "ControlPressed",
        "TextPrimary", "TextSecondary", "TextTertiary", "Danger", "Success", "Warning", "ScrollThumb", "IslandRim",
    ];

    static Dictionary<string, string> P(params string[] pairs)
    {
        var d = new Dictionary<string, string>();
        for (var i = 0; i < pairs.Length; i += 2) d[pairs[i]] = pairs[i + 1];
        return d;
    }

    // The stock Orbit palettes, used as the base for presets that only change materials.
    static readonly Dictionary<string, string> OrbitDark = P(
        "Surface", "#000000", "SurfaceRaised", "#0C0C0D", "Window", "#101011", "Card", "#141415", "CardHover", "#1C1C1E",
        "Stroke", "#17FFFFFF", "StrokeStrong", "#2BFFFFFF", "Control", "#14FFFFFF", "ControlHover", "#1FFFFFFF", "ControlPressed", "#2AFFFFFF",
        "TextPrimary", "#F4F4F5", "TextSecondary", "#A3A3A8", "TextTertiary", "#85858B", "Danger", "#F2605A", "Success", "#4CC38A",
        "Warning", "#F2A93B", "ScrollThumb", "#47FFFFFF", "IslandRim", "#17FFFFFF");
    static readonly Dictionary<string, string> OrbitLight = P(
        "Surface", "#F6F6F7", "SurfaceRaised", "#EDEDEF", "Window", "#F1F1F3", "Card", "#FFFFFF", "CardHover", "#F0F0F2",
        "Stroke", "#14000000", "StrokeStrong", "#26000000", "Control", "#0D000000", "ControlHover", "#17000000", "ControlPressed", "#21000000",
        "TextPrimary", "#1C1C1E", "TextSecondary", "#55555B", "TextTertiary", "#69696F", "Danger", "#D63A33", "Success", "#1F8F57",
        "Warning", "#A65F00", "ScrollThumb", "#40000000", "IslandRim", "#14000000");

    static Dictionary<string, string> With(Dictionary<string, string> b, params string[] pairs)
    {
        var d = new Dictionary<string, string>(b);
        foreach (var (k, v) in P(pairs)) d[k] = v;
        return d;
    }

    /// <summary>The built-in presets, in gallery order. The first follows the Theme setting.</summary>
    public static readonly IReadOnlyList<ThemePreset> BuiltIns =
    [
        new() { Id = DefaultId, Name = "Orbit", BuiltIn = true },
        new() { Id = "liquid-glass-dark", Name = "Liquid Glass Dark", Dark = true, BuiltIn = true,
            Colors = With(OrbitDark, "Surface", "#0B0B0E", "Card", "#16161A", "CardHover", "#202026"),
            WindowMaterial = WindowMaterial.MicaAlt, IslandMaterial = IslandMaterial.Glass, IslandOpacity = 0.72, GlassHighlight = 0.85 },
        new() { Id = "liquid-glass-light", Name = "Liquid Glass Light", Dark = false, BuiltIn = true, Colors = new(OrbitLight),
            WindowMaterial = WindowMaterial.Mica, IslandMaterial = IslandMaterial.Glass, IslandOpacity = 0.8, GlassHighlight = 0.8 },
        new() { Id = "catppuccin-mocha", Name = "Catppuccin Mocha", Dark = true, BuiltIn = true, Accent = "#CBA6F7",
            Colors = P("Surface", "#11111B", "SurfaceRaised", "#181825", "Window", "#181825", "Card", "#1E1E2E", "CardHover", "#313244",
                "Stroke", "#2EB4BEFE", "StrokeStrong", "#4DB4BEFE", "Control", "#1ACDD6F4", "ControlHover", "#29CDD6F4", "ControlPressed", "#38CDD6F4",
                "TextPrimary", "#CDD6F4", "TextSecondary", "#A6ADC8", "TextTertiary", "#9399B2", "Danger", "#F38BA8", "Success", "#A6E3A1",
                "Warning", "#FAB387", "ScrollThumb", "#59585B70", "IslandRim", "#2EB4BEFE") },
        new() { Id = "nord", Name = "Nord", Dark = true, BuiltIn = true, Accent = "#88C0D0",
            Colors = P("Surface", "#242933", "SurfaceRaised", "#2E3440", "Window", "#2E3440", "Card", "#3B4252", "CardHover", "#434C5E",
                "Stroke", "#26D8DEE9", "StrokeStrong", "#40D8DEE9", "Control", "#1AD8DEE9", "ControlHover", "#29D8DEE9", "ControlPressed", "#38D8DEE9",
                "TextPrimary", "#ECEFF4", "TextSecondary", "#D8DEE9", "TextTertiary", "#A9B1C1", "Danger", "#E07A84", "Success", "#A3BE8C",
                "Warning", "#EBCB8B", "ScrollThumb", "#594C566A", "IslandRim", "#26D8DEE9") },
        new() { Id = "dracula", Name = "Dracula", Dark = true, BuiltIn = true, Accent = "#BD93F9",
            Colors = P("Surface", "#1E1F29", "SurfaceRaised", "#21222C", "Window", "#21222C", "Card", "#282A36", "CardHover", "#343746",
                "Stroke", "#26F8F8F2", "StrokeStrong", "#40F8F8F2", "Control", "#1AF8F8F2", "ControlHover", "#29F8F8F2", "ControlPressed", "#38F8F8F2",
                "TextPrimary", "#F8F8F2", "TextSecondary", "#C9CCE0", "TextTertiary", "#8B96C8", "Danger", "#FF5555", "Success", "#50FA7B",
                "Warning", "#FFB86C", "ScrollThumb", "#596272A4", "IslandRim", "#26F8F8F2") },
        new() { Id = "amoled", Name = "AMOLED Black", Dark = true, BuiltIn = true, WindowMaterial = WindowMaterial.Solid,
            Colors = P("Surface", "#000000", "SurfaceRaised", "#000000", "Window", "#000000", "Card", "#0B0B0B", "CardHover", "#161616",
                "Stroke", "#1FFFFFFF", "StrokeStrong", "#33FFFFFF", "Control", "#14FFFFFF", "ControlHover", "#21FFFFFF", "ControlPressed", "#2EFFFFFF",
                "TextPrimary", "#FFFFFF", "TextSecondary", "#B3B3B3", "TextTertiary", "#808080", "Danger", "#FF5A52", "Success", "#3DD68C",
                "Warning", "#FFB020", "ScrollThumb", "#4DFFFFFF", "IslandRim", "#1FFFFFFF") },
        new() { Id = "windows-fluent", Name = "Windows Fluent", Dark = true, BuiltIn = true, Accent = "#4CC2FF",
            Colors = P("Surface", "#1C1C1C", "SurfaceRaised", "#202020", "Window", "#202020", "Card", "#2B2B2B", "CardHover", "#323232",
                "Stroke", "#14FFFFFF", "StrokeStrong", "#24FFFFFF", "Control", "#0FFFFFFF", "ControlHover", "#17FFFFFF", "ControlPressed", "#1FFFFFFF",
                "TextPrimary", "#FFFFFF", "TextSecondary", "#C5C5C5", "TextTertiary", "#9A9A9A", "Danger", "#FF99A4", "Success", "#6CCB5F",
                "Warning", "#FCE100", "ScrollThumb", "#59FFFFFF", "IslandRim", "#14FFFFFF") },
        new() { Id = "frosted-glass", Name = "Frosted Glass", Dark = false, BuiltIn = true, Accent = "#3D7EF0",
            Colors = P("Surface", "#ECECEF", "SurfaceRaised", "#E4E4E8", "Window", "#F2F2F5", "Card", "#FFFFFF", "CardHover", "#F5F5F7",
                "Stroke", "#1A000000", "StrokeStrong", "#2E000000", "Control", "#0F000000", "ControlHover", "#1A000000", "ControlPressed", "#24000000",
                "TextPrimary", "#1D1D1F", "TextSecondary", "#505057", "TextTertiary", "#626268", "Danger", "#D73A3F", "Success", "#23854C",
                "Warning", "#A06205", "ScrollThumb", "#40000000", "IslandRim", "#1A000000"),
            WindowMaterial = WindowMaterial.Acrylic, IslandMaterial = IslandMaterial.Glass, IslandOpacity = 0.75, GlassHighlight = 0.8 },
        new() { Id = "minimal-transparent", Name = "Minimal Transparent", Dark = true, BuiltIn = true, Colors = new(OrbitDark),
            WindowMaterial = WindowMaterial.Acrylic, IslandMaterial = IslandMaterial.Clear, IslandOpacity = 0.6, GlassHighlight = 0.3 },
        new() { Id = "high-contrast", Name = "High Contrast", Dark = true, BuiltIn = true, Accent = "#FFD400", WindowMaterial = WindowMaterial.Solid,
            Colors = P("Surface", "#000000", "SurfaceRaised", "#000000", "Window", "#000000", "Card", "#000000", "CardHover", "#1A1A1A",
                "Stroke", "#FFFFFF", "StrokeStrong", "#FFFFFF", "Control", "#26FFFFFF", "ControlHover", "#40FFFFFF", "ControlPressed", "#59FFFFFF",
                "TextPrimary", "#FFFFFF", "TextSecondary", "#FFFFFF", "TextTertiary", "#E0E0E0", "Danger", "#FF6B6B", "Success", "#3FF23F",
                "Warning", "#FFD400", "ScrollThumb", "#FFFFFF", "IslandRim", "#FFFFFF") },
    ];

    /// <summary>A preset by id, built-in first, else custom; null when unknown.</summary>
    public static ThemePreset? Find(string? id, IEnumerable<ThemePreset> custom) =>
        BuiltIns.FirstOrDefault(p => p.Id == id) ?? custom.FirstOrDefault(p => p.Id == id);

    /// <summary>The palette a preset renders with: its colors over the stock palette for its mode.</summary>
    public static Dictionary<string, string> Palette(ThemePreset preset, bool dark)
    {
        var palette = new Dictionary<string, string>(dark ? OrbitDark : OrbitLight);
        foreach (var (k, v) in preset.Colors) palette[k] = v;
        return palette;
    }

    /// <summary>Parses "#RRGGBB" or "#AARRGGBB" into ARGB bytes; false for anything else.</summary>
    public static bool TryParseColor(string? text, out byte a, out byte r, out byte g, out byte b)
    {
        a = 255; r = g = b = 0;
        if (text is null || text.Length is not (7 or 9) || text[0] != '#'
            || !uint.TryParse(text.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v)) return false;
        if (text.Length == 9) a = (byte)(v >> 24);
        r = (byte)(v >> 16); g = (byte)(v >> 8); b = (byte)v;
        return true;
    }

    /// <summary>WCAG relative luminance of an opaque color.</summary>
    public static double Luminance(byte r, byte g, byte b)
    {
        static double C(byte c) { var s = c / 255.0; return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4); }
        return 0.2126 * C(r) + 0.7152 * C(g) + 0.0722 * C(b);
    }

    /// <summary>WCAG contrast ratio of an opaque foreground over an opaque background (1 to 21).</summary>
    public static double Contrast(string foreground, string background)
    {
        TryParseColor(foreground, out _, out var r1, out var g1, out var b1);
        TryParseColor(background, out _, out var r2, out var g2, out var b2);
        double l1 = Luminance(r1, g1, b1), l2 = Luminance(r2, g2, b2);
        return (Math.Max(l1, l2) + 0.05) / (Math.Min(l1, l2) + 0.05);
    }

    /// <summary>
    /// Checks a preset from a file or import: a safe id and name, known palette keys with valid colors, a valid accent, and
    /// materials and ranges in bounds. Returns a normalized copy (custom, not built-in) or throws with a message for the user.
    /// </summary>
    public static ThemePreset Validate(ThemePreset p)
    {
        var name = (p.Name ?? "").Trim();
        if (name.Length is 0 or > MaxName) throw new InvalidDataException($"A preset name must be 1 to {MaxName} characters.");
        var id = (p.Id ?? "").Trim();
        if (id.Length is 0 or > 64 || !id.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_'))
            throw new InvalidDataException($"The preset \"{name}\" has an invalid id.");
        if (BuiltIns.Any(b => b.Id == id)) throw new InvalidDataException($"The preset \"{name}\" uses the id of a built-in preset.");
        var colors = new Dictionary<string, string>();
        foreach (var (key, value) in p.Colors ?? new())
        {
            if (!PaletteKeys.Contains(key)) throw new InvalidDataException($"The preset \"{name}\" has an unknown color \"{key}\".");
            if (!TryParseColor(value, out _, out _, out _, out _)) throw new InvalidDataException($"The preset \"{name}\" has an invalid {key} color.");
            colors[key] = value.ToUpperInvariant();
        }
        var accent = (p.Accent ?? "").Trim();
        if (!string.Equals(accent, "mono", StringComparison.OrdinalIgnoreCase) && !TryParseColor(accent, out _, out _, out _, out _))
            throw new InvalidDataException($"The preset \"{name}\" has an invalid accent color.");
        if (!Enum.IsDefined(p.WindowMaterial) || !Enum.IsDefined(p.IslandMaterial))
            throw new InvalidDataException($"The preset \"{name}\" names an unknown material.");
        if (double.IsNaN(p.IslandOpacity) || double.IsNaN(p.GlassHighlight))
            throw new InvalidDataException($"The preset \"{name}\" has an invalid opacity or highlight.");
        return p with
        {
            Id = id, Name = name, Colors = colors, Accent = accent, BuiltIn = false,
            IslandOpacity = Math.Clamp(p.IslandOpacity, 0.5, 1), GlassHighlight = Math.Clamp(p.GlassHighlight, 0, 1),
        };
    }

    /// <summary>Reads an exported or stored presets file. Unknown versions, bad JSON and invalid presets are rejected whole.</summary>
    public static List<ThemePreset> Read(string json)
    {
        if (json.Length > 1_000_000) throw new InvalidDataException("The presets file is too large.");
        PresetFile? file;
        try { file = JsonSerializer.Deserialize<PresetFile>(json, SettingsStore.Json); }
        catch (JsonException) { throw new InvalidDataException("This is not an Orbit Desk presets file (invalid JSON)."); }
        if (file is null) throw new InvalidDataException("This is not an Orbit Desk presets file.");
        if (file.Version != FormatVersion) throw new InvalidDataException($"This presets file is version {file.Version}; this Orbit Desk reads version {FormatVersion}.");
        if (file.Presets.Count is 0 or > MaxCustom) throw new InvalidDataException($"A presets file holds 1 to {MaxCustom} presets.");
        var presets = file.Presets.Select(Validate).ToList();
        if (presets.Select(p => p.Id).Distinct().Count() != presets.Count) throw new InvalidDataException("The presets file repeats a preset id.");
        return presets;
    }

    /// <summary>The valid custom presets of the stored file; invalid or duplicate entries are logged and skipped.</summary>
    public static List<ThemePreset> LoadValid(PresetFile file)
    {
        var valid = new List<ThemePreset>();
        foreach (var p in file.Presets.Take(MaxCustom))
        {
            try { var v = Validate(p); if (valid.All(x => x.Id != v.Id)) valid.Add(v); }
            catch (InvalidDataException ex) { Log.Warn("Skipped a stored appearance preset: " + ex.Message); }
        }
        return valid;
    }

    /// <summary>Writes presets in the export format (the same as appearance-presets.json).</summary>
    public static string Write(IEnumerable<ThemePreset> presets) =>
        JsonSerializer.Serialize(new PresetFile { Presets = presets.Select(p => p with { BuiltIn = false }).ToList() }, SettingsStore.Json);

    /// <summary>A new unique custom id derived from a name.</summary>
    public static string NewId(string name, IEnumerable<ThemePreset> existing)
    {
        var slug = new string(name.ToLowerInvariant().Select(c => char.IsAsciiLetterOrDigit(c) ? c : '-').ToArray()).Trim('-');
        if (slug.Length == 0) slug = "preset";
        if (slug.Length > 40) slug = slug[..40];
        var ids = existing.Select(p => p.Id).Concat(BuiltIns.Select(b => b.Id)).ToHashSet();
        var id = "custom-" + slug;
        for (var i = 2; ids.Contains(id); i++) id = $"custom-{slug}-{i}";
        return id;
    }
}
