using System.IO;
using QNotch.Core;

/// <summary>Appearance presets: built-in palettes are complete and readable, and imported files are validated before use.</summary>
static class PresetChecks
{
    static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }

    public static void Builtins()
    {
        var ids = AppearancePresets.BuiltIns.Select(p => p.Id).ToList();
        Require(ids.Count == 11 && ids.Distinct().Count() == ids.Count && ids[0] == AppearancePresets.DefaultId, "11 unique built-ins, Orbit first");
        foreach (var name in new[] { "Liquid Glass Dark", "Liquid Glass Light", "Catppuccin Mocha", "Nord", "Dracula", "AMOLED Black",
            "Windows Fluent", "Frosted Glass", "Minimal Transparent", "High Contrast" })
            Require(AppearancePresets.BuiltIns.Any(p => p.Name == name), "missing preset " + name);

        foreach (var preset in AppearancePresets.BuiltIns)
        foreach (var dark in preset.Dark is { } fixedMode ? new[] { fixedMode } : new[] { true, false })
        {
            var palette = AppearancePresets.Palette(preset, dark);
            Require(AppearancePresets.PaletteKeys.All(palette.ContainsKey), $"{preset.Name}: every palette token is set");
            Require(palette.Values.All(v => AppearancePresets.TryParseColor(v, out _, out _, out _, out _)), $"{preset.Name}: colors parse");
            Require(AppearancePresets.Validate(preset with { Id = "copy-" + preset.Id }) is not null, $"{preset.Name}: passes import validation");
            // WCAG: primary text AAA (7:1), secondary and hint text AA for body text (4.5:1), on every opaque background.
            foreach (var bg in new[] { "Card", "Window", "Surface", "SurfaceRaised" })
            {
                if (palette[bg].Length != 7) continue; // translucent tokens depend on what is behind them
                var p = AppearancePresets.Contrast(palette["TextPrimary"], palette[bg]);
                var s = AppearancePresets.Contrast(palette["TextSecondary"], palette[bg]);
                var t = AppearancePresets.Contrast(palette["TextTertiary"], palette[bg]);
                Require(p >= 7, $"{preset.Name} ({(dark ? "dark" : "light")}): primary text on {bg} is {p:0.0}:1");
                Require(s >= 4.5, $"{preset.Name} ({(dark ? "dark" : "light")}): secondary text on {bg} is {s:0.0}:1");
                Require(t >= 4.5, $"{preset.Name} ({(dark ? "dark" : "light")}): hint text on {bg} is {t:0.0}:1");
            }
        }
        Require(Math.Abs(AppearancePresets.Contrast("#FFFFFF", "#000000") - 21) < 0.01 && Math.Abs(AppearancePresets.Contrast("#777777", "#777777") - 1) < 0.01, "contrast formula");
    }

    public static void Files()
    {
        var custom = new ThemePreset { Id = "custom-ocean", Name = "Ocean", Dark = true, Accent = "#38bdb0",
            Colors = new() { ["Surface"] = "#001018", ["TextPrimary"] = "#e8f6f8" }, IslandMaterial = IslandMaterial.Glass, IslandOpacity = 0.2, GlassHighlight = 4 };
        var json = AppearancePresets.Write([custom]);
        Require(json.Contains("\"Version\": 1") && !json.Contains("BuiltIn"), "versioned export without runtime fields");
        var back = AppearancePresets.Read(json).Single();
        Require(back.Id == "custom-ocean" && back.Colors["Surface"] == "#001018" && back.Colors["TextPrimary"] == "#E8F6F8", "round trip keeps colors, normalized");
        Require(back.IslandOpacity == 0.5 && back.GlassHighlight == 1 && !back.BuiltIn, "ranges are clamped on import");

        void Rejects(string what, string text)
        {
            try { AppearancePresets.Read(text); }
            catch (InvalidDataException ex) { Require(ex.Message.Length > 0, what); return; }
            throw new Exception("accepted " + what);
        }
        string Bad(ThemePreset p) => AppearancePresets.Write([p]);
        Rejects("invalid JSON", "{ nope");
        Rejects("a future version", json.Replace("\"Version\": 1", "\"Version\": 2"));
        Rejects("an empty file", "{\"Version\":1,\"Presets\":[]}");
        Rejects("an unknown color token", Bad(custom with { Colors = new() { ["Wallpaper"] = "#000000" } }));
        Rejects("a malformed color", Bad(custom with { Colors = new() { ["Surface"] = "blue" } }));
        Rejects("a malformed accent", Bad(custom with { Accent = "#12" }));
        Rejects("an empty name", Bad(custom with { Name = "  " }));
        Rejects("a long name", Bad(custom with { Name = new string('x', 41) }));
        Rejects("a path-like id", Bad(custom with { Id = "../../general" }));
        Rejects("a built-in id", Bad(custom with { Id = "nord" }));
        Rejects("a repeated id", AppearancePresets.Write([custom, custom with { Name = "Ocean 2" }]));
        Rejects("an unknown material", json.Replace("\"Glass\"", "\"Plasma\""));
        Rejects("too many presets", AppearancePresets.Write(Enumerable.Range(0, 51).Select(i => custom with { Id = "p" + i })));

        // Stored files: one bad entry (edited by hand) is skipped, the rest load.
        var stored = new PresetFile { Presets = [custom, custom with { Id = "x y" }, custom with { Id = "custom-two", Name = "Two" }] };
        Require(AppearancePresets.LoadValid(stored).Select(p => p.Id).SequenceEqual(new[] { "custom-ocean", "custom-two" }), "LoadValid skips invalid entries");

        var existing = new List<ThemePreset> { custom with { Id = "custom-ocean" } };
        Require(AppearancePresets.NewId("Ocean", existing) == "custom-ocean-2" && AppearancePresets.NewId("Nord!", []) == "custom-nord", "new ids are unique slugs");
        Require(AppearancePresets.Find("nord", []) is { BuiltIn: true } && AppearancePresets.Find("custom-ocean", existing) is { BuiltIn: false }
            && AppearancePresets.Find("gone", existing) is null, "lookup finds built-in and custom presets");
        Require(new GeneralSettings().ThemePreset == AppearancePresets.DefaultId, "default preset is Orbit");
    }
}
