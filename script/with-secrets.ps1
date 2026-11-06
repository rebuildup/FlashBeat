#!/usr/bin/env pwsh
# script/with-secrets — PowerShell mirror of the bash wrapper.
#
#   pwsh script/with-secrets.ps1 command [args...]
#
# Runs the command with the FlashBeat Infisical `dev` secret set injected
# (process-scoped). Non-secret instance metadata comes from
# config/infisical.sh, the tracked project binding must match it, and only
# infisical.exe is accepted so argument passing stays faithful.
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$metadata = Get-Content -Raw (Join-Path $repoRoot 'config' 'infisical.sh')
$projectId = [regex]::Match($metadata, 'INFISICAL_PROJECT_ID="([^"]+)"').Groups[1].Value
$environment = [regex]::Match($metadata, 'INFISICAL_ENVIRONMENT="([^"]+)"').Groups[1].Value
$apiUrl = [regex]::Match($metadata, 'INFISICAL_API_URL="([^"]+)"').Groups[1].Value
if (-not $projectId -or -not $environment -or -not $apiUrl) {
  [Console]::Error.WriteLine('config/infisical.sh does not declare INFISICAL_PROJECT_ID / INFISICAL_ENVIRONMENT / INFISICAL_API_URL')
  exit 1
}

if ($args.Count -lt 1) {
  [Console]::Error.WriteLine("usage: $($MyInvocation.MyCommand.Name) command [args...]")
  exit 2
}

$binding = Join-Path $repoRoot '.infisical.json'
if (-not (Test-Path $binding)) {
  [Console]::Error.WriteLine("missing tracked Infisical project binding: $binding")
  exit 1
}

try {
  $bindingObject = Get-Content -Raw $binding | ConvertFrom-Json
} catch {
  [Console]::Error.WriteLine("invalid JSON in Infisical binding $binding")
  exit 1
}
if ($bindingObject.workspaceId -ne $projectId) {
  [Console]::Error.WriteLine("Infisical binding workspaceId does not match expected self-host project $projectId")
  exit 1
}

$infisicalCommand = Get-Command infisical -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $infisicalCommand) {
  [Console]::Error.WriteLine('missing required command: infisical')
  exit 1
}
$infisicalPath = $infisicalCommand.Source
if ([IO.Path]::GetExtension($infisicalPath) -ne '.exe') {
  [Console]::Error.WriteLine("infisical must resolve to infisical.exe; refusing '$infisicalPath' (.cmd/.bat shims are not supported)")
  exit 1
}

# Pin the self-hosted endpoint explicitly so the CLI never falls back to the public Cloud API.
$env:INFISICAL_API_URL = $apiUrl

$PSNativeCommandArgumentPassing = 'Standard'
& $infisicalPath run "--projectId=$projectId" "--env=$environment" "--project-config-dir=$repoRoot" -- @args
exit $LASTEXITCODE
