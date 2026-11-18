#Requires -Version 5.1
<#
.SYNOPSIS
    Update.ps1 の「インストール単位のスコープ」(Issue #185) の検証スクリプト。

.DESCRIPTION
    Pester が無い環境でも動く、依存ゼロのアサーション形式のテスト。
    Update.ps1 を dot-source して純粋関数を取り込み、次を検証する:

      1. 正規インストール先 (フォルダ名 FlashBeat) と副次インストール
         (例: FlashBeat-0.1.14) の identity が区別できること。
      2. 退避フォルダ名がインストール単位の名前空間を持つこと
         (旧形式 'FlashBeat-backup-<stamp>' は所有不明 = 対象外)。
      3. 所有 manifest による所有判定 (キー一致 / パス一致 / 不一致)。
      4. 同じ親フォルダに 2 つのインストールを置いたとき、互いの退避 (復旧データ) が
         残ること (issue #185 の回帰テスト)。
      5. 同じインストールでは「旧版 1 世代だけ残す」契約が守られること。

.EXAMPLE
    pwsh -NoProfile -File tools/update/tests/Run-UpdateScopeTests.ps1

    # Windows PowerShell 5.1 でも同じテストが動きます:
    powershell -NoProfile -ExecutionPolicy Bypass -File tools\update\tests\Run-UpdateScopeTests.ps1

    終了コード: 0 = 全テスト成功 / 1 = 失敗あり / 2 = 前提不備 (Update.ps1 が見つからない)
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Continue'

# ── Update.ps1 の関数を取り込む (dot-source 時は本体は走らない) ──────────
$repoRoot = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $PSCommandPath)))
$updateScript = Join-Path (Join-Path (Join-Path $repoRoot 'tools') 'update') 'Update.ps1'
if (-not (Test-Path -LiteralPath $updateScript -PathType Leaf)) {
    Write-Host ('[FATAL] Update.ps1 が見つかりません: {0}' -f $updateScript)
    exit 2
}
. $updateScript
# 共通のアサーション関数 (Update.ps1 側の $ErrorActionPreference = 'Stop' を上書きする)。
. (Join-Path (Join-Path (Join-Path $repoRoot 'tools') 'update') 'tests/TestHelpers.ps1')
# 想定外の PowerShell エラーでテストが黙って通らないようにする (失敗は非 0 終了)。
$ErrorActionPreference = 'Stop'

Write-Host ('=== Update.ps1 インストール単位スコープの検証 ({0}) ===' -f (Get-Date).ToString('yyyy-MM-dd HH:mm:ss'))
Write-Host ('対象: {0}' -f $updateScript)
Write-Host ''

# ── T1-T5: identity の判定 ───────────────────────────────────────────
$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('flashbeat-scope-test-' + [guid]::NewGuid().ToString('N').Substring(0, 8))
New-Item -ItemType Directory -Path $tempRoot -Force | Out-Null

try {
    $installA = Join-Path $tempRoot 'FlashBeat'
    $installB = Join-Path $tempRoot 'FlashBeat-0.1.14'
    New-Item -ItemType Directory -Path $installA -Force | Out-Null
    New-Item -ItemType Directory -Path $installB -Force | Out-Null

    $identityA = Get-FlashBeatInstallIdentity -InstallDir $installA
    $identityB = Get-FlashBeatInstallIdentity -InstallDir $installB

    Assert-True 'T1 正規インストール先 (FlashBeat) は IsCanonical=true' ($identityA.IsCanonical -eq $true)
    Assert-Equal 'T2 正規インストール先の安定ID' 'flashbeat' $identityA.InstallKey
    Assert-True 'T3 副次インストール (FlashBeat-0.1.14) は IsCanonical=false' ($identityB.IsCanonical -eq $false)
    Assert-Equal 'T4 副次インストールの安定ID' 'flashbeat-0.1.14' $identityB.InstallKey
    Assert-True 'T5 2 つのインストールの安定IDが異なる' ($identityA.InstallKey -ne $identityB.InstallKey)
    Assert-Equal 'T6 安定IDは英小文字と区切り記号のみ' 'flashbeat-exhibit' (ConvertTo-FlashBeatInstallKey -Name 'FlashBeat Exhibit!')

    # ── T7-T9: 退避フォルダ名 / 作業フォルダ名 ───────────────────────
    $runId = New-FlashBeatRunId
    $runId2 = New-FlashBeatRunId
    Assert-True 'T7 実行IDは実行ごとに異なる (同時刻更新でも共有しない)' ($runId -ne $runId2)
    Assert-True 'T8 実行IDに PID を含む' ($runId -match '^\d{8}-\d{6}-\d+-[0-9a-f]{4,8}$')

    $backupName = New-FlashBeatBackupName -InstallKey $identityA.InstallKey -RunId $runId
    $workName = New-FlashBeatWorkDirName -InstallKey $identityA.InstallKey -RunId $runId
    Assert-True 'T9 退避フォルダ名にインストール単位の名前空間がある' ($backupName -like 'FlashBeat-backup-flashbeat-*')
    Assert-True 'T10 作業フォルダ名にインストール単位の名前空間がある' ($workName -like 'FlashBeat-update-work-flashbeat-*')

    Assert-True 'T11 自分の退避名は自分の名前空間内' (Test-FlashBeatScopedName -Name $backupName -InstallKey 'flashbeat')
    Assert-True 'T12 隣のインストールの退避名は自分の名前空間外' (-not (Test-FlashBeatScopedName -Name (New-FlashBeatBackupName -InstallKey $identityB.InstallKey -RunId $runId) -InstallKey 'flashbeat'))
    Assert-True 'T13 旧形式の退避名 (所有不明) は自分の名前空間外' (-not (Test-FlashBeatScopedName -Name 'FlashBeat-backup-20261107-013942' -InstallKey 'flashbeat'))

    # ── T14-T16: 所有 manifest の判定 ────────────────────────────────
    $manifestA = [pscustomobject]@{ installKey = 'flashbeat'; installPath = $identityA.InstallPath }
    $ownerOk = Test-FlashBeatOwnerManifest -Manifest $manifestA -Identity $identityA
    Assert-True 'T14 同じインストールの manifest は所有と判定' ($ownerOk.Owned -eq $true)

    $ownerOther = Test-FlashBeatOwnerManifest -Manifest ([pscustomobject]@{ installKey = 'flashbeat-0.1.14'; installPath = $identityB.InstallPath }) -Identity $identityA
    Assert-True 'T15 別インストールの manifest は所有ではない' ($ownerOther.Owned -eq $false)

    $ownerPathMismatch = Test-FlashBeatOwnerManifest -Manifest ([pscustomobject]@{ installKey = 'flashbeat'; installPath = $identityB.InstallPath }) -Identity $identityA
    Assert-True 'T16 パスが違う manifest は所有ではない' ($ownerPathMismatch.Owned -eq $false)

    $ownerNone = Test-FlashBeatOwnerManifest -Manifest $null -Identity $identityA
    Assert-True 'T17 manifest が無い退避は所有ではない' ($ownerNone.Owned -eq $false)

    # ── T18-T25: 同じ親フォルダに 2 つのインストール (回帰テスト) ──────
    # issue #574: 退避の置き場所は <parent>\.flashbeat-update\backup\ に集約された。
    $backupRoot = Join-Path $tempRoot '.flashbeat-update\backup'
    New-Item -ItemType Directory -Path $backupRoot -Force | Out-Null
    $runA1 = '20261108-010101-1111-aaaaaa'
    $runA2 = '20261108-020202-2222-bbbbbb'
    $runB1 = '20261108-030303-3333-cccccc'
    $backupA1 = Join-Path $backupRoot (New-FlashBeatBackupName -InstallKey $identityA.InstallKey -RunId $runA1)
    $backupA2 = Join-Path $backupRoot (New-FlashBeatBackupName -InstallKey $identityA.InstallKey -RunId $runA2)
    $backupB1 = Join-Path $backupRoot (New-FlashBeatBackupName -InstallKey $identityB.InstallKey -RunId $runB1)
    $legacyBackup = Join-Path $backupRoot 'FlashBeat-backup-20261107-013942'
    foreach ($dir in @($backupA1, $backupA2, $backupB1, $legacyBackup)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
    }
    Write-FlashBeatOwnerManifest -BackupDir $backupA1 -Identity $identityA -RunId $runA1 -Version '0.1.13' | Out-Null
    Write-FlashBeatOwnerManifest -BackupDir $backupA2 -Identity $identityA -RunId $runA2 -Version '0.1.14' | Out-Null
    Write-FlashBeatOwnerManifest -BackupDir $backupB1 -Identity $identityB -RunId $runB1 -Version '0.1.14' | Out-Null

    $candidates = Get-FlashBeatBackupCandidates -ParentDir $backupRoot
    Assert-Equal 'T18 退避候補を backup 領域から集める' 4 @($candidates).Count

    # A の更新 (今回の退避は A2) としてクリーンアップ対象を決める。
    $selectionA = Select-FlashBeatStaleBackups -Candidates $candidates -Identity $identityA -CurrentBackupPath $backupA2
    $deletableA = @($selectionA.Deletable | ForEach-Object { $_.Path })
    Assert-True 'T19 削除対象は自インストールの旧世代だけ' (($deletableA.Count -eq 1) -and ($deletableA[0] -eq $backupA1))
    Assert-True 'T20 隣のインストールの退避は削除対象にならない' (-not ($deletableA -contains $backupB1))
    Assert-True 'T21 旧形式 (所有不明) の退避は削除対象にならない' (-not ($deletableA -contains $legacyBackup))
    Assert-True 'T22 今回の退避は残す' (@($selectionA.Kept | Where-Object { $_.Path -eq $backupA2 }).Count -eq 1)

    foreach ($stale in $selectionA.Deletable) {
        Remove-Item -LiteralPath $stale.Path -Recurse -Force
    }
    Assert-True 'T23 自インストールの旧世代は削除される (1 世代契約)' (-not (Test-Path -LiteralPath $backupA1))
    Assert-True 'T24 隣のインストールの退避 (復旧データ) は残る' (Test-Path -LiteralPath $backupB1)
    Assert-True 'T25 旧形式の退避は残る' (Test-Path -LiteralPath $legacyBackup)

    # B の更新 (今回の退避は B1) では、A の退避にも触れない。
    $selectionB = Select-FlashBeatStaleBackups -Candidates (Get-FlashBeatBackupCandidates -ParentDir $backupRoot) -Identity $identityB -CurrentBackupPath $backupB1
    Assert-True 'T26 副次インストールのクリーンアップは何も削除しない' (@($selectionB.Deletable).Count -eq 0)
    Assert-True 'T27 副次インストールの退避は残る' (Test-Path -LiteralPath $backupB1)
    Assert-True 'T28 副次インストールの退避は「他名前空間」として保持される' (@($selectionB.Kept | Where-Object { $_.Path -eq $backupA2 }).Count -eq 1)

    # ── T29-T36: パス境界 (issue #670) ─────────────────────────────
    # すべての派生パスがインストール先フォルダ内に収まること、境界外指定が
    # 拒否されること、reparse point 経由の脱出が検出されることを検証する。
    $installRoot = Join-Path $tempRoot 'FlashBeat'
    $insidePath = Join-Path $installRoot '.flashbeat-update\backup\x'
    $outsidePath = Join-Path $tempRoot 'outside\x'
    $siblingPath = ($installRoot + '-evil') + '\x'

    Assert-True 'T29 インストール先内のパスは内部と判定' (Test-PathInside -Path $insidePath -Root $installRoot)
    Assert-True 'T30 インストール先と一致するパスは内部と判定' (Test-PathInside -Path $installRoot -Root $installRoot)
    Assert-True 'T31 インストール先外のパスは内部ではない' (-not (Test-PathInside -Path $outsidePath -Root $installRoot))
    Assert-True 'T32 兄弟フォルダ (名前が前方一致するだけ) は内部ではない' (-not (Test-PathInside -Path $siblingPath -Root $installRoot))
    Assert-True 'T33 Test-FlashBeatInsideInstallRoot: 内部パスは true' (Test-FlashBeatInsideInstallRoot -Path $insidePath -InstallRoot $installRoot)
    Assert-True 'T34 Test-FlashBeatInsideInstallRoot: 外部パスは false' (-not (Test-FlashBeatInsideInstallRoot -Path $outsidePath -InstallRoot $installRoot))
    Assert-Equal 'T35 更新領域ルートは <install>\.flashbeat-update' (Join-Path $installRoot '.flashbeat-update') (Get-FlashBeatUpdateRootPath -InstallDir $installRoot)

    # reparse point (symlink) 経由の脱出検出。symlink を作れない環境ではスキップ。
    $linkParent = Join-Path $tempRoot 'linktest'
    New-Item -ItemType Directory -Path $linkParent -Force | Out-Null
    $linkTarget = Join-Path $tempRoot 'real-target'
    New-Item -ItemType Directory -Path $linkTarget -Force | Out-Null
    $linkPath = Join-Path $linkParent 'escape'
    $linkMade = $false
    try {
        New-Item -ItemType SymbolicLink -Path $linkPath -Target $linkTarget -ErrorAction Stop | Out-Null
        $linkMade = $true
    } catch {
        try {
            New-Item -ItemType Junction -Path $linkPath -Target $linkTarget -ErrorAction Stop | Out-Null
            $linkMade = $true
        } catch { }
    }
    if ($linkMade) {
        Assert-True 'T36 reparse point 配下は境界外と判定' (Test-FlashBeatPathHasReparsePoint -Path (Join-Path $linkPath 'sub') -Root $linkParent)
        Assert-True 'T37 reparse point 経由のパスは InsideInstallRoot=false' (-not (Test-FlashBeatInsideInstallRoot -Path (Join-Path $linkPath 'sub') -InstallRoot $linkParent))
        Assert-True 'T38 通常フォルダは reparse point ではない' (-not (Test-FlashBeatPathHasReparsePoint -Path (Join-Path $linkTarget 'sub') -Root $linkTarget))
    } else {
        Write-Host '[SKIP] T36-T38 symlink/junction を作成できない環境のため reparse point 検査をスキップ'
    }

    # ── T39-T47: 旧レイアウト残骸は「検出して警告のみ」(issue #670) ──
    # 境界外のファイルを移動・削除しないことを実ファイルシステムで検証する。
    $legacyParent = Join-Path $tempRoot 'legacy-parent'
    New-Item -ItemType Directory -Path $legacyParent -Force | Out-Null
    $legacyUpdateRoot = Join-Path $legacyParent '.flashbeat-update'
    $legacyBackupRoot = Join-Path $legacyUpdateRoot 'backup'

    [System.IO.File]::WriteAllText((Join-Path $legacyParent 'FlashBeat-update.log'), 'old log')
    [System.IO.File]::WriteAllText((Join-Path $legacyParent 'readme.txt'), 'unrelated')
    New-Item -ItemType Directory -Path $legacyUpdateRoot -Force | Out-Null
    $legacyWorkDir = Join-Path $legacyParent 'FlashBeat-update-work-flashbeat-20260101-000000-1234-aaaaaa'
    $legacyBackupOwned = Join-Path $legacyParent (New-FlashBeatBackupName -InstallKey 'flashbeat' -RunId '20260101-000000-1234-aaaaaa')
    New-Item -ItemType Directory -Path $legacyWorkDir -Force | Out-Null
    New-Item -ItemType Directory -Path $legacyBackupOwned -Force | Out-Null
    [System.IO.File]::WriteAllText((Join-Path $legacyBackupOwned 'old-payload.txt'), 'recovery')
    Write-FlashBeatOwnerManifest -BackupDir $legacyBackupOwned -Identity $identityA -RunId '20260101-000000-1234-aaaaaa' -Version '0.1.13' | Out-Null

    # 検出のみ: すべての旧ファイルがそのまま残ること (移動・削除されない)。
    Write-FlashBeatLegacyArtifactNotice -ParentDir $legacyParent

    Assert-True 'T39 旧 .flashbeat-update 領域は残る (移動も削除もしない)' (Test-Path -LiteralPath $legacyUpdateRoot)
    Assert-True 'T40 旧ログは親フォルダに残る' (Test-Path -LiteralPath (Join-Path $legacyParent 'FlashBeat-update.log'))
    Assert-True 'T41 旧作業フォルダは残る' (Test-Path -LiteralPath $legacyWorkDir)
    Assert-True 'T42 manifest 付き旧退避は親フォルダに残る (backup 領域へ移動しない)' (Test-Path -LiteralPath $legacyBackupOwned)
    Assert-True 'T43 旧退避の中身もそのまま' (Test-Path -LiteralPath (Join-Path $legacyBackupOwned 'old-payload.txt'))
    Assert-True 'T44 無関係ファイルは触られない' (Test-Path -LiteralPath (Join-Path $legacyParent 'readme.txt'))
    Assert-True 'T45 親フォルダに新しい backup 領域は作られない' (-not (Test-Path -LiteralPath $legacyBackupRoot))

    # ── T46: ログ世代管理 ────────────────────────────────────────────
    $logDir2 = Join-Path $tempRoot 'loggen'
    New-Item -ItemType Directory -Path $logDir2 -Force | Out-Null
    foreach ($i in 1..25) {
        [System.IO.File]::WriteAllText((Join-Path $logDir2 ('FlashBeat-update-20260101-0000{0:d2}-1-aaaaaa.log' -f $i)), 'x')
    }
    Limit-FlashBeatUpdateLogs -LogDir $logDir2 -Keep 20
    $remaining = @(Get-ChildItem -LiteralPath $logDir2 -File -Filter 'FlashBeat-update-*.log').Count
    Assert-Equal 'T46 ログは最新 20 世代だけ残る' 20 $remaining
}
finally {
    Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host ''
Write-TestSummary -Title 'Update.ps1 インストール単位スコープ'
