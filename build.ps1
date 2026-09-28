param (
    [switch]$SkipTests,
    [switch]$SkipPack,
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

$RepoRoot = $PSScriptRoot
$ArtifactsDir = Join-Path $RepoRoot "artifacts"
$DotnetDir = Join-Path $RepoRoot ".dotnet"
$IsWindowsOS = $env:OS -eq "Windows_NT"

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

# True if dotnet has the SDK from global.json and the runtimes used by the tests
Function Test-Dotnet([string]$Dotnet) {
    if (-not $Dotnet -or -not (Test-Path $Dotnet)) {
        return $false
    }

    $ErrorActionPreference = "Continue"
    & $Dotnet --version *> $null

    if ($LASTEXITCODE -ne 0) {
        return $false
    }

    $runtimes = (& $Dotnet --list-runtimes) -join "`n"
    return $runtimes -match "(?m)^Microsoft\.NETCore\.App 8\." -and $runtimes -match "(?m)^Microsoft\.NETCore\.App 9\."
}

# Install the SDK from global.json and the runtimes used by the tests to .dotnet
Function Install-Dotnet {
    New-Item -ItemType Directory -Force -Path $DotnetDir | Out-Null
    $installScript = Join-Path $DotnetDir "dotnet-install.ps1"
    Invoke-WebRequest https://dot.net/v1/dotnet-install.ps1 -OutFile $installScript -UseBasicParsing

    & $installScript -JsonFile (Join-Path $RepoRoot "global.json") -InstallDir $DotnetDir -NoPath
    & $installScript -Runtime dotnet -Channel 8.0 -InstallDir $DotnetDir -NoPath
    & $installScript -Runtime dotnet -Channel 9.0 -InstallDir $DotnetDir -NoPath
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

    Invoke-Exe $DotnetExe @("--info")

    if (Test-Path $ArtifactsDir) {
        Remove-Item $ArtifactsDir -Force -Recurse
    }

    Invoke-Exe $DotnetExe @("msbuild", "build/version.proj", "-nologo", "-v:m")
    Invoke-Exe $DotnetExe @("build", "Sleet.slnx", "-c", $Configuration)

    if ($IsWindowsOS) {
        # Sleet.exe for the SleetExe package and the CmdExe tests
        Invoke-Exe $DotnetExe @("publish", "src/Sleet/Sleet.csproj", "-c", $Configuration, "-f", "net10.0", "-p:PublishSingleFile=true", "-o", (Join-Path $ArtifactsDir "publish"))
    }

    if (-not $SkipPack) {
        Invoke-Exe $DotnetExe @("pack", "Sleet.slnx", "-c", $Configuration, "--no-build")

        if ($IsWindowsOS) {
            Invoke-Exe $DotnetExe @("pack", "src/Sleet/Sleet.csproj", "-c", $Configuration, "--no-build", "-p:PackageId=SleetExe")
        }
    }

    if (-not $SkipTests) {
        # The functional tests need Docker or cloud accounts, run them with functional-tests.ps1
        Invoke-Exe $DotnetExe @("test", "--solution", "Sleet.slnx", "-c", $Configuration, "--no-build", "--results-directory", (Join-Path $ArtifactsDir "TestResults"), "--report-trx", "--hangdump", "--hangdump-timeout", "20m", "--hangdump-type", "Mini", "-p:ExcludeFunctionalTests=true")
    }
}
finally {
    Pop-Location
    $env:PATH = $originalPath
    $env:DOTNET_ROOT = $originalDotnetRoot
}

Write-Host "Success!"