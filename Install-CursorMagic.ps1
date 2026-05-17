$ErrorActionPreference = "Stop"

$appDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$exe = Join-Path $appDir "bin\Release\net8.0-windows\publish\CursorMagic.exe"

if (-not (Test-Path $exe)) {
    throw "Could not find CursorMagic.exe. Build/publish the app first."
}

$shell = New-Object -ComObject WScript.Shell
$desktopShortcut = Join-Path ([Environment]::GetFolderPath("Desktop")) "Cursor Magic.lnk"
$startMenuDir = Join-Path ([Environment]::GetFolderPath("Programs")) "Cursor Magic"
$startMenuShortcut = Join-Path $startMenuDir "Cursor Magic.lnk"

New-Item -ItemType Directory -Path $startMenuDir -Force | Out-Null

foreach ($shortcutPath in @($desktopShortcut, $startMenuShortcut)) {
    $shortcut = $shell.CreateShortcut($shortcutPath)
    $shortcut.TargetPath = $exe
    $shortcut.WorkingDirectory = Split-Path -Parent $exe
    $shortcut.IconLocation = "$exe,0"
    $shortcut.Description = "Cursor Magic"
    $shortcut.Save()
}

Write-Host "Installed Cursor Magic shortcuts:"
Write-Host " - $desktopShortcut"
Write-Host " - $startMenuShortcut"
