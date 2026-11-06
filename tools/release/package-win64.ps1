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
    [long]$MaxResourcesBytes = 300MB
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script:MediaExtensions = @('.wav', '.mp3', '.mp4', '.mov', '.ogg', '.m4a', '.webm', '.avi')

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

    $managed = Join-Path $Dir 'FlashBeat_Data\Managed'
    if (-not (Test-Path -LiteralPath $managed -PathType Container)) {
        throw "FlashBeat_Data\Managed が見つかりません: $managed"
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

    $packed = Join-Path $Dir 'FlashBeat_Data\resources.resource'
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
        [string]$ZipPath
    )

    # Windows 同梱の bsdtar (libarchive) を使用。拡張子 .zip で自動的に zip 形式になる。
    # -C でカーソルを移動し、トップレベルのエントリをそのまま ZIP ルートに置く。
    # version.json はステージングから ZIP ルートへ追加する。
    $entries = Get-ChildItem -LiteralPath $Dir -Force |
        Where-Object { $_.Name -ne 'FlashBeat_BurstDebugInformation_DoNotShip' } |
        ForEach-Object { $_.Name }

    if ($entries.Count -lt 5) {
        throw "ビルドフォルダのトップレベル エントリが少なすぎます: $($entries.Count)"
    }

    $staging = [System.IO.Path]::GetDirectoryName($VersionJsonPath)
    & tar.exe -a -cf $ZipPath -C $Dir @entries -C $staging 'version.json'
    if ($LASTEXITCODE -ne 0) {
        throw "tar.exe が失敗しました (exit=$LASTEXITCODE): $ZipPath"
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

        # arch integrity: 全エントリの圧縮/非圧縮サイズが読めることを確認
        foreach ($e in $zip.Entries) {
            if ($e.Length -lt 0 -or $e.CompressedLength -lt 0) {
                throw "壊れたエントリ: $($e.FullName)"
            }
        }

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
$stagingDir = Join-Path $OutDir ([System.Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $stagingDir -Force | Out-Null
$versionJsonPath = Join-Path $stagingDir 'version.json'
$versionJson = [ordered]@{
    version     = ($Version -split '-win64-')[0]
    buildSha    = $CommitSha
    tag         = "v$Version"
    mediaPolicy = $MediaPolicy
    createdAt   = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
}
[System.IO.File]::WriteAllText(
    $versionJsonPath,
    ([System.Text.UTF8Encoding]::new($false)).GetString([System.Text.Encoding]::UTF8.GetBytes(($versionJson | ConvertTo-Json))),
    (New-Object System.Text.UTF8Encoding($false)))

try {
    Write-Host "[1/4] ZIP 作成中: $zipPath"
    New-BuildZip -Dir $SourceDir -VersionJsonPath $versionJsonPath -ZipPath $zipPath

    Write-Host '[2/4] ZIP 内容検査中'
    $inspect = Test-ZipContents -ZipPath $zipPath -ExpectedDllCount $expectedDlls

    Write-Host '[3/4] SHA-256 計算中'
    $hash = Write-Sha256Sidecar -ZipPath $zipPath

    $sizeBytes = (Get-Item -LiteralPath $zipPath).Length
    Write-Host ("[4/4] 完了: entries={0} managedDlls={1} mediaPolicy={2} size={3:N0} bytes" -f `
            $inspect.TotalEntries, $inspect.ManagedDlls, $MediaPolicy, $sizeBytes)
    Write-Host "SHA-256: $hash"
    Write-Host "Sidecar: $zipPath.sha256"
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
    }
}
finally {
    Remove-Item -LiteralPath $stagingDir -Recurse -Force -ErrorAction SilentlyContinue
}
