<#
.SYNOPSIS
Runs the functional tests against the local storage emulators, or against Azure and Amazon S3 accounts.

.DESCRIPTION
The emulator targets start the local test environment in local-env with Docker, and stop it afterwards if it wasn't
already running. The cloud targets use the accounts in the environment variables listed below.

The test results are written to artifacts/TestResults/functional.

.PARAMETER Target
What to test:
  emulators  The emulator targets. This is the default and needs Docker.
  cloud      The cloud targets. They need the account environment variables.
  all        All targets.
  azurite    The Azure tests against Azurite.
  azure      The Azure tests against SLEET_TEST_ACCOUNT, an Azure Storage connection string.
  aws        The Amazon S3 tests against AWS_ACCESS_KEY_ID and AWS_SECRET_ACCESS_KEY.
             AWS_DEFAULT_REGION defaults to us-east-1.

.EXAMPLE
./functional-tests.ps1

.EXAMPLE
$env:SLEET_TEST_ACCOUNT = "<connection string>"
./functional-tests.ps1 -Target azure
#>
param (
    [Parameter(Position = 0)]
    [ValidateSet("emulators", "cloud", "all", "azurite", "azure", "aws")]
    [string[]]$Target = @("emulators")
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

$RepoRoot = $PSScriptRoot
$DotnetDir = Join-Path $RepoRoot ".dotnet"
$LocalEnvDir = Join-Path $RepoRoot "local-env"
$ComposeFile = Join-Path $LocalEnvDir "docker-compose.yml"
$ResultsDir = [IO.Path]::Combine($RepoRoot, "artifacts", "TestResults", "functional")
$IsWindowsOS = $env:OS -eq "Windows_NT"
$Configuration = "Release"

$AzureTests = "test/Sleet.Azure.Tests/Sleet.Azure.Tests.csproj"
$AmazonS3Tests = "test/Sleet.AmazonS3.Tests/Sleet.AmazonS3.Tests.csproj"

# Emulator targets list the local-env services they use and the env vars to set for them.
# Cloud targets list the env vars they need.
$Targets = [ordered]@{
    azurite = @{ Project = $AzureTests; Services = @("azurite"); Env = @{ SLEET_TEST_ACCOUNT = "UseDevelopmentStorage=true" }; Required = @() }
    azure   = @{ Project = $AzureTests; Services = @(); Env = @{}; Required = @("SLEET_TEST_ACCOUNT") }
    aws     = @{ Project = $AmazonS3Tests; Services = @(); Env = @{}; Required = @("AWS_ACCESS_KEY_ID", "AWS_SECRET_ACCESS_KEY") }
}

$Groups = @{
    emulators = @("azurite")
    cloud     = @("azure", "aws")
    all       = @($Targets.Keys)
}

Function Invoke-Exe {
    param(
        [string]$Exe,
        [string[]]$Arguments
    )

    Write-Host "[Exec] $Exe $Arguments" -ForegroundColor Cyan
    & $Exe @Arguments

    if ($LASTEXITCODE -ne 0) {
        throw "Command failed with exit code $($LASTEXITCODE): $Exe $Arguments"
    }
}

# True if dotnet has the SDK from global.json. The functional tests don't need other runtimes.
Function Test-Dotnet([string]$Dotnet) {
    if (-not $Dotnet -or -not (Test-Path $Dotnet)) {
        return $false
    }

    $ErrorActionPreference = "Continue"
    & $Dotnet --version *> $null
    return $LASTEXITCODE -eq 0
}

# Install the SDK from global.json to .dotnet
Function Install-Dotnet {
    New-Item -ItemType Directory -Force -Path $DotnetDir | Out-Null
    $installScript = Join-Path $DotnetDir "dotnet-install.ps1"
    Invoke-WebRequest https://dot.net/v1/dotnet-install.ps1 -OutFile $installScript -UseBasicParsing

    & $installScript -JsonFile (Join-Path $RepoRoot "global.json") -InstallDir $DotnetDir -NoPath
}

# True if Docker is running and none of the local-env services are
Function Test-LocalEnvStopped {
    if (-not (Get-Command docker -CommandType Application -ErrorAction Ignore)) {
        return $false
    }

    $ErrorActionPreference = "Continue"
    $running = & docker compose -f $ComposeFile ps --services --status running 2> $null
    return $LASTEXITCODE -eq 0 -and -not $running
}

$requested = foreach ($name in $Target) {
    if ($Groups.ContainsKey($name)) { $Groups[$name] } else { $name }
}

$selected = @($Targets.Keys | Where-Object { $requested -contains $_ })
$missing = @(foreach ($name in $selected) { $Targets[$name].Required | Where-Object { -not [Environment]::GetEnvironmentVariable($_) } })

if ($missing.Count -gt 0) {
    Write-Host "The cloud tests need these environment variables: $($missing -join ', ')" -ForegroundColor Red
    Write-Host "Set them to a test account, or run ./functional-tests.ps1 without -Target to test against the local emulators." -ForegroundColor Red
    exit 1
}

$services = @($selected | ForEach-Object { $Targets[$_].Services } | Select-Object -Unique)
$projects = @($selected | ForEach-Object { $Targets[$_].Project } | Select-Object -Unique)
$results = [ordered]@{}
$startedEnv = $false
$originalPath = $env:PATH
$originalDotnetRoot = $env:DOTNET_ROOT
Push-Location $RepoRoot

try {
    $env:DOTNET_NOLOGO = "1"
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
    $env:MSBUILDDISABLENODEREUSE = "1"

    # Prefer dotnet on PATH, otherwise use the repo local .dotnet
    $DotnetExe = (Get-Command dotnet -CommandType Application -TotalCount 1 -ErrorAction Ignore).Path

    if (-not (Test-Dotnet $DotnetExe)) {
        $DotnetExe = Join-Path $DotnetDir $(if ($IsWindowsOS) { "dotnet.exe" } else { "dotnet" })

        if (-not (Test-Dotnet $DotnetExe)) {
            Install-Dotnet
        }

        $env:DOTNET_ROOT = $DotnetDir
        $env:PATH = "$DotnetDir$([IO.Path]::PathSeparator)$env:PATH"
    }

    if (Test-Path $ResultsDir) {
        Remove-Item $ResultsDir -Force -Recurse
    }

    if ($services.Count -gt 0) {
        # Leave the environment running afterwards if it's already running
        $startedEnv = Test-LocalEnvStopped
        & (Join-Path $LocalEnvDir "start.ps1") @services

        if ($LASTEXITCODE -ne 0) {
            exit 1
        }
    }

    foreach ($project in $projects) {
        Invoke-Exe $DotnetExe @("build", $project, "-c", $Configuration)
    }

    foreach ($name in $selected) {
        $settings = $Targets[$name]
        $savedEnv = @{}

        foreach ($var in $settings.Env.Keys) {
            $savedEnv[$var] = [Environment]::GetEnvironmentVariable($var)
            [Environment]::SetEnvironmentVariable($var, $settings.Env[$var])
            Write-Host "[Env] $var=$($settings.Env[$var])" -ForegroundColor Cyan
        }

        try {
            # --fail-skips makes the run fail if the tests are skipped instead of run
            $arguments = @("test", "--project", $settings.Project, "-c", $Configuration, "--no-build", "--results-directory", (Join-Path $ResultsDir $name), "--report-trx", "--hangdump", "--hangdump-timeout", "20m", "--hangdump-type", "Mini", "--fail-skips", "on")
            Write-Host "[Exec] $DotnetExe $arguments" -ForegroundColor Cyan
            & $DotnetExe @arguments
            $results[$name] = if ($LASTEXITCODE -eq 0) { "passed" } else { "failed" }
        }
        finally {
            foreach ($var in $savedEnv.Keys) {
                [Environment]::SetEnvironmentVariable($var, $savedEnv[$var])
            }
        }
    }

    Write-Host ""
    Write-Host "Functional test results:"

    foreach ($name in $results.Keys) {
        $color = if ($results[$name] -eq "passed") { "Green" } else { "Red" }
        Write-Host ("  {0,-10} {1}" -f $name, $results[$name]) -ForegroundColor $color
    }

    if (@($results.Values | Where-Object { $_ -ne "passed" }).Count -gt 0) {
        exit 1
    }
}
finally {
    if ($startedEnv) {
        # Keep the container logs with the test results
        $ErrorActionPreference = "Continue"
        New-Item -ItemType Directory -Force -Path $ResultsDir | Out-Null
        & docker compose -f $ComposeFile logs --no-color --timestamps | Out-File -FilePath (Join-Path $ResultsDir "local-env.log") -Encoding utf8
        & (Join-Path $LocalEnvDir "stop.ps1")
    }

    Pop-Location
    $env:PATH = $originalPath
    $env:DOTNET_ROOT = $originalDotnetRoot
}

Write-Host "Success!"
