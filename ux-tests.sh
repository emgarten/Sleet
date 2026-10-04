#!/usr/bin/env bash
set -e

# Runs the browser tests for the gallery that Sleet writes to index.html.
#
# The tests need Google Chrome or Microsoft Edge. To use a different Chromium
# based browser set SLEET_UX_BROWSER to the path of its executable.
#
# These tests are not part of build.sh, they run in the UX Tests workflow.

cd "$(dirname "$0")"

# Download dotnet cli and run the tests
. build/common/common.sh
install_dotnet_cli
run_command "$DOTNET" test test/Sleet.UX.Tests/Sleet.UX.Tests.csproj

echo "Success!"
