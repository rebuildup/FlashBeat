#requires -Version 5.1
<#
.SYNOPSIS
    Player.log の `[FullscreenDiagnostics]` 行から「実フルスクリーンのピクセル被覆」を判定する純関数。
    ゲーム本体 (Assets/Game/Scripts/UI/V5/FullscreenDiagnostics.cs) が起動時に 1 行だけ出す
    実測ログを読み取るだけで、ゲームや OS の状態は変更しない。

.DESCRIPTION
    `FullscreenDiagnostics.LogAtStartup` は Release ビルドでも動き、起動直後に次の形の行を残す:

        [FullscreenDiagnostics] event=startup, screen=1920x1080, currentResolution=1920x1080@60Hz,
        fullScreen=True, fullScreenMode=FullScreenWindow, dpi=96, safeArea=0,0,1920x1080,
        displays=1, display0=system:1920x1080,rendering:1920x1080,active:True, quality=1, time=...

    この行から
      - fullScreen / fullScreenMode  (本当にフルスクリーンか)
      - screen と currentResolution  (描画領域が表示モード全体を覆っているか = 被覆率)
      - screen と期待解像度          (FHD / 2K の実ディスプレイで実行したか)
    を判定し、証跡として残す。ウィンドウをリサイズした擬似全画面は被覆率で FAIL になる
    (docs/operations/three-resolution-fullscreen-verification.md)。

    display0.system は OS 側のシステム面で、macOS では描画面と一致しない
    (実測 2026-11-11: screen=2560x1664 / system=3420x2224)。そのため system との比は
    INFO (`systemCoverageX/Y`) として残すだけで、判定には使わない。

    Issue #242 / #88 の「実フルスクリーン」条件を、画像の目視ではなく実測で裏付けるための補助。
    判定は Windows に依存しないので、テスト (fullscreen-report.test.ps1) は macOS でも走る。
#>

function Find-FullscreenDiagnosticsLine {
    <#
    .SYNOPSIS
        ログ行の配列から最後の `[FullscreenDiagnostics] ... event=` 行を返す (無ければ $null)。
        再起動を繰り返した Player.log では最後の起動が現在の実行を表す。
    #>
    param([string[]]$Lines)

    if (-not $Lines) { return $null }

    $found = $null
    foreach ($line in $Lines) {
        if (-not $line) { continue }
        if ($line.Contains('[FullscreenDiagnostics]') -and $line.Contains('event=')) {
            $found = $line
        }
    }
    return $found
}

function Resolve-ExpectedResolution {
    <#
    .SYNOPSIS
        期待解像度の表記を `WxH` へ正規化する。既知の別名 FHD / 2K / MAC も受ける。
        解釈できないときは空文字を返す (呼び出し側が警告する)。
    #>
    param([string]$Expected)

    if (-not $Expected) { return '' }

    $value = $Expected.Trim().Replace([string][char]0x00D7, 'x').ToUpperInvariant()
    switch ($value) {
        'FHD' { return '1920x1080' }
        '2K' { return '2560x1440' }
        'MAC' { return '2560x1664' }
    }
    if ($value -match '^\d+X\d+$') { return $value.ToLowerInvariant() }
    return ''
}

function ConvertFrom-FullscreenDiagnostics {
    <#
    .SYNOPSIS
        診断行 1 本を判定結果 (ordered hashtable) へ変換する。
        `Line` が空なら全項目 NO-DATA (ゲームが起動していない)。
        `Expected` は Resolve-ExpectedResolution で正規化済みの `WxH` (空なら解像度判定は NOT-CHECKED)。
    #>
    param([string]$Line, [string]$Expected)

    $report = [ordered]@{
        raw             = ''
        screen          = ''
        screenW         = 0
        screenH         = 0
        mode            = ''
        modeW           = 0
        modeH           = 0
        fullScreen      = ''
        fullScreenMode  = ''
        dpi             = ''
        displays        = ''
        systemW         = 0
        systemH         = 0
        renderingW      = 0
        renderingH      = 0
        coverageX       = ''
        coverageY       = ''
        systemCoverageX = ''
        systemCoverageY = ''
        expected        = $Expected
        checkFullscreen = 'NO-DATA'
        checkCoverage   = 'NO-DATA'
        checkResolution = 'NO-DATA'
        result          = 'NO-DATA'
    }

    if (-not $Line) { return $report }

    $report.raw = $Line

    $m = [regex]::Match($Line, 'screen=(\d+)x(\d+)')
    if ($m.Success) {
        $report.screenW = [int]$m.Groups[1].Value
        $report.screenH = [int]$m.Groups[2].Value
        $report.screen = $report.screenW.ToString() + 'x' + $report.screenH.ToString()
    }

    # currentResolution = ディスプレイが使っている表示モード。被覆率の基準はこちら
    # (display0.system は OS 側のシステム面で、macOS では描画面と一致しない。下の INFO を参照)。
    $m = [regex]::Match($Line, 'currentResolution=(\d+)x(\d+)')
    if ($m.Success) {
        $report.modeW = [int]$m.Groups[1].Value
        $report.modeH = [int]$m.Groups[2].Value
        $report.mode = $report.modeW.ToString() + 'x' + $report.modeH.ToString()
    }

    $m = [regex]::Match($Line, 'fullScreen=([A-Za-z]+)')
    if ($m.Success) { $report.fullScreen = $m.Groups[1].Value }

    $m = [regex]::Match($Line, 'fullScreenMode=([A-Za-z]+)')
    if ($m.Success) { $report.fullScreenMode = $m.Groups[1].Value }

    $m = [regex]::Match($Line, 'dpi=([\d.]+)')
    if ($m.Success) { $report.dpi = $m.Groups[1].Value }

    $m = [regex]::Match($Line, 'displays=(\d+)')
    if ($m.Success) { $report.displays = $m.Groups[1].Value }

    $m = [regex]::Match($Line, 'display0=system:(\d+)x(\d+),rendering:(\d+)x(\d+)')
    if ($m.Success) {
        $report.systemW = [int]$m.Groups[1].Value
        $report.systemH = [int]$m.Groups[2].Value
        $report.renderingW = [int]$m.Groups[3].Value
        $report.renderingH = [int]$m.Groups[4].Value
    }

    # --- 判定 ---------------------------------------------------------------

    if ($report.fullScreen) {
        $modeOk = ($report.fullScreenMode -eq 'FullScreenWindow') -or ($report.fullScreenMode -eq 'ExclusiveFullScreen')
        if (($report.fullScreen -eq 'True') -and $modeOk) {
            $report.checkFullscreen = 'PASS'
        } else {
            $report.checkFullscreen = 'FAIL'
        }
    }

    # 被覆率は「描画領域 (Screen) / 表示モード (currentResolution)」で測る。
    # ウィンドウ表示なら Screen がモードより小さくなるので FAIL になる。
    if ($report.modeW -gt 0 -and $report.modeH -gt 0 -and $report.screenW -gt 0 -and $report.screenH -gt 0) {
        $coverageX = [math]::Round((100.0 * $report.screenW / $report.modeW), 3)
        $coverageY = [math]::Round((100.0 * $report.screenH / $report.modeH), 3)
        $report.coverageX = $coverageX.ToString([System.Globalization.CultureInfo]::InvariantCulture)
        $report.coverageY = $coverageY.ToString([System.Globalization.CultureInfo]::InvariantCulture)
        if ($coverageX -ge 99.0 -and $coverageY -ge 99.0) {
            $report.checkCoverage = 'PASS'
        } else {
            $report.checkCoverage = 'FAIL'
        }
    }

    # INFO: display0.system は OS 側のシステム面。macOS では描画面と一致しない
    # (実測 2026-11-11 r28: screen=2560x1664, system=3420x2224)。判定には使わない。
    if ($report.systemW -gt 0 -and $report.systemH -gt 0 -and $report.screenW -gt 0 -and $report.screenH -gt 0) {
        $report.systemCoverageX = ([math]::Round((100.0 * $report.screenW / $report.systemW), 3)).ToString([System.Globalization.CultureInfo]::InvariantCulture)
        $report.systemCoverageY = ([math]::Round((100.0 * $report.screenH / $report.systemH), 3)).ToString([System.Globalization.CultureInfo]::InvariantCulture)
    }

    if ($Expected) {
        if ($report.screen) {
            if ($report.screen -eq $Expected) { $report.checkResolution = 'PASS' } else { $report.checkResolution = 'FAIL' }
        }
    } else {
        $report.checkResolution = 'NOT-CHECKED'
    }

    $checks = @($report.checkFullscreen, $report.checkCoverage, $report.checkResolution)
    if ($checks -contains 'FAIL') {
        $report.result = 'FAIL'
    } elseif ($checks -contains 'PASS') {
        $report.result = 'PASS'
    } else {
        $report.result = 'NO-DATA'
    }

    return $report
}

function Format-FullscreenReport {
    <#
    .SYNOPSIS
        判定結果を証跡ファイル (fullscreen.txt) の行配列へ整形する。
        先頭に判定の意味を短く書き、機械可読な key=value を並べる。
    #>
    param($Report)

    $lines = New-Object System.Collections.Generic.List[string]
    $lines.Add('# Fullscreen evidence (parsed from Player.log [FullscreenDiagnostics])')
    $lines.Add('# coverageX/Y = Screen.width/height が currentResolution を覆う割合 (100 で全面)')
    $lines.Add('# systemCoverageX/Y = display0.system との比 (INFO。macOS では描画面と一致しないため判定に使わない)')
    $lines.Add('# result=PASS は「実フルスクリーン + 被覆 99% 以上 + 期待解像度と一致」の全部が成立したときだけ')
    $lines.Add('# 判定に使う行が Player.log に無い場合は result=NO-DATA (ゲームを一度起動してから再実行する)')
    foreach ($key in $Report.Keys) {
        $lines.Add($key.ToString() + '=' + [string]$Report[$key])
    }
    return $lines
}
