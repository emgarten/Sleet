<#
.SYNOPSIS
Stops the local test environment and removes its containers and data.

.EXAMPLE
./local-env/stop.ps1
#>

$ErrorActionPreference = "Stop"
$ComposeFile = Join-Path $PSScriptRoot "docker-compose.yml"

if (-not (Get-Command docker -CommandType Application -ErrorAction Ignore)) {
    Write-Host "Docker is required for the local test environment. Install it from https://docs.docker.com/get-started/get-docker/" -ForegroundColor Red
    exit 1
}

# --volumes removes the anonymous data volumes that the images declare
$arguments = @("compose", "-f", $ComposeFile, "down", "--volumes", "--remove-orphans")
Write-Host "[Exec] docker $arguments" -ForegroundColor Cyan
& docker @arguments
exit $LASTEXITCODE
