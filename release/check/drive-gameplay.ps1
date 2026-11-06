<#
    .SYNOPSIS
        Win64 gameplay smoke driver for the FlashBeat release gate (#22).

        This is the driver written for the #11 baseline (docs/evidence/win64-2026-11-06/drive-gameplay.ps1
        on branch 11), reused unchanged in its core logic: it drives the built player with synthetic
        keyboard input and captures the screen at every step. The state machine is driven by the player's
        own log ("Your scene has been loaded" = one scene load per fade-in, one "Miss"/"Perfect"/... line
        per judged note), so the key timing does not depend on guessed delays.

        What changed for the release gate: the artifact path, the output directory and the window
        geometry are parameters, the run summary is written as JSON, and the startup time is measured.

        It does NOT build anything, does not touch the Unity Editor and does not modify the artifact.

    .EXAMPLE
        powershell -NoProfile -ExecutionPolicy Bypass -File release/check/drive-gameplay.ps1 `
            -Exe 'D:\4_apps\Unity\Build\FlashBeatBuild\Build0.1.0-win64-1c5137e\FlashBeat.exe' `
            -OutDir 'D:\4_apps\Unity\Build\FlashBeatEvidence\2026-11-06-S0-archive' `
            -Width 1280 -Height 720 -Fullscreen 0
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Exe,
    [Parameter(Mandatory = $true)][string]$OutDir,
    [int]$Width = 1280,
    [int]$Height = 720,
    # 0 = windowed (the 1280x720 UX smoke), 1 = fullscreen window, 2 = exclusive fullscreen, 3 = borderless
    [int]$Fullscreen = 0,
    [string]$YtDlpPath = '',
    [string]$LogName = 'Player.log',
    [string]$SummaryName = 'run-summary.json',
    [int]$NextSongKey = 0x4B,      # K: move the selection from No.1 to No.2
    [int]$TitleTimeoutSec = 90,
    [int]$ResultTimeoutSec = 240,
    [switch]$SkipRetry
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

$log = Join-Path $OutDir $LogName
if (Test-Path $log) { Remove-Item $log }
if ($YtDlpPath) { $env:FLASHBEAT_YTDLP_PATH = $YtDlpPath }

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
    if ($w -le 0 -or $h -le 0) { Write-Host '[drive] bad rect'; return $null }
    $bmp = New-Object System.Drawing.Bitmap($w, $h)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($r.Left, $r.Top, 0, 0, (New-Object System.Drawing.Size($w, $h)))
    $path = Join-Path $OutDir $file
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $bmp.Dispose()
    Write-Host "[drive] shot $file ($($w)x$h)"
    return $path
}

if (-not (Test-Path -LiteralPath $Exe)) { throw "artifact not found: $Exe" }

$launchArgs = @('-logFile', "`"$log`"", '-screen-width', "$Width", '-screen-height', "$Height", '-screen-fullscreen', "$Fullscreen")

Write-Host "[drive] launching $Exe"
Write-Host "[drive] args: $($launchArgs -join ' ')"
$started = Get-Date
$p = Start-Process -FilePath $Exe -ArgumentList $launchArgs -PassThru

$hwnd = [IntPtr]::Zero
for ($i = 0; $i -lt 120; $i++) {
    Start-Sleep -Milliseconds 500
    $p.Refresh()
    if ($p.HasExited) { Write-Host "[drive] exited early code=$($p.ExitCode)"; exit 2 }
    if ($p.MainWindowHandle -ne [IntPtr]::Zero) { $hwnd = $p.MainWindowHandle; break }
}
if ($hwnd -eq [IntPtr]::Zero) { Write-Host '[drive] no window'; exit 2 }
Write-Host "[drive] window=$hwnd"

# 1) TitleScene
$titleOk = Wait-For { (Scene-Count) -ge 2 } $TitleTimeoutSec 'TitleScene'
$startupSec = [math]::Round(((Get-Date) - $started).TotalSeconds, 1)
Write-Host "[drive] startup to TitleScene = $startupSec s"
Save-Shot $hwnd '01_title.png' | Out-Null

# 2) Space -> SelectScene
Send-Key $hwnd 0x20 'Space'
Start-Sleep -Milliseconds 900
Save-Shot $hwnd '02_select.png' | Out-Null
Start-Sleep -Seconds 2
Save-Shot $hwnd '03_select.png' | Out-Null

# 3) move the selection (No.1 -> No.2)
Send-Key $hwnd $NextSongKey 'NextSong'
Start-Sleep -Seconds 1
Save-Shot $hwnd '04_select_song2.png' | Out-Null

# 4) Space -> GameScene
Send-Key $hwnd 0x20 'Space'
Start-Sleep -Seconds 4
Save-Shot $hwnd '05_game.png' | Out-Null

# 5) Space -> start the song (8s pre-roll)
Start-Sleep -Seconds 10
Send-Key $hwnd 0x20 'Space'
Start-Sleep -Seconds 4
Save-Shot $hwnd '06_game_playing.png' | Out-Null
$judgeAfterStart = Judge-Count
Write-Host "[drive] judge lines after start = $judgeAfterStart"

# 6) ResultScene
$resultOk = Wait-For { (Scene-Count) -ge 8 } $ResultTimeoutSec 'ResultScene'
Start-Sleep -Seconds 2
Save-Shot $hwnd '07_result.png' | Out-Null
$judgeAtResult = Judge-Count
Write-Host "[drive] judge lines at result = $judgeAtResult, scenes = $(Scene-Count)"

# 7) Retry (R)
$retried = $false
if (-not $SkipRetry) {
    Send-Key $hwnd 0x52 'R'
    Start-Sleep -Seconds 8
    Save-Shot $hwnd '08_retry.png' | Out-Null
    Write-Host "[drive] final scenes = $(Scene-Count)"
    $retried = ((Scene-Count) -ge 9)
}

try { $p.CloseMainWindow() | Out-Null } catch {}
Start-Sleep -Seconds 3
if (-not $p.HasExited) { $p.Kill() }

$summary = [ordered]@{
    startedAt        = $started.ToString('yyyy-MM-ddTHH:mm:ssK')
    exe              = $Exe
    outDir           = $OutDir
    resolution       = "$Width x $Height (fullscreen=$Fullscreen)"
    startupSeconds   = $startupSec
    titleReached     = [bool]$titleOk
    resultReached    = [bool]$resultOk
    retryReloaded    = [bool]$retried
    scenes           = (Scene-Count)
    judgedNotes      = $judgeAtResult
    logFile          = $log
}
$summaryPath = Join-Path $OutDir $SummaryName
$summary | ConvertTo-Json | Set-Content -LiteralPath $summaryPath -Encoding UTF8
Write-Host "[drive] summary -> $summaryPath"
Write-Host '[drive] done'
