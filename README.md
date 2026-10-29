# WinSpotLight

A WinSpotLight-style launcher for Windows. It runs in the system tray and opens a
search bar on a global hotkey (default `Alt+Space`).

## Features

- Search installed apps and Start Menu shortcuts, files, system commands, and
  evaluate math expressions.
- Global hotkey (configurable via the tray menu → **Settings**).
- Single-instance: launching it again re-opens the existing search bar.
- "Run at startup" toggle in the tray menu.

## Build locally

```powershell
dotnet build -c Release
```

Pull requests run a CI build check (`.github/workflows/build.yml`) that must pass
before merging.

## Build the installer

Requires [Inno Setup 6](https://jrsoftware.org/isinfo.php). Publish first, then
compile the installer:

```powershell
dotnet publish WinSpotLight.csproj -c Release -r win-x64 --self-contained true -o publish
$env:APP_VERSION = "1.0.0"
& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" installer.iss
```

The setup executable is written to `dist\`.

## Releases

Pushing to `main` triggers the
[`.github/workflows/release.yml`](.github/workflows/release.yml) workflow, which
builds the installer and publishes a new GitHub release automatically. The
version is auto-incremented from the latest `v*.*.*` tag (starting at `1.0.0`).
