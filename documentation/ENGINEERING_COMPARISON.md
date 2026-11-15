# Foundation comparison (2026-11-15)

| Candidate | Licence | Stack | Last activity | Fit for Orbit Desk |
|---|---|---|---|---|
| **QNotch** (Somafet/qnotch) | MIT | C# .NET 10 WPF, only dep CommunityToolkit.Mvvm | 2026-11-12 | ~80% of the spec already built: pill/panel, media (GSMTC), stats (PDH, GPU temp), clipboard (in-memory default), file tray + QR phone transfer, notes, weather (MET Norway, free), notifications, search, Claude Code/Codex agents, Game mode, multi-monitor, autostart, snapshot/smoke/measure tooling, ~32k lines. **Chosen.** |
| DynamicWin (FlorianButz) | CC BY-SA 4.0 | C# WPF | 2025-10 | Share-alike content licence, unusual for code; inactive a year. Rejected. |
| V-Notch (w0otdev) | Apache-2.0 | C# .NET 8 | 2026-11-14 | Smaller, older .NET. Reference only. |
| Electron notches (Windows Notch Overlay, Ripple) | various | Electron | 2026 | Rejected: spec forbids heavy browser frameworks. |

## Gaps in QNotch vs the Orbit Desk spec (work plan)

1. Telemetry on by default (PostHog) -> **removed, off** (done).
2. Updater pulls upstream QNotch releases -> **disabled until own repo** (done).
3. Phone transfer is plain HTTP + 128-bit token, 15-min session -> add AES-256-GCM in-page encryption with key in the QR `#fragment`, explicit incoming-file approval, size limits (planned; WebCrypto is unavailable on plain-HTTP LAN origins, so a bundled audited JS AES-GCM is needed).
4. Neutral island theme with Mono accent (done, replaced the earlier Catppuccin palette), Inter + JetBrains Mono Nerd Font (planned, fonts are OFL).
5. Missing modules: Notes as SQLite with tasks/due dates/import-export; stopwatch/countdown/reminders; DPAPI-encrypted opt-in clipboard persistence (verify); presentation mode; floating dock; desktop widget framework; Windows UserNotificationListener integration.
6. Packaging: per-user Inno Setup installer (no admin), portable zip, scripts/ (build, test, publish, diagnose, backup, restore), documentation/.
7. Terminal environment: WezTerm Lua config, PowerShell 7 profile, Starship etc. (separate from the app).
