$ErrorActionPreference = "Stop"

$desktopShortcut = Join-Path ([Environment]::GetFolderPath("Desktop")) "Cursor Magic.lnk"
$startMenuDir = Join-Path ([Environment]::GetFolderPath("Programs")) "Cursor Magic"
$startupKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"

Remove-Item -LiteralPath $desktopShortcut -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $startMenuDir -Recurse -Force -ErrorAction SilentlyContinue
Remove-ItemProperty -Path $startupKey -Name "CursorMagic" -ErrorAction SilentlyContinue

Write-Host "Removed Cursor Magic shortcuts and startup registration."
