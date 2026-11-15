# Changelog

All notable changes to Orbit Desk. Orbit Desk is a fork of [QNotch](https://github.com/Somafet/qnotch) (MIT); see NOTICE.md.
Versions follow [semantic versioning](https://semver.org/). Release tags are `vX.Y.Z`.

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
