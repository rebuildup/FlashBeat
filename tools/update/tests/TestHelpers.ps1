#Requires -Version 5.1
<#
.SYNOPSIS
    Update.ps1 のテストで共有するアサーション関数 (Pester 不使用・依存ゼロ)。

.DESCRIPTION
    tools/update/tests/ 配下の検証スクリプトから dot-source して使う。
    成功件数 / 失敗件数を数え、Write-TestSummary で終了コードを返す。
#>
$ErrorActionPreference = 'Continue'

$script:Passed = 0
$script:Failed = 0

function Assert-True {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][bool]$Condition,
        [string]$Detail = ''
    )
    if ($Condition) {
        $script:Passed++
        Write-Host ('[PASS] {0}' -f $Name)
    } else {
        $script:Failed++
        $suffix = ''
        if ($Detail) { $suffix = ' — ' + $Detail }
        Write-Host ('[FAIL] {0}{1}' -f $Name, $suffix)
    }
}

function Assert-Equal {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        $Expected,
        $Actual
    )
    $ok = ([string]$Expected -eq [string]$Actual)
    Assert-True -Name $Name -Condition $ok -Detail ('expected=[{0}] actual=[{1}]' -f $Expected, $Actual)
}

function Write-TestSummary {
    param([string]$Title = 'テスト')
    Write-Host ''
    Write-Host ('=== {0}: PASS {1} / FAIL {2} ===' -f $Title, $script:Passed, $script:Failed)
    if ($script:Failed -gt 0) {
        exit 1
    }
    exit 0
}
