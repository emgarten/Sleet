$RepoRoot = $PSScriptRoot
$ArtifactsDir = Join-Path $RepoRoot 'artifacts'
$NuGetExe = Join-Path $RepoRoot '.nuget\nuget.exe'
$ILMergeExe = Join-Path $RepoRoot 'packages\ILMerge.2.14.1208\tools\ILMerge.exe'
$DnvmCmd = Join-Path $env:USERPROFILE '.dnx\bin\dnvm.cmd'

trap
{
    Write-Host "build failed"
    exit 1
}

if (-not (Test-Path $NuGetExe))
{
    wget https://dist.nuget.org/win-x86-commandline/latest/nuget.exe -OutFile $NuGetExe
}

& $NuGetExe restore (Join-Path $RepoRoot '.nuget\packages.config') -SolutionDirectory $RepoRoot

# install DNX
if (-not (Test-Path $DnvmCmd)) {
    iex (`
      (new-object net.webclient).DownloadString('https://raw.githubusercontent.com/aspnet/Home/dev/dnvminstall.ps1')`
    )
}

$env:DNX_FEED = 'https://www.nuget.org/api/v2/'
& dnvm install 1.0.0-rc1-update1 -runtime coreclr -arch x64
& dnvm install 1.0.0-rc1-update1 -runtime clr -arch x86 -alias default

# restore
& dnu restore

if (-not $?) {
    Write-Host "restore failed"
    exit 1
}

# test
& dnvm use 1.0.0-rc1-update1 -runtime coreclr -arch x64
& dnx --project test\Sleet.Test test

if (-not $?) {
    Write-Host "tests failed"
    exit 1
}

& dnvm use 1.0.0-rc1-update1 -runtime clr -arch x86
& dnx --project test\Sleet.Test test

if (-not $?) {
    Write-Host "tests failed"
    exit 1
}

& dnvm use 1.0.0-rc1-update1 -runtime coreclr -arch x64
& dnx --project test\Sleet.Integration.Test test

if (-not $?) {
    Write-Host "tests failed"
    exit 1
}

& dnvm use 1.0.0-rc1-update1 -runtime clr -arch x86
& dnx --project test\Sleet.Integration.Test test

if (-not $?) {
    Write-Host "tests failed"
    exit 1
}