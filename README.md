# TimeSnap

TimeSnap is a small Windows tray app that takes screenshots via a global hotkey and automatically displays them in a weekly view, sorted by calendar week and weekday. It's designed to support your own time tracking — a quick screenshot documents what you were working on at any given moment.

## Main Features

- Runs quietly in the background in the system tray (notification area)
- Take a screenshot via the global hotkey **Ctrl+Alt+Shift+Y**, even when the app isn't in the foreground
- Screenshots are automatically sorted and stored by calendar week (ISO week) and weekday (Monday–Friday)
- Also captures the title of the most recently active window for each screenshot
- Weekly view with navigation between weeks and a jump-to-current-week shortcut
- Image preview with forward/back navigation between a day's screenshots
- Multilingual UI (German/English, depending on system language)

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
