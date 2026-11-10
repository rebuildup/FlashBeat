#Requires -Version 5.1
<#
.SYNOPSIS
    release/check/verify-stage.ps1 (#22) のフィクスチャテスト。

.DESCRIPTION
    一時フォルダに「stage アーティファクト (FlashBeat.exe + Data + dll)」「証跡ファイル」
    「docs/evidence/index.md 相当のインデックス」「stage-manifest.json 相当の manifest」を作り、
    実際に verify-stage.ps1 を子 pwsh プロセスで実行して次を確かめる:

      1. 正常な stage は終了コード 0 で通る。
      2. FlashBeat.exe が欠けたら終了コード 1 で失敗する。
      3. フォルダ fingerprint が崩れたら終了コード 1 で失敗する。
      4. 証跡ファイルの SHA-256 が崩れたら終了コード 1 で失敗する。

    注意: verify-stage.ps1 は「証跡ファイルがこの機械に無い」場合は FAIL ではなく SKIP する
    (証跡は Windows 実機に置かれる前提のため)。そのため「証跡欠落 → 失敗」のフィクスチャは
    作れないので、代わりに「証跡は存在するが中身が変わった → 失敗」を assert する
    (assert を弱めたわけではなく、スクリプトの契約に合わせた置き換え)。

    ネットワーク・秘密情報・Windows 実機には依存しない (macOS / Linux の pwsh でも走る)。

.EXAMPLE
    pwsh -NoProfile -File release/check/tests/Run-VerifyStageTests.ps1

    終了コード: 0 = 全テスト成功 / 1 = 失敗あり / 2 = 前提不備
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Continue'

$repoRoot = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $PSCommandPath)))
$verifyScript = Join-Path (Join-Path (Join-Path $repoRoot 'release') 'check') 'verify-stage.ps1'
if (-not (Test-Path -LiteralPath $verifyScript -PathType Leaf)) {
    Write-Host ('[FATAL] verify-stage.ps1 が見つかりません: {0}' -f $verifyScript)
    exit 2
}

# 既存テスト (tools/update/tests) と同じアサーション関数を流用する。
. (Join-Path (Join-Path (Join-Path (Join-Path $repoRoot 'tools') 'update') 'tests') 'TestHelpers.ps1')
# 想定外の PowerShell エラーでテストが黙って通らないようにする (失敗は非 0 終了)。
$ErrorActionPreference = 'Stop'

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

function Get-FolderFingerprint {
    # verify-stage.ps1 の Get-FolderFingerprint と同じ定義:
    # '<fileSha256>  <relative/path>' 行を relative path でソートして LF 連結した UTF-8 バイト列の SHA-256。
    param([Parameter(Mandatory = $true)][string]$Dir)
    $files = Get-ChildItem -LiteralPath $Dir -Recurse -File | Sort-Object FullName
    $lines = foreach ($f in $files) {
        $rel = $f.FullName.Substring($Dir.Length).TrimStart('\', '/') -replace '\\', '/'
        '{0}  {1}' -f (Get-Sha256Hex -Path $f.FullName), $rel
    }
    $bytes = [System.Text.Encoding]::UTF8.GetBytes(($lines -join "`n"))
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        return ([System.BitConverter]::ToString($sha.ComputeHash($bytes)) -replace '-', '').ToLowerInvariant()
    } finally {
        $sha.Dispose()
    }
}

function New-StageFixture {
    # 正常系の stage を一時フォルダに作り、manifest / index / 証跡も揃える。
    param(
        [Parameter(Mandatory = $true)][string]$Root,
        [Parameter(Mandatory = $true)][string]$Marker
    )
    $stageDir = Join-Path $Root 'FlashBeat'
    New-Item -ItemType Directory -Path (Join-Path $stageDir 'FlashBeat_Data\Managed') -Force | Out-Null
    [System.IO.File]::WriteAllText((Join-Path $stageDir 'FlashBeat.exe'), ('fake-exe ' + $Marker))
    [System.IO.File]::WriteAllText((Join-Path $stageDir 'UnityPlayer.dll'), ('fake-dll ' + $Marker))
    [System.IO.File]::WriteAllText((Join-Path $stageDir 'FlashBeat_Data\Managed\Assembly-CSharp.dll'), ('fake-managed ' + $Marker))

    $evidenceFile = Join-Path $Root 'evidence' '01-select.png'
    New-Item -ItemType Directory -Path (Split-Path -Parent $evidenceFile) -Force | Out-Null
    [System.IO.File]::WriteAllText($evidenceFile, ('evidence ' + $Marker))

    $indexPath = Join-Path $Root 'evidence-index.md'

    $fixture = [pscustomobject]@{
        StageDir        = $stageDir
        EvidenceFile    = $evidenceFile
        IndexPath       = $indexPath
        ManifestPath    = (Join-Path $Root 'stage-manifest.json')
        Fingerprint     = Get-FolderFingerprint -Dir $stageDir
        ExeSha256       = (Get-Sha256Hex -Path (Join-Path $stageDir 'FlashBeat.exe'))
        EvidenceSha256  = (Get-Sha256Hex -Path $evidenceFile)
    }

    Write-StageManifest -Fixture $fixture
    # verify-stage.ps1 は「index が全 fingerprint に言及する」ことも検査するので、言及させておく。
    [System.IO.File]::WriteAllText($indexPath, ('fingerprint: ' + $fixture.Fingerprint + "`n"))
    return $fixture
}

function Write-StageManifest {
    param([Parameter(Mandatory = $true)][pscustomobject]$Fixture)
    $manifest = @{
        manifestVersion = 1
        fingerprint     = @{
            algorithm  = 'sha256'
            definition = 'SHA-256 over the UTF-8 bytes of the concatenation of ''<fileSha256>  <relative/path>'' lines, one per file, sorted by relative path (LF separated).'
            command    = 'powershell -NoProfile -ExecutionPolicy Bypass -File release/check/verify-stage.ps1 -Stage S0'
            note       = 'fixture'
        }
        stages          = @(
            @{
                id           = 'S0'
                title        = 'fixture stage'
                status       = 'proven'
                source       = @{ branch = '11'; commit = 'deadbee'; pullRequest = 26 }
                artifact     = @{
                    archivePath       = $Fixture.StageDir
                    worktreeBuildPath = $null
                    exeSha256         = $Fixture.ExeSha256
                    fingerprintSha256 = $Fixture.Fingerprint
                    fileCount         = 3
                }
                playEvidence = @{
                    state         = 'complete'
                    proven        = @('fixture run')
                    unproven      = @()
                    evidenceFiles = @(
                        @{
                            gitRef    = 'origin/11:docs/evidence/fixture/01-select.png'
                            localPath = $Fixture.EvidenceFile
                            sha256    = $Fixture.EvidenceSha256
                        }
                    )
                }
                cfUrl        = $null
                chartVersion = $null
            }
        )
    }
    ($manifest | ConvertTo-Json -Depth 10) | Set-Content -LiteralPath $Fixture.ManifestPath -Encoding UTF8
}

function Invoke-VerifyStage {
    # verify-stage.ps1 を子プロセスで実行して終了コードと出力を返す
    # (スクリプト本体が exit するので、テストプロセスごと終了しないように子に切り出す)。
    param(
        [Parameter(Mandatory = $true)][string]$ManifestPath,
        [Parameter(Mandatory = $true)][string]$IndexPath
    )
    $output = & pwsh -NoProfile -ExecutionPolicy Bypass -File $verifyScript `
        -ManifestPath $ManifestPath -IndexPath $IndexPath 2>&1
    return [pscustomobject]@{
        ExitCode = $LASTEXITCODE
        Output   = ($output | Out-String)
    }
}

Write-Host ('=== verify-stage.ps1 フィクスチャテスト ({0}) ===' -f (Get-Date).ToString('yyyy-MM-dd HH:mm:ss'))
Write-Host ('対象: {0}' -f $verifyScript)
Write-Host ''

$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('flashbeat-verify-stage-test-' + [guid]::NewGuid().ToString('N').Substring(0, 8))
New-Item -ItemType Directory -Path $tempRoot -Force | Out-Null

try {
    # ── 1. 正常な stage は通る ─────────────────────────────────────
    $good = New-StageFixture -Root (Join-Path $tempRoot 'good') -Marker 'good'
    $run1 = Invoke-VerifyStage -ManifestPath $good.ManifestPath -IndexPath $good.IndexPath
    Assert-Equal 'T1 正常な stage は終了コード 0' 0 $run1.ExitCode
    Assert-True 'T2 フォルダ fingerprint の一致を報告する' $run1.Output.Contains('[OK] folder fingerprint matches')
    Assert-True 'T3 証跡の一致を報告する' $run1.Output.Contains('[OK] evidence')

    # ── 2. FlashBeat.exe 欠落は失敗 ────────────────────────────────
    $noExe = New-StageFixture -Root (Join-Path $tempRoot 'no-exe') -Marker 'no-exe'
    Remove-Item -LiteralPath (Join-Path $noExe.StageDir 'FlashBeat.exe') -Force
    $run2 = Invoke-VerifyStage -ManifestPath $noExe.ManifestPath -IndexPath $noExe.IndexPath
    Assert-Equal 'T4 FlashBeat.exe 欠落は終了コード 1' 1 $run2.ExitCode
    Assert-True 'T5 FlashBeat.exe 欠落を報告する' $run2.Output.Contains('FlashBeat.exe is missing')

    # ── 3. fingerprint 不一致は失敗 ────────────────────────────────
    $tampered = New-StageFixture -Root (Join-Path $tempRoot 'tampered') -Marker 'tampered'
    # manifest 作成後にアーティファクトの中身を変える → フォルダ fingerprint が崩れる。
    [System.IO.File]::WriteAllText((Join-Path $tampered.StageDir 'UnityPlayer.dll'), 'tampered-dll after manifest')
    $run3 = Invoke-VerifyStage -ManifestPath $tampered.ManifestPath -IndexPath $tampered.IndexPath
    Assert-Equal 'T6 fingerprint 不一致は終了コード 1' 1 $run3.ExitCode
    Assert-True 'T7 fingerprint 不一致を報告する' $run3.Output.Contains('[FAIL] folder fingerprint mismatch')

    # ── 4. 証跡の SHA-256 不一致は失敗 ─────────────────────────────
    # (verify-stage.ps1 は「証跡がこの機械に無い」場合は SKIP するため、
    #  「存在するが中身が変わった」ケースで失敗を確かめる)
    $badEvidence = New-StageFixture -Root (Join-Path $tempRoot 'bad-evidence') -Marker 'bad-evidence'
    [System.IO.File]::WriteAllText($badEvidence.EvidenceFile, 'evidence tampered after manifest')
    $run4 = Invoke-VerifyStage -ManifestPath $badEvidence.ManifestPath -IndexPath $badEvidence.IndexPath
    Assert-Equal 'T8 証跡の SHA-256 不一致は終了コード 1' 1 $run4.ExitCode
    Assert-True 'T9 証跡の不一致を報告する' $run4.Output.Contains('[FAIL] evidence hash mismatch')
}
finally {
    Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host ''
Write-TestSummary -Title 'verify-stage.ps1 フィクスチャテスト'
