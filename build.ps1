param (
    [switch]$SkipTests,
    [switch]$SkipPack,
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [string]$StorageTestAccount,
    [switch]$UseDevStorage,
    [string]$AWSAccessKeyId,
    [string]$AWSSecretAccessKey,
    [string]$AWSDefaultRegion
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

$RepoRoot = $PSScriptRoot
$ArtifactsDir = Join-Path $RepoRoot "artifacts"
$DotnetDir = Join-Path $RepoRoot ".dotnet"
$IsWindowsOS = $env:OS -eq "Windows_NT"
$DotnetExe = Join-Path $DotnetDir $(if ($IsWindowsOS) { "dotnet.exe" } else { "dotnet" })

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

# Install the SDK from global.json and the runtimes used by the tests to .dotnet
Function Install-Dotnet {
    $globalJson = Join-Path $RepoRoot "global.json"
    $sdkVersion = (Get-Content $globalJson -Raw | ConvertFrom-Json).sdk.version

    if ((Test-Path "$DotnetDir/sdk/$sdkVersion") -and
        (Test-Path "$DotnetDir/shared/Microsoft.NETCore.App/8.*") -and
        (Test-Path "$DotnetDir/shared/Microsoft.NETCore.App/9.*")) {
        return
    }

    New-Item -ItemType Directory -Force -Path $DotnetDir | Out-Null
    $installScript = Join-Path $DotnetDir "dotnet-install.ps1"
    Invoke-WebRequest https://dot.net/v1/dotnet-install.ps1 -OutFile $installScript -UseBasicParsing

    & $installScript -JsonFile $globalJson -InstallDir $DotnetDir -NoPath
    & $installScript -Runtime dotnet -Channel 8.0 -InstallDir $DotnetDir -NoPath
    & $installScript -Runtime dotnet -Channel 9.0 -InstallDir $DotnetDir -NoPath
}

# Settings for the Azure and AWS S3 functional tests
if ($StorageTestAccount) { $env:SLEET_TEST_ACCOUNT = $StorageTestAccount }
if ($UseDevStorage) { $env:SLEET_TEST_ACCOUNT = "UseDevelopmentStorage=true" }
if ($AWSAccessKeyId) { $env:AWS_ACCESS_KEY_ID = $AWSAccessKeyId }
if ($AWSSecretAccessKey) { $env:AWS_SECRET_ACCESS_KEY = $AWSSecretAccessKey }
if ($AWSDefaultRegion) { $env:AWS_DEFAULT_REGION = $AWSDefaultRegion }

$originalPath = $env:PATH
$originalDotnetRoot = $env:DOTNET_ROOT
Push-Location $RepoRoot

try {
    Install-Dotnet

    # Use the repo local SDK for the build and any dotnet processes started by the tests
    $env:DOTNET_ROOT = $DotnetDir
    $env:PATH = "$DotnetDir$([IO.Path]::PathSeparator)$env:PATH"
    $env:DOTNET_NOLOGO = "1"
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
    $env:MSBUILDDISABLENODEREUSE = "1"

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
        Invoke-Exe $DotnetExe @("test", "--solution", "Sleet.slnx", "-c", $Configuration, "--no-build", "--results-directory", (Join-Path $ArtifactsDir "TestResults"), "--report-trx", "--hangdump", "--hangdump-timeout", "20m", "--hangdump-type", "Mini")
    }
}
finally {
    Pop-Location
    $env:PATH = $originalPath
    $env:DOTNET_ROOT = $originalDotnetRoot
}

Write-Host "Success!"