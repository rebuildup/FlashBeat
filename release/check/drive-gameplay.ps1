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
    [switch]$SkipRetry,
    # press lane keys while the song plays, so the judgement is exercised on real input
    # (otherwise every note is judged Miss because nobody plays)
    [switch]$PlayLanes,
    # Shared machine: start even though other player instances are open (they fight over focus).
    [switch]$AllowConcurrentPlayers
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
    [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);
    [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int X, int Y);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, UIntPtr dwExtraInfo);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
}
'@

$log = Join-Path $OutDir $LogName
if (Test-Path $log) { Remove-Item $log }
if ($YtDlpPath) { $env:FLASHBEAT_YTDLP_PATH = $YtDlpPath }

function Log-Text {
    # The player keeps the log file open for writing: read it with FileShare.ReadWrite so the
    # scene / judge counters follow the live log instead of lagging behind it.
    if (-not (Test-Path -LiteralPath $log)) { return '' }
    try {
        $fs = [IO.File]::Open($log, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::ReadWrite)
        try {
            $sr = New-Object IO.StreamReader($fs)
            return $sr.ReadToEnd()
        } finally { $fs.Dispose() }
    } catch { return '' }
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

# The player window can be recreated (resolution switch, scene change), so the handle is
# resolved again for every interaction instead of being cached once.
function Find-PlayerWindow([int]$processId) {
    $best = [IntPtr]::Zero
    $bestArea = 0
    $callback = [Win32+EnumWindowsProc]{
        param($h, $l)
        $winPid = 0
        [void][Win32]::GetWindowThreadProcessId($h, [ref]$winPid)
        if ($winPid -eq $processId -and [Win32]::IsWindowVisible($h)) {
            $r = New-Object Win32+RECT
            [void][Win32]::GetClientRect($h, [ref]$r)
            $area = ($r.Right - $r.Left) * ($r.Bottom - $r.Top)
            if ($area -gt $bestArea) { $script:foundArea = $area; $script:foundWindow = $h }
        }
        return $true
    }
    $script:foundWindow = [IntPtr]::Zero
    [void][Win32]::EnumWindows($callback, [IntPtr]::Zero)
    $best = $script:foundWindow
    return $best
}

# True when the foreground window belongs to the player process.
function Test-ForegroundIsPlayer([int]$processId) {
    $fg = [Win32]::GetForegroundWindow()
    if ($fg -eq [IntPtr]::Zero) { return $false }
    $fgPid = 0
    [void][Win32]::GetWindowThreadProcessId($fg, [ref]$fgPid)
    return ($fgPid -eq $processId)
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

function Send-Key($proc, [int]$vk, [string]$name) {
    try { $proc.Refresh() } catch {}
    $hwnd = Find-PlayerWindow $proc.Id
    if ($hwnd -eq [IntPtr]::Zero) {
        Write-Host "[drive] ABORT: the player window is gone - stopping"
        exit 3
    }
    Focus-Window $hwnd
    # Safety: another automation on this machine would steal the focus and receive our keystrokes.
    # Abort instead of corrupting somebody else's run. The comparison is by process id:
    # the window handle may change while the game runs.
    if (-not (Test-ForegroundIsPlayer $proc.Id)) {
        Write-Host "[drive] ABORT: foreground window is not the player (another automation is running) - stopping to avoid interference"
        exit 3
    }
    Write-Host "[drive] key $name at $(Get-Date -Format HH:mm:ss)"
    [Win32]::keybd_event([byte]$vk, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 120
    [Win32]::keybd_event([byte]$vk, 0, 2, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 300
}

function Save-Shot($proc, [string]$file) {
    try { $proc.Refresh() } catch {}
    $hwnd = Find-PlayerWindow $proc.Id
    if ($hwnd -eq [IntPtr]::Zero) {
        Write-Host "[drive] shot $file skipped (player window not found)"
        return $null
    }
    # restore + focus first: a minimised or background window has an empty rect and no visible pixels
    [void][Win32]::ShowWindow($hwnd, 9)   # SW_RESTORE
    [void][Win32]::SetForegroundWindow($hwnd)
    Start-Sleep -Milliseconds 300

    $r = New-Object Win32+RECT
    [void][Win32]::GetClientRect($hwnd, [ref]$r)
    $w = $r.Right - $r.Left; $h = $r.Bottom - $r.Top
    if ($w -le 0 -or $h -le 0) {
        Write-Host "[drive] bad client rect ($w x $h)"
        return $null
    }
    $origin = New-Object Win32+POINT
    [void][Win32]::ClientToScreen($hwnd, [ref]$origin)
    $path = Join-Path $OutDir $file
    try {
        $bmp = New-Object System.Drawing.Bitmap($w, $h)
        $g = [System.Drawing.Graphics]::FromImage($bmp)
        $g.CopyFromScreen($origin.X, $origin.Y, 0, 0, (New-Object System.Drawing.Size($w, $h)))
        $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
        $g.Dispose(); $bmp.Dispose()
        Write-Host "[drive] shot $file (client $($w)x$h at $($origin.X),$($origin.Y))"
    } catch {
        # 画面がロックされている / 空港モードなどで CopyFromScreen が失敗する。
        # スクリーンショットは証跡の補助なので、失敗してもゲーム検証は続行する
        # (判定・シーン遷移・譜面同期は Player.log が証明する)。
        Write-Host "[drive] shot $file failed: $($_.Exception.Message)"
        return $null
    }
    return $path
}

if (-not (Test-Path -LiteralPath $Exe)) { throw "artifact not found: $Exe" }

# Preflight: another player instance (e.g. a parallel worker automation) would fight over the
# keyboard and the focus. Refuse to start instead of corrupting the other run.
# -AllowConcurrentPlayers: only when the other instances are known to be idle (shared machine).
$others = @(Get-Process -Name 'FlashBeat' -ErrorAction SilentlyContinue)
if ($others.Count -gt 0) {
    if ($AllowConcurrentPlayers) {
        Write-Host ("[drive] WARNING: {0} other FlashBeat.exe process(es) are running (ids: {1}) and -AllowConcurrentPlayers was given." -f $others.Count, (($others | ForEach-Object { $_.Id }) -join ', '))
        Write-Host '[drive]          The run can be disturbed by the other automation; treat it as unreliable if that happens.'
    } else {
        Write-Host ("[drive] ABORT: {0} other FlashBeat.exe process(es) are running (ids: {1})." -f $others.Count, (($others | ForEach-Object { $_.Id }) -join ', '))
        Write-Host '[drive]         Close them (or wait for the other automation) and run this again.'
        exit 4
    }
}

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
Save-Shot $p '01_title.png' | Out-Null

# 2) Space -> SelectScene
Send-Key $p 0x20 'Space'
Start-Sleep -Milliseconds 900
Save-Shot $p '02_select.png' | Out-Null
Start-Sleep -Seconds 2
Save-Shot $p '03_select.png' | Out-Null

# 3) move the selection (No.1 -> No.2)
Send-Key $p $NextSongKey 'NextSong'
Start-Sleep -Seconds 1
Save-Shot $p '04_select_song2.png' | Out-Null

# 4) Space -> GameScene
Send-Key $p 0x20 'Space'
Start-Sleep -Seconds 4
Save-Shot $p '05_game.png' | Out-Null

# 5) Space -> start the song (8s pre-roll)
Start-Sleep -Seconds 10
Send-Key $p 0x20 'Space'
Start-Sleep -Seconds 4
Save-Shot $p '06_game_playing.png' | Out-Null
$judgeAfterStart = Judge-Count
Write-Host "[drive] judge lines after start = $judgeAfterStart"

# 5b) the #13 build prepares the media first and only then asks for a Space
#     ("[MusicManager] media ready: ... - press Space to start"). If that happened, the first
#     Space was consumed too early -> press it again (max 5 times).
$extraStarts = 0
while ($extraStarts -lt 5 -and (Judge-Count) -eq 0) {
    if ((Log-Text) -match 'press Space to start') {
        Start-Sleep -Seconds 2
        Send-Key $p 0x20 'Space(start)'
        $extraStarts++
        Start-Sleep -Seconds 3
        Write-Host "[drive] extra start key #$extraStarts, judge lines = $(Judge-Count)"
    } else {
        Start-Sleep -Seconds 2
    }
}
if ($extraStarts -gt 0) { Save-Shot $p '06b_game_playing.png' | Out-Null }

# 6) ResultScene (optionally press lane keys, so the judgement is exercised on real input)
$laneKeys = @(0x51, 0x57, 0x44, 0x46, 0x4A)   # Q W D F J  (lanes 0 1 2 3 4)
$laneIdx = 0
$resultOk = $false
$deadline = (Get-Date).AddSeconds($ResultTimeoutSec)
while ((Get-Date) -lt $deadline) {
    if ((Scene-Count) -ge 8) { $resultOk = $true; Write-Host '[drive] OK: ResultScene'; break }
    if ($PlayLanes) {
        Send-Key $p $laneKeys[$laneIdx % $laneKeys.Count] ('lane' + ($laneIdx % $laneKeys.Count))
        $laneIdx++
    } else {
        Start-Sleep -Milliseconds 400
    }
}
if (-not $resultOk) { Write-Host '[drive] TIMEOUT: ResultScene' }
Start-Sleep -Seconds 2
Save-Shot $p '07_result.png' | Out-Null
$judgeAtResult = Judge-Count
Write-Host "[drive] judge lines at result = $judgeAtResult, scenes = $(Scene-Count)"

# 7) Retry (R)
$retried = $false
if (-not $SkipRetry) {
    Send-Key $p 0x52 'R'
    Start-Sleep -Seconds 8
    Save-Shot $p '08_retry.png' | Out-Null
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
