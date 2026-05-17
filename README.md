# Cursor Magic

Windows-only custom cursor app built with .NET 8 WPF.

## Run

Launch:

```powershell
.\bin\Release\net8.0-windows\publish\CursorMagic.exe
```

## Install Shortcuts

Create Desktop and Start Menu shortcuts:

```powershell
.\Install-CursorMagic.ps1
```

Remove shortcuts and startup registration:

```powershell
.\Uninstall-CursorMagic.ps1
```

## What It Does

- Includes 12 generated cursor themes, including Star Wand.
- Applies cursor themes through current-user Windows cursor settings.
- Runs subtle click effects in a transparent, click-through overlay.
- Auto-pauses effects over fullscreen apps.
- Supports a per-app blocklist from the Compatibility tab or tray menu.
- Lets you import an image, set a hotspot, choose an effect/accent, and save a custom cursor theme.
- Imports and exports `.cmpack` cursor packs.
- Can start with Windows.
- Can restore your original Windows cursor scheme on exit.

Cursor packs and settings are stored in `%LOCALAPPDATA%\CursorMagic`.
