$ErrorActionPreference = "Stop"

$install = Join-Path $env:LOCALAPPDATA "Programs\GTA6Countdown"
$shortcutName = "GTA VI " + [char]0x5012 + [char]0x8BA1 + [char]0x65F6 + ".lnk"

Get-CimInstance Win32_Process |
    Where-Object { $_.CommandLine -match "GTA6Countdown\.(exe|dll)" } |
    ForEach-Object {
        try { Stop-Process -Id $_.ProcessId -Force -ErrorAction Stop } catch { }
    }

$runKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"
Remove-ItemProperty -Path $runKey -Name "GTA6Countdown" -ErrorAction SilentlyContinue

$desktop = [Environment]::GetFolderPath("Desktop")
$programs = [Environment]::GetFolderPath("Programs")
Remove-Item -LiteralPath (Join-Path $desktop $shortcutName) -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath (Join-Path $programs $shortcutName) -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath (Join-Path $env:LOCALAPPDATA "GTA6Countdown\GTA6CountdownLauncher.vbs") -Force -ErrorAction SilentlyContinue

if (Test-Path $install) {
    Remove-Item -LiteralPath $install -Recurse -Force
}

Write-Host "GTA VI countdown uninstalled. Data and logs remain in $env:LOCALAPPDATA\GTA6Countdown."
