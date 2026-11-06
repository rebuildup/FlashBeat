#Requires -Version 5.1
<#
.SYNOPSIS
    FlashBeat 展示運営者向けの単発パッチ適用スクリプト (常駐なし・自動起動なし)。

.DESCRIPTION
    運営者が明示的に実行する 1 回限りの更新ツールです。通常のゲーム起動経路 (FlashBeat.exe を直接実行)、
    ショートカット、ゲーム UI は一切変更しません。Update.cmd / Play.cmd / Rollback.cmd は作りません。

    処理の流れ:
      1. インストール先 (FlashBeat.exe があるフォルダ) の前提を確認
      2. FlashBeat.exe / UnityPlayer が実行中なら中止 (強制終了はしない)
      3. リリース ZIP を取得 (ローカル ZIP / Releases の URL / gh release download)
      4. SHA-256 を計算し期待値と照合 (不一致なら中止)
      5. 別ディレクトリ (作業フォルダ) に完全展開し内容を検査 (FlashBeat.exe / FlashBeat_Data / *.dll)
      6. 検証済みの展開物と既存配置を一括切替 (旧配置は退避 → 切替完了後に破棄)

    通信失敗 / ZIP 破損 / ダイジェスト不一致 / 内容不足のいずれかでは既存配置を一切変更しません。
    ロールバック機能・更新履歴管理・永続的なバージョン倉庫は実装していません。

    運用手順 (最短の手動手順を含む): docs/operations/update-patch.md

.PARAMETER InstallDir
    更新対象のインストールフォルダ (FlashBeat.exe を含む)。既定はカレントディレクトリ。

.PARAMETER ZipPath
    ローカルのリリース ZIP パス。

.PARAMETER ReleaseUrl
    GitHub Releases のアセット URL (直接ダウンロード)。

.PARAMETER Tag
    GitHub Releases のタグ名。gh CLI 経由で取得します。

.PARAMETER AssetName
    -Tag / -ReleaseUrl で取得するアセット名 (ZIP)。-Tag のみ指定時は .zip アセットが 1 個なら自動選択。

.PARAMETER ExpectedSha256
    期待する SHA-256 (64 桁 hex)。未指定の場合は ZIP と同名の .sha256 ファイルを参照します。

.PARAMETER Repo
    GitHub リポジトリ (owner/repo)。未指定時は本スクリプトのリポジトリから自動判定します。

.PARAMETER WorkDir
    ダウンロード・展開に使う作業フォルダ。未指定時はインストール先の親フォルダ配下を生成します。

.PARAMETER LogPath
    運営者向けログの出力先。既定はインストール先の親フォルダにある FlashBeat-update.log。

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\tools\update\Update.ps1 `
        -InstallDir C:\FlashBeat `
        -ZipPath C:\dl\FlashBeat-v0.1.1-win64.zip `
        -ExpectedSha256 1a2b3c...

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\tools\update\Update.ps1 `
        -InstallDir C:\FlashBeat -Tag v0.1.1
#>
[CmdletBinding()]
param(
    [string]$InstallDir = (Get-Location).Path,
    [string]$ZipPath,
    [string]$ReleaseUrl,
    [string]$Tag,
    [string]$AssetName,
    [string]$ExpectedSha256,
    [string]$Repo,
    [string]$WorkDir,
    [string]$LogPath
)

$ErrorActionPreference = 'Stop'

# 終了コード (運営者が結果を見分けるための固定値)
$ExitUsage    = 1   # パラメータ / 前提不備
$ExitDownload = 2   # ネットワーク / ダウンロード失敗
$ExitDigest   = 3   # SHA-256 不一致
$ExitArchive  = 4   # ZIP 破損 / 展開失敗
$ExitContent  = 5   # 展開物の内容検査失敗
$ExitRunning  = 6   # FlashBeat 実行中
$ExitSwap     = 7   # 切替 / ファイル操作の失敗

$script:LogFile = $null
$script:WorkRoot = $null

function Write-OpLog {
    param(
        [Parameter(Mandatory = $true)][ValidateSet('INFO', 'STEP', 'WARN', 'ERROR')][string]$Level,
        [Parameter(Mandatory = $true)][string]$Message
    )
    $line = '{0} [{1}] {2}' -f (Get-Date).ToString('yyyy-MM-ddTHH:mm:ssK'), $Level, $Message
    Write-Host $line
    if ($script:LogFile) {
        try {
            Add-Content -LiteralPath $script:LogFile -Value $line -Encoding UTF8
        } catch {
            Write-Host ('{0} [WARN] ログに追記できません: {1}' -f (Get-Date).ToString('yyyy-MM-ddTHH:mm:ssK'), $_.Exception.Message)
        }
    }
}

function Stop-WithError {
    param(
        [Parameter(Mandatory = $true)][int]$Code,
        [Parameter(Mandatory = $true)][string]$Message,
        [string]$Hint
    )
    Write-OpLog -Level 'ERROR' -Message $Message
    if ($Hint) {
        Write-OpLog -Level 'INFO' -Message $Hint
    }
    if ($script:WorkRoot -and (Test-Path -LiteralPath $script:WorkRoot)) {
        try {
            Remove-Item -LiteralPath $script:WorkRoot -Recurse -Force -ErrorAction Stop
            Write-OpLog -Level 'INFO' -Message ('作業フォルダを削除しました: {0}' -f $script:WorkRoot)
        } catch {
            Write-OpLog -Level 'WARN' -Message ('作業フォルダの削除に失敗しました (手動で削除してください): {0}' -f $script:WorkRoot)
        }
    }
    Write-OpLog -Level 'INFO' -Message ('処理を中止しました (終了コード {0})。' -f $Code)
    exit $Code
}

function ConvertTo-Sha256Hex {
    param([Parameter(Mandatory = $true)][AllowEmptyString()][string]$Text)
    $token = ($Text -split '\s+') | Where-Object { $_ -ne '' } | Select-Object -First 1
    if (-not $token) { return $null }
    $token = $token.Trim().Trim('"')
    if ($token -notmatch '^[0-9a-fA-F]{64}$') { return $null }
    return $token.ToLowerInvariant()
}

function Get-Sha256OfFile {
    param([Parameter(Mandatory = $true)][string]$Path)
    # Get-FileHash は環境によって存在しないため .NET で直接計算する
    $sha = [System.Security.Cryptography.SHA256]::Create()
    $stream = [System.IO.File]::OpenRead($Path)
    try {
        $bytes = $sha.ComputeHash($stream)
    } finally {
        $stream.Dispose()
        $sha.Dispose()
    }
    return ([System.BitConverter]::ToString($bytes) -replace '-', '').ToLowerInvariant()
}

function Get-FlashBeatProcesses {
    $found = @()
    # Unity のクラッシュハンドラーは実行中のファイルを保持しうるため一緒に判定する
    foreach ($name in @('FlashBeat', 'UnityPlayer', 'UnityCrashHandler64')) {
        $procs = @(Get-Process -Name $name -ErrorAction SilentlyContinue)
        foreach ($proc in $procs) {
            $found += $proc
        }
    }
    return $found
}

function Format-ProcessList {
    param([object[]]$Processes)
    $parts = @()
    foreach ($proc in $Processes) {
        $parts += ('{0}(PID={1})' -f $proc.ProcessName, $proc.Id)
    }
    return ($parts -join ', ')
}

function Get-RepositorySlug {
    param([string]$ScriptPath)
    if ($Repo) { return $Repo }
    try {
        $root = Split-Path -Parent (Split-Path -Parent $ScriptPath)
        $remote = (& git -C $root remote get-url origin 2>$null | Select-Object -First 1)
        if ($LASTEXITCODE -eq 0 -and $remote) {
            if ($remote -match 'github\.com[:/](?<slug>[^/]+/[^/\.]+)(\.git)?$') {
                return $Matches['slug']
            }
        }
    } catch {
        # git が使えない場合は既定値に落とす
    }
    return 'rebuildup/FlashBeat'
}

function Invoke-Gh {
    param([string[]]$Arguments)
    $output = & gh @Arguments 2>&1
    return [pscustomobject]@{
        ExitCode = $LASTEXITCODE
        Output   = ($output | Out-String).Trim()
    }
}

function Get-ReleaseAssetName {
    param(
        [Parameter(Mandatory = $true)][string]$Repository,
        [Parameter(Mandatory = $true)][string]$ReleaseTag,
        [string]$Preferred
    )
    $view = Invoke-Gh @('release', 'view', $ReleaseTag, '--repo', $Repository, '--json', 'assets')
    if ($view.ExitCode -ne 0) {
        Stop-WithError $ExitDownload ('gh release view に失敗しました: {0}' -f $view.Output) ('リリース {0} を確認してください。' -f $ReleaseTag)
    }
    $assets = @()
    try {
        $parsed = $view.Output | ConvertFrom-Json
        $assets = @($parsed.assets)
    } catch {
        Stop-WithError $ExitDownload ('リリースのアセット一覧を解釈できません: {0}' -f $view.Output)
    }
    if ($Preferred) {
        $hit = @($assets | Where-Object { $_.name -eq $Preferred })
        if ($hit.Count -eq 0) {
            Stop-WithError $ExitDownload ('リリース {0} にアセット {1} がありません。' -f $ReleaseTag, $Preferred)
        }
        return $Preferred
    }
    $zips = @($assets | Where-Object { $_.name -like '*.zip' })
    if ($zips.Count -eq 0) {
        Stop-WithError $ExitDownload ('リリース {0} に ZIP アセットがありません。' -f $ReleaseTag)
    }
    if ($zips.Count -gt 1) {
        Stop-WithError $ExitUsage ('ZIP アセットが複数あります。-AssetName で指定してください: {0}' -f (($zips | ForEach-Object { $_.name }) -join ', '))
    }
    return $zips[0].name
}

function Invoke-DownloadUrl {
    param(
        [Parameter(Mandatory = $true)][string]$Url,
        [Parameter(Mandatory = $true)][string]$OutFile
    )
    try {
        $ProgressPreference = 'SilentlyContinue'
        Invoke-WebRequest -Uri $Url -OutFile $OutFile -UseBasicParsing -ErrorAction Stop
    } catch {
        Stop-WithError $ExitDownload ('ダウンロードに失敗しました: {0} ({1})' -f $Url, $_.Exception.Message) 'ネットワーク接続と URL を確認してください。既存配置は変更していません。'
    }
}

try {
    $scriptPath = $null
    try { $scriptPath = $MyInvocation.MyCommand.Path } catch { $scriptPath = $null }

    Write-OpLog -Level 'STEP' -Message 'FlashBeat 単発パッチ適用を開始します。'

    # ── 1. インストール先の前提確認 ───────────────────────────────
    try {
        $installFull = (Resolve-Path -LiteralPath $InstallDir -ErrorAction Stop).Path
    } catch {
        Stop-WithError $ExitUsage ('インストール先フォルダが見つかりません: {0}' -f $InstallDir) '-InstallDir に FlashBeat.exe を含むフォルダを指定してください。'
    }
    if (-not (Test-Path -LiteralPath $installFull -PathType Container)) {
        Stop-WithError $ExitUsage ('インストール先フォルダがディレクトリではありません: {0}' -f $installFull)
    }
    if (-not (Test-Path -LiteralPath (Join-Path $installFull 'FlashBeat.exe') -PathType Leaf)) {
        Stop-WithError $ExitUsage ('インストール先に FlashBeat.exe が見つかりません: {0}' -f $installFull) 'このツールは既存インストールへの更新専用です。-InstallDir を FlashBeat.exe のあるフォルダにしてください。'
    }
    if ($scriptPath -and $scriptPath.StartsWith($installFull, [StringComparison]::OrdinalIgnoreCase)) {
        Stop-WithError $ExitUsage ('更新スクリプトがインストール先フォルダ内にあります: {0}' -f $scriptPath) 'スクリプトはインストール先外に置いてください。'
    }

    $parentDir = Split-Path -Parent $installFull
    if (-not $LogPath) {
        $LogPath = Join-Path $parentDir 'FlashBeat-update.log'
    }
    try {
        $script:LogFile = $LogPath
        Write-OpLog -Level 'INFO' -Message ('ログ出力先: {0}' -f $LogPath)
    } catch {
        $script:LogFile = $null
    }

    Write-OpLog -Level 'INFO' -Message ('更新対象: {0}' -f $installFull)

    # ── 2. 実行中チェック (先に落とし、ダウンロードを無駄にしない) ──
    $running = Get-FlashBeatProcesses
    if ($running.Count -gt 0) {
        Stop-WithError $ExitRunning ('FlashBeat が実行中のため更新を中止しました: {0}' -f (Format-ProcessList $running)) 'ゲームを終了してから Update.ps1 を再実行してください。強制終了は行いません。既存配置は変更していません。'
    }

    # ── 3. 取得元の決定 ─────────────────────────────────────────
    $mode = ''
    if ($ZipPath) {
        $mode = 'local'
    } elseif ($ReleaseUrl) {
        $mode = 'url'
    } elseif ($Tag) {
        $mode = 'gh'
    } else {
        Stop-WithError $ExitUsage '更新元の指定がありません。' '-ZipPath / -ReleaseUrl / -Tag のいずれかを指定してください。'
    }

    $stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
    if ($WorkDir) {
        $workRoot = [System.IO.Path]::GetFullPath($WorkDir)
    } else {
        $workRoot = Join-Path $parentDir ('FlashBeat-update-work-' + $stamp)
    }
    if ($workRoot.StartsWith($installFull, [StringComparison]::OrdinalIgnoreCase)) {
        Stop-WithError $ExitUsage ('作業フォルダがインストール先フォルダ内にあります: {0}' -f $workRoot) '-WorkDir にはインストール先外のフォルダを指定してください。'
    }
    $downloadDir = Join-Path $workRoot 'download'
    $stagingDir = Join-Path $workRoot 'staging'
    $backupDir = Join-Path $parentDir ('FlashBeat-backup-' + $stamp)

    try {
        New-Item -ItemType Directory -Path $workRoot -Force | Out-Null
        New-Item -ItemType Directory -Path $downloadDir -Force | Out-Null
        New-Item -ItemType Directory -Path $stagingDir -Force | Out-Null
    } catch {
        Stop-WithError $ExitSwap ('作業フォルダを作成できません: {0} ({1})' -f $workRoot, $_.Exception.Message) 'インストール先の親フォルダに書き込み権限があるか確認してください。既存配置は変更していません。'
    }
    $script:WorkRoot = $workRoot
    Write-OpLog -Level 'INFO' -Message ('作業フォルダ: {0}' -f $workRoot)

    # ── 4. ZIP の取得 ───────────────────────────────────────────
    $zipFile = ''
    $assetFileName = $AssetName
    switch ($mode) {
        'local' {
            try {
                $zipFile = (Resolve-Path -LiteralPath $ZipPath -ErrorAction Stop).Path
            } catch {
                Stop-WithError $ExitUsage ('ZIP が見つかりません: {0}' -f $ZipPath) '既存配置は変更していません。'
            }
            if (-not (Test-Path -LiteralPath $zipFile -PathType Leaf)) {
                Stop-WithError $ExitUsage ('ZIP がファイルではありません: {0}' -f $zipFile)
            }
            if (-not $assetFileName) { $assetFileName = Split-Path -Leaf $zipFile }
            Write-OpLog -Level 'INFO' -Message ('ローカル ZIP を使用します: {0}' -f $zipFile)
        }
        'url' {
            if (-not $assetFileName) {
                $assetFileName = [System.IO.Path]::GetFileName(([Uri]$ReleaseUrl).AbsolutePath)
                if (-not $assetFileName) { $assetFileName = 'FlashBeat-release.zip' }
            }
            $zipFile = Join-Path $downloadDir $assetFileName
            Write-OpLog -Level 'STEP' -Message ('Releases の URL からダウンロードします: {0}' -f $ReleaseUrl)
            Invoke-DownloadUrl -Url $ReleaseUrl -OutFile $zipFile
            Write-OpLog -Level 'INFO' -Message ('ダウンロード完了: {0}' -f $zipFile)
        }
        'gh' {
            if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
                Stop-WithError $ExitDownload 'gh CLI が見つかりません。' 'gh を導入するか、-ReleaseUrl / -ZipPath を利用してください。既存配置は変更していません。'
            }
            $repository = Get-RepositorySlug -ScriptPath $scriptPath
            $assetFileName = Get-ReleaseAssetName -Repository $repository -ReleaseTag $Tag -Preferred $AssetName
            $zipFile = Join-Path $downloadDir $assetFileName
            Write-OpLog -Level 'STEP' -Message ('gh release download を使います: {0} / {1}' -f $repository, $Tag)
            $dl = Invoke-Gh @('release', 'download', $Tag, '--repo', $repository, '--pattern', $assetFileName, '--dir', $downloadDir, '--clobber')
            if ($dl.ExitCode -ne 0) {
                Stop-WithError $ExitDownload ('gh release download に失敗しました: {0}' -f $dl.Output) '既存配置は変更していません。'
            }
            if (-not (Test-Path -LiteralPath $zipFile -PathType Leaf)) {
                Stop-WithError $ExitDownload ('ダウンロードされた ZIP が見つかりません: {0}' -f $zipFile)
            }
            Write-OpLog -Level 'INFO' -Message ('ダウンロード完了: {0}' -f $zipFile)
        }
    }

    # ── 5. 期待する SHA-256 の特定 ──────────────────────────────
    $expected = $null
    if ($ExpectedSha256) {
        $expected = ConvertTo-Sha256Hex -Text $ExpectedSha256
        if (-not $expected) {
            Stop-WithError $ExitUsage ('-ExpectedSha256 の形式が不正です (64 桁の hex を指定してください): {0}' -f $ExpectedSha256)
        }
    } else {
        $sidecar = $null
        if ($mode -eq 'local') {
            $candidate = $zipFile + '.sha256'
            if (Test-Path -LiteralPath $candidate -PathType Leaf) { $sidecar = $candidate }
        } elseif ($mode -eq 'url') {
            $candidate = Join-Path $downloadDir ($assetFileName + '.sha256')
            try {
                $ProgressPreference = 'SilentlyContinue'
                Invoke-WebRequest -Uri ($ReleaseUrl + '.sha256') -OutFile $candidate -UseBasicParsing -ErrorAction Stop
                $sidecar = $candidate
            } catch {
                $sidecar = $null
            }
        } else {
            $candidate = Join-Path $downloadDir ($assetFileName + '.sha256')
            $dl = Invoke-Gh @('release', 'download', $Tag, '--repo', (Get-RepositorySlug -ScriptPath $scriptPath), '--pattern', ($assetFileName + '.sha256'), '--dir', $downloadDir, '--clobber')
            if ($dl.ExitCode -eq 0 -and (Test-Path -LiteralPath $candidate -PathType Leaf)) {
                $sidecar = $candidate
            }
        }
        if ($sidecar) {
            $expected = ConvertTo-Sha256Hex -Text ((Get-Content -LiteralPath $sidecar -Raw -ErrorAction SilentlyContinue))
            if (-not $expected) {
                Stop-WithError $ExitUsage ('SHA-256 ファイルを解釈できません: {0}' -f $sidecar) '既存配置は変更していません。'
            }
            Write-OpLog -Level 'INFO' -Message ('SHA-256 期待値は {0} から取得しました。' -f $sidecar)
        } else {
            Stop-WithError $ExitUsage '期待する SHA-256 が特定できません。' '-ExpectedSha256 を指定するか、ZIP と同名の .sha256 ファイルを Releases に置いてください。既存配置は変更していません。'
        }
    }

    # ── 6. ダイジェスト検証 ─────────────────────────────────────
    $actual = Get-Sha256OfFile -Path $zipFile
    Write-OpLog -Level 'INFO' -Message ('計算した SHA-256: {0}' -f $actual)
    Write-OpLog -Level 'INFO' -Message ('期待する SHA-256: {0}' -f $expected)
    if ($actual -ne $expected) {
        Stop-WithError $ExitDigest 'SHA-256 が一致しません。ZIP が破損、または想定と違うファイルです。' '既存配置は一切変更していません。'
    }
    Write-OpLog -Level 'INFO' -Message 'SHA-256 検証に成功しました。'

    # ── 7. 別ディレクトリへ展開 + 内容検査 ───────────────────────
    try {
        Add-Type -AssemblyName System.IO.Compression.FileSystem -ErrorAction SilentlyContinue
        [System.IO.Compression.ZipFile]::ExtractToDirectory($zipFile, $stagingDir)
    } catch {
        Stop-WithError $ExitArchive ('ZIP の展開に失敗しました: {0} ({1})' -f $zipFile, $_.Exception.Message) '既存配置は一切変更していません。'
    }
    Write-OpLog -Level 'INFO' -Message ('展開先: {0}' -f $stagingDir)

    $payloadRoot = $null
    if (Test-Path -LiteralPath (Join-Path $stagingDir 'FlashBeat.exe') -PathType Leaf) {
        $payloadRoot = $stagingDir
    } else {
        $roots = @(Get-ChildItem -LiteralPath $stagingDir -Directory -ErrorAction SilentlyContinue |
            Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName 'FlashBeat.exe') -PathType Leaf })
        if ($roots.Count -eq 1) {
            $payloadRoot = $roots[0].FullName
        } elseif ($roots.Count -gt 1) {
            Stop-WithError $ExitContent ('展開物に FlashBeat.exe を含むフォルダが複数あります: {0}' -f (($roots | ForEach-Object { $_.Name }) -join ', ')) '既存配置は一切変更していません。'
        }
    }
    if (-not $payloadRoot) {
        Stop-WithError $ExitContent '展開物に FlashBeat.exe が見つかりません。' '既存配置は一切変更していません。'
    }

    $missing = @()
    if (-not (Test-Path -LiteralPath (Join-Path $payloadRoot 'FlashBeat.exe') -PathType Leaf)) { $missing += 'FlashBeat.exe' }
    if (-not (Test-Path -LiteralPath (Join-Path $payloadRoot 'FlashBeat_Data') -PathType Container)) { $missing += 'FlashBeat_Data' }
    $dllFiles = @(Get-ChildItem -LiteralPath $payloadRoot -Filter '*.dll' -File -ErrorAction SilentlyContinue)
    if ($dllFiles.Count -eq 0) { $missing += '*.dll' }
    if ($missing.Count -gt 0) {
        Stop-WithError $ExitContent ('展開物の内容検査に失敗しました (不足: {0})' -f ($missing -join ', ')) '既存配置は一切変更していません。'
    }
    Write-OpLog -Level 'INFO' -Message ('内容検査に合格しました (FlashBeat.exe / FlashBeat_Data / *.dll / 展開物 {0} 個)' -f (Get-ChildItem -LiteralPath $payloadRoot -Recurse -Force | Measure-Object).Count)

    # ── 8. 切替直前の実行中チェック ─────────────────────────────
    $running = Get-FlashBeatProcesses
    if ($running.Count -gt 0) {
        Stop-WithError $ExitRunning ('FlashBeat が実行中のため更新を中止しました: {0}' -f (Format-ProcessList $running)) 'ゲームを終了してから Update.ps1 を再実行してください。強制終了は行いません。既存配置は変更していません。'
    }

    # ── 9. 一括切替 ─────────────────────────────────────────────
    try {
        New-Item -ItemType Directory -Path $backupDir -Force | Out-Null
    } catch {
        Stop-WithError $ExitSwap ('退避フォルダを作成できません: {0} ({1})' -f $backupDir, $_.Exception.Message) 'インストール先の親フォルダの書き込み権限を確認してください。既存配置は変更していません。'
    }

    $oldItems = @(Get-ChildItem -LiteralPath $installFull -Force -ErrorAction Stop)
    Write-OpLog -Level 'STEP' -Message ('既存配置を退避します ({0} 項目): {1}' -f $oldItems.Count, $backupDir)
    try {
        foreach ($item in $oldItems) {
            Move-Item -LiteralPath $item.FullName -Destination $backupDir -Force -ErrorAction Stop
        }
    } catch {
        # 退避途中なら、退避済み、元に戻す
        foreach ($item in @(Get-ChildItem -LiteralPath $backupDir -Force -ErrorAction SilentlyContinue)) {
            try { Move-Item -LiteralPath $item.FullName -Destination $installFull -Force -ErrorAction SilentlyContinue } catch { }
        }
        Stop-WithError $ExitSwap ('既存配置の退避に失敗しました: {0}' -f $_.Exception.Message) ('退避データは {0} に残しています。既存配置は元に戻されています。' -f $backupDir)
    }

    Write-OpLog -Level 'STEP' -Message '検証済みの展開物をインストール先へ配置します。'
    try {
        foreach ($item in @(Get-ChildItem -LiteralPath $payloadRoot -Force -ErrorAction Stop)) {
            Move-Item -LiteralPath $item.FullName -Destination $installFull -Force -ErrorAction Stop
        }
    } catch {
        $failed = $_.Exception.Message
        foreach ($item in @(Get-ChildItem -LiteralPath $installFull -Force -ErrorAction SilentlyContinue)) {
            try { Move-Item -LiteralPath $item.FullName -Destination $stagingDir -Force -ErrorAction SilentlyContinue } catch { }
        }
        foreach ($item in @(Get-ChildItem -LiteralPath $backupDir -Force -ErrorAction SilentlyContinue)) {
            try { Move-Item -LiteralPath $item.FullName -Destination $installFull -Force -ErrorAction SilentlyContinue } catch { }
        }
        Stop-WithError $ExitSwap ('切替に失敗しました: {0}' -f $failed) ('既存配置は退避データから復元しました。退避データは {0} に残しています。' -f $backupDir)
    }

    $postMissing = @()
    if (-not (Test-Path -LiteralPath (Join-Path $installFull 'FlashBeat.exe') -PathType Leaf)) { $postMissing += 'FlashBeat.exe' }
    if (-not (Test-Path -LiteralPath (Join-Path $installFull 'FlashBeat_Data') -PathType Container)) { $postMissing += 'FlashBeat_Data' }
    if ($postMissing.Count -gt 0) {
        foreach ($item in @(Get-ChildItem -LiteralPath $installFull -Force -ErrorAction SilentlyContinue)) {
            try { Move-Item -LiteralPath $item.FullName -Destination $stagingDir -Force -ErrorAction SilentlyContinue } catch { }
        }
        foreach ($item in @(Get-ChildItem -LiteralPath $backupDir -Force -ErrorAction SilentlyContinue)) {
            try { Move-Item -LiteralPath $item.FullName -Destination $installFull -Force -ErrorAction SilentlyContinue } catch { }
        }
        Stop-WithError $ExitSwap ('切替後の検査に失敗しました (不足: {0})' -f ($postMissing -join ', ')) ('既存配置は退避データから復元しました。退避データは {0} に残しています。' -f $backupDir)
    }

    # ── 10. 退避データを破棄 (ロールバック用の永続保存はしない) ──
    try {
        Remove-Item -LiteralPath $backupDir -Recurse -Force -ErrorAction Stop
        Write-OpLog -Level 'INFO' -Message ('旧配置の退避データを破棄しました: {0}' -f $backupDir)
    } catch {
        Write-OpLog -Level 'WARN' -Message ('退避データの削除に失敗しました (手動で削除してください): {0} ({1})' -f $backupDir, $_.Exception.Message)
    }
    try {
        Remove-Item -LiteralPath $workRoot -Recurse -Force -ErrorAction Stop
    } catch {
        Write-OpLog -Level 'WARN' -Message ('作業フォルダの削除に失敗しました (手動で削除してください): {0}' -f $workRoot)
    }

    Write-OpLog -Level 'INFO' -Message ('更新が完了しました: {0}' -f (Join-Path $installFull 'FlashBeat.exe'))
    Write-OpLog -Level 'INFO' -Message 'FlashBeat.exe を直接起動して動作を確認してください (起動確認は運営者が手動で行います)。'
    exit 0
}
catch {
    Write-OpLog -Level 'ERROR' -Message ('想定外のエラーが発生しました: {0}' -f $_.Exception.Message)
    if ($script:WorkRoot -and (Test-Path -LiteralPath $script:WorkRoot)) {
        try {
            Remove-Item -LiteralPath $script:WorkRoot -Recurse -Force -ErrorAction Stop
        } catch {
            Write-OpLog -Level 'WARN' -Message ('作業フォルダの削除に失敗しました (手動で削除してください): {0}' -f $script:WorkRoot)
        }
    }
    Write-OpLog -Level 'INFO' -Message ('処理を中止しました (終了コード {0})。' -f $ExitSwap)
    exit $ExitSwap
}
