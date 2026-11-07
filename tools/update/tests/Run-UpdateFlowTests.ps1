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

function Get-ScopedBackups {
    # 特定のインストール (名前空間) に属する退避フォルダだけを列挙する。
    param(
        [Parameter(Mandatory = $true)][string]$ParentDir,
        [Parameter(Mandatory = $true)][string]$InstallKey
    )
    return @(Get-ChildItem -LiteralPath $ParentDir -Directory -ErrorAction SilentlyContinue |
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
        [Parameter(Mandatory = $true)][string]$LogPath
    )
    $wrapper = Join-Path (Join-Path (Join-Path $repoRoot 'tools') 'update') 'tests/Invoke-UpdateForTest.ps1'
    $output = & pwsh -NoProfile -ExecutionPolicy Bypass -File $wrapper `
        -UpdateScript $updateScript `
        -TargetInstallDir $InstallDir -TargetZipPath $ZipPath `
        -TargetSha256 $Sha256 -TargetLogPath $LogPath 2>&1
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

    # 副次インストールが更新で作った退避 (復旧データ) を置いておく。
    $identityB = Get-FlashBeatInstallIdentity -InstallDir $installB
    $backupB = Join-Path $tempRoot (New-FlashBeatBackupName -InstallKey $identityB.InstallKey -RunId '20261108-000000-9999-bbbbbb')
    New-Item -ItemType Directory -Path $backupB -Force | Out-Null
    [System.IO.File]::WriteAllText((Join-Path $backupB 'old-payload.txt'), 'recovery-data-of-B')
    Write-FlashBeatOwnerManifest -BackupDir $backupB -Identity $identityB -RunId '20261108-000000-9999-bbbbbb' -Version '0.1.14' | Out-Null

    # 旧形式 (所有不明) の退避も置いておく (旧スクリプトで作られたもの)。
    $legacyBackup = Join-Path $tempRoot 'FlashBeat-backup-20261107-013942'
    New-Item -ItemType Directory -Path $legacyBackup -Force | Out-Null
    [System.IO.File]::WriteAllText((Join-Path $legacyBackup 'old-payload.txt'), 'unknown-owner-data')

    $zipPath = Join-Path $tempRoot 'FlashBeat-0.1.99-win64-test.zip'
    New-FakeReleaseZip -Dir $tempRoot -ZipPath $zipPath -Marker 'new-A'
    $sha = Get-Sha256Hex -Path $zipPath

    # ── 1 回目の更新 ───────────────────────────────────────────────
    $logPath = Join-Path $tempRoot 'update.log'
    $run1 = Invoke-Update -InstallDir $installA -ZipPath $zipPath -Sha256 $sha -LogPath $logPath
    Assert-Equal 'T1 1 回目の更新は終了コード 0' 0 $run1.ExitCode
    Assert-True 'T2 更新後の配置が新しい内容に置き換わる' ((Get-Content -LiteralPath (Join-Path (Join-Path $installA 'FlashBeat_Data') 'marker.txt') -Raw).Trim() -eq 'new-A')
    Assert-True 'T3 副次インストールの退避 (復旧データ) が消えない' (Test-Path -LiteralPath (Join-Path $backupB 'old-payload.txt'))
    Assert-True 'T4 旧形式 (所有不明) の退避が消えない' (Test-Path -LiteralPath (Join-Path $legacyBackup 'old-payload.txt'))
    Assert-True 'T5 副次インストールの中身は更新されない' ((Get-Content -LiteralPath (Join-Path (Join-Path $installB 'FlashBeat_Data') 'marker.txt') -Raw).Trim() -eq 'B-untouched')

    $backupsA = @(Get-ScopedBackups -ParentDir $tempRoot -InstallKey $identityA.InstallKey)
    Assert-Equal 'T6 自インストールの退避が 1 世代できる' 1 $backupsA.Count
    Assert-True 'T7 退避に所有 manifest がある' (Test-Path -LiteralPath (Join-Path $backupsA[0].FullName 'flashbeat-backup-owner.json'))
    Assert-True 'T8 退避に旧版の中身が退避されている' (Test-Path -LiteralPath (Join-Path (Join-Path $backupsA[0].FullName 'FlashBeat_Data') 'marker.txt'))

    # ── 2 回目の更新 (自インストールの旧世代は消える / 他は残る) ──
    $run2 = Invoke-Update -InstallDir $installA -ZipPath $zipPath -Sha256 $sha -LogPath $logPath
    Assert-Equal 'T9 2 回目の更新は終了コード 0' 0 $run2.ExitCode

    $backupsA2 = @(Get-ScopedBackups -ParentDir $tempRoot -InstallKey $identityA.InstallKey)
    Assert-Equal 'T10 自インストールの退避は 1 世代だけ残る' 1 $backupsA2.Count
    Assert-True 'T11 2 回目の退避は前回と違う' ($backupsA2[0].FullName -ne $backupsA[0].FullName)
    Assert-True 'T12 副次インストールの退避は 2 回目の更新後も残る' (Test-Path -LiteralPath (Join-Path $backupB 'old-payload.txt'))
    Assert-True 'T13 旧形式の退避は 2 回目の更新後も残る' (Test-Path -LiteralPath (Join-Path $legacyBackup 'old-payload.txt'))

    # ── 失敗時は黙って成功にしない ───────────────────────────────
    $badSha = ('0' * 64)
    $run3 = Invoke-Update -InstallDir $installA -ZipPath $zipPath -Sha256 $badSha -LogPath $logPath
    Assert-Equal 'T14 ダイジェスト不一致は終了コード 3' 3 $run3.ExitCode
    Assert-True 'T15 ダイジェスト不一致でも配置は変わらない' ((Get-Content -LiteralPath (Join-Path (Join-Path $installA 'FlashBeat_Data') 'marker.txt') -Raw).Trim() -eq 'new-A')
    Assert-Equal 'T16 ダイジェスト不一致でも退避は増えない' 1 @(Get-ScopedBackups -ParentDir $tempRoot -InstallKey $identityA.InstallKey).Count
}
finally {
    Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host ''
Write-TestSummary -Title 'Update.ps1 更新フロー結合テスト'
