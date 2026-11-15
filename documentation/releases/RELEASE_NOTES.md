## Orbit Desk 0.5.0

The first Orbit Desk release: a monochrome island look, its own update channel and sturdier release handling.

### Highlights
- **Island theme.** A pure black notch that blends into the top of the screen, neutral grey cards, white icons. Color comes from your content (album art, weather, alerts) rather than the chrome. Light mode uses neutral greys and white cards.
- **Mono accent.** Toggles, sliders and selection marks follow the theme in black or white. Seven muted accent colors are available in Settings, Appearance.
- **Own update channel.** Orbit Desk checks this repository's releases only. Updates install only when you choose Update and restart.
- **Clear update states.** "No releases published yet", repository and network errors, and missing release details each have readable text. Nothing shows `null`.

### Fixes
- The collapsed pill keeps the right width after its contents change while the panel is open, and never overflows its window.
- Display-scaling (125%) fixes in the built-in smoke checks.

### Install
Windows 10 2004 or newer, x64.
- **`QNotch-self-contained.zip`** (recommended): unzip anywhere and run `QNotch.exe`. No .NET install needed.
- **`QNotch.exe`**: smaller, needs the [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0).

The downloads keep the names `QNotch.exe` and `QNotch-self-contained.zip` that the built-in updater expects. The app itself is Orbit Desk: its settings live in `%APPDATA%\OrbitDesk`, so it runs side by side with upstream QNotch.

### Upgrading
This is the first Orbit Desk release; there is nothing to migrate. Later versions install from Settings, Updates.

### Known limitations
- Windows does not offer Mica or Acrylic backdrops for the notch's transparent window, so it uses solid island colors.
- Each download's SHA-256 checksum is shown next to it on this page.
