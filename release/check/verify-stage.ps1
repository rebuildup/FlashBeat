<#
    .SYNOPSIS
        Release gate check for the FlashBeat Win64 exhibition stages (#22).

        Reads release/check/stage-manifest.json and verifies, for every stage that owns an
        artifact on this machine:
          * the artifact directory exists and its content fingerprint still matches the manifest
          * the evidence files that are reachable on this machine still match their SHA-256
          * the human readable index (docs/evidence/index.md) mentions every fingerprint

        The script is read-only: it never builds, copies, deletes or modifies an artifact.
        Stages without an artifact are reported as UNPROVEN - they are not a failure of this
        script, they are an open item of the release gate.

    .EXAMPLE
        powershell -NoProfile -ExecutionPolicy Bypass -File release/check/verify-stage.ps1
        powershell -NoProfile -ExecutionPolicy Bypass -File release/check/verify-stage.ps1 -Stage S0
#>
[CmdletBinding()]
param(
    [Alias('Stage')][string[]]$StageFilter = @(),
    [string]$ManifestPath = '',
    [string]$IndexPath = ''
)

$ErrorActionPreference = 'Stop'

$scriptRoot = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path $MyInvocation.MyCommand.Path -Parent }
$repoRoot = Split-Path (Split-Path $scriptRoot -Parent) -Parent
if (-not $ManifestPath) { $ManifestPath = Join-Path $scriptRoot 'stage-manifest.json' }
if (-not $IndexPath) { $IndexPath = Join-Path $repoRoot 'docs/evidence/index.md' }

function Get-Sha256File([string]$path) {
    $sha = [Security.Cryptography.SHA256]::Create()
    $fs = [IO.File]::OpenRead($path)
    try { return ([BitConverter]::ToString($sha.ComputeHash($fs))).Replace('-', '').ToLower() }
    finally { $fs.Dispose(); $sha.Dispose() }
}

function Get-FolderFingerprint([string]$dir) {
    $files = Get-ChildItem -LiteralPath $dir -Recurse -File | Sort-Object FullName
    $lines = foreach ($f in $files) {
        $rel = $f.FullName.Substring($dir.Length).TrimStart('\', '/') -replace '\\', '/'
        '{0}  {1}' -f (Get-Sha256File $f.FullName), $rel
    }
    $bytes = [Text.Encoding]::UTF8.GetBytes(($lines -join "`n"))
    $sha = [Security.Cryptography.SHA256]::Create()
    try { return ([BitConverter]::ToString($sha.ComputeHash($bytes))).Replace('-', '').ToLower() }
    finally { $sha.Dispose() }
}

if (-not (Test-Path -LiteralPath $ManifestPath)) { throw "manifest not found: $ManifestPath" }
$manifest = Get-Content -LiteralPath $ManifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
$indexText = if (Test-Path -LiteralPath $IndexPath) { Get-Content -LiteralPath $IndexPath -Raw -Encoding UTF8 } else { '' }

$failures = 0
$unproven = 0

foreach ($st in $manifest.stages) {
    if ($StageFilter.Count -gt 0 -and ($StageFilter -notcontains $st.id)) { continue }

    Write-Host ''
    Write-Host ("=== {0} [{1}] {2}" -f $st.id, $st.status, $st.title)
    Write-Host ("    source: branch {0} commit {1} PR {2}" -f $st.source.branch, $st.source.commit, $st.source.pullRequest)

    if ($st.artifact.fingerprintSha256 -eq $null) {
        Write-Host '    [UNPROVEN] no artifact recorded - this stage is still open'
        $unproven++
        continue
    }

    # pick the path that exists on this machine
    $dir = $null
    foreach ($candidate in @($st.artifact.archivePath, $st.artifact.worktreeBuildPath)) {
        if ($candidate -and (Test-Path -LiteralPath $candidate)) { $dir = $candidate; break }
    }
    if (-not $dir) {
        Write-Host '    [FAIL] artifact directory not found on this machine'
        Write-Host ("           archive:    {0}" -f $st.artifact.archivePath)
        Write-Host ("           worktree:   {0}" -f $st.artifact.worktreeBuildPath)
        $failures++
        continue
    }

    Write-Host ("    artifact:  {0}" -f $dir)
    $fp = Get-FolderFingerprint $dir
    if ($fp -eq $st.artifact.fingerprintSha256) {
        Write-Host ("    [OK] folder fingerprint matches: {0}" -f $fp)
    } else {
        Write-Host ("    [FAIL] folder fingerprint mismatch")
        Write-Host ("           expected: {0}" -f $st.artifact.fingerprintSha256)
        Write-Host ("           actual:   {0}" -f $fp)
        $failures++
    }

    $fileCount = (Get-ChildItem -LiteralPath $dir -Recurse -File).Count
    if ($st.artifact.fileCount -eq $null -or $fileCount -eq $st.artifact.fileCount) {
        Write-Host ("    [OK] file count: {0}" -f $fileCount)
    } else {
        Write-Host ("    [FAIL] file count mismatch: expected {0}, actual {1}" -f $st.artifact.fileCount, $fileCount)
        $failures++
    }

    $exe = Join-Path $dir 'FlashBeat.exe'
    if (Test-Path -LiteralPath $exe) {
        $exeHash = Get-Sha256File $exe
        if ($exeHash -eq $st.artifact.exeSha256) {
            Write-Host ("    [OK] FlashBeat.exe sha256: {0}" -f $exeHash)
        } else {
            Write-Host ("    [FAIL] FlashBeat.exe sha256 mismatch: expected {0}, actual {1}" -f $st.artifact.exeSha256, $exeHash)
            $failures++
        }
    } else {
        Write-Host '    [FAIL] FlashBeat.exe is missing in the artifact directory'
        $failures++
    }

    if ($indexText -and ($indexText -notmatch [regex]::Escape($st.artifact.fingerprintSha256))) {
        Write-Host ("    [FAIL] {0} does not mention fingerprint {1}" -f $IndexPath, $st.artifact.fingerprintSha256)
        $failures++
    }

    foreach ($ev in @($st.playEvidence.evidenceFiles)) {
        if (-not $ev.localPath) { continue }
        if (-not (Test-Path -LiteralPath $ev.localPath)) {
            Write-Host ("    [SKIP] evidence not on this machine: {0}" -f $ev.gitRef)
            continue
        }
        $h = Get-Sha256File $ev.localPath
        if ($h -eq $ev.sha256) {
            Write-Host ("    [OK] evidence {0}" -f $ev.gitRef)
        } else {
            Write-Host ("    [FAIL] evidence hash mismatch {0}" -f $ev.gitRef)
            Write-Host ("           expected: {0}" -f $ev.sha256)
            Write-Host ("           actual:   {0}" -f $h)
            $failures++
        }
    }

    if ($st.playEvidence.state -ne 'complete') {
        Write-Host '    [UNPROVEN] remaining gaps of this stage:'
        foreach ($gap in @($st.playEvidence.unproven)) { Write-Host ("             - {0}" -f $gap) }
        $unproven++
    }
}

Write-Host ''
Write-Host ('result: {0} failure(s), {1} stage(s) still unproven' -f $failures, $unproven)
if ($failures -gt 0) { exit 1 }
exit 0
