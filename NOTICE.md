# Notice

Orbit Desk is a fork of **QNotch** (https://github.com/Somafet/qnotch), forked from upstream commit
`cad0d4ef5dd3a8851c09505e89c3f931a8816692` (2026-11-12).

QNotch is Copyright (c) 2026 Soma Somorjai and is distributed under the MIT License, reproduced in full in `LICENSE`.
That copyright and permission notice must remain in every copy or substantial portion of this software.

Orbit Desk modifications are Copyright (c) 2026 the Orbit Desk authors, also under the MIT License.

Third-party material carried over from QNotch:

- Weather symbols in `src/QNotch/Modules/Weather/Icons/` keep their own licence file in that folder (yr.no / MET Norway weather icons).
- NuGet: CommunityToolkit.Mvvm (MIT).

Orbit Desk changes from upstream (summary; see CHANGELOG.md):

- Product renamed to Orbit Desk; separate data folder (`%APPDATA%\OrbitDesk`), mutex, pipes and autostart entry, so it can run beside QNotch.
- Usage analytics removed (no key shipped) and off by default.
- Update checks never contact upstream QNotch releases; disabled until `Brand.UpdateRepository` names your own repo.
- Neutral "island" palettes (pure black notch in dark mode, neutral greys in light) and a monochrome "Mono" accent by default.
- macOS port, website and founder material removed (Windows-only fork).
