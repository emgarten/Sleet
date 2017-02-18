param (
    [switch]$SkipTests,
    [switch]$SkipPack,
    [switch]$Push,
    [switch]$StableVersion
)

$BuildNumberDateBase = "2017-01-08"
$RepoRoot = $PSScriptRoot
$PackageId = "Sleet"
$SleetFeedId = "packages"

# Load common build script helper methods
. "$PSScriptRoot\build\common.ps1"

# Ensure dotnet.exe exists in .cli
Install-DotnetCLI $RepoRoot

# Ensure packages.config packages
Install-PackagesConfig $RepoRoot

$ArtifactsDir = Join-Path $RepoRoot 'artifacts'
$nugetExe = Join-Path $RepoRoot '.nuget\nuget.exe'
$dotnetExe = Get-DotnetCLIExe $RepoRoot
$nupkgWrenchExe = Join-Path $RepoRoot "packages\NupkgWrench.1.1.0\tools\NupkgWrench.exe"
$sleetExe = Join-Path $ArtifactsDir "Sleet.exe"
$ILMergeExe = Join-Path $RepoRoot 'packages\ILRepack.2.0.12\tools\ILRepack.exe'
$zipExe = Join-Path $RepoRoot 'packages\7ZipCLI.9.20.0\tools\7za.exe'

# Clear artifacts
Remove-Item -Recurse -Force $ArtifactsDir | Out-Null

# Git commit
$commitHash = git rev-parse HEAD | Out-String
$commitHash = $commitHash.Trim()
$gitBranch = git rev-parse --abbrev-ref HEAD | Out-String
$gitBranch = $gitBranch.Trim()

# Restore project.json files
& $dotnetExe restore (Join-Path $RepoRoot "$PackageId.sln")

if (-not $?)
{
    Write-Host "Restore failed!"
    exit 1
}

& $dotnetExe clean (Join-Path $RepoRoot "$PackageId.sln") --configuration release /m
& $dotnetExe build (Join-Path $RepoRoot "$PackageId.sln") --configuration release /m

if (-not $?)
{
    Write-Host "Build failed!"
    exit 1
}

# Run tests
if (-not $SkipTests)
{
    Run-Tests $RepoRoot $DotnetExe
}

$json608Lib  = (Join-Path $RepoRoot 'packages\Newtonsoft.Json.6.0.8\lib\net45')
$net46Root = (Join-Path $RepoRoot 'src\Sleet\bin\release\net46')
$ILMergeOpts = , (Join-Path $net46Root 'Sleet.exe')
$ILMergeOpts += Get-ChildItem $net46Root -Exclude @('*.exe', '*compression*', '*System.*', '*.config', '*.pdb', '*.json', '*.xml') | where { ! $_.PSIsContainer } | %{ $_.FullName }
$ILMergeOpts += '/out:' + (Join-Path $ArtifactsDir 'Sleet.exe')
$ILMergeOpts += '/log'

# Newtonsoft.Json 6.0.8 is used by NuGet and is needed only for reference.
$ILMergeOpts += '/lib:' + $json608Lib
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
    & $dotnetExe pack (Join-Path $RepoRoot "src\$PackageId\$PackageId.csproj") --configuration release --output $ArtifactsDir /p:NoPackageAnalysis=true

    if (-not $?)
    {
       Write-Host "Pack failed!"
       exit 1
    }

    & $dotnetExe pack (Join-Path $RepoRoot "src\SleetLib\SleetLib.csproj") --configuration release --output $ArtifactsDir /p:NoPackageAnalysis=true
    
    if (-not $?)
    {
       Write-Host "Pack failed!"
       exit 1
    }

    $buildNumber = Get-BuildNumber $BuildNumberDateBase
    
    # Clear out net46 lib
    & $nupkgWrenchExe files emptyfolder artifacts -p lib/net46 --id Sleet
    & $nupkgWrenchExe nuspec frameworkassemblies clear artifacts
    & $nupkgWrenchExe nuspec dependencies emptygroup artifacts -f net46 --id Sleet

    # Add net46 tools
    & $nupkgWrenchExe files add --path tools/Sleet.exe --file $sleetExe --id Sleet

    # Get version number
    $nupkgVersion = (& $nupkgWrenchExe version $ArtifactsDir --id Sleet) | Out-String
    $nupkgVersion = $nupkgVersion.Trim()

    if (-not $StableVersion)
    {
        $nupkgVersion = $nupkgVersion + "-" + "beta.$buildNumber"
    }

    $updatedVersion = $nupkgVersion + "+git." + $commitHash

    & $nupkgWrenchExe nuspec edit --property version --value $updatedVersion $ArtifactsDir
    & $nupkgWrenchExe updatefilename $ArtifactsDir

    # Create xplat tar
    $versionFolderName = "sleet.$nupkgVersion".ToLowerInvariant()
    $publishDir = Join-Path $ArtifactsDir publish
    $versionFolder = Join-Path $publishDir $versionFolderName
    & $dotnetExe publish src\Sleet\Sleet.csproj -o $versionFolder -f netcoreapp1.0 --configuration release

    if (-not $?)
    {
        Write-Host "Publish failed!"
        exit 1
    }

    pushd $publishDir

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

    Write-Host "-----------------------------"
    Write-Host "Version: $updatedVersion"
    Write-Host "-----------------------------"
}

if ($Push -and ($gitBranch -eq "master"))
{
    & $sleetExe push --source $SleetFeedId $ArtifactsDir

    if (-not $?)
    {
       Write-Host "Push failed!"
       exit 1
    }

    & $sleetExe validate --source $SleetFeedId

    if (-not $?)
    {
       Write-Host "Feed corrupt!"
       exit 1
    }
}

Write-Host "Success!"