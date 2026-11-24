---
trigger: always_on
description: "Mandatory automatic version bump across installer, Program.cs, and package.json."
---

# Mandatory Automatic Version Bump Rule

Whenever you make any changes, add new features, fix bugs, or rebuild the installer for this project:
- **YOU MUST ALWAYS BUMP THE VERSION NUMBER AUTOMATICALLY.** Never wait for the user to ask or remind you.
- Increment the version:
  - From 1.9 -> 2.0 (or next minor/major version).
- Synchronize the new version number in:
  1. `installer\DolphinXVDO_Setup.iss` (`#define MyAppVersion "X.X"`)
  2. `DolphinXVDO\src\Program.cs` (Window titles, Dialog titles, Tray text, About text)
  3. `DolphinXVDO\package.json` (`"version": "X.X.0"`)
- Ensure the resulting installer is built with the new version name: `DolphinXvdo-Setup-vX.X.exe`.
