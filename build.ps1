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

# Set AWS S3 storage test values
if ($AWSAccessKeyId) {
    Write-Host "Setting AWS_ACCESS_KEY_ID"
    $env:AWS_ACCESS_KEY_ID=$AWSAccessKeyId
}

if ($AWSSecretAccessKey) {
    Write-Host "Setting AWS_SECRET_ACCESS_KEY"
    $env:AWS_SECRET_ACCESS_KEY=$AWSSecretAccessKey
}

if ($AWSDefaultRegion) {
    Write-Host "Setting AWS_DEFAULT_REGION"
    $env:AWS_DEFAULT_REGION=$AWSDefaultRegion
}

# Download tools
Install-CommonBuildTools $RepoRoot

# Run dotnet-format to apply style fixes or fail on CI builds
Invoke-DotnetFormat $RepoRoot

# Clean and write git info
Remove-Artifacts $RepoRoot
Invoke-DotnetMSBuild $RepoRoot ("build\build.proj", "/t:Clean;WriteGitInfo", "/p:Configuration=$Configuration")

# Build Exe
Invoke-DotnetExe $RepoRoot ("publish", "--force", "-r", "win-x64", "-p:PublishSingleFile=true", "--self-contained", "true", "-f", "net6.0", "-o", (Join-Path $RepoRoot "artifacts\publish"), (Join-Path $RepoRoot "\src\Sleet\Sleet.csproj"))

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
