<#
    Win64 gameplay driver used for the issue #11 baseline evidence (2026-11-06).

    Drives the built player with synthetic keyboard input (keybd_event) and captures the
    screen at every step. The state machine is driven by the player's own log
    ("Your scene has been loaded" = one scene load per fade-in, one "Miss"/"Perfect"/… line
    per judged note), so the key timing does not depend on guessed delays.

    Usage:
        powershell -NoProfile -ExecutionPolicy Bypass -File .\drive-gameplay.ps1 `
            -Exe 'C:\...\Builds\Windows\FlashBeat.exe' -OutDir 'C:\tmp\fb_run'
#>
param(
    [string]$Exe = 'C:\Users\rebui\Desktop\FlashBeat.11\Builds\Windows\FlashBeat.exe',
    [string]$OutDir = 'C:\tmp\fb_run3'
)

$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;
public class Win32 {
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    [DllImport("user32.dll")] public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int X, int Y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, UIntPtr dwExtraInfo);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
}
'@

$log = Join-Path $OutDir 'Player.log'
if (Test-Path $log) { Remove-Item $log }
$env:FLASHBEAT_YTDLP_PATH = 'C:\Users\rebui\AppData\Local\mise\installs\yt-dlp\2026.10.26\yt-dlp.exe'

function Log-Text {
    if (Test-Path $log) { try { return [IO.File]::ReadAllText($log) } catch { return '' } }
    return ''
}
function Scene-Count { return ([regex]::Matches((Log-Text), 'Your scene has been loaded')).Count }
function Judge-Count { return ([regex]::Matches((Log-Text), '(?m)^(Miss|Perfect|Great|Bad)\r?$')).Count }

function Wait-For([scriptblock]$cond, [int]$timeoutSec, [string]$what) {
    $deadline = (Get-Date).AddSeconds($timeoutSec)
    while ((Get-Date) -lt $deadline) {
        if (& $cond) { Write-Host "[drive] OK: $what"; return $true }
        Start-Sleep -Milliseconds 400
    }
    Write-Host "[drive] TIMEOUT: $what"
    return $false
}

function Focus-Window([IntPtr]$hwnd) {
    $r = New-Object Win32+RECT
    [void][Win32]::GetWindowRect($hwnd, [ref]$r)
    [void][Win32]::SetCursorPos([int](($r.Left + $r.Right) / 2), [int]($r.Top + 10))
    [Win32]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 60
    [Win32]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 250
    [void][Win32]::SetForegroundWindow($hwnd)
    Start-Sleep -Milliseconds 250
}

function Send-Key([IntPtr]$hwnd, [int]$vk, [string]$name) {
    Focus-Window $hwnd
    Write-Host "[drive] key $name at $(Get-Date -Format HH:mm:ss)"
    [Win32]::keybd_event([byte]$vk, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 120
    [Win32]::keybd_event([byte]$vk, 0, 2, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 300
}

function Save-Shot([IntPtr]$hwnd, [string]$file) {
    $r = New-Object Win32+RECT
    [void][Win32]::GetWindowRect($hwnd, [ref]$r)
    $w = $r.Right - $r.Left; $h = $r.Bottom - $r.Top
    if ($w -le 0 -or $h -le 0) { Write-Host "[drive] bad rect"; return }
    $bmp = New-Object System.Drawing.Bitmap($w, $h)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($r.Left, $r.Top, 0, 0, (New-Object System.Drawing.Size($w, $h)))
    $bmp.Save((Join-Path $OutDir $file), [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $bmp.Dispose()
    Write-Host "[drive] shot $file"
}

Write-Host "[drive] launching $Exe"
$p = Start-Process -FilePath $Exe -ArgumentList @('-logFile', "`"$log`"") -PassThru

$hwnd = [IntPtr]::Zero
for ($i = 0; $i -lt 120; $i++) {
    Start-Sleep -Milliseconds 500
    $p.Refresh()
    if ($p.HasExited) { Write-Host "[drive] exited early code=$($p.ExitCode)"; exit 2 }
    if ($p.MainWindowHandle -ne [IntPtr]::Zero) { $hwnd = $p.MainWindowHandle; break }
}
if ($hwnd -eq [IntPtr]::Zero) { Write-Host "[drive] no window"; exit 2 }
Write-Host "[drive] window=$hwnd"

# 1) wait for TitleScene
[void](Wait-For { (Scene-Count) -ge 2 } 60 'TitleScene')
Save-Shot $hwnd '01_title.png'

# 2) single Space -> SelectScene (capture it before it can cascade)
Send-Key $hwnd 0x20 'Space'
Start-Sleep -Milliseconds 900
Save-Shot $hwnd '02_select.png'
Start-Sleep -Seconds 2
Save-Shot $hwnd '03_select.png'

# 3) move the selection up one slot (No.1 -> No.2)
Send-Key $hwnd 0x4B 'K'
Start-Sleep -Seconds 1
Save-Shot $hwnd '04_select_song2.png'

# 4) Space -> GameScene
Send-Key $hwnd 0x20 'Space'
Start-Sleep -Seconds 4
Save-Shot $hwnd '05_game.png'

# 5) Space -> start the song (8s pre-roll)
Start-Sleep -Seconds 10
Send-Key $hwnd 0x20 'Space'
Start-Sleep -Seconds 4
Save-Shot $hwnd '06_game_playing.png'
Write-Host "[drive] judge lines after start = $(Judge-Count)"

# 6) wait for ResultScene
[void](Wait-For { (Scene-Count) -ge 8 } 100 'ResultScene')
Start-Sleep -Seconds 2
Save-Shot $hwnd '07_result.png'
Write-Host "[drive] judge lines at result = $(Judge-Count), scenes = $(Scene-Count)"

# 7) Retry (R)
Send-Key $hwnd 0x52 'R'
Start-Sleep -Seconds 8
Save-Shot $hwnd '08_retry.png'
Write-Host "[drive] final scenes = $(Scene-Count)"

try { $p.CloseMainWindow() | Out-Null } catch {}
Start-Sleep -Seconds 3
if (-not $p.HasExited) { $p.Kill() }
Write-Host "[drive] done"
