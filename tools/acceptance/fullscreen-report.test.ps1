#requires -Version 5.1
<#
.SYNOPSIS
    `fullscreen-report.ps1` の判定ロジックの自己テスト。Windows は不要 (macOS + pwsh で走る)。

.DESCRIPTION
    実機の Player.log を待たずに、判定式と表記ゆれの扱いを固定する。
    ケースが 1 つでも落ちたら終了コード 1 を返す。

.EXAMPLE
    pwsh -NoProfile -File tools/acceptance/fullscreen-report.test.ps1
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'fullscreen-report.ps1')

$script:passed = 0
$script:failed = 0

function Assert-Equal($Expected, $Actual, [string]$What) {
    if ([string]$Expected -eq [string]$Actual) {
        $script:passed++
    } else {
        $script:failed++
        Write-Host ("FAIL " + $What + ": expected=" + [string]$Expected + " actual=" + [string]$Actual)
    }
}

function New-LogLine {
    param(
        [string]$Screen,
        [string]$FullScreen = 'True',
        [string]$Mode = 'FullScreenWindow',
        [string]$System = '1920x1080',
        [string]$Rendering = '1920x1080'
    )
    return '[FullscreenDiagnostics] event=startup, screen=' + $Screen +
        ', currentResolution=' + $System + '@60Hz, fullScreen=' + $FullScreen +
        ', fullScreenMode=' + $Mode + ', dpi=96, safeArea=0,0,' + $Screen +
        ', displays=1, display0=system:' + $System + ',rendering:' + $Rendering +
        ',active:True, quality=1, time=2026-11-11T10:24:47Z'
}

# --- 1. 実フルスクリーン FHD: 全部 PASS -------------------------------------
$line = New-LogLine -Screen '1920x1080'
$r = ConvertFrom-FullscreenDiagnostics -Line $line -Expected '1920x1080'
Assert-Equal '1920x1080' $r.screen 'fhd screen'
Assert-Equal 'True' $r.fullScreen 'fhd fullScreen'
Assert-Equal 'FullScreenWindow' $r.fullScreenMode 'fhd mode'
Assert-Equal '100' $r.coverageX 'fhd coverageX'
Assert-Equal '100' $r.coverageY 'fhd coverageY'
Assert-Equal 'PASS' $r.checkFullscreen 'fhd checkFullscreen'
Assert-Equal 'PASS' $r.checkCoverage 'fhd checkCoverage'
Assert-Equal 'PASS' $r.checkResolution 'fhd checkResolution'
Assert-Equal 'PASS' $r.result 'fhd result'

# --- 2. ウィンドウ表示 (擬似全画面) は FAIL ----------------------------------
$line = New-LogLine -Screen '1600x900' -FullScreen 'False' -Mode 'Windowed' -Rendering '1600x900'
$r = ConvertFrom-FullscreenDiagnostics -Line $line -Expected '1920x1080'
Assert-Equal 'FAIL' $r.checkFullscreen 'windowed checkFullscreen'
Assert-Equal 'FAIL' $r.checkCoverage 'windowed checkCoverage'
Assert-Equal 'FAIL' $r.result 'windowed result'

# --- 3. fullScreenMode が Windowed のままなら fullScreen=True でも FAIL -------
$line = New-LogLine -Screen '1920x1080' -FullScreen 'True' -Mode 'MaximizedWindow'
$r = ConvertFrom-FullscreenDiagnostics -Line $line -Expected '1920x1080'
Assert-Equal 'FAIL' $r.checkFullscreen 'maximized checkFullscreen'
Assert-Equal 'FAIL' $r.result 'maximized result'

# --- 4. 被覆 99% 未満 (影・枠で描画領域が縮んでいる) は FAIL -----------------
$line = New-LogLine -Screen '1900x1070' -Rendering '1900x1070'
$r = ConvertFrom-FullscreenDiagnostics -Line $line -Expected ''
Assert-Equal 'FAIL' $r.checkCoverage 'coverage 98.9 checkCoverage'
Assert-Equal 'NOT-CHECKED' $r.checkResolution 'coverage no expected checkResolution'
Assert-Equal 'FAIL' $r.result 'coverage result'

# 99.0% ちょうどは PASS (閾値の境界)
$line = New-LogLine -Screen '1901x1069' -Rendering '1901x1069'
$r = ConvertFrom-FullscreenDiagnostics -Line $line -Expected ''
# 1901/1920 = 99.010%, 1069/1080 = 98.981% -> FAIL 側 (Y が不足)
Assert-Equal 'FAIL' $r.checkCoverage 'coverage boundary checkCoverage'

# --- 5. 期待解像度と違う実ディスプレイは FAIL --------------------------------
$line = New-LogLine -Screen '2560x1664' -System '2560x1664' -Rendering '2560x1664'
$r = ConvertFrom-FullscreenDiagnostics -Line $line -Expected '1920x1080'
Assert-Equal 'FAIL' $r.checkResolution 'wrong display checkResolution'
Assert-Equal 'PASS' $r.checkCoverage 'wrong display checkCoverage'
Assert-Equal 'FAIL' $r.result 'wrong display result'

# --- 6. 2K も同じ式で PASS ---------------------------------------------------
$line = New-LogLine -Screen '2560x1440' -System '2560x1440' -Rendering '2560x1440'
$r = ConvertFrom-FullscreenDiagnostics -Line $line -Expected (Resolve-ExpectedResolution '2K')
Assert-Equal '2560x1440' $r.expected '2k expected'
Assert-Equal 'PASS' $r.result '2k result'

# --- 7. ログが無い / 行が無い場合は NO-DATA (FAIL と区別する) -----------------
$r = ConvertFrom-FullscreenDiagnostics -Line '' -Expected '1920x1080'
Assert-Equal 'NO-DATA' $r.result 'no line result'
Assert-Equal 'NO-DATA' $r.checkFullscreen 'no line checkFullscreen'
Assert-Equal '' $r.screen 'no line screen'

$null = Find-FullscreenDiagnosticsLine -Lines @('startup', '[MusicManager] x', '')
Assert-Equal '' ([string](Find-FullscreenDiagnosticsLine -Lines @('a', 'b'))) 'no diagnostics line'

# --- 8. 再起動を繰り返したログは最後の起動行を使う ---------------------------
$first = New-LogLine -Screen '1280x720' -FullScreen 'False' -Mode 'Windowed' -Rendering '1280x720'
$second = New-LogLine -Screen '1920x1080'
$found = Find-FullscreenDiagnosticsLine -Lines @('boot 1', $first, 'boot 2', $second)
Assert-Equal $second $found 'last startup line'
$r = ConvertFrom-FullscreenDiagnostics -Line $found -Expected '1920x1080'
Assert-Equal 'PASS' $r.result 'last startup result'

# --- 9. 期待解像度の表記ゆれ ------------------------------------------------
Assert-Equal '1920x1080' (Resolve-ExpectedResolution 'FHD') 'alias FHD'
Assert-Equal '1920x1080' (Resolve-ExpectedResolution '1920×1080') 'multiplication sign'
Assert-Equal '2560x1440' (Resolve-ExpectedResolution '2k') 'alias 2k lower'
Assert-Equal '2560x1664' (Resolve-ExpectedResolution 'mac') 'alias mac'
Assert-Equal '' (Resolve-ExpectedResolution 'fullhd') 'unknown alias'
Assert-Equal '' (Resolve-ExpectedResolution '') 'empty expected'

# --- 10. 証跡ファイルの行に result と raw が入る -----------------------------
$line = New-LogLine -Screen '1920x1080'
$r = ConvertFrom-FullscreenDiagnostics -Line $line -Expected '1920x1080'
$text = (Format-FullscreenReport -Report $r) -join "`n"
if ($text.Contains('result=PASS') -and $text.Contains('raw=[FullscreenDiagnostics]')) {
    $script:passed++
} else {
    $script:failed++
    Write-Host 'FAIL format: result/raw が証跡行に無い'
}

# --- 11. macOS の実測行: system が描画面と違っても FAIL にしない --------------
# docs/evidence/v6-repro-2026-11-11/mac-2560x1664-r28-0-1-23/Player.log の実測値。
# display0.system (3420x2224) は OS のシステム面で、描画面 2560x1664 と一致しない。
$macLine = '[FullscreenDiagnostics] event=startup, screen=2560x1664, currentResolution=2560x1664@60.0024000960038Hz, fullScreen=True, fullScreenMode=FullScreenWindow, dpi=299, safeArea=0,0,2560x1664, displays=1, display0=system:3420x2224,rendering:2560x1664,active:True, quality=1, time=2026-11-11T09:56:44Z'
$r = ConvertFrom-FullscreenDiagnostics -Line $macLine -Expected (Resolve-ExpectedResolution 'MAC')
Assert-Equal '2560x1664' $r.mode 'mac mode'
Assert-Equal '2560x1664' $r.screen 'mac screen'
Assert-Equal '100' $r.coverageX 'mac coverageX'
Assert-Equal 'PASS' $r.checkCoverage 'mac checkCoverage (system 比は判定に使わない)'
Assert-Equal 'PASS' $r.result 'mac result'
Assert-Equal '74.854' $r.systemCoverageX 'mac systemCoverageX (INFO)'
Assert-Equal '74.82' $r.systemCoverageY 'mac systemCoverageY (INFO)'

Write-Host ('fullscreen-report tests: pass=' + $script:passed + ' fail=' + $script:failed)
if ($script:failed -gt 0) { exit 1 }
exit 0
