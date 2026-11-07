#Requires -Version 5.1
<#
.SYNOPSIS
    テスト用に Update.ps1 の本体 (Invoke-FlashBeatUpdateMain) を実行するラッパー。

.DESCRIPTION
    Update.ps1 の「実行中 FlashBeat プロセスがあれば中止する」判定は
    プロセス名 (FlashBeat / UnityPlayer / UnityCrashHandler64) で行うため、
    同じマシンで別の展示ビルドが起動しているとテストが exit code 6 で止まる。
    そこで **Get-Process だけ**をスタブ化して、 Update.ps1 の本体はそのまま実行する。

    dot-source で Update.ps1 を取り込み (パラメータも指定する)、
    続けて本体関数 Invoke-FlashBeatUpdateMain を呼ぶ。

    終了コードは Update.ps1 本体そのまま (0 成功 / 1 前提不備 / 3 ダイジェスト不一致 / ...)。

.EXAMPLE
    pwsh -NoProfile -File tools/update/tests/Invoke-UpdateForTest.ps1 `
        -UpdateScript tools/update/Update.ps1 `
        -TargetInstallDir /tmp/x/FlashBeat -TargetZipPath /tmp/x/rel.zip `
        -TargetSha256 abc... -TargetLogPath /tmp/x/update.log
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$UpdateScript,
    [Parameter(Mandatory = $true)][string]$TargetInstallDir,
    [Parameter(Mandatory = $true)][string]$TargetZipPath,
    [Parameter(Mandatory = $true)][string]$TargetSha256,
    [Parameter(Mandatory = $true)][string]$TargetLogPath
)

$ErrorActionPreference = 'Continue'

# 実行中判定だけをスタブ化する (他のプロセス照会には影響させない)。
function Get-Process {
    param(
        [string]$Name,
        [int]$Id,
        $ErrorAction
    )
    return @()
}

# Update.ps1 の関数とパラメータを取り込む (本体は dot-source 時には走らない)。
. $UpdateScript -InstallDir $TargetInstallDir -ZipPath $TargetZipPath -ExpectedSha256 $TargetSha256 -LogPath $TargetLogPath

Invoke-FlashBeatUpdateMain
