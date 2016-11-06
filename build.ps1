param (
    [switch]$SkipTests,
    [switch]$SkipPack,
    [switch]$StableVersion
)

$BuildNumberDateBase = "2016-11-01"
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
$ILMergeExe = Join-Path $RepoRoot 'packages\ILRepack.2.0.12\tools\ILRepack.exe'
$dotnetExe = Get-DotnetCLIExe $RepoRoot
$nupkgWrenchExe = Join-Path $RepoRoot 'packages\NupkgWrench.1.0.2\tools\NupkgWrench.exe'
$zipExe = Join-Path $RepoRoot 'packages\7ZipCLI.9.20.0\tools\7za.exe'

# Clear artifacts
Remove-Item -Recurse -Force $ArtifactsDir | Out-Null

# Restore project.json files
& $nugetExe restore $RepoRoot

# Run tests
if (-not $SkipTests)
{
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
}

# Publish for ILMerge
& $dotnetExe publish src\sleet -o artifacts\publish\net451 -f net451 -r win7-x86 --configuration release

$net46Root = (Join-Path $ArtifactsDir 'publish\net451')
$ILMergeOpts = , (Join-Path $net46Root 'Sleet.exe')
$ILMergeOpts += Get-ChildItem $net46Root -Exclude @('*.exe', '*compression*', '*System.*', '*.config', '*.pdb') | where { ! $_.PSIsContainer } | %{ $_.FullName }
$ILMergeOpts += '/out:' + (Join-Path $ArtifactsDir 'Sleet.exe')
$ILMergeOpts += '/log'
$ILMergeOpts += '/ndebug'
$ILMergeOpts += '/parallel'

Write-Host "ILMerging Sleet.exe"
& $ILMergeExe $ILMergeOpts | Out-Null

if (-not $?)
{
    # Get failure message
    Write-Host $ILMergeExe $ILMergeOpts
    & $ILMergeExe $ILMergeOpts
    Write-Host "ILMerge failed!"
    exit 1
}

if (-not $SkipPack)
{
    # Pack
    if ($StableVersion)
    {
        & $dotnetExe pack (Join-Path $RepoRoot "src\Sleet") --no-build --output $ArtifactsDir
    }
    else
    {
        $buildNumber = Get-BuildNumber $BuildNumberDateBase

        & $dotnetExe pack (Join-Path $RepoRoot "src\Sleet") --no-build --output $ArtifactsDir --version-suffix "beta.$buildNumber"
    }

    if (-not $?)
    {
        Write-Host "Pack failed!"
        exit 1
    }

    & $nupkgWrenchExe files emptyfolder artifacts -p lib/net451
    & $nupkgWrenchExe nuspec frameworkassemblies clear artifacts
    & $nupkgWrenchExe nuspec dependencies emptygroup artifacts -f net451

    # Get version number
    $nupkgPath = (& $nupkgWrenchExe list artifacts --exclude-symbols -id sleet) | Out-String
    $nupkgPath = $nupkgPath.Trim()

    Write-Host "-----------------------------"
    Write-Host "Nupkg: $nupkgPath" 
    $nupkgVersion = (& $nupkgWrenchExe version $nupkgPath) | Out-String
    $nupkgVersion = $nupkgVersion.Trim()
    Write-Host "Version: $nupkgVersion"
    Write-Host "-----------------------------"

    # Create xplat tar
    $versionFolderName = "sleet.$nupkgVersion".ToLowerInvariant()
    $versionFolder = Join-Path artifacts\publish $versionFolderName
    & $dotnetExe publish src\Sleet -o $versionFolder -f netcoreapp1.0 --configuration release

    if (-not $?)
    {
        Write-Host "Publish failed!"
        exit 1
    }

    pushd "artifacts\publish"

    # clean up pdbs
    rm $versionFolderName\*.pdb

    # bzip the portable netcore app folder
    & $zipExe "a" "$versionFolderName.tar" $versionFolderName
    & $zipExe "a" "..\$versionFolderName.tar.bz2" "$versionFolderName.tar"

    if (-not $?)
    {
        Write-Host "Zip failed!"
        exit 1
    }

    popd
}

Write-Host "Success!"