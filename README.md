# TimeSnap

TimeSnap is a small Windows tray app that takes screenshots via a global hotkey and automatically displays them in a weekly view, sorted by calendar week and weekday. It's designed to support your own time tracking — a quick screenshot documents what you were working on at any given moment.

## Main Features

- Runs quietly in the background in the system tray (notification area)
- Take a screenshot via a configurable global hotkey (default **Ctrl+Alt+Shift+Y**), even when the app isn't in the foreground
- Screenshots are automatically sorted and stored by calendar week (ISO week) and weekday, optionally including the weekend
- Also captures the title of the window that's actually in front on the captured screen
- Multi-monitor support: choose which connected displays to capture; each selected monitor is saved as its own screenshot
- Weekly view with navigation between weeks and a jump-to-current-week shortcut
- Image preview with forward/back navigation between a day's screenshots
- Multilingual UI (German/English), selectable manually or following the system language
- Settings page to configure:
  - Hotkey combination (recorded live, with a check against combinations already used by other programs)
  - Optional sound on screenshot
  - Which screen(s) to capture
  - Screenshot storage location (with an option to move existing screenshots when changed)
  - Language (German/English/system default)
  - Appearance (system/light/dark)
  - Whether the weekend is shown in the weekly view
  - Deleting all saved screenshots

## Requirements

- Windows 10/11
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) (x64/x86/ARM64, matching your system)

## Running the Release Build

1. Make sure the .NET 8 Desktop Runtime is installed.
2. Build the app via `dotnet publish` for the desired architecture, e.g.:
   ```
   dotnet publish -c Release -r win-x64 --self-contained false
   ```
3. Run `TimeSnap.exe` from the publish output folder. The app minimizes to the tray; screenshots are stored under `Pictures\TimeSnap\Screenshots`.
