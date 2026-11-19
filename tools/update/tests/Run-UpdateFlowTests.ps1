#Requires -Version 5.1
<#
.SYNOPSIS
    Update.ps1 の更新フロー (Issue #185) の結合テスト。

.DESCRIPTION
    一時フォルダに「正規インストール (FlashBeat)」と「副次インストール (FlashBeat-0.1.14)」を
    同じ親フォルダに置き、実際に Update.ps1 を実行して次を確かめる:

      1. 更新が成功する (終了コード 0・新しい中身が配置される)。
      2. 副次インストール側の退避 (復旧データ) が消えない。
      3. 同じインストールについては「旧版 1 世代だけ残す」契約が守られる
         (2 回続けて更新すると自分の旧世代は消え、1 世代だけ残る)。
      4. 副次インストールの中身 (配置) は更新されない。

    Windows 実機には依存しない (macOS / Linux の pwsh でも走る)。
    展示機での実地検証は Supervisor が別途行う。

.EXAMPLE
    pwsh -NoProfile -File tools/update/tests/Run-UpdateFlowTests.ps1

    終了コード: 0 = 全テスト成功 / 1 = 失敗あり / 2 = 前提不備
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Continue'

$repoRoot = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $PSCommandPath)))
$updateScript = Join-Path (Join-Path (Join-Path $repoRoot 'tools') 'update') 'Update.ps1'
if (-not (Test-Path -LiteralPath $updateScript -PathType Leaf)) {
    Write-Host ('[FATAL] Update.ps1 が見つかりません: {0}' -f $updateScript)
    exit 2
}

# Update.ps1 の純粋関数 (identity / 所有 manifest) を取り込む。
. $updateScript
. (Join-Path (Join-Path (Join-Path $repoRoot 'tools') 'update') 'tests/TestHelpers.ps1')
# 想定外の PowerShell エラーでテストが黙って通らないようにする (失敗は非 0 終了)。
$ErrorActionPreference = 'Stop'

function New-FakeInstall {
    # テスト用の擬似インストールを作る (exe / Data / dll だけあれば Update.ps1 は進む)。
    param(
        [Parameter(Mandatory = $true)][string]$Dir,
        [Parameter(Mandatory = $true)][string]$Marker
    )
    New-Item -ItemType Directory -Path (Join-Path $Dir 'FlashBeat_Data\Managed') -Force | Out-Null
    [System.IO.File]::WriteAllText((Join-Path $Dir 'FlashBeat.exe'), ('fake-exe ' + $Marker))
    [System.IO.File]::WriteAllText((Join-Path $Dir 'UnityPlayer.dll'), ('fake-dll ' + $Marker))
    [System.IO.File]::WriteAllText((Join-Path $Dir 'FlashBeat_Data\Managed\Assembly-CSharp.dll'), ('fake-managed ' + $Marker))
    [System.IO.File]::WriteAllText((Join-Path $Dir 'FlashBeat_Data\marker.txt'), $Marker)
}

function New-FakeReleaseZip {
    # テスト用の配布 ZIP (FlashBeat.exe / FlashBeat_Data / *.dll / version.json) を作る。
    param(
        [Parameter(Mandatory = $true)][string]$Dir,
        [Parameter(Mandatory = $true)][string]$ZipPath,
        [Parameter(Mandatory = $true)][string]$Marker
    )
    $staging = Join-Path $Dir ('staging-' + $Marker)
    New-Item -ItemType Directory -Path (Join-Path $staging 'FlashBeat_Data\Managed') -Force | Out-Null
    [System.IO.File]::WriteAllText((Join-Path $staging 'FlashBeat.exe'), ('fake-exe ' + $Marker))
    [System.IO.File]::WriteAllText((Join-Path $staging 'UnityPlayer.dll'), ('fake-dll ' + $Marker))
    [System.IO.File]::WriteAllText((Join-Path $staging 'FlashBeat_Data\Managed\Assembly-CSharp.dll'), ('fake-managed ' + $Marker))
    [System.IO.File]::WriteAllText((Join-Path $staging 'FlashBeat_Data\marker.txt'), $Marker)
    [System.IO.File]::WriteAllText((Join-Path $staging 'version.json'), ('{"version":"0.1.99","tag":"v0.1.99-win64-test"}'))
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [System.IO.Compression.ZipFile]::CreateFromDirectory($staging, $ZipPath)
}

function Get-UpdateAreaDir {
    # issue #670: 更新領域は <install>\.flashbeat-update\ にインストール先内へ閉じ込められる。
    param(
        [Parameter(Mandatory = $true)][string]$InstallDir,
        [Parameter(Mandatory = $true)][string]$Name
    )
    return (Join-Path (Join-Path $InstallDir '.flashbeat-update') $Name)
}

function Get-ScopedBackups {
    # 特定のインストール (名前空間) に属する退避フォルダだけを列挙する。
    param(
        [Parameter(Mandatory = $true)][string]$InstallDir,
        [Parameter(Mandatory = $true)][string]$InstallKey
    )
    $backupRoot = Get-UpdateAreaDir -InstallDir $InstallDir -Name 'backup'
    return @(Get-ChildItem -LiteralPath $backupRoot -Directory -ErrorAction SilentlyContinue |
        Where-Object { Test-FlashBeatScopedName -Name $_.Name -InstallKey $InstallKey })
}

function Get-Sha256Hex {
    param([Parameter(Mandatory = $true)][string]$Path)
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        $stream = [System.IO.File]::OpenRead($Path)
        try {
            $bytes = $sha.ComputeHash($stream)
        } finally {
            $stream.Dispose()
        }
    } finally {
        $sha.Dispose()
    }
    return ([System.BitConverter]::ToString($bytes) -replace '-', '').ToLowerInvariant()
}

function Invoke-Update {
    # Update.ps1 の本体を子プロセスで実行して終了コードを返す。
    # 「実行中 FlashBeat プロセスがあれば中止」の判定だけはテスト用にスタブ化する
    # (同じマシンで別の展示ビルドが動いていると exit code 6 になるため)。
    param(
        [Parameter(Mandatory = $true)][string]$InstallDir,
        [Parameter(Mandatory = $true)][string]$ZipPath,
        [Parameter(Mandatory = $true)][string]$Sha256,
        [Parameter(Mandatory = $true)][string]$LogPath,
        [string]$WorkDir = ''
    )
    $wrapper = Join-Path (Join-Path (Join-Path $repoRoot 'tools') 'update') 'tests/Invoke-UpdateForTest.ps1'
    $wrapperArgs = @{
        UpdateScript       = $updateScript
        TargetInstallDir   = $InstallDir
        TargetZipPath      = $ZipPath
        TargetSha256       = $Sha256
        TargetLogPath      = $LogPath
    }
    if ($WorkDir) { $wrapperArgs['TargetWorkDir'] = $WorkDir }
    $output = & pwsh -NoProfile -ExecutionPolicy Bypass -File $wrapper @wrapperArgs 2>&1
    return [pscustomobject]@{
        ExitCode = $LASTEXITCODE
        Output   = ($output | Out-String)
    }
}

Write-Host ('=== Update.ps1 更新フロー結合テスト ({0}) ===' -f (Get-Date).ToString('yyyy-MM-dd HH:mm:ss'))
Write-Host ('対象: {0}' -f $updateScript)
Write-Host ''

$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('flashbeat-flow-test-' + [guid]::NewGuid().ToString('N').Substring(0, 8))
New-Item -ItemType Directory -Path $tempRoot -Force | Out-Null

try {
    $installA = Join-Path $tempRoot 'FlashBeat'
    $installB = Join-Path $tempRoot 'FlashBeat-0.1.14'
    New-FakeInstall -Dir $installA -Marker 'old-A'
    New-FakeInstall -Dir $installB -Marker 'B-untouched'
    $identityA = Get-FlashBeatInstallIdentity -InstallDir $installA

    # 境界外 (インストール先の親フォルダ) に残る旧レイアウトのファイルを置いておく。
    # issue #670: これらは移動も削除もされず、そのまま残る (データ保全)。
    $identityB = Get-FlashBeatInstallIdentity -InstallDir $installB
    $backupBName = New-FlashBeatBackupName -InstallKey $identityB.InstallKey -RunId '20261108-000000-9999-bbbbbb'
    $backupB = Join-Path $tempRoot $backupBName
    New-Item -ItemType Directory -Path $backupB -Force | Out-Null
    [System.IO.File]::WriteAllText((Join-Path $backupB 'old-payload.txt'), 'recovery-data-of-B')
    Write-FlashBeatOwnerManifest -BackupDir $backupB -Identity $identityB -RunId '20261108-000000-9999-bbbbbb' -Version '0.1.14' | Out-Null

    # 旧形式 (所有不明) の退避と旧 .flashbeat-update 領域も親フォルダに置いておく。
    $legacyBackup = Join-Path $tempRoot 'FlashBeat-backup-20261107-013942'
    New-Item -ItemType Directory -Path $legacyBackup -Force | Out-Null
    [System.IO.File]::WriteAllText((Join-Path $legacyBackup 'old-payload.txt'), 'unknown-owner-data')
    $legacyUpdateRoot = Join-Path $tempRoot '.flashbeat-update'
    New-Item -ItemType Directory -Path $legacyUpdateRoot -Force | Out-Null
    [System.IO.File]::WriteAllText((Join-Path $legacyUpdateRoot 'stale.bin'), 'x')

    $zipPath = Join-Path $tempRoot 'FlashBeat-0.1.99-win64-test.zip'
    New-FakeReleaseZip -Dir $tempRoot -ZipPath $zipPath -Marker 'new-A'
    $sha = Get-Sha256Hex -Path $zipPath

    # ログはインストール先内の .flashbeat-update\logs に置く (境界内でなければ拒否される)。
    $logDirA = Get-UpdateAreaDir -InstallDir $installA -Name 'logs'
    New-Item -ItemType Directory -Path $logDirA -Force | Out-Null
    $logPath = Join-Path $logDirA 'update.log'
    # last-result.json は既定値 <install>\.flashbeat-update\results を使う (明示しない)。
    $resultDirA = Get-UpdateAreaDir -InstallDir $installA -Name 'results'

    # ── 1 回目の更新 ───────────────────────────────────────────────
    $run1 = Invoke-Update -InstallDir $installA -ZipPath $zipPath -Sha256 $sha -LogPath $logPath
    Assert-Equal 'T1 1 回目の更新は終了コード 0' 0 $run1.ExitCode
    Assert-True 'T2 更新後の配置が新しい内容に置き換わる' ((Get-Content -LiteralPath (Join-Path (Join-Path $installA 'FlashBeat_Data') 'marker.txt') -Raw).Trim() -eq 'new-A')
    Assert-True 'T3 境界外の旧退避 (復旧データ) はそのまま残る (移動も削除もしない)' (Test-Path -LiteralPath (Join-Path $backupB 'old-payload.txt'))
    Assert-True 'T4 旧形式 (所有不明) の退避が消えない' (Test-Path -LiteralPath (Join-Path $legacyBackup 'old-payload.txt'))
    Assert-True 'T4b 境界外の旧 .flashbeat-update 領域が消えない' (Test-Path -LiteralPath (Join-Path $legacyUpdateRoot 'stale.bin'))
    Assert-True 'T5 副次インストールの中身は更新されない' ((Get-Content -LiteralPath (Join-Path (Join-Path $installB 'FlashBeat_Data') 'marker.txt') -Raw).Trim() -eq 'B-untouched')
    Assert-True 'T5b last-result.json が <install>\.flashbeat-update\results に書かれる' (Test-Path -LiteralPath (Join-Path $resultDirA 'last-result.json'))
    $resultJson = Get-Content -LiteralPath (Join-Path $resultDirA 'last-result.json') -Raw | ConvertFrom-Json
    Assert-Equal 'T5c last-result.json の result は success' 'success' $resultJson.result

    # issue #670: 更新領域はインストール先の中。親フォルダには何も作らない。
    Assert-True 'T5d <install>\.flashbeat-update 領域が作られる' (Test-Path -LiteralPath (Join-Path $installA '.flashbeat-update'))
    Assert-True 'T5e 作業フォルダは実行後に残らない' (@(Get-ChildItem -LiteralPath (Get-UpdateAreaDir -InstallDir $installA -Name 'work') -ErrorAction SilentlyContinue).Count -eq 0)
    # 親フォルダの旧 .flashbeat-update には更新処理が新規作成した work/backup/results/logs が無い
    # (検出のみで、中身には触れない)。
    $legacyChildren = @(Get-ChildItem -LiteralPath $legacyUpdateRoot -Force -ErrorAction SilentlyContinue | ForEach-Object { $_.Name })
    Assert-Equal 'T5f 親フォルダの旧領域に新規ディレクトリは作られない' 'stale.bin' ($legacyChildren -join ',')
    Assert-True 'T5g 親フォルダに FlashBeat-update-work-* が残らない' (@(Get-ChildItem -LiteralPath $tempRoot -Directory -Filter 'FlashBeat-update-work-*' -ErrorAction SilentlyContinue).Count -eq 0)
    Assert-Equal 'T5h 親フォルダの FlashBeat-backup-* は fixture の 2 件だけ (新規退避は作られない)' 2 ((Get-ChildItem -LiteralPath $tempRoot -Directory -Filter 'FlashBeat-backup-*' -ErrorAction SilentlyContinue | Measure-Object).Count)
    Assert-True 'T5i インストール先内の .flashbeat-update 以外に更新ファイルは無い' (-not (Test-Path -LiteralPath (Join-Path $tempRoot '.flashbeat-update\work')))

    $backupsA = @(Get-ScopedBackups -InstallDir $installA -InstallKey $identityA.InstallKey)
    Assert-Equal 'T6 自インストールの退避が 1 世代できる' 1 $backupsA.Count
    Assert-True 'T7 退避は <install>\.flashbeat-update\backup の内側' ($backupsA[0].FullName.StartsWith((Get-UpdateAreaDir -InstallDir $installA -Name 'backup'), [StringComparison]::OrdinalIgnoreCase))
    Assert-True 'T8 退避に所有 manifest がある' (Test-Path -LiteralPath (Join-Path $backupsA[0].FullName 'flashbeat-backup-owner.json'))
    Assert-True 'T9 退避に旧版の中身が退避されている' (Test-Path -LiteralPath (Join-Path (Join-Path $backupsA[0].FullName 'FlashBeat_Data') 'marker.txt'))
    Assert-True 'T9b 切替で .flashbeat-update 自身は退避対象外 (backup の中に無い)' (-not (Test-Path -LiteralPath (Join-Path $backupsA[0].FullName '.flashbeat-update')))
    Assert-True 'T9c .flashbeat-update は切替後もインストール先に残る' (Test-Path -LiteralPath (Join-Path $installA '.flashbeat-update'))

    # ── 2 回目の更新 (自インストールの旧世代は消える / 他は残る) ──
    $run2 = Invoke-Update -InstallDir $installA -ZipPath $zipPath -Sha256 $sha -LogPath $logPath
    Assert-Equal 'T10 2 回目の更新は終了コード 0' 0 $run2.ExitCode

    $backupsA2 = @(Get-ScopedBackups -InstallDir $installA -InstallKey $identityA.InstallKey)
    Assert-Equal 'T11 自インストールの退避は 1 世代だけ残る' 1 $backupsA2.Count
    Assert-True 'T12 2 回目の退避は前回と違う' ($backupsA2[0].FullName -ne $backupsA[0].FullName)
    Assert-True 'T13 境界外の旧退避は 2 回目の更新後も残る' (Test-Path -LiteralPath (Join-Path $backupB 'old-payload.txt'))
    Assert-True 'T14 旧形式の退避は 2 回目の更新後も残る' (Test-Path -LiteralPath (Join-Path $legacyBackup 'old-payload.txt'))

    # ── 失敗時は黙って成功にしない ───────────────────────────────
    $badSha = ('0' * 64)
    $run3 = Invoke-Update -InstallDir $installA -ZipPath $zipPath -Sha256 $badSha -LogPath $logPath
    Assert-Equal 'T15 ダイジェスト不一致は終了コード 3' 3 $run3.ExitCode
    Assert-True 'T16 ダイジェスト不一致でも配置は変わらない' ((Get-Content -LiteralPath (Join-Path (Join-Path $installA 'FlashBeat_Data') 'marker.txt') -Raw).Trim() -eq 'new-A')
    Assert-Equal 'T17 ダイジェスト不一致でも退避は増えない' 1 @(Get-ScopedBackups -InstallDir $installA -InstallKey $identityA.InstallKey).Count
    $resultJson3 = Get-Content -LiteralPath (Join-Path $resultDirA 'last-result.json') -Raw | ConvertFrom-Json
    Assert-Equal 'T18 ダイジェスト不一致の結果が failed で記録される' 'failed' $resultJson3.result
    Assert-Equal 'T19 ダイジェスト不一致の終了コードが記録される' 3 $resultJson3.exitCode

    # ── 境界外を指す明示指定は拒否される (issue #670) ──────────────
    # -WorkDir にインストール先の外を指定 → 終了コード 1 (前提不備)。
    $outsideWork = Join-Path $tempRoot 'outside-work'
    $run4 = Invoke-Update -InstallDir $installA -ZipPath $zipPath -Sha256 $sha -LogPath $logPath -WorkDir $outsideWork
    Assert-Equal 'T20 境界外の -WorkDir は拒否される (exit 1)' 1 $run4.ExitCode
    Assert-True 'T21 境界外の作業フォルダは作られない' (-not (Test-Path -LiteralPath $outsideWork))

    # ── 悪意あるアーカイブエントリ (パストラバーサル) は拒否される ──
    $evilZip = Join-Path $tempRoot 'evil.zip'
    $evilStaging = Join-Path $tempRoot 'evil-staging'
    New-Item -ItemType Directory -Path (Join-Path $evilStaging 'FlashBeat_Data') -Force | Out-Null
    [System.IO.File]::WriteAllText((Join-Path $evilStaging 'FlashBeat.exe'), 'fake')
    [System.IO.File]::WriteAllText((Join-Path $evilStaging 'UnityPlayer.dll'), 'fake')
    [System.IO.File]::WriteAllText((Join-Path $evilStaging 'FlashBeat_Data\x.dll'), 'fake')
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [System.IO.Compression.ZipFile]::Open($evilZip, 'Create')
    try {
        $e = $zip.CreateEntry('../escape.txt')
        $w = New-Object System.IO.StreamWriter($e.Open())
        $w.Write('x'); $w.Dispose()
    } finally { $zip.Dispose() }
    $evilSha = Get-Sha256Hex -Path $evilZip
    $evilLog = Join-Path $logDirA 'evil.log'
    $run5 = Invoke-Update -InstallDir $installA -ZipPath $evilZip -Sha256 $evilSha -LogPath $evilLog
    Assert-Equal 'T22 パストラバーサルを含む ZIP は拒否される (exit 4)' 4 $run5.ExitCode
    Assert-True 'T23 パストラバーサル先にファイルが作られない' (-not (Test-Path -LiteralPath (Join-Path $tempRoot 'escape.txt')))
}
finally {
    Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host ''
Write-TestSummary -Title 'Update.ps1 更新フロー結合テスト'
