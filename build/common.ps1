
# install CLI
Function Install-DotnetCLI {
    param(
        [string]$RepositoryRootDir
    )

    Write-Host 'Fetching dotnet CLI'

    $CLIRoot = Get-DotnetCLIRoot $RepositoryRootDir

    New-Item -ItemType Directory -Force -Path $CLIRoot | Out-Null

    $env:DOTNET_HOME=$CLIRoot
    $installDotnet = Join-Path $CLIRoot "install.ps1"

    New-Item -ItemType Directory -Force -Path $CLIRoot

    Write-Host "Fetching $installDotnet"

    wget https://raw.githubusercontent.com/dotnet/cli/5a77b03f36904670faa36c0d294237ec8841e8f1/scripts/obtain/dotnet-install.ps1 -OutFile $installDotnet

    & $installDotnet -Channel beta -i $CLIRoot -Version 1.0.0-preview1-002702

    $DotnetExe = DotnetCLIExe $RepositoryRootDir

    if (-not (Test-Path $DotnetExe)) {
        Write-Log "Missing $DotnetExe"
        exit 1
    }

    & $DotnetExe --info
}

Function Get-DotnetCLIRoot {
    param(
        [string]$RepositoryRootDir
    )

    return Join-Path $RepositoryRootDir ".cli"
}

Function Get-DotnetCLIExe {
    param(
        [string]$RepositoryRootDir
    )

    $CLIRoot = Get-DotnetCLIRoot $RepositoryRootDir

    return Join-Path $CLIRoot "dotnet.exe"
}

Function Get-NuGetExePath {
    param(
        [string]$RepositoryRootDir
    )

    return Join-Path $RepositoryRootDir ".nuget/nuget.exe"
}

# restore packages.config
Function Install-PackagesConfig {
    param(
        [string]$RepositoryRootDir
    )

    Write-Host "Restoring packages.config"

    $nugetExe = Get-NuGetExePath $RepositoryRootDir
    $packagesConfig = Join-Path $RepositoryRootDir ".nuget/packages.config"

    if (-not (Test-Path $nugetExe))
    {
        wget https://dist.nuget.org/win-x86-commandline/v3.5.0-beta/NuGet.exe -OutFile $nugetExe
    }

    & $nugetExe restore $packagesConfig -SolutionDirectory $RepositoryRootDir
}
