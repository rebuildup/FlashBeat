<#
    .SYNOPSIS
        Redact (and optionally check) a Unity player log before it is published as release evidence.

        The player log contains transient YouTube media URLs (signed googlevideo links) and video ids.
        Those are not credentials, but they are signed, time limited URLs - they must not end up in a
        published artifact, a Git commit or a GitHub Release.

        What is replaced:
          * googlevideo.com media URLs          -> <REDACTED-YOUTUBE-STREAM-URL>
          * every other http(s) URL             -> <REDACTED-URL>   (unless the host is allow-listed)
          * "videoId=<11 chars>"                -> "videoId=<REDACTED-VIDEO-ID>"
          * "[youtube] <11 chars>:" (yt-dlp stderr) -> "[youtube] <REDACTED-VIDEO-ID>:"

    .EXAMPLE
        powershell -NoProfile -ExecutionPolicy Bypass -File release/check/redact-log.ps1 `
            -InPath 'C:\tmp\fb_run\Player.log' -OutPath 'C:\tmp\fb_run\Player.redacted.log'

        powershell -NoProfile -ExecutionPolicy Bypass -File release/check/redact-log.ps1 `
            -InPath 'C:\tmp\fb_run\Player.redacted.log' -Check
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$InPath,
    [string]$OutPath = '',
    # Hosts that are kept as-is (public, unsigned, stable).
    [string[]]$AllowHost = @('www.youtube.com', 'youtu.be', 'yt.chocolatemoo53.com', 'localhost', '127.0.0.1'),
    # Check mode: do not write, only report whether something still has to be redacted.
    [switch]$Check
)

$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath $InPath)) { throw "log not found: $InPath" }

$original = [IO.File]::ReadAllText($InPath)

$counts = [ordered]@{
    youtubeStreamUrl = 0
    otherUrl         = 0
    videoId          = 0
    youtubeStderrId  = 0
}

$text = [regex]::Replace($original, 'https?://[^\s''"<>]+', {
    param($m)
    $url = $m.Value
    if ($url -match 'googlevideo\.com') {
        $counts.youtubeStreamUrl++
        return '<REDACTED-YOUTUBE-STREAM-URL>'
    }
    $urlHost = ''
    if ($url -match '^https?://([^/:]+)') { $urlHost = $Matches[1].ToLower() }
    if ($AllowHost -contains $urlHost) { return $url }
    $counts.otherUrl++
    return '<REDACTED-URL>'
})

$text = [regex]::Replace($text, 'videoId=[A-Za-z0-9_-]{6,}', {
    param($m)
    $counts.videoId++
    return 'videoId=<REDACTED-VIDEO-ID>'
})

$text = [regex]::Replace($text, '\[youtube\]\s+[A-Za-z0-9_-]{6,}:', {
    param($m)
    $counts.youtubeStderrId++
    return '[youtube] <REDACTED-VIDEO-ID>:'
})

if ($Check) {
    # analyse the ORIGINAL file: the redacted copy must not contain anything that needs masking
    $googlevideo = ([regex]::Matches($original, 'googlevideo\.com').Count)
    $videoIds = ([regex]::Matches($original, 'videoId=[A-Za-z0-9_-]{6,}').Count)
    $stderrIds = ([regex]::Matches($original, '\[youtube\]\s+[A-Za-z0-9_-]{6,}:').Count)
    $urls = [regex]::Matches($original, 'https?://[^\s''"<>]+')
    $unexpectedUrls = 0
    foreach ($m in $urls) {
        $urlHost = ''
        if ($m.Value -match '^https?://([^/:]+)') { $urlHost = $Matches[1].ToLower() }
        if ($AllowHost -notcontains $urlHost) { $unexpectedUrls++ }
    }
    Write-Host ("signed media urls:   {0}" -f $googlevideo)
    Write-Host ("unexpected urls:      {0}" -f $unexpectedUrls)
    Write-Host ("video ids:            {0}" -f $videoIds)
    Write-Host ("yt-dlp stderr ids:    {0}" -f $stderrIds)
    if ($googlevideo -gt 0 -or $unexpectedUrls -gt 0 -or $videoIds -gt 0 -or $stderrIds -gt 0) {
        Write-Host 'RESULT: FAIL - the log still contains URLs or video ids that must be redacted'
        exit 1
    }
    Write-Host 'RESULT: OK - nothing left to redact'
    exit 0
}

if (-not $OutPath) { $OutPath = [IO.Path]::ChangeExtension($InPath, '.redacted.log') }
[IO.File]::WriteAllText($OutPath, $text, (New-Object Text.UTF8Encoding($false)))

Write-Host ("youtubeStreamUrl redacted: {0}" -f $counts.youtubeStreamUrl)
Write-Host ("otherUrl redacted:         {0}" -f $counts.otherUrl)
Write-Host ("videoId redacted:          {0}" -f $counts.videoId)
Write-Host ("yt-dlp stderr id redacted: {0}" -f $counts.youtubeStderrId)
Write-Host ("written: {0}" -f $OutPath)
