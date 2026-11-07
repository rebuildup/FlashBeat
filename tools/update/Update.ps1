#Requires -Version 5.1
<#
.SYNOPSIS
    FlashBeat 展示運営者向けのパッチ適用スクリプト (常駐なし・独自サーバーなし)。

.DESCRIPTION
    GitHub Releases の配布 ZIP を取得して適用する 1 回限りの更新ツールです。
    2 つの使い方があります:

    (1) 運営者が明示的に実行 (従来どおり): タグ / ZIP / URL を指定して適用。
        適用後の起動確認は運営者が FlashBeat.exe を直接実行します。

    (2) アプリからの自動適用 (-Apply): アプリが起動時に Releases latest を確認し、
        新版があれば %LOCALAPPDATA%\FlashBeat\update\staging に ZIP + .sha256 を取得済み。
        アプリ終了時に本スクリプトが -Apply -WaitPid <PID> -Relaunch で分離プロセス起動され、
        「アプリ終了待ち → 別ディレクトリ展開 → 検証 → 一括切替 → 新 exe 再起動」を行います。
        運営者は exe を直接起動するだけで、再起動後には新版になっています。

    共通の保証:
      - 通信失敗 / ZIP 破損 / ダイジェスト不一致 / 内容不足 / 二重起動のいずれかでは
        既存配置を一切変更せず現行版を残します (ゲームは止めません)。
      - すべての終了経路で %LOCALAPPDATA%\FlashBeat\update\last-result.json に結果を書きます
        (result: success / failed / aborted + 終了コード + 日本語の理由。アプリが次回起動時に
        読んで Title 画面で利用者へ通知します。issue #125)。
      - 適用成功後、旧版は 1 世代だけ残し (FlashBeat-backup-*)、それより古い世代は削除します。
    ロールバック UI・常設ランチャー・独自更新サーバーは実装していません。

    運用手順: docs/operations/update-patch.md

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

.PARAMETER Apply
    アプリからの自動適用モード。スクリプトが配布物内 (インストール先 tools\update) から
    起動されることを許可します。

.PARAMETER WaitPid
    終了を待つ FlashBeat のプロセス ID。指定すると、そのプロセスの終了を待ってから更新します
    (最大 300 秒)。待機後に他の FlashBeat が実行中 (二重起動) なら終了コード 6 で中止します。

.PARAMETER Relaunch
    更新成功後に FlashBeat.exe を再起動します (-Apply と併用)。

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\tools\update\Update.ps1 `
        -InstallDir C:\FlashBeat `
        -ZipPath C:\dl\FlashBeat-v0.1.1-win64.zip `
        -ExpectedSha256 1a2b3c...

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\tools\update\Update.ps1 `
        -InstallDir C:\FlashBeat -Tag v0.1.1

.EXAMPLE
    # アプリからの自動適用 (UpdateChecker が終了時に起動する形)
    powershell -NoProfile -ExecutionPolicy Bypass -File "C:\FlashBeat\tools\update\Update.ps1" `
        -InstallDir "C:\FlashBeat" -Apply -Relaunch -WaitPid 12345 `
        -ZipPath "$env:LOCALAPPDATA\FlashBeat\update\staging\FlashBeat-0.1.1-win64-abc1234.zip"
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
    [string]$LogPath,
    # アプリ起動フロー (UpdateChecker) から使う自動適用モード:
    #   -WaitPid  : 指定 PID の FlashBeat プロセス終了を待ってから更新する
    #   -Apply    : スクリプトが配布物内 (インストール先) から起動されることを許可する
    #   -Relaunch : 更新成功後に FlashBeat.exe を再起動する
    [switch]$Apply,
    [int]$WaitPid = 0,
    [switch]$Relaunch
)

$ErrorActionPreference = 'Stop'

# このスクリプト自身のパス (本体を関数に閉じ込めた後も参照できるようスクリプトスコープに保持する)。
$script:UpdateScriptPath = $null
try { $script:UpdateScriptPath = $MyInvocation.MyCommand.Path } catch { $script:UpdateScriptPath = $null }

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
# 結果ファイル (last-result.json) 用の記録 (issue #125)。把握できた時点で埋める。
$script:ResultFromVersion = ''
$script:ResultToVersion = ''
$script:ResultTag = ''

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

function Get-UpdateVersionFromText {
    # 'v0.1.4-win64-4d6b407' / 'FlashBeat-0.1.4-win64-4d6b407.zip' から '0.1.4' を取り出す (無ければ $null)。
    param([string]$Text)
    if (-not $Text) { return $null }
    $m = [regex]::Match($Text, '(?<!\d)(\d+\.\d+\.\d+)(?!\d)')
    if (-not $m.Success) { return $null }
    return $m.Groups[1].Value
}

function Write-UpdateResult {
    # アプリが次回起動時に読む結果ファイル (issue #125):
    # %LOCALAPPDATA%\FlashBeat\update\last-result.json
    # すべての終了経路から一度だけ呼ぶ。書けない場合も更新処理自体は失敗させない (best-effort)。
    param(
        [Parameter(Mandatory = $true)][ValidateSet('success', 'failed', 'aborted')][string]$Result,
        [Parameter(Mandatory = $true)][int]$Code,
        [string]$Message
    )
    try {
        $base = $env:LOCALAPPDATA
        if ([string]::IsNullOrWhiteSpace($base)) { return }
        $dir = Join-Path (Join-Path $base 'FlashBeat') 'update'
        $path = Join-Path $dir 'last-result.json'
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
        $payload = [ordered]@{
            result      = $Result
            exitCode    = $Code
            fromVersion = ('' + $script:ResultFromVersion)
            toVersion   = ('' + $script:ResultToVersion)
            tag         = ('' + $script:ResultTag)
            message     = ('' + $Message)
            timeUtc     = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
        }
        $json = $payload | ConvertTo-Json -Compress
        # 一旦 tmp に書いてから差し替える (アプリが読みかけの不完全ファイルを掴まないように)。
        $tmp = $path + '.tmp'
        [System.IO.File]::WriteAllText($tmp, $json, (New-Object System.Text.UTF8Encoding($false)))
        Move-Item -LiteralPath $tmp -Destination $path -Force
        Write-OpLog -Level 'INFO' -Message ('結果ファイルを書きました: {0} (result={1}, exitCode={2})' -f $path, $Result, $Code)
    } catch {
        Write-OpLog -Level 'WARN' -Message ('結果ファイルを書けませんでした (更新処理には影響しません): {0}' -f $_.Exception.Message)
    }
}

function Test-PathInside {
    # True only when $Path is the same as, or below, $Root. A plain StartsWith() is wrong: 'C:\x\FlashBeat-update-work'
    # starts with 'C:\x\FlashBeat' although it is a sibling folder, which aborted every update of an install
    # folder named FlashBeat (found on the exhibit machine, v0.1.2 -> v0.1.3).
    param([string]$Path, [string]$Root)
    if (-not $Path -or -not $Root) { return $false }
    $p = ([System.IO.Path]::GetFullPath($Path)).TrimEnd('\', '/') + '\'
    $r = ([System.IO.Path]::GetFullPath($Root)).TrimEnd('\', '/') + '\'
    return $p.StartsWith($r, [StringComparison]::OrdinalIgnoreCase)
}

# ============================================================
# インストール単位のスコープ (Issue #185)
# ------------------------------------------------------------
# 更新 (退避の生成・削除) の対象は「更新対象とするインストール自身」に限定する。
# 同じ親フォルダに別のインストール (例: FlashBeat と FlashBeat-0.1.14) が
# あるとき、互いの退避 (復旧データ) を消してはいけない。
#
#   identity  = 正規化インストールパス + 安定 ID (フォルダ名由来) + 正規名かどうか
#   名前空間  = 退避フォルダ名に安定 ID を含める (FlashBeat-backup-<key>-<runId>)
#   所有証明  = 退避フォルダ内の flashbeat-backup-owner.json に identity を記録する
#
# 削除は「名前が自分の名前空間内」かつ「所有 manifest が自分の identity と一致」の
# ときだけ。旧形式 (所有不明) と他インストールの退避は残す。
# ============================================================

function Get-FlashBeatNormalizedPath {
    # 比較用の正規化パス (末尾区切りなし・小文字。Windows は大文字小文字を区別しない)。
    param([Parameter(Mandatory = $true)][string]$Path)
    $full = [System.IO.Path]::GetFullPath($Path).TrimEnd('\', '/')
    return $full.ToLowerInvariant()
}

function ConvertTo-FlashBeatInstallKey {
    # フォルダ名から安定 ID を作る (小文字・英数字と . _ - のみ・連続区切りは畳む)。
    param([Parameter(Mandatory = $true)][string]$Name)
    $key = $Name.ToLowerInvariant()
    $key = [regex]::Replace($key, '[^a-z0-9._-]+', '-')
    $key = [regex]::Replace($key, '-{2,}', '-')
    $key = $key.Trim('-', '.', '_')
    if (-not $key) { $key = 'flashbeat' }
    return $key
}

function Get-FlashBeatInstallIdentity {
    # 正規化したインストール identity。
    #   InstallPath : 絶対パス (比較用に小文字)
    #   InstallName : フォルダ名
    #   InstallKey  : 安定 ID (フォルダ名由来)
    #   IsCanonical : 正規名 (既定 FlashBeat) と一致するか = 自チームの正規インストール先か
    #   ParentPath  : 親フォルダ (退避・作業フォルダの置き場所)
    param(
        [Parameter(Mandatory = $true)][string]$InstallDir,
        [string]$CanonicalInstallName = 'FlashBeat'
    )
    $full = [System.IO.Path]::GetFullPath($InstallDir).TrimEnd('\', '/')
    $name = [System.IO.Path]::GetFileName($full)
    if (-not $name) { $name = $InstallDir }
    $parent = [System.IO.Path]::GetDirectoryName($full)
    return [pscustomobject]@{
        InstallPath = $full.ToLowerInvariant()
        InstallName = $name
        InstallKey  = (ConvertTo-FlashBeatInstallKey -Name $name)
        IsCanonical = ($name -eq $CanonicalInstallName)
        ParentPath  = $parent
    }
}

function New-FlashBeatRunId {
    # 実行ごとに一意な ID。同時刻の複数更新が同じ退避/作業フォルダを共有しないよう
    # PID と乱数を含める (issue #185 と同時刻更新の識別)。
    param(
        [int]$ProcessId = 0,
        [string]$RandomSuffix = '',
        [datetime]$Timestamp = [datetime]::Now
    )
    if ($ProcessId -le 0) { $ProcessId = $PID }
    if (-not $RandomSuffix) {
        $RandomSuffix = ([guid]::NewGuid().ToString('N')).Substring(0, 6)
    }
    return ('{0}-{1}-{2}' -f $Timestamp.ToString('yyyyMMdd-HHmmss'), $ProcessId, $RandomSuffix.ToLowerInvariant())
}

function New-FlashBeatBackupName {
    # 自インストール専用の退避フォルダ名 (名前空間 = インストール単位)。
    param(
        [Parameter(Mandatory = $true)][string]$InstallKey,
        [Parameter(Mandatory = $true)][string]$RunId
    )
    return ('FlashBeat-backup-{0}-{1}' -f $InstallKey, $RunId)
}

function New-FlashBeatWorkDirName {
    # 自インストール専用の作業フォルダ名。
    param(
        [Parameter(Mandatory = $true)][string]$InstallKey,
        [Parameter(Mandatory = $true)][string]$RunId
    )
    return ('FlashBeat-update-work-{0}-{1}' -f $InstallKey, $RunId)
}

function Test-FlashBeatScopedName {
    # 自分 (InstallKey) の名前空間内の退避フォルダ名かどうか。
    # 旧形式 'FlashBeat-backup-<yyyyMMdd-HHmmss>' は所有不明なので false (自動削除しない)。
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string]$InstallKey
    )
    $prefix = 'FlashBeat-backup-' + $InstallKey + '-'
    if (-not $Name.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) { return $false }
    $rest = $Name.Substring($prefix.Length)
    return [regex]::IsMatch($rest, '^\d{8}-\d{6}-\d+-[0-9a-f]{4,8}$')
}

function Test-FlashBeatOwnerManifest {
    # 退避フォルダの所有 manifest が「今回のインストール」と一致するか (純粋関数)。
    #   Owned  : 削除してよい退避か
    #   Reason : 判定理由 (no-manifest / invalid-manifest / owner-key-mismatch / owner-path-mismatch / owned)
    param(
        $Manifest,
        [Parameter(Mandatory = $true)]$Identity
    )
    if ($null -eq $Manifest) {
        return [pscustomobject]@{ Owned = $false; Reason = 'no-manifest' }
    }
    $key = ''
    $path = ''
    try {
        $key = [string]$Manifest.installKey
        $path = [string]$Manifest.installPath
    } catch {
        return [pscustomobject]@{ Owned = $false; Reason = 'invalid-manifest' }
    }
    if (-not $key -or -not $path) {
        return [pscustomobject]@{ Owned = $false; Reason = 'invalid-manifest' }
    }
    if ($key -ne $Identity.InstallKey) {
        return [pscustomobject]@{ Owned = $false; Reason = 'owner-key-mismatch' }
    }
    if ((Get-FlashBeatNormalizedPath -Path $path) -ne $Identity.InstallPath) {
        return [pscustomobject]@{ Owned = $false; Reason = 'owner-path-mismatch' }
    }
    return [pscustomobject]@{ Owned = $true; Reason = 'owned' }
}

function Get-FlashBeatBackupCandidates {
    # 親フォルダ内の退避フォルダ候補と、その所有 manifest を集める (FS アクセス)。
    param(
        [Parameter(Mandatory = $true)][string]$ParentDir,
        [string]$ManifestName = 'flashbeat-backup-owner.json'
    )
    $result = @()
    $dirs = @(Get-ChildItem -LiteralPath $ParentDir -Directory -Filter 'FlashBeat-backup-*' -ErrorAction SilentlyContinue)
    foreach ($dir in $dirs) {
        $manifest = $null
        $state = 'missing'
        $manifestPath = Join-Path $dir.FullName $ManifestName
        if (Test-Path -LiteralPath $manifestPath -PathType Leaf) {
            try {
                $manifest = Get-Content -LiteralPath $manifestPath -Raw -ErrorAction Stop | ConvertFrom-Json
                $state = 'ok'
            } catch {
                $manifest = $null
                $state = 'invalid'
            }
        }
        $result += [pscustomobject]@{
            Name         = $dir.Name
            FullName     = $dir.FullName
            Manifest     = $manifest
            ManifestState = $state
        }
    }
    return $result
}

function Select-FlashBeatStaleBackups {
    # 削除してよい退避 (自インストールの旧世代) と、残す退避 (理由つき) を判定する (純粋関数)。
    #   Deletable : 削除してよい (自インストールの旧世代)
    #   Kept      : 残す退避 (理由: current-generation / other-namespace / no-manifest / ...)
    param(
        [object[]]$Candidates,
        [Parameter(Mandatory = $true)]$Identity,
        [string]$CurrentBackupPath = ''
    )
    $deletable = @()
    $kept = @()
    $currentFull = ''
    if ($CurrentBackupPath) {
        $currentFull = Get-FlashBeatNormalizedPath -Path $CurrentBackupPath
    }
    foreach ($candidate in $Candidates) {
        $full = [string]$candidate.FullName
        $name = [string]$candidate.Name
        if ($currentFull -and ((Get-FlashBeatNormalizedPath -Path $full) -eq $currentFull)) {
            $kept += [pscustomobject]@{ Path = $full; Name = $name; Reason = 'current-generation' }
            continue
        }
        if (-not (Test-FlashBeatScopedName -Name $name -InstallKey $Identity.InstallKey)) {
            # 他インストールの退避 / 旧形式 (所有不明) → 触らない。
            $kept += [pscustomobject]@{ Path = $full; Name = $name; Reason = 'other-namespace' }
            continue
        }
        $owner = Test-FlashBeatOwnerManifest -Manifest $candidate.Manifest -Identity $Identity
        if (-not $owner.Owned) {
            $reason = $owner.Reason
            if ($candidate.ManifestState -eq 'invalid') { $reason = 'invalid-manifest' }
            $kept += [pscustomobject]@{ Path = $full; Name = $name; Reason = $reason }
            continue
        }
        $deletable += [pscustomobject]@{ Path = $full; Name = $name; Reason = 'owned-old-generation' }
    }
    return [pscustomobject]@{ Deletable = $deletable; Kept = $kept }
}

function Write-FlashBeatOwnerManifest {
    # 退避フォルダに所有 manifest を書く (これが無い退避は自動削除しない)。
    param(
        [Parameter(Mandatory = $true)][string]$BackupDir,
        [Parameter(Mandatory = $true)]$Identity,
        [Parameter(Mandatory = $true)][string]$RunId,
        [string]$Version = '',
        [string]$ManifestName = 'flashbeat-backup-owner.json'
    )
    $payload = [ordered]@{
        schema       = 'flashbeat-backup-owner/1'
        installPath  = $Identity.InstallPath
        installName  = $Identity.InstallName
        installKey   = $Identity.InstallKey
        isCanonical  = [bool]$Identity.IsCanonical
        parentPath   = (Get-FlashBeatNormalizedPath -Path $Identity.ParentPath)
        runId        = $RunId
        version      = $Version
        createdAtUtc = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
    }
    $json = $payload | ConvertTo-Json -Depth 4
    $path = Join-Path $BackupDir $ManifestName
    [System.IO.File]::WriteAllText($path, $json, (New-Object System.Text.UTF8Encoding($false)))
    return $path
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
    # 結果ファイル (issue #125): 前提不備 (1) と実行中 (6) は「中止」、それ以外は「失敗」として記録する。
    $resultKind = 'failed'
    if ($Code -eq $ExitUsage -or $Code -eq $ExitRunning) { $resultKind = 'aborted' }
    Write-UpdateResult -Result $resultKind -Code $Code -Message $Message
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

function Wait-FlashBeatExit {
    # -WaitPid で指定されたプロセスの終了を待つ (アプリ終了後に更新を適用するフロー用)。
    # PID が既に無い / 別プロセスに付替え済みの場合は即座に続行する。
    param([Parameter(Mandatory = $true)][int]$ProcessId)
    $deadline = (Get-Date).AddSeconds(300)
    while ((Get-Date) -lt $deadline) {
        $proc = Get-Process -Id $ProcessId -ErrorAction SilentlyContinue
        if (-not $proc) {
            Write-OpLog -Level 'INFO' -Message ('待機対象のプロセスが終了しました (PID={0})。' -f $ProcessId)
            return $true
        }
        if ($proc.ProcessName -ne 'FlashBeat') {
            # PID が別プロセスに再利用された → 元プロセスは終了済みとみなす
            Write-OpLog -Level 'WARN' -Message ('PID={0} は別プロセス ({1}) に再利用されました。待機を打ち切ります。' -f $ProcessId, $proc.ProcessName)
            return $true
        }
        Start-Sleep -Milliseconds 500
    }
    Write-OpLog -Level 'ERROR' -Message ('PID={0} の終了を 300 秒待っても終了しませんでした。' -f $ProcessId)
    return $false
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

function Invoke-FlashBeatUpdateMain {
try {
    $scriptPath = $script:UpdateScriptPath

    Write-OpLog -Level 'STEP' -Message 'FlashBeat 単発パッチ適用を開始します。'

    # ── 0. アプリ終了待ち (-WaitPid、自動適用フロー用) ─────────────
    if ($WaitPid -gt 0) {
        Write-OpLog -Level 'STEP' -Message ('アプリの終了を待機します (PID={0})。' -f $WaitPid)
        if (-not (Wait-FlashBeatExit -ProcessId $WaitPid)) {
            Stop-WithError $ExitRunning ('アプリ (PID={0}) が終了しないため更新を中止しました。既存配置は変更していません。' -f $WaitPid)
        }
    }

    # ── 1. インストール先の前提確認 ───────────────────────────────
    $freshInstall = $false
    try {
        $installFull = (Resolve-Path -LiteralPath $InstallDir -ErrorAction Stop).Path
    } catch {
        # フォルダ自体が無い → 新規インストール (Release から取得して配置)
        $freshInstall = $true
        $installFull = [System.IO.Path]::GetFullPath($InstallDir)
    }
    if (-not $freshInstall -and -not (Test-Path -LiteralPath $installFull -PathType Container)) {
        Stop-WithError $ExitUsage ('インストール先フォルダがディレクトリではありません: {0}' -f $installFull)
    }
    if (-not $freshInstall -and -not (Test-Path -LiteralPath (Join-Path $installFull 'FlashBeat.exe') -PathType Leaf)) {
        # FlashBeat.exe が無い → 新規インストールとして扱う
        $freshInstall = $true
    }
    if ($freshInstall) {
        Write-OpLog -Level 'STEP' -Message ('インストール先に FlashBeat.exe がないため新規インストールとして実行します: {0}' -f $installFull)
    }
    if (-not $Apply -and $scriptPath -and (Test-PathInside $scriptPath $installFull)) {
        Stop-WithError $ExitUsage ('更新スクリプトがインストール先フォルダ内にあります: {0}' -f $scriptPath) 'スクリプトはインストール先外に置くか、-Apply (自動適用モード) を使ってください。'
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

    # 結果ファイル用 (issue #125): 更新元 (現行) のバージョンを exe から取得できれば記録する。
    try {
        if (-not $freshInstall) {
            $oldExe = Join-Path $installFull 'FlashBeat.exe'
            if (Test-Path -LiteralPath $oldExe -PathType Leaf) {
                $script:ResultFromVersion = (Get-Item -LiteralPath $oldExe).VersionInfo.ProductVersion
            }
        }
    } catch {
        $script:ResultFromVersion = ''
    }

    # ── 2. 実行中チェック (先に落とし、ダウンロードを無駄にしない) ──
    # -WaitPid 指定時は既に待機済み。二重起動 (待機対象以外の FlashBeat) があれば中止する。
    $running = Get-FlashBeatProcesses
    if ($running.Count -gt 0) {
        if ($WaitPid -gt 0) {
            Stop-WithError $ExitRunning ('待機対象以外の FlashBeat が実行中のため (二重起動) 更新を中止しました: {0}' -f (Format-ProcessList $running)) 'すべての FlashBeat を終了してから再実行してください。強制終了は行いません。既存配置は変更していません。'
        }
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

    # インストール identity と実行 ID (issue #185)。
    #   - 退避 / 作業フォルダは「インストール単位の名前空間」に入れて、隣のインストールのものに触らない。
    #   - 実行 ID に PID と乱数を含めるので、同時刻の複数実行が同じフォルダを共有しない。
    $identity = Get-FlashBeatInstallIdentity -InstallDir $installFull
    $runId = New-FlashBeatRunId
    $canonicalLabel = 'no'
    if ($identity.IsCanonical) { $canonicalLabel = 'yes' }
    Write-OpLog -Level 'INFO' -Message ('インストール identity: key={0} name={1} 正規インストール先={2} path={3}' -f `
            $identity.InstallKey, $identity.InstallName, $canonicalLabel, $identity.InstallPath)

    if ($WorkDir) {
        $workRoot = [System.IO.Path]::GetFullPath($WorkDir)
    } else {
        $workRoot = Join-Path $parentDir (New-FlashBeatWorkDirName -InstallKey $identity.InstallKey -RunId $runId)
    }
    if (Test-PathInside $workRoot $installFull) {
        Stop-WithError $ExitUsage ('作業フォルダがインストール先フォルダ内にあります: {0}' -f $workRoot) '-WorkDir にはインストール先外のフォルダを指定してください。'
    }
    $downloadDir = Join-Path $workRoot 'download'
    $stagingDir = Join-Path $workRoot 'staging'
    $backupDir = Join-Path $parentDir (New-FlashBeatBackupName -InstallKey $identity.InstallKey -RunId $runId)

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
    # 結果ファイル用 (issue #125): タグ / 新版バージョンを把握できたら記録する。
    if ($Tag) {
        $script:ResultTag = $Tag
    } elseif ($assetFileName) {
        $script:ResultTag = $assetFileName
    }
    $script:ResultToVersion = Get-UpdateVersionFromText -Text $script:ResultTag

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
        if ($WaitPid -gt 0) {
            Stop-WithError $ExitRunning ('待機対象以外の FlashBeat が実行中のため (二重起動) 更新を中止しました: {0}' -f (Format-ProcessList $running)) 'すべての FlashBeat を終了してから再実行してください。強制終了は行いません。既存配置は変更していません。'
        }
        Stop-WithError $ExitRunning ('FlashBeat が実行中のため更新を中止しました: {0}' -f (Format-ProcessList $running)) 'ゲームを終了してから Update.ps1 を再実行してください。強制終了は行いません。既存配置は変更していません。'
    }

    # ── 9. 一括切替 (新規インストール時は退避なしで配置) ──────────
    if (-not $freshInstall) {
        try {
            New-Item -ItemType Directory -Path $backupDir -Force | Out-Null
        } catch {
            Stop-WithError $ExitSwap ('退避フォルダを作成できません: {0} ({1})' -f $backupDir, $_.Exception.Message) 'インストール先の親フォルダの書き込み権限を確認してください。既存配置は変更していません。'
        }

        # 所有 manifest を先に書いてから退避する (issue #185)。
        # manifest が無い退避は「所有不明」として自動削除しないため、書けなければ中止する。
        try {
            $manifestPath = Write-FlashBeatOwnerManifest -BackupDir $backupDir -Identity $identity -RunId $runId -Version $script:ResultFromVersion
            Write-OpLog -Level 'INFO' -Message ('退避の所有 manifest を書きました: {0}' -f $manifestPath)
        } catch {
            Stop-WithError $ExitSwap ('退避の所有 manifest を書けません: {0} ({1})' -f $backupDir, $_.Exception.Message) '既存配置は変更していません。'
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
    } else {
        try {
            New-Item -ItemType Directory -Path $installFull -Force -ErrorAction Stop | Out-Null
        } catch {
            Stop-WithError $ExitSwap ('インストール先フォルダを作成できません: {0} ({1})' -f $installFull, $_.Exception.Message)
        }
    }

    Write-OpLog -Level 'STEP' -Message '検証済みの展開物をインストール先へ配置します。'
    try {
        foreach ($item in @(Get-ChildItem -LiteralPath $payloadRoot -Force -ErrorAction Stop)) {
            Move-Item -LiteralPath $item.FullName -Destination $installFull -Force -ErrorAction Stop
        }
    } catch {
        $failed = $_.Exception.Message
        if (-not $freshInstall) {
            foreach ($item in @(Get-ChildItem -LiteralPath $installFull -Force -ErrorAction SilentlyContinue)) {
                try { Move-Item -LiteralPath $item.FullName -Destination $stagingDir -Force -ErrorAction SilentlyContinue } catch { }
            }
            foreach ($item in @(Get-ChildItem -LiteralPath $backupDir -Force -ErrorAction SilentlyContinue)) {
                try { Move-Item -LiteralPath $item.FullName -Destination $installFull -Force -ErrorAction SilentlyContinue } catch { }
            }
            Stop-WithError $ExitSwap ('切替に失敗しました: {0}' -f $failed) ('既存配置は退避データから復元しました。退避データは {0} に残しています。' -f $backupDir)
        }
        Stop-WithError $ExitSwap ('配置に失敗しました: {0}' -f $failed)
    }

    $postMissing = @()
    if (-not (Test-Path -LiteralPath (Join-Path $installFull 'FlashBeat.exe') -PathType Leaf)) { $postMissing += 'FlashBeat.exe' }
    if (-not (Test-Path -LiteralPath (Join-Path $installFull 'FlashBeat_Data') -PathType Container)) { $postMissing += 'FlashBeat_Data' }
    if ($postMissing.Count -gt 0) {
        if (-not $freshInstall) {
            foreach ($item in @(Get-ChildItem -LiteralPath $installFull -Force -ErrorAction SilentlyContinue)) {
                try { Move-Item -LiteralPath $item.FullName -Destination $stagingDir -Force -ErrorAction SilentlyContinue } catch { }
            }
            foreach ($item in @(Get-ChildItem -LiteralPath $backupDir -Force -ErrorAction SilentlyContinue)) {
                try { Move-Item -LiteralPath $item.FullName -Destination $installFull -Force -ErrorAction SilentlyContinue } catch { }
            }
            Stop-WithError $ExitSwap ('切替後の検査に失敗しました (不足: {0})' -f ($postMissing -join ', ')) ('既存配置は退避データから復元しました。退避データは {0} に残しています。' -f $backupDir)
        }
        Stop-WithError $ExitSwap ('配置後の検査に失敗しました (不足: {0})' -f ($postMissing -join ', '))
    }

    # ── 10. 旧版を 1 世代だけ残し、それより古い退避を削除する ─────
    if (-not $freshInstall) {
        Write-OpLog -Level 'INFO' -Message ('旧版を 1 世代残します: {0}' -f $backupDir)

        # 削除対象は「自インストールの退避」のみ (issue #185)。
        #   判定は「名前が自分の名前空間内」+「所有 manifest が自分の identity と一致」の二重チェック。
        #   隣のインストールの退避と旧形式 (所有不明) は残す。
        $candidates = Get-FlashBeatBackupCandidates -ParentDir $parentDir
        $selection = Select-FlashBeatStaleBackups -Candidates $candidates -Identity $identity -CurrentBackupPath $backupDir
        foreach ($keep in $selection.Kept) {
            Write-OpLog -Level 'INFO' -Message ('退避を削除せず残します: {0} (理由: {1})' -f $keep.Path, $keep.Reason)
        }
        foreach ($stale in $selection.Deletable) {
            try {
                Remove-Item -LiteralPath $stale.Path -Recurse -Force -ErrorAction Stop
                Write-OpLog -Level 'INFO' -Message ('自インストールの旧世代の退避を削除しました: {0}' -f $stale.Path)
            } catch {
                Write-OpLog -Level 'WARN' -Message ('旧世代の退避の削除に失敗しました (手動で削除してください): {0} ({1})' -f $stale.Path, $_.Exception.Message)
            }
        }
    }
    try {
        Remove-Item -LiteralPath $workRoot -Recurse -Force -ErrorAction Stop
    } catch {
        Write-OpLog -Level 'WARN' -Message ('作業フォルダの削除に失敗しました (手動で削除してください): {0}' -f $workRoot)
    }

    Write-OpLog -Level 'INFO' -Message ('更新が完了しました: {0}' -f (Join-Path $installFull 'FlashBeat.exe'))

    # 結果ファイル (issue #125): 切替と検査が完了したここでのみ success を書く (再起動の成否には依存しない)。
    Write-UpdateResult -Result 'success' -Code 0 -Message '更新が完了しました。'

    # ── 11. 再起動 (-Relaunch、自動適用フロー用) ──────────────────
    if ($Relaunch) {
        try {
            $relaunched = Start-Process -FilePath (Join-Path $installFull 'FlashBeat.exe') -WorkingDirectory $installFull -PassThru -ErrorAction Stop
            Write-OpLog -Level 'INFO' -Message ('FlashBeat.exe を再起動しました (PID={0})。' -f $relaunched.Id)
        } catch {
            Write-OpLog -Level 'WARN' -Message ('FlashBeat.exe の再起動に失敗しました (更新自体は完了しています): {0}' -f $_.Exception.Message)
        }
    } else {
        Write-OpLog -Level 'INFO' -Message 'FlashBeat.exe を直接起動して動作を確認してください (起動確認は運営者が手動で行います)。'
    }
    exit 0
}
catch {
    Write-OpLog -Level 'ERROR' -Message ('想定外のエラーが発生しました: {0}' -f $_.Exception.Message)
    Write-UpdateResult -Result 'failed' -Code $ExitSwap -Message ('想定外のエラーが発生しました: {0}' -f $_.Exception.Message)
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
}

# 直接実行されたとき (powershell -File / & で 호출) だけ本体を走らせる。
# テストから dot-source されたときは関数定義だけを取り込む (純粋関数を検証するため)。
if ($MyInvocation.InvocationName -ne '.') {
    Invoke-FlashBeatUpdateMain
}
