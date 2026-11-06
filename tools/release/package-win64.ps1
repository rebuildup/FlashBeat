#requires -Version 5.1
<#
.SYNOPSIS
    検証済み Win64 Unity ビルドフォルダ一式をバージョン付き ZIP + SHA-256 sidecar にパッケージする (Issue #35)。

.DESCRIPTION
    - Unity 出力フォルダを丸ごと ZIP 化する (FlashBeat.exe + FlashBeat_Data + UnityPlayer.dll 等)。
      exe 単体の ZIP は作らない。
    - FlashBeat_BurstDebugInformation_DoNotShip は除外する (Unity の指示: ship しない)。
    - ZIP 作成後、内容の最低限の検査 (exe / Data / Managed DLL の存在と数、arch integrity) を行う。
    - ZIP の隣に `<zip名>.sha256` (SHA-256 sidecar) を書き出す。
    - 秘密情報 (トークン・署名付き URL・credential) は ZIP に一切含めない。入力はビルドフォルダのみ。

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File tools/release/package-win64.ps1 `
        -SourceDir C:\path\to\Builds\Windows -OutDir D:\dist -Version 0.1.0-win64-743c88d
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$SourceDir,

    [Parameter(Mandatory = $true)]
    [string]$OutDir,

    [Parameter(Mandatory = $true)]
    [string]$Version
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

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

function New-BuildZip {
    param(
        [string]$Dir,
        [string]$ZipPath
    )

    # Windows 同梱の bsdtar (libarchive) を使用。拡張子 .zip で自動的に zip 形式になる。
    # -C でカーソルを移動し、トップレベルのエントリをそのまま ZIP ルートに置く。
    $entries = Get-ChildItem -LiteralPath $Dir -Force |
        Where-Object { $_.Name -ne 'FlashBeat_BurstDebugInformation_DoNotShip' } |
        ForEach-Object { $_.Name }

    if ($entries.Count -lt 5) {
        throw "ビルドフォルダのトップレベル エントリが少なすぎます: $($entries.Count)"
    }

    & tar.exe -a -cf $ZipPath -C $Dir @entries
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
    param(
        [string]$ZipPath
    )

    $hash = (Get-FileHash -LiteralPath $ZipPath -Algorithm SHA256).Hash.ToLowerInvariant()
    $name = [System.IO.Path]::GetFileName($ZipPath)
    $sidecar = "$ZipPath.sha256"
    # 標準形式: "<hash>  <filename>" (sha256sum -c 互換)
    [System.IO.File]::WriteAllText($sidecar, "$hash  $name`n", (New-Object System.Text.UTF8Encoding($false)))
    return $hash
}

# --- main ---

$SourceDir = (Resolve-Path -LiteralPath $SourceDir).Path
Assert-Win64BuildLayout -Dir $SourceDir

if (-not (Test-Path -LiteralPath $OutDir -PathType Container)) {
    New-Item -ItemType Directory -Path $OutDir -Force | Out-Null
}
$OutDir = (Resolve-Path -LiteralPath $OutDir).Path

$expectedDlls = Get-ManagedDllCount -Dir $SourceDir
$zipPath = Join-Path $OutDir "FlashBeat-$Version.zip"
if (Test-Path -LiteralPath $zipPath) {
    throw "出力先に既に ZIP が存在します: $zipPath"
}

Write-Host "[1/4] ZIP 作成中: $zipPath"
New-BuildZip -Dir $SourceDir -ZipPath $zipPath

Write-Host '[2/4] ZIP 内容検査中'
$inspect = Test-ZipContents -ZipPath $zipPath -ExpectedDllCount $expectedDlls

Write-Host '[3/4] SHA-256 計算中'
$hash = Write-Sha256Sidecar -ZipPath $zipPath

$sizeBytes = (Get-Item -LiteralPath $zipPath).Length
Write-Host ("[4/4] 完了: entries={0} managedDlls={1} size={2:N0} bytes" -f `
        $inspect.TotalEntries, $inspect.ManagedDlls, $sizeBytes)
Write-Host "SHA-256: $hash"
Write-Host "Sidecar: $zipPath.sha256"

[pscustomobject]@{
    Zip          = $zipPath
    Sha256       = $hash
    SizeBytes    = $sizeBytes
    TotalEntries = $inspect.TotalEntries
    ManagedDlls  = $inspect.ManagedDlls
    SourceDir    = $SourceDir
}
