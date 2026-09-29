<#
.SYNOPSIS
Starts the local test environment with Docker and waits until it's ready.

.PARAMETER Service
The services to start from docker-compose.yml. The default is all of them.

.EXAMPLE
./local-env/start.ps1

.EXAMPLE
./local-env/start.ps1 azurite
#>
param (
    [Parameter(Position = 0, ValueFromRemainingArguments = $true)]
    [string[]]$Service = @()
)

$ErrorActionPreference = "Stop"
$ComposeFile = Join-Path $PSScriptRoot "docker-compose.yml"

if (-not (Get-Command docker -CommandType Application -ErrorAction Ignore)) {
    Write-Host "Docker is required for the local test environment. Install it from https://docs.docker.com/get-started/get-docker/" -ForegroundColor Red
    exit 1
}

# Native commands write errors to stderr, which must not stop the script
$ErrorActionPreference = "Continue"
& docker info *> $null

if ($LASTEXITCODE -ne 0) {
    Write-Host "Docker isn't running. Start Docker Desktop or the Docker service, then try again." -ForegroundColor Red
    exit 1
}

$arguments = @("compose", "-f", $ComposeFile, "up", "--detach", "--wait", "--wait-timeout", "120") + $Service
Write-Host "[Exec] docker $arguments" -ForegroundColor Cyan
& docker @arguments

if ($LASTEXITCODE -ne 0) {
    & docker compose -f $ComposeFile logs @Service
    Write-Host "The local test environment didn't start, see the logs above." -ForegroundColor Red
    exit 1
}

& docker compose -f $ComposeFile ps @Service
exit 0
