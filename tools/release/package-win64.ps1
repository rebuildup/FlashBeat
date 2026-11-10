#requires -Version 5.1
<#
.SYNOPSIS
    検証済み Win64 Unity ビルドフォルダ一式をバージョン付き ZIP + SHA-256 sidecar にパッケージする (Issue #35)。

.DESCRIPTION
    - Unity 出力フォルダを丸ごと ZIP 化する (FlashBeat.exe + FlashBeat_Data + UnityPlayer.dll 等)。
      exe 単体の ZIP は作らない。
    - FlashBeat_BurstDebugInformation_DoNotShip は除外する (Unity の指示: ship しない)。
    - 第三者楽曲の音声・動画を配布物に含めない (オーナー要件 2026-11-07):
        * ルーズなメディアファイル (*.wav/*.mp3/*.mp4/...) がビルド出力にあれば失敗する。
        - Assets/Game/Resources/** が Unity により resources.resource に焼き込まれる場合、ZIP レベルでは
          除去できない。-MediaPolicy free のときは resources.resource のサイズ上限で検知して失敗する。
          media-free リリースは Unity 側でメディアを Resources 外 (ストリーミング/キャッシュ) に分離してから。
    - ZIP ルートに version.json を埋め込む (人が読む同梱メタデータ。更新判定は UpdateChecker が
      Application.version と Release タグで行い、version.json は読まない。下記 docs 参照):
        docs/operations/update-release.md の「配布仕様」。
    - ZIP 作成後、内容の最低限の検査 (exe / Data / Managed DLL / version.json の存在、arch integrity) を行う。
      ビルド出力の StreamingAssets/update-channel.txt (更新チャンネル) もそのまま同梱されます。
    - ZIP の隣に `<zip名>.sha256` (SHA-256 sidecar, sha256sum -c 互換) を書き出す。
    - 秘密情報 (トークン・署名付き URL・credential) は ZIP に一切含めない。入力はビルドフォルダのみ。
    - **再現性**: 同じビルド出力から再パッケージすると **SHA-256 が一致する**。
      version.json の createdAt は実行時刻 (Get-Date) ではなく **ビルド元 commit の日時** から作り、
      version.json 自身の mtime も同じ値に固定する (実行時刻を使うと ZIP がバイト単位で再現しなくなる)。
      実測: docs/evidence/win64-build-from-macos.md。

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File tools/release/package-win64.ps1 `
        -SourceDir C:\...\Builds\Windows -OutDir D:\dist `
        -Version 0.1.0-win64-743c88d -CommitSha 743c88d14a1931d4637c34b1ec5d1bec5e502d44 `
        -MediaPolicy bundled
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$SourceDir,

    [Parameter(Mandatory = $true)]
    [string]$OutDir,

    # <semver>-win64-<build commit SHA 短縮形>。GitHub Release タグは "v" + この値。
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+-win64-[0-9a-f]{7,40}$')]
    [string]$Version,

    # ビルド元 commit のフル SHA。version.json に記録され、Version 内の短縮 SHA と整合を検証する。
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[0-9a-f]{40}$')]
    [string]$CommitSha,

    # bundled    = resources.resource にメディアが焼き込まれているビルド (検査なし)。
    # free       = メディア完全分離ビルド。焼き込みサイズ上限 (MaxResourcesBytes) を超えたら失敗。
    # song-free  = 第三者楽曲の音声・動画だけが外されたビルド (自作の背景アニメーションは同梱可)。
    #              ビルドが出力する flashbeat-media-policy.json で除外を証明する。
    [ValidateSet('bundled', 'free', 'song-free')]
    [string]$MediaPolicy = 'bundled',

    # MediaPolicy=free / song-free のときの resources.resource サイズ上限 (既定 300MB)。
    # 自作の背景アニメーション (Resources/Videos/*.mp4) だけで約 196MB になる。
    [long]$MaxResourcesBytes = 300MB,

    # ビルド元 commit の日時 (ISO 8601)。version.json の createdAt はこの値から決める。
    # 未指定なら git (git show -s --format=%cI <CommitSha>) から導出する。
    # **実行時刻を使ってはいけない**: 同じ入力でも ZIP がバイト単位で再現しなくなり、
    # 再現性検証 (同じソース → 同じ SHA) が成立しなくなる。
    [string]$CommitDate = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# PowerShell 7.4+ は「ネイティブコマンドの非ゼロ終了を例外にする」設定がある。
# tar の失敗は .NET 例外ではなくこのスクリプトの日本語診断で扱うため、無効化する。
if (Get-Variable -Name PSNativeCommandUseErrorActionPreference -ErrorAction SilentlyContinue) {
    $PSNativeCommandUseErrorActionPreference = $false
}

$script:MediaExtensions = @('.wav', '.mp3', '.mp4', '.mov', '.ogg', '.m4a', '.webm', '.avi')

# --- プラットフォーム判定 ----------------------------------------------------
# このスクリプトは Windows 上で使う想定だったが、macOS (pwsh) からも
# Win64 ビルドをパッケージできる必要がある (Unity の Windows Build Support が
# macOS Editor に入っていれば macOS から Win64 をビルドできる)。
# そのため (a) パス区切りを Join-Path で解決し (b) tar は Windows 同梱の
# tar.exe ではなく libarchive の tar を優先して使う。
# PowerShell 5.1 は Windows 専用なので $IsWindows は使えない。
$script:OnWindows = if ($PSVersionTable.PSVersion.Major -ge 6) { $IsWindows } else { $true }

function Get-ExecutableFromPath {
    param([string]$Name)

    # PATH を先頭から順に走査して **実行ファイルだけ** を探す。
    # Get-Command を使うと Function / Alias を優先して掴まされ、
    # ユーザープロフィールの `function tar { ... }` で .Source が空文字になる。
    $exts = @('')
    if ($script:OnWindows) {
        $pathext = $env:PATHEXT
        if ([string]::IsNullOrWhiteSpace($pathext)) { $pathext = '.COM;.EXE;.BAT;.CMD' }
        $exts = @($pathext -split ';' | Where-Object { $_ } | ForEach-Object { $_.ToLowerInvariant() })
    }

    $sep = [System.IO.Path]::PathSeparator
    foreach ($dir in ($env:PATH -split [regex]::Escape($sep))) {
        if ([string]::IsNullOrWhiteSpace($dir)) { continue }
        foreach ($ext in $exts) {
            $candidate = Join-Path $dir ($Name + $ext)
            if (Test-Path -LiteralPath $candidate -PathType Leaf) { return $candidate }
        }
    }
    return $null
}

function Test-ZipMagic {
    param([string]$Path)

    # ZIP かどうか確実に判定する (拡張子 .zip でも中身が tar なら false)。
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { return $false }
    $stream = [System.IO.File]::OpenRead($Path)
    try {
        $head = New-Object byte[] 4
        $read = $stream.Read($head, 0, 4)
        if ($read -lt 4) { return $false }
        if ($head[0] -ne 0x50 -or $head[1] -ne 0x4B) { return $false }
        # "PK\x03\x04" = ローカルファイルヘッダ / "PK\x05\x06" = 空 ZIP の EOCD
        return ($head[2] -eq 0x03 -and $head[3] -eq 0x04) -or ($head[2] -eq 0x05 -and $head[3] -eq 0x06)
    }
    finally { $stream.Dispose() }
}

function Resolve-TarCommand {
    # 候補は PATH の先頭から順に見て、最初に見つかった実行ファイルを採る
    # (Function / Alias は実行ファイルではないので採らない)。
    # Windows 同梱の tar.exe (libarchive) と macOS / Linux の bsdtar はどちらも
    # zip 形式を作れる。**zip を作れない tar は Resolve-TarZipFlags で弾く**。
    $candidates = if ($script:OnWindows) { @('tar.exe', 'tar') } else { @('tar', 'tar.exe') }
    foreach ($name in $candidates) {
        $path = Get-ExecutableFromPath -Name $name
        if ($path) { return $path }
    }
    throw "ZIP 作成に使う tar が見つかりません (試した名前: $($candidates -join ', '))"
}

function Resolve-TarZipFlags {
    param([string]$TarPath)

    # libarchive 系 (Windows 同梱 tar.exe / macOS の bsdtar) は拡張子から形式を
    # 推断する -a と、明示指定の --format=zip の両方を受け付ける。
    # GNU tar は zip を作れない (拡張子 .zip でも中身を無圧縮 tar にする) ため、
    # **名前ではなく実際に作ってから ZIP のマジックで判定する**。
    $flagSets = @(
        , @('-a')
        , @('--format=zip')
    )

    $probeDir = Join-Path ([System.IO.Path]::GetTempPath()) ('fb-tar-probe-' + [System.Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $probeDir -Force | Out-Null
    $prevEap = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        [System.IO.File]::WriteAllText((Join-Path $probeDir 'probe.txt'), "probe`n")
        foreach ($flags in $flagSets) {
            $out = Join-Path $probeDir 'probe.zip'
            if (Test-Path -LiteralPath $out) { Remove-Item -LiteralPath $out -Force }
            $code = -1
            try {
                & $TarPath @flags -cf $out -C $probeDir 'probe.txt' 1>$null 2>$null
                $code = $LASTEXITCODE
            }
            catch { $code = -1 }
            if ($code -eq 0 -and (Test-ZipMagic -Path $out)) { return , $flags }
        }
    }
    finally {
        $ErrorActionPreference = $prevEap
        Remove-Item -LiteralPath $probeDir -Recurse -Force -ErrorAction SilentlyContinue
    }

    $tried = (($flagSets | ForEach-Object { $_ -join ' ' }) -join ' / ')
    throw ("ZIP を作れない tar が選ばれました: {0}`n" +
           "この tar は zip 形式に対応していません (拡張子 .zip の中身が tar .archive になります)。" +
           "Windows 同梱の tar.exe (libarchive) または macOS / Linux の bsdtar を PATH の先頭に置いてください。" +
           " 試した形式指定: {1}") -f $TarPath, $tried
}

function Test-TarTwoHopSupport {
    param(
        [string]$TarPath,
        [string[]]$ZipFlags
    )

    # 「2 段構え」(ディスク → ustar → zip) が使えるか調べる。
    # ustar には atime / ctime のフィールドが無いので、この経路で ZIP を作ると
    # ZIP に記録されるのは **mtime だけ** になり、ZIP がバイト単位で再現する。
    # (1 段構えだとローカルヘッダの UT 拡張フィールドに実行時刻の atime / ctime が
    #  載り、version.json のエントリだけ ZIP が変わる)
    $probeDir = Join-Path ([System.IO.Path]::GetTempPath()) ('fb-tar-hop-' + [System.Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $probeDir -Force | Out-Null
    $prevEap = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        [System.IO.File]::WriteAllText((Join-Path $probeDir 'probe.txt'), "probe`n")
        $mid = Join-Path $probeDir 'probe.tar'
        $out = Join-Path $probeDir 'probe.zip'

        & $TarPath -c --format ustar -f $mid -C $probeDir 'probe.txt' 1>$null 2>$null
        if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $mid -PathType Leaf)) { return $false }

        & $TarPath @ZipFlags -cf $out "@$mid" 1>$null 2>$null
        if ($LASTEXITCODE -ne 0) { return $false }

        return (Test-ZipMagic -Path $out)
    }
    catch { return $false }
    finally {
        $ErrorActionPreference = $prevEap
        Remove-Item -LiteralPath $probeDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}

function Join-PathSegments {
    param([string]$Base, [string[]]$Segments)
    $p = $Base
    foreach ($s in $Segments) { $p = Join-Path $p $s }
    return $p
}

function Assert-Win64BuildLayout {
    param([string]$Dir)

    if (-not (Test-Path -LiteralPath (Join-Path $Dir 'FlashBeat.exe') -PathType Leaf)) {
        throw "FlashBeat.exe が見つかりません: $Dir"
    }
    if (-not (Test-Path -LiteralPath (Join-Path $Dir 'FlashBeat_Data') -PathType Container)) {
        throw "FlashBeat_Data が見つかりません: $Dir"
    }
    if (-not (Test-Path -LiteralPath (Join-Path $Dir 'UnityPlayer.dll') -PathType Leaf)) {
        throw "UnityPlayer.dll が見つかりません: $Dir"
    }
}

function Get-ManagedDllCount {
    param([string]$Dir)

    $managed = Join-PathSegments -Base $Dir -Segments @('FlashBeat_Data', 'Managed')
    if (-not (Test-Path -LiteralPath $managed -PathType Container)) {
        throw ("{0} が見つかりません: {1}" -f (Join-PathSegments -Base 'FlashBeat_Data' -Segments @('Managed')), $managed)
    }
    return (Get-ChildItem -LiteralPath $managed -Filter '*.dll' -File).Count
}

function Assert-NoLooseMedia {
    param([string]$Dir)

    $found = Get-ChildItem -LiteralPath $Dir -Recurse -File -ErrorAction SilentlyContinue |
        Where-Object { $script:MediaExtensions -contains $_.Extension.ToLowerInvariant() }
    if ($found) {
        $names = ($found | Select-Object -First 5 | ForEach-Object { $_.FullName.Substring($Dir.Length + 1) }) -join ', '
        throw "配布 ZIP にメディアを含めないオーナー要件に反します。ルーズなメディアファイル: $names"
    }
}

function Assert-MediaPolicy {
    param([string]$Dir)

    # song-free: 第三者楽曲の音声・動画 (Resources/Musics, Resources/Videos/UnityYoutube) が
    # ビルド対象から外されたことを、ビルドが出力した manifest で確認する。
    # ゲーム自身の背景アニメーション (Resources/Videos/*.mp4) は自作素材なので同梱してよい
    # (media-rights-audit: これは権利ではなくサイズの問題)。
    if ($MediaPolicy -eq 'song-free') {
        $manifestPath = Join-Path $Dir 'flashbeat-media-policy.json'
        if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
            throw ("MediaPolicy=song-free ですがビルド出力に flashbeat-media-policy.json がありません。" +
                   "BuildScript が書き出す manifest が必要です (ビルドし直してください)。")
        }
        $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
        $excluded = @($manifest.excludedPaths)
        $required = @('Assets/Game/Resources/Musics', 'Assets/Game/Resources/Videos/UnityYoutube')
        foreach ($need in $required) {
            if ($excluded -notcontains $need) {
                throw ("MediaPolicy=song-free ですが manifest に '{0}' が含まれていません。" +
                       "第三者楽曲の音声・動画が配布物に焼き込まれます。") -f $need
            }
        }
        Write-Host ("[media] 第三者楽曲メディアはビルド対象から除外済み (manifest: {0})" -f ($excluded -join ', '))
    }

    $packed = Join-PathSegments -Base $Dir -Segments @('FlashBeat_Data', 'resources.resource')
    if (-not (Test-Path -LiteralPath $packed -PathType Leaf)) {
        if ($MediaPolicy -eq 'free' -or $MediaPolicy -eq 'song-free') {
            Write-Warning 'resources.resource が存在しません (メディア完全分離済みと解釈します)'
        }
        return
    }
    $size = (Get-Item -LiteralPath $packed).Length
    if (($MediaPolicy -eq 'free' -or $MediaPolicy -eq 'song-free') -and $size -gt $MaxResourcesBytes) {
        $nl = [Environment]::NewLine
        throw ("MediaPolicy={0} ですが resources.resource が {1:N0} bytes と上限 {2:N0} を超えています。{3}" +
               "Assets/Game/Resources/** のメディア (第三者楽曲) が焼き込まれている可能性。{3}" +
               'Unity 側でメディアを Resources 外に分離してから再パッケージしてください。') -f $MediaPolicy, $size, $MaxResourcesBytes, $nl
    }
}

function New-BuildZip {
    param(
        [string]$Dir,
        [string]$VersionJsonPath,
        [string]$ZipPath,
        [string]$TarPath,
        [string[]]$TarFlags,
        [string]$IntermediateTarPath,
        [bool]$UseTwoHop
    )

    # libarchive の tar を使用 (Windows 同梱の tar.exe / macOS の bsdtar)。
    # -C でカーソルを移動し、トップレベルのエントリをそのまま ZIP ルートに置く。
    # version.json はステージングから ZIP ルートへ追加する。
    $entries = Get-ChildItem -LiteralPath $Dir -Force |
        Where-Object { $_.Name -ne 'FlashBeat_BurstDebugInformation_DoNotShip' } |
        ForEach-Object { $_.Name }

    if ($entries.Count -lt 5) {
        throw "ビルドフォルダのトップレベル エントリが少なすぎます: $($entries.Count)"
    }

    $staging = [System.IO.Path]::GetDirectoryName($VersionJsonPath)
    try {
        if ($UseTwoHop) {
            # 1 段目: ビルド出力 + version.json を ustar に固める。
            # ustar には atime / ctime のフィールドが無いので、mtime だけが載る。
            & $TarPath -c --format ustar -f $IntermediateTarPath -C $Dir @entries -C $staging 'version.json'
            $code = $LASTEXITCODE
            if ($code -ne 0) {
                throw "tar (ustar) が失敗しました (exit=$code): $IntermediateTarPath"
            }
            # 2 段目: ustar を zip に変換する (atime / ctime は載らない → 再現する)。
            & $TarPath @TarFlags -cf $ZipPath "@$IntermediateTarPath"
            $code = $LASTEXITCODE
            if ($code -ne 0) {
                throw "tar (zip) が失敗しました (exit=$code): $ZipPath"
            }
        }
        else {
            & $TarPath @TarFlags -cf $ZipPath -C $Dir @entries -C $staging 'version.json'
            $code = $LASTEXITCODE
            if ($code -ne 0) {
                throw "tar が失敗しました (exit=$code): $ZipPath"
            }
        }
    }
    catch {
        # 壊れた / ZIP でない .zip を残さない (release 手順は *.zip を glob で拾うため)。
        Remove-Item -LiteralPath $ZipPath -Force -ErrorAction SilentlyContinue
        throw
    }
    if (-not (Test-ZipMagic -Path $ZipPath)) {
        Remove-Item -LiteralPath $ZipPath -Force -ErrorAction SilentlyContinue
        throw ("tar が ZIP 形式でないファイルを作りました (削除しました): {0}" -f $ZipPath)
    }
}

function Test-ZipContents {
    param(
        [string]$ZipPath,
        [int]$ExpectedDllCount
    )

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [System.IO.Compression.ZipFile]::OpenRead($ZipPath)
    try {
        if ($zip.Entries.Count -eq 0) {
            throw 'ZIP が空です (中央ディレクトリ読取失敗の可能性)'
        }

        $names = @{}
        foreach ($e in $zip.Entries) { $names[$e.FullName] = $true }

        if (-not $names.ContainsKey('FlashBeat.exe')) { throw 'ZIP 内に FlashBeat.exe がない' }
        if (-not ($names.Keys | Where-Object { $_ -like 'FlashBeat_Data/*' })) {
            throw 'ZIP 内に FlashBeat_Data/* がない'
        }
        if (-not $names.ContainsKey('UnityPlayer.dll')) { throw 'ZIP 内に UnityPlayer.dll がない' }
        if (-not $names.ContainsKey('version.json')) { throw 'ZIP 内に version.json がない' }

        $dllCount = ($zip.Entries | Where-Object {
            $_.FullName -like 'FlashBeat_Data/Managed/*.dll'
        }).Count
        if ($dllCount -ne $ExpectedDllCount) {
            throw "Managed DLL 数が不一致: zip=$dllCount source=$ExpectedDllCount"
        }

        # 全エントリが展開できること (壊れたエントリの検出)。
        # Length / CompressedLength は ZipArchiveEntry では常に 0 以上なので、
        # 値を見る検査では何も検出できない。実際に読み出す。
        foreach ($e in $zip.Entries) {
            $s = $e.Open()
            try {
                $buf = New-Object byte[] 1
                [void]$s.Read($buf, 0, 1)
            }
            finally { $s.Dispose() }
        }

        # FlashBeat.exe が PE 実行ファイル (MZ ヘッダ) であること。
        $exeEntry = $zip.Entries | Where-Object { $_.FullName -eq 'FlashBeat.exe' } | Select-Object -First 1
        $s = $exeEntry.Open()
        try {
            $mz = New-Object byte[] 2
            [void]$s.Read($mz, 0, 2)
            if ($mz[0] -ne 0x4D -or $mz[1] -ne 0x5A) {
                throw 'ZIP 内の FlashBeat.exe が PE 実行ファイルではありません (MZ ヘッダがありません)'
            }
        }
        finally { $s.Dispose() }

        [pscustomobject]@{
            TotalEntries = $zip.Entries.Count
            ManagedDlls  = $dllCount
        }
    }
    finally {
        $zip.Dispose()
    }
}

function Write-Sha256Sidecar {
    param([string]$ZipPath)

    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        $stream = [System.IO.File]::OpenRead($ZipPath)
        try {
            $hashBytes = $sha.ComputeHash($stream)
        }
        finally {
            $stream.Dispose()
        }
    }
    finally {
        $sha.Dispose()
    }
    $hash = ([System.BitConverter]::ToString($hashBytes)).Replace('-', '').ToLowerInvariant()
    $name = [System.IO.Path]::GetFileName($ZipPath)
    $sidecar = "$ZipPath.sha256"
    # 標準形式: "<hash>  <filename>" (sha256sum -c 互換)
    [System.IO.File]::WriteAllText($sidecar, "$hash  $name`n", (New-Object System.Text.UTF8Encoding($false)))
    return $hash
}

function ConvertTo-UtcDate {
    param([string]$Text)

    # ISO 8601 を UTC に正規化する (ロケール非依存)。
    return [DateTimeOffset]::Parse(
        $Text,
        [Globalization.CultureInfo]::InvariantCulture,
        [Globalization.DateTimeStyles]::AssumeUniversal -bor [Globalization.DateTimeStyles]::AdjustToUniversal
    ).UtcDateTime
}

function Resolve-CommitDateUtc {
    param(
        [string]$CommitSha,
        [string]$Explicit
    )

    # version.json の createdAt は「パッケージ実行時刻」ではなく「ビルド元 commit の日時」にする。
    # 実行時刻を使うと、同じビルド出力から再パッケージするたびに ZIP の SHA-256 が変わる
    # (version.json の内容と mtime が変わるため) ので、再現性検証が成立しない。
    if (-not [string]::IsNullOrWhiteSpace($Explicit)) {
        try { return (ConvertTo-UtcDate -Text $Explicit.Trim()) }
        catch {
            throw ("-CommitDate の書式が不正です (ISO 8601 を指定してください。例: 2026-11-09T19:00:00+09:00): {0}" -f $Explicit)
        }
    }

    # 明示されなければ git からビルド元 commit の committer date を取る。
    $roots = @()
    if ($PSScriptRoot) { $roots += (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) }
    $roots += (Get-Location).Path
    $seen = @{}
    foreach ($root in $roots) {
        if ([string]::IsNullOrWhiteSpace($root) -or $seen.ContainsKey($root)) { continue }
        $seen[$root] = $true
        $raw = @()
        $code = 1
        try {
            $raw = @(& git -C $root show -s --format=%cI $CommitSha 2>$null)
            $code = $LASTEXITCODE
        }
        catch { $code = 1 }
        if ($code -eq 0 -and $raw.Count -gt 0 -and -not [string]::IsNullOrWhiteSpace($raw[0])) {
            try { return (ConvertTo-UtcDate -Text $raw[0].Trim()) }
            catch { throw ("git が返した commit 日時の書式が不正です: {0}" -f $raw[0]) }
        }
    }

    throw ("ビルド元 commit ({0}) の日時を git から取得できませんでした。-CommitDate (ISO 8601) を明示してください。" -f $CommitSha)
}

# --- main ---

# Version 内の短縮 SHA と CommitSha の整合を検証 (タグ命名契約: v<Version> の SHA 部は CommitSha の先頭)
$shortSha = ($Version -split '-win64-')[1]
if (-not $CommitSha.StartsWith($shortSha)) {
    throw "Version の SHA 部 ($shortSha) が CommitSha ($CommitSha) の先頭と一致しません"
}

$SourceDir = (Resolve-Path -LiteralPath $SourceDir).Path
Assert-Win64BuildLayout -Dir $SourceDir
Assert-NoLooseMedia -Dir $SourceDir
Assert-MediaPolicy -Dir $SourceDir

if (-not (Test-Path -LiteralPath $OutDir -PathType Container)) {
    New-Item -ItemType Directory -Path $OutDir -Force | Out-Null
}
$OutDir = (Resolve-Path -LiteralPath $OutDir).Path

$expectedDlls = Get-ManagedDllCount -Dir $SourceDir
$zipPath = Join-Path $OutDir "FlashBeat-$Version.zip"
if (Test-Path -LiteralPath $zipPath) {
    throw "出力先に既に ZIP が存在します: $zipPath"
}

# version.json をステージングに作成 (#36 UpdateChecker / Update.ps1 が読む契約)
# createdAt は「実行時刻」ではなく「ビルド元 commit の日時」にする (再現性のため)。
$commitDateUtc = Resolve-CommitDateUtc -CommitSha $CommitSha -Explicit $CommitDate
$createdAt = $commitDateUtc.ToString('yyyy-MM-ddTHH:mm:ss', [Globalization.CultureInfo]::InvariantCulture) + 'Z'

$stagingDir = Join-Path $OutDir ([System.Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $stagingDir -Force | Out-Null
$versionJsonPath = Join-Path $stagingDir 'version.json'
$versionJson = [ordered]@{
    version     = ($Version -split '-win64-')[0]
    buildSha    = $CommitSha
    tag         = "v$Version"
    platform    = 'win64'
    mediaPolicy = $MediaPolicy
    createdAt   = $createdAt
}
[System.IO.File]::WriteAllText(
    $versionJsonPath,
    ([System.Text.UTF8Encoding]::new($false)).GetString([System.Text.Encoding]::UTF8.GetBytes(($versionJson | ConvertTo-Json))),
    (New-Object System.Text.UTF8Encoding($false)))

# version.json 自身の mtime も commit 日時に固定する。
# tar は各エントリの mtime を ZIP に埋め込むので、ここが実行時刻のままだと
# 2 段構えでも ZIP がバイト単位で再現しない (createdAt を固定しても mtime が違う)。
[System.IO.File]::SetLastWriteTimeUtc($versionJsonPath, $commitDateUtc)

try {
    Write-Host "[1/4] ZIP 作成中: $zipPath"
    $tarPath = Resolve-TarCommand
    $tarFlags = Resolve-TarZipFlags -TarPath $tarPath
    $useTwoHop = Test-TarTwoHopSupport -TarPath $tarPath -ZipFlags $tarFlags
    if ($useTwoHop) {
        Write-Host ("      tar: {0} ({1}) / 2 段構え (ustar → zip): 再現します" -f $tarPath, ($tarFlags -join ' '))
    }
    else {
        Write-Warning ("この tar は 2 段構え (--format ustar + @archive) に非対応です。" +
                       "ZIP はバイト単位で再現しません (atime / ctime が記録されるため): {0}" -f $tarPath)
        Write-Host ("      tar: {0} ({1})" -f $tarPath, ($tarFlags -join ' '))
    }
    $intermediateTarPath = Join-Path $stagingDir 'intermediate.tar'
    New-BuildZip -Dir $SourceDir -VersionJsonPath $versionJsonPath -ZipPath $zipPath `
        -TarPath $tarPath -TarFlags $tarFlags -IntermediateTarPath $intermediateTarPath -UseTwoHop $useTwoHop

    Write-Host '[2/4] ZIP 内容検査中'
    $inspect = Test-ZipContents -ZipPath $zipPath -ExpectedDllCount $expectedDlls

    Write-Host '[3/4] SHA-256 計算中'
    $hash = Write-Sha256Sidecar -ZipPath $zipPath

    $sizeBytes = (Get-Item -LiteralPath $zipPath).Length
    Write-Host ("[4/4] 完了: entries={0} managedDlls={1} mediaPolicy={2} size={3:N0} bytes" -f `
            $inspect.TotalEntries, $inspect.ManagedDlls, $MediaPolicy, $sizeBytes)
    Write-Host "SHA-256: $hash"
    Write-Host "Sidecar: $zipPath.sha256"
    Write-Host "createdAt (ビルド元 commit の日時, UTC): $createdAt"
    Write-Host "タグ (GitHub Release): v$Version"

    [pscustomobject]@{
        Zip          = $zipPath
        Sha256       = $hash
        SizeBytes    = $sizeBytes
        TotalEntries = $inspect.TotalEntries
        ManagedDlls  = $inspect.ManagedDlls
        MediaPolicy  = $MediaPolicy
        Tag          = "v$Version"
        SourceDir    = $SourceDir
        CreatedAt    = $createdAt
    }
}
catch {
    # 失敗したとき zip / sidecar を残さない (release 手順は *.zip を glob で拾うため)。
    Remove-Item -LiteralPath $zipPath -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath "$zipPath.sha256" -Force -ErrorAction SilentlyContinue
    throw
}
finally {
    Remove-Item -LiteralPath $stagingDir -Recurse -Force -ErrorAction SilentlyContinue
}
