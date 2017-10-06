param (
    [switch]$SkipTests,
    [switch]$SkipPack,
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [string]$StorageTestAccount,
    [switch]$UseDevStorage
)

$RepoName = "Sleet"
$RepoRoot = $PSScriptRoot
pushd $RepoRoot

# Load common build script helper methods
. "$PSScriptRoot\build\common\common.ps1"

# Set test account if available
if ($StorageTestAccount)
{
  Write-Host "SLEET_TEST_ACCOUNT set"
  $env:SLEET_TEST_ACCOUNT=$StorageTestAccount
}

if ($UseDevStorage)
{
  Write-Host "SLEET_TEST_ACCOUNT set to dev storage"
  $env:SLEET_TEST_ACCOUNT="UseDevelopmentStorage=true"
}

# Download tools
Install-DotnetCLI $RepoRoot
Install-NuGetExe $RepoRoot

# Clean and write git info
Remove-Artifacts $RepoRoot
Invoke-DotnetMSBuild $RepoRoot ("build\build.proj", "/t:Clean;WriteGitInfo", "/p:Configuration=$Configuration")

# Restore
Invoke-DotnetMSBuild $RepoRoot ("build\build.proj", "/t:Restore", "/p:Configuration=$Configuration")

# Run main build
$buildTargets = "Build"

if (-not $SkipPack)
{
    $buildTargets += ";Pack"
}

if (-not $SkipTests)
{
    $buildTargets += ";Test"
}

# Run build.proj
Invoke-DotnetMSBuild $RepoRoot ("build\build.proj", "/t:$buildTargets", "/p:Configuration=$Configuration")
 
popd
Write-Host "Success!"
