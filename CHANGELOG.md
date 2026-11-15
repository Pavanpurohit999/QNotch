# Changelog

All notable changes to Orbit Desk. Orbit Desk is a fork of [QNotch](https://github.com/Somafet/qnotch) (MIT); see NOTICE.md.
Versions follow [semantic versioning](https://semver.org/). Release tags are `vX.Y.Z`.

## [Unreleased]

### New
- Material engine (`Theme/Materials.cs`). The Settings window can use a real Windows backdrop: Mica (default), Mica Alt or
  Acrylic on Windows 11 22H2 and later, with a solid fallback elsewhere or if Windows refuses it. Acrylic gets a theme tint so
  hint text keeps its contrast.
- Notch materials: Solid (default), Glass and Clear, with opacity (50 to 100%) and glass highlight controls in Settings,
  Appearance. These are simulated (tint, sheen, lit rim) because Windows cannot blur the notch's transparent window.
- Design tokens (`Theme/Tokens.xaml`): corner radii, spacing, type ramp, icon sizes and motion timings in one place; the shared
  styles now take their corner radii from it.

- Appearance Studio at the top of Settings, Appearance: a live preview built from real controls (hover or focus a preset to
  preview it, click to apply) and 11 presets: Orbit (follows Theme), Liquid Glass Dark and Light, Catppuccin Mocha, Nord,
  Dracula, AMOLED Black, Windows Fluent, Frosted Glass, Minimal Transparent and High Contrast. A preset sets the palette of the
  whole app (notch, panel, cards, settings, menus, tooltips) and its accent and materials, which stay adjustable.
- Preset manager: save the current look as a preset, rename, duplicate, delete (two-step), import and export
  (`.orbit-preset.json`, versioned and validated before use), restore default. Custom presets live in `appearance-presets.json`.

### Changed
- Hint and secondary text colors were raised so every built-in palette meets WCAG AA (4.5:1) for body text and AAA (7:1) for
  primary text on its opaque surfaces; a unit test enforces it.
- "Start with Windows" says Orbit Desk instead of QNotch.

### Known limitations
- Presets set colors and materials; there is no per-color editor yet (an exported preset file can be edited and imported).
- Popups and context menus are transparent windows too, so they do not get real Mica or Acrylic.
- On the test laptop (Ryzen 3 3250U) idle CPU while collapsed measures about 0.2% of one core, above the 0.1% target, for
  both 0.5.0 and this build.

## [0.5.0] - 2026-11-15

First Orbit Desk release.

### New
- Neutral "island" theme: a pure black notch in dark mode, neutral greys in light mode, monochrome controls.
- Mono accent (the default) that follows the theme, plus seven restrained accent colors in Settings, Appearance.
- Updates come from Orbit Desk's own releases (github.com/Pavanpurohit999/QNotch), never from upstream QNotch.
- The Updates page shows the release name, date and a notes excerpt for an available update.

### Fixed
- Release information never shows a raw `null`: a missing name shows the tag, missing notes show "No release notes provided.",
  a missing date shows "Date unavailable.", and a release without downloads says "No downloadable assets available."
- An empty release feed reads "No releases published yet" instead of "You're up to date"; a missing repository, invalid JSON,
  rate limiting and network failures each show their own message.
- The collapsed pill no longer keeps a stale width after segments change while the panel is open, and never grows wider than
  its window (it could spill past the screen edge when nudged).
- Smoke checks are reliable at 125% display scaling.
- Remaining "QNotch" wording in update messages and the tray menu now says Orbit Desk.

### Release and packaging
- The release workflow publishes from this repository only. Downloads keep the updater's names: `QNotch.exe`
  (needs the .NET 10 Desktop Runtime) and `QNotch-self-contained.zip` (no runtime needed).
- Upstream QNotch's Microsoft Store and package-manager publishing never runs from this fork.

### Known limitations
- The notch is a transparent, software-rendered window, so it cannot use Windows Mica or Acrylic backdrops.
- Windows 10 2004 or newer, x64 only.
