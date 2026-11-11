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
        -OutDir "$env:USERPROFILE\Desktop\acceptance"
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$GameDir,

    [string]$ZipPath,

    # 期待する SHA-256 (小文字/大文字どちらでもよい)。指定すると ZIP と照合して結果を残す。
    [string]$ExpectedSha256,

    [string]$OutDir,

    # 明示したときだけ FlashBeat.exe を起動する (キー入力は送らない)。
    [switch]$Launch,

    [int]$WaitSeconds = 25,

    # スクリーンショットを撮らずに証跡だけ集める。
    [switch]$NoScreenshot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

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
$playerLog = $null
$playerLogBackup = $null
if ($env:USERPROFILE) {
    $playerLog = Join-Path $env:USERPROFILE 'AppData\LocalLow\rebuildup\FlashBeat\Player.log'
    $playerLogBackup = Join-Path $env:USERPROFILE 'AppData\LocalLow\rebuildup\FlashBeat\Player-prev.log'
}
if ($Launch) {
    if ($playerLog -and (Test-Path -LiteralPath $playerLog)) {
        Move-Item -LiteralPath $playerLog -Destination $playerLogBackup -Force
    }
    Write-Step ('FlashBeat.exe を起動します (キー入力は送りません): ' + $exe)
    Start-Process -FilePath $exe -WorkingDirectory $GameDir | Out-Null
    $deadline = (Get-Date).AddSeconds([Math]::Max($WaitSeconds, 5))
    while ($playerLog -and (Get-Date) -lt $deadline -and -not (Test-Path -LiteralPath $playerLog)) {
        Start-Sleep -Milliseconds 500
    }
}

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
if ($playerLog -and (Test-Path -LiteralPath $playerLog)) {
    Copy-Item -LiteralPath $playerLog -Destination (Join-Path $OutDir 'Player.log') -Force
    Write-Step 'Player.log をコピーしました'
} else {
    Write-Step '警告: Player.log が見つかりません (ゲームを一度起動してください)'
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
- [ ] Player.log の `[FullscreenDiagnostics]` 行: screen= / fullScreenMode= / dpi=
      (貼り付け)

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
- [ ] 値を変えて戻る → 再度開くと値が残っている (保存方向)
- [ ] ゲームを再起動しても値が残っている (復元方向)
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
