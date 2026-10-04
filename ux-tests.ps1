# Runs the browser tests for the gallery that Sleet writes to index.html.
#
# The tests need Google Chrome or Microsoft Edge. To use a different Chromium
# based browser set SLEET_UX_BROWSER to the path of its executable.
#
# These tests are not part of build.ps1, they run in the UX Tests workflow.

$RepoRoot = $PSScriptRoot
pushd $RepoRoot

# Load common build script helper methods
. "$PSScriptRoot\build\common\common.ps1"

# Download tools
Install-DotnetCLI $RepoRoot

# Test
Invoke-DotnetExe $RepoRoot ("test", (Join-Path $RepoRoot "test\Sleet.UX.Tests\Sleet.UX.Tests.csproj"))

popd
Write-Host "Success!"
