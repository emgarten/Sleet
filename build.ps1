$RepoRoot = $PSScriptRoot

trap
{
    Write-Host "build failed"
    exit 1
}

# Load common build script helper methods
. "$PSScriptRoot\build\common.ps1"

# Ensure dotnet.exe exists in .cli
Install-DotnetCLI $RepoRoot

# Ensure packages.config packages
Install-PackagesConfig $RepoRoot

$ArtifactsDir = Join-Path $RepoRoot 'artifacts'
$nugetExe = Join-Path $RepoRoot '.nuget\nuget.exe'
$ILMergeExe = Join-Path $RepoRoot 'packages\ILMerge.2.14.1208\tools\ILMerge.exe'
$dotnetExe = Get-DotnetCLIExe $RepoRoot

# Clear artifacts
Remove-Item -Recurse -Force $ArtifactsDir | Out-Null

# Restore project.json files
& $nugetExe restore $RepoRoot

# Run tests
& $dotnetExe test (Join-Path $RepoRoot "test\Sleet.Test")

if (-not $?)
{
    Write-Host "tests failed!!!"
    exit 1
}

& $dotnetExe test (Join-Path $RepoRoot "test\Sleet.Integration.Test")

if (-not $?)
{
    Write-Host "tests failed!!!"
    exit 1
}


# Publish for ILMerge
& $dotnetExe publish src\sleet -o artifacts\publish\net451 -f net451 -r win7-x86 --configuration release

$net46Root = (Join-Path $ArtifactsDir 'publish\net451')
$ILMergeOpts = , (Join-Path $net46Root 'Sleet.exe')
$ILMergeOpts += Get-ChildItem $net46Root -Exclude @('*.exe', '*compression*', '*System.*', '*.config', '*.pdb') | where { ! $_.PSIsContainer } | %{ $_.FullName }
$ILMergeOpts += '/out:' + (Join-Path $ArtifactsDir 'Sleet.exe')
$ILMergeOpts += '/log'
$ILMergeOpts += '/ndebug'

Write-Host "ILMerging Sleet.exe"
& $ILMergeExe $ILMergeOpts | Out-Null

if (-not $?)
{
    Write-Host "ILMerge failed!"
    exit 1
}

# Pack
& $dotnetExe pack (Join-Path $RepoRoot "src\Sleet") --no-build --output $ArtifactsDir

if (-not $?)
{
    Write-Host "Pack failed!"
    exit 1
}

Write-Host "Success!"