
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

    wget https://raw.githubusercontent.com/dotnet/cli/58b0566d9ac399f5fa973315c6827a040b7aae1f/scripts/obtain/dotnet-install.ps1 -OutFile $installDotnet

    & $installDotnet -Channel preview -i $CLIRoot -Version 1.0.0-rc4-004706

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
        wget https://dist.nuget.org/win-x86-commandline/v4.0.0-rc3/NuGet.exe -OutFile $nugetExe
    }

    & $nugetExe restore $packagesConfig -SolutionDirectory $RepositoryRootDir
}

Function Get-BuildNumber([string]$inputDate) {
    [int](((Get-Date) - (Get-Date $inputDate)).TotalMinutes / 5)
}

Function Get-SleetConfig {
    param(
        [string]$RepositoryRootDir
    )

    $path = Join-Path $RepositoryRootDir "sleet.json"

    if (-not (Test-Path $path))
    {
        $parentPath =(get-item $RepositoryRootDir ).parent.FullName

        $path = Join-Path $parentPath "sleet.json"
    }

    if (-not (Test-Path $path))
    {
        $path = "sleet.json"
    }

    return $path
}

# Tests
Function Run-Tests {
    param(
        [string]$RepoRoot,
        [string]$DotnetExe
    )

    Write-Host "Running Tests"

    $failed = $false

    Get-ChildItem (Join-Path $RepoRoot "test") -Filter *.csproj -Recurse | 
    Foreach-Object {
        $testProject = $_.FullName
        Write-Host $testProject

        & $dotnetExe test $testProject -c release --no-build

        if (-not $?)
        {
            Write-Host "$testProject FAILED!!!"
            $failed = $true
        }
    }

    if ($failed -eq $true)
    {
        exit 1
    }
}