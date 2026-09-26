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

# Run an external command and fail the build if it fails
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

# Returns true if the .NET SDK from global.json and the runtimes needed by the tests are installed to .dotnet
Function Test-DotnetInstalled {
    if (-not (Test-Path $DotnetExe)) {
        return $false
    }

    # Native stderr output must not stop the script, dotnet --version fails when the global.json SDK is missing
    $previousErrorActionPreference = $ErrorActionPreference
    $ErrorActionPreference = "Continue"

    try {
        & $DotnetExe --version *> $null

        if ($LASTEXITCODE -ne 0) {
            return $false
        }

        $runtimes = & $DotnetExe --list-runtimes 2> $null
    }
    finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }

    return [bool](($runtimes -match "^Microsoft\.NETCore\.App 8\.") -and ($runtimes -match "^Microsoft\.NETCore\.App 9\."))
}

# Install the .NET SDK from global.json and the runtimes needed by the tests to .dotnet
Function Install-Dotnet {
    if (Test-DotnetInstalled) {
        return
    }

    New-Item -ItemType Directory -Force -Path $DotnetDir | Out-Null
    $installScript = Join-Path $DotnetDir "dotnet-install.ps1"

    Write-Host "Downloading dotnet-install.ps1"
    Invoke-WebRequest https://dot.net/v1/dotnet-install.ps1 -OutFile $installScript -UseBasicParsing

    & $installScript -JsonFile (Join-Path $RepoRoot "global.json") -InstallDir $DotnetDir -NoPath
    & $installScript -Runtime dotnet -Channel 8.0 -InstallDir $DotnetDir -NoPath
    & $installScript -Runtime dotnet -Channel 9.0 -InstallDir $DotnetDir -NoPath

    if (-not (Test-Path $DotnetExe)) {
        throw "Missing $DotnetExe"
    }
}

# Test settings for the Azure and AWS S3 functional tests
if ($StorageTestAccount) {
    Write-Host "SLEET_TEST_ACCOUNT set"
    $env:SLEET_TEST_ACCOUNT = $StorageTestAccount
}

if ($UseDevStorage) {
    Write-Host "SLEET_TEST_ACCOUNT set to dev storage"
    $env:SLEET_TEST_ACCOUNT = "UseDevelopmentStorage=true"
}

if ($AWSAccessKeyId) {
    Write-Host "Setting AWS_ACCESS_KEY_ID"
    $env:AWS_ACCESS_KEY_ID = $AWSAccessKeyId
}

if ($AWSSecretAccessKey) {
    Write-Host "Setting AWS_SECRET_ACCESS_KEY"
    $env:AWS_SECRET_ACCESS_KEY = $AWSSecretAccessKey
}

if ($AWSDefaultRegion) {
    Write-Host "Setting AWS_DEFAULT_REGION"
    $env:AWS_DEFAULT_REGION = $AWSDefaultRegion
}

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
    $env:TESTINGPLATFORM_TELEMETRY_OPTOUT = "1"
    $env:MSBUILDDISABLENODEREUSE = "1"

    Invoke-Exe $DotnetExe @("--info")

    # Clean
    if (Test-Path $ArtifactsDir) {
        Remove-Item $ArtifactsDir -Force -Recurse
    }

    # Write the version from git to artifacts/obj/git.props
    Invoke-Exe $DotnetExe @("msbuild", "build/version.proj", "-t:WriteGitInfo", "-nologo", "-v:m")

    # Restore and build
    Invoke-Exe $DotnetExe @("restore", "Sleet.slnx")
    Invoke-Exe $DotnetExe @("build", "Sleet.slnx", "-c", $Configuration, "--no-restore")

    if ($IsWindowsOS) {
        # Publish the single file Sleet.exe for the SleetExe package and the CmdExe tests
        Invoke-Exe $DotnetExe @("publish", "src/Sleet/Sleet.csproj", "-c", $Configuration, "-f", "net10.0", "-r", "win-x64", "--self-contained", "true", "-p:PublishSingleFile=true", "--force", "-o", (Join-Path $ArtifactsDir "publish"))
    }

    # Pack
    if (-not $SkipPack) {
        $packArgs = @("pack", "Sleet.slnx", "-c", $Configuration, "--no-build")

        if ($IsWindowsOS) {
            $packArgs += "-p:PackSleetExe=true"
        }

        Invoke-Exe $DotnetExe $packArgs
    }

    # Test
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