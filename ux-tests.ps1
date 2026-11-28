<#
.SYNOPSIS
Runs the browser tests for the gallery that Sleet writes to index.html.

.DESCRIPTION
The tests need Google Chrome or Microsoft Edge. To use a different Chromium based browser set SLEET_UX_BROWSER to the
path of its executable.

These tests are not part of build.ps1, they run in the UX Tests workflow.

The test results are written to artifacts/TestResults/ux.

.EXAMPLE
./ux-tests.ps1
#>

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

$RepoRoot = $PSScriptRoot
$DotnetDir = Join-Path $RepoRoot ".dotnet"
$ResultsDir = [IO.Path]::Combine($RepoRoot, "artifacts", "TestResults", "ux")
$IsWindowsOS = $env:OS -eq "Windows_NT"
$Configuration = "Release"
$Project = "test/Sleet.UX.Tests/Sleet.UX.Tests.csproj"

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

# True if dotnet has the SDK from global.json and the runtime used by the tests
Function Test-Dotnet([string]$Dotnet) {
    if (-not $Dotnet -or -not (Test-Path $Dotnet)) {
        return $false
    }

    $ErrorActionPreference = "Continue"
    & $Dotnet --version *> $null

    if ($LASTEXITCODE -ne 0) {
        return $false
    }

    return ((& $Dotnet --list-runtimes) -join "`n") -match "(?m)^Microsoft\.NETCore\.App 10\."
}

# Install the SDK from global.json and the runtime used by the tests to .dotnet
Function Install-Dotnet {
    New-Item -ItemType Directory -Force -Path $DotnetDir | Out-Null
    $installScript = Join-Path $DotnetDir "dotnet-install.ps1"
    Invoke-WebRequest https://dot.net/v1/dotnet-install.ps1 -OutFile $installScript -UseBasicParsing

    & $installScript -JsonFile (Join-Path $RepoRoot "global.json") -InstallDir $DotnetDir -NoPath
    & $installScript -Runtime dotnet -Channel 10.0 -InstallDir $DotnetDir -NoPath
}

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

    Invoke-Exe $DotnetExe @("build", $Project, "-c", $Configuration)
    Invoke-Exe $DotnetExe @("test", "--project", $Project, "-c", $Configuration, "--no-build", "--results-directory", $ResultsDir, "--report-trx", "--hangdump", "--hangdump-timeout", "20m", "--hangdump-type", "Mini")
}
finally {
    Pop-Location
    $env:PATH = $originalPath
    $env:DOTNET_ROOT = $originalDotnetRoot
}

Write-Host "Success!"
exit 0
