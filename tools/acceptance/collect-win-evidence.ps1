#requires -Version 5.1
<#
.SYNOPSIS
    Win64 展示候補の受入証跡を 1 パスで集める (Issue #242 / #233)。
    Windows 実機で **オーナーが実行する** 補助スクリプト。ゲームの操作は行わない。

.DESCRIPTION
    FlashBeat.exe を展開したフォルダで実行すると、次の証跡を 1 つのフォルダにまとめる:

      - machine.txt        OS / GPU / プライマリ画面の解像度・DPI / 実行日時
      - version.json       候補の同梱メタデータ (そのままコピー)
      - exe-version.txt    FlashBeat.exe の FileVersion / ProductVersion
      - Player.log         直近の Player.log (存在すれば)
      - fullscreen.txt     Player.log の [FullscreenDiagnostics] から測った実フルスクリーン判定
                           (被覆率 / fullScreenMode / 期待解像度との一致。判定式は
                            fullscreen-report.ps1、自己テストは fullscreen-report.test.ps1)
      - screenshot-*.png   スクリーンショット (全画面表示の状態で撮る)
      - operation-log.md   操作ログの記入テンプレート (手で埋める)
      - zip-sha256.txt     ZIP の SHA-256 実測 (ZIP パスを渡したときだけ)

    起動・終了・キー入力は行わない (-Launch を明示したときだけ起動する)。

    **入力はこのフォルダと ZIP パスだけ。** 秘密情報は扱わない。

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File collect-win-evidence.ps1 `
        -GameDir "$env:USERPROFILE\Desktop\FlashBeat-Exhibit" `
        -ZipPath "$env:USERPROFILE\Downloads\FlashBeat-0.1.23-win64-b6860a80.zip" `
        -ExpectedSha256 9ee7e4a6ff89c4548e7bf4dc5836a5ab08b57878baea2c148e5a6e6309283d20 `
        -ExpectResolution FHD `
        -OutDir "$env:USERPROFILE\Desktop\acceptance"
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$GameDir,

    [string]$ZipPath,

    # 期待する SHA-256 (小文字/大文字どちらでもよい)。指定すると ZIP と照合して結果を残す。
    [string]$ExpectedSha256,

    # この実行を撮る実ディスプレイの解像度 (FHD / 2K / MAC / 1920x1080 形式)。
    # 指定すると Player.log の実測 screen= と照合し、fullscreen.txt に PASS / FAIL を残す。
    [string]$ExpectResolution,

    [string]$OutDir,

    # 明示したときだけ FlashBeat.exe を起動する (キー入力は送らない)。
    [switch]$Launch,

    # Player.log の場所を明示する。省略時は
    # %USERPROFILE%\AppData\LocalLow\<companyName>\FlashBeat\Player.log を自動探索する
    # (companyName は ProjectSettings。実測 2026-11-11: KimuraYusuke)。
    [string]$PlayerLogPath,

    [int]$WaitSeconds = 25,

    # スクリーンショットを撮らずに証跡だけ集める。
    [switch]$NoScreenshot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# 実フルスクリーンの判定式は純関数に分離してある (テスト: fullscreen-report.test.ps1)。
. (Join-Path $PSScriptRoot 'fullscreen-report.ps1')

$expectedResolution = Resolve-ExpectedResolution $ExpectResolution
if ($ExpectResolution -and -not $expectedResolution) {
    Write-Host ('[collect] 警告: -ExpectResolution "' + $ExpectResolution + '" を解釈できません (FHD / 2K / MAC / WxH)。解像度の照合は行いません')
}

function Write-Step([string]$Message) { Write-Host ("[collect] " + $Message) }

if (-not (Test-Path -LiteralPath $GameDir)) { throw "ゲームフォルダが見つかりません: $GameDir" }
$exe = Join-Path $GameDir 'FlashBeat.exe'
if (-not (Test-Path -LiteralPath $exe)) { throw "FlashBeat.exe が見つかりません: $exe" }

if (-not $OutDir) {
    $stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
    $OutDir = Join-Path $GameDir ("acceptance-" + $stamp)
}
New-Item -ItemType Directory -Path $OutDir -Force | Out-Null
Write-Step ("出力先: " + $OutDir)

# --- 1. 機械の情報 -----------------------------------------------------------
$lines = New-Object System.Collections.Generic.List[string]
$lines.Add('collectedAt=' + (Get-Date).ToString('o'))
$os = $null
try { $os = Get-CimInstance Win32_OperatingSystem -ErrorAction Stop } catch { $os = $null }
if ($os) { $lines.Add('os=' + $os.Caption + ' ' + $os.Version) }
$gpuName = ''
try {
    $gpu = Get-CimInstance Win32_VideoController -ErrorAction Stop | Select-Object -First 1
    if ($gpu) { $gpuName = [string]$gpu.Name; $lines.Add('gpu=' + $gpuName) }
} catch { }

try {
    Add-Type -AssemblyName System.Windows.Forms -ErrorAction Stop
    $screen = [System.Windows.Forms.Screen]::PrimaryScreen
    if ($screen) {
        $b = $screen.Bounds
        $lines.Add('primaryScreen=' + $b.Width + 'x' + $b.Height)
    }
} catch { $screen = $null }
try {
    Add-Type -AssemblyName PresentationFramework -ErrorAction Stop
    $g = [System.Windows.Media.VisualTreeHelper]::GetDpi([System.Windows.Media.Visual]::FromHandle([IntPtr]::Zero))
    $lines.Add('dpi=' + [int]$g.PixelsPerInchX)
} catch { }
$lines.Add('gameDir=' + (Resolve-Path -LiteralPath $GameDir).Path)
Set-Content -LiteralPath (Join-Path $OutDir 'machine.txt') -Value $lines -Encoding UTF8
Write-Step 'machine.txt を書き出しました'

# --- 2. 候補の同一性 ---------------------------------------------------------
$versionJson = Join-Path $GameDir 'version.json'
if (Test-Path -LiteralPath $versionJson) {
    Copy-Item -LiteralPath $versionJson -Destination (Join-Path $OutDir 'version.json') -Force
    Write-Step 'version.json をコピーしました'
} else {
    Write-Step '警告: version.json が見つかりません (古い成果物の可能性)'
}

$exeInfo = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($exe)
@(
    'fileVersion=' + $exeInfo.FileVersion,
    'productVersion=' + $exeInfo.ProductVersion,
    'sizeBytes=' + (Get-Item -LiteralPath $exe).Length
) | Set-Content -LiteralPath (Join-Path $OutDir 'exe-version.txt') -Encoding UTF8
Write-Step 'exe-version.txt を書き出しました'

if ($ZipPath) {
    if (-not (Test-Path -LiteralPath $ZipPath)) { throw "ZIP が見つかりません: $ZipPath" }
    $actual = (Get-FileHash -LiteralPath $ZipPath -Algorithm SHA256).Hash.ToLowerInvariant()
    $expected = if ($ExpectedSha256) { $ExpectedSha256.Trim().ToLowerInvariant() } else { '' }
    $match = if ($expected) { if ($actual -eq $expected) { 'MATCH' } else { 'MISMATCH' } } else { 'NOT-CHECKED' }
    @(
        'zip=' + (Split-Path -Leaf $ZipPath),
        'sha256=' + $actual,
        'expected=' + $expected,
        'result=' + $match
    ) | Set-Content -LiteralPath (Join-Path $OutDir 'zip-sha256.txt') -Encoding UTF8
    Write-Step ('zip-sha256.txt: ' + $match)
}

# --- 3. 起動 (明示したときだけ) ---------------------------------------------
# Player.log の場所は ProjectSettings の companyName で決まる
# (`%USERPROFILE%\AppData\LocalLow\<companyName>\FlashBeat\Player.log`)。
# 実測 2026-11-11: companyName = KimuraYusuke。会社名が変わったビルドでも拾えるよう、
# LocalLow 直下を走査して「FlashBeat\Player.log」がある最新のものを選ぶ。
function Find-PlayerLogPath([string]$UserProfile) {
    $localLow = Join-Path $UserProfile 'AppData\LocalLow'
    if (-not (Test-Path -LiteralPath $localLow)) { return $null }
    $candidates = @()
    foreach ($dir in (Get-ChildItem -LiteralPath $localLow -Directory -ErrorAction SilentlyContinue)) {
        $candidate = Join-Path $dir.FullName 'FlashBeat\Player.log'
        if (Test-Path -LiteralPath $candidate) {
            $candidates += Get-Item -LiteralPath $candidate
        }
    }
    if ($candidates.Count -eq 0) { return $null }
    return ($candidates | Sort-Object LastWriteTime -Descending | Select-Object -First 1).FullName
}

$playerLog = $null
$playerLogBackup = $null
if ($PlayerLogPath) {
    $playerLog = $PlayerLogPath
    $playerLogBackup = $PlayerLogPath + '.prev'
} elseif ($env:USERPROFILE) {
    $playerLog = Find-PlayerLogPath -UserProfile $env:USERPROFILE
    if ($playerLog) {
        $playerLogBackup = Join-Path (Split-Path -Parent $playerLog) 'Player-prev.log'
    }
}
if ($Launch) {
    if ($playerLog -and (Test-Path -LiteralPath $playerLog)) {
        Move-Item -LiteralPath $playerLog -Destination $playerLogBackup -Force
    }
    Write-Step ('FlashBeat.exe を起動します (キー入力は送りません): ' + $exe)
    Start-Process -FilePath $exe -WorkingDirectory $GameDir | Out-Null
    # ファイルが出来ただけでは足りない (起動途中のログを読むと [FullscreenDiagnostics] 行が
    # まだ無く、NO-DATA の証跡になる)。診断行が出るまで待つ。
    $deadline = (Get-Date).AddSeconds([Math]::Max($WaitSeconds, 5))
    while ((Get-Date) -lt $deadline) {
        if (-not $playerLog -and -not $PlayerLogPath -and $env:USERPROFILE) {
            $playerLog = Find-PlayerLogPath -UserProfile $env:USERPROFILE
            if ($playerLog) {
                $playerLogBackup = Join-Path (Split-Path -Parent $playerLog) 'Player-prev.log'
            }
        }
        if ($playerLog -and (Test-Path -LiteralPath $playerLog)) {
            try {
                $probe = Get-Content -LiteralPath $playerLog -TotalCount 2000 -ErrorAction Stop
                if (Find-FullscreenDiagnosticsLine -Lines $probe) { break }
            } catch { }
        }
        Start-Sleep -Milliseconds 500
    }
}
if ($playerLog) { Write-Step ('Player.log: ' + $playerLog) }

# --- 4. スクリーンショット ---------------------------------------------------
if (-not $NoScreenshot) {
    $shotDone = $false
    try {
        Add-Type -AssemblyName System.Drawing -ErrorAction Stop
        Add-Type -AssemblyName System.Windows.Forms -ErrorAction Stop
        $bounds = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
        $bmp = New-Object System.Drawing.Bitmap $bounds.Width, $bounds.Height
        $graphics = [System.Drawing.Graphics]::FromImage($bmp)
        try {
            $graphics.CopyFromScreen($bounds.Location, [System.Drawing.Point]::Empty, $bounds.Size)
            $shot = Join-Path $OutDir ('screenshot-' + (Get-Date -Format 'HHmmss') + '.png')
            $bmp.Save($shot, [System.Drawing.Imaging.ImageFormat]::Png)
            $shotDone = $true
            Write-Step ('スクリーンショット: ' + (Split-Path -Leaf $shot))
        } finally {
            $graphics.Dispose()
            $bmp.Dispose()
        }
    } catch {
        Write-Step ('警告: スクリーンショットを撮れませんでした (' + $_.Exception.Message + ')。PrintScreen で手動撮影してください')
    }
    if (-not $shotDone) { Write-Step 'スクリーンショットは手動で保存してください' }
}

# --- 5. Player.log -----------------------------------------------------------
$logLines = $null
if ($playerLog -and (Test-Path -LiteralPath $playerLog)) {
    Copy-Item -LiteralPath $playerLog -Destination (Join-Path $OutDir 'Player.log') -Force
    Write-Step 'Player.log をコピーしました'
    try {
        # 診断行は起動直後 (最初のシーンロード後) に出る。長いセッションのログでも先頭側で足りる。
        $logLines = Get-Content -LiteralPath $playerLog -TotalCount 2000 -ErrorAction Stop
    } catch {
        Write-Step ('警告: Player.log を読めませんでした (' + $_.Exception.Message + ')')
        $logLines = $null
    }
} else {
    Write-Step '警告: Player.log が見つかりません (ゲームを一度起動してください)'
}

# --- 5b. 実フルスクリーン判定 (実測) -----------------------------------------
$diagnostics = Find-FullscreenDiagnosticsLine -Lines $logLines
if ($ExpectResolution -and -not $diagnostics) {
    Write-Step '警告: [FullscreenDiagnostics] 行がありません (-Launch で起動するか、起動後に再実行してください)'
}
$fullscreenReport = ConvertFrom-FullscreenDiagnostics -Line $diagnostics -Expected $expectedResolution
Format-FullscreenReport -Report $fullscreenReport |
    Set-Content -LiteralPath (Join-Path $OutDir 'fullscreen.txt') -Encoding UTF8
$showScreen = if ($fullscreenReport.screen) { $fullscreenReport.screen } else { '-' }
$showDisplayMode = if ($fullscreenReport.mode) { $fullscreenReport.mode } else { '-' }
$showFullScreenMode = if ($fullscreenReport.fullScreenMode) { $fullscreenReport.fullScreenMode } else { '-' }
$showCoverageX = if ($fullscreenReport.coverageX) { $fullscreenReport.coverageX + '%' } else { '-' }
$showCoverageY = if ($fullscreenReport.coverageY) { $fullscreenReport.coverageY + '%' } else { '-' }
Write-Step ('fullscreen.txt: result=' + $fullscreenReport.result +
    ' screen=' + $showScreen +
    ' displayMode=' + $showDisplayMode +
    ' fullScreenMode=' + $showFullScreenMode +
    ' coverage=' + $showCoverageX + '/' + $showCoverageY)
if ($fullscreenReport.result -eq 'FAIL') {
    Write-Step '注意: 実フルスクリーン条件を満たしていません (fullscreen.txt の check* を確認)'
}
if (-not $expectedResolution) {
    # 解像度未指定だと checkResolution=NOT-CHECKED のまま result=PASS になりうる。
    # 条件 1 / 2 (FHD / 2K) の証跡と誤読されないよう、必ず知らせる。
    Write-Step '注意: -ExpectResolution が未指定のため checkResolution=NOT-CHECKED です。解像度の証拠にはなりません (-ExpectResolution FHD / 2K / MAC を指定してください)'
}

# --- 6. 操作ログのテンプレート ----------------------------------------------
$template = @'
# 受入操作ログ (候補 0.1.23+b6860a80)

記入者: 
日時: 
ZIP: FlashBeat-0.1.23-win64-b6860a80.zip
Sha256: 9ee7e4a6ff89c4548e7bf4dc5836a5ab08b57878baea2c148e5a6e6309283d20
モニタ: (解像度 / 接続方式)

## 1. 全画面

- [ ] 起動直後に黒帯・タスクバー・ウィンドウ枠が見えない
- [ ] `fullscreen.txt` の result= (PASS / FAIL / NO-DATA をそのまま転記)
- [ ] `fullscreen.txt` の checkFullscreen= / checkCoverage= / checkResolution=
- [ ] 期待解像度を指定した場合: `-ExpectResolution` に何を渡したか
- [ ] Player.log の `[FullscreenDiagnostics]` 行 (screen= / fullScreenMode= / dpi=) を貼り付け
      (fullscreen.txt の raw= と同じ行)

## 2. 1 曲完走

- [ ] Title → Select → Ready → Game → Result → Retry が 1 周できる
- [ ] 8 レーン / ノーツ / 判定ラインが見える
- [ ] 映像が出る (出ない場合は Player.log の該当行)
- [ ] 音が鳴る (音が出ない場合は `[MusicManager] PLAYBACK_START` の source=)
- [ ] 準備完了前に Space を押しても開始する (#351)
- [ ] Pause → 再開 / リトライ / 選曲へ が動く
- 使用曲 / スコア / 判定数:

## 3. Option

- [ ] 4 タブ (Play / Display / Sound / Data) が開ける
- [ ] 値を変えて戻る → 再度開くと値が残っている
- [ ] ゲームを再起動しても値が残っている (**保存方向の証拠はこちら**。再表示だけでは
      メモリ上の値でも通るため、PlayerPrefs に書けたことの証拠にならない)
- [ ] Data の Media cache 消去は確認ダイアログが出る (#352)

## 4. 登録 (#134)

- [ ] Result で名前 `ACCEPT` を入れて Register → 順位が出る
- [ ] ランキングに `ACCEPT` が出る
- [ ] 失敗時は `オフライン` / `同じ内容を送信済み` / `HTTP nnn` を区別して記録

## 5. 異常用

- 画面が崩れた / 止まった時刻と操作:
- 保存した Player.log / スクショ:
'@
Set-Content -LiteralPath (Join-Path $OutDir 'operation-log.md') -Value $template -Encoding UTF8
Write-Step 'operation-log.md を書き出しました'

Write-Host ''
Write-Step ('完了: ' + (Resolve-Path -LiteralPath $OutDir).Path)
