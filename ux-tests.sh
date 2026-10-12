#!/usr/bin/env bash
# Runs the browser tests for the gallery that Sleet writes to index.html.
#
# The tests need Google Chrome or Microsoft Edge. To use a different Chromium
# based browser set SLEET_UX_BROWSER to the path of its executable.
#
# These tests are not part of build.sh, they run in the UX Tests workflow.
#
# The test results are written to artifacts/TestResults/ux.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DOTNET_DIR="$REPO_ROOT/.dotnet"
RESULTS_DIR="$REPO_ROOT/artifacts/TestResults/ux"
CONFIGURATION="Release"
PROJECT="test/Sleet.UX.Tests/Sleet.UX.Tests.csproj"

cd "$REPO_ROOT"

export DOTNET_NOLOGO=1
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export MSBUILDDISABLENODEREUSE=1

run_command()
{
  echo ">> $*"
  "$@"
}

# True if dotnet has the SDK from global.json. The UX tests don't need other runtimes.
has_dotnet()
{
  [ -x "$1" ] && "$1" --version > /dev/null 2>&1
}

# Prefer dotnet on PATH, otherwise use the repo local .dotnet
DOTNET="$(command -v dotnet || true)"

if ! has_dotnet "$DOTNET"; then
  DOTNET="$DOTNET_DIR/dotnet"

  if ! has_dotnet "$DOTNET"; then
    mkdir -p "$DOTNET_DIR"
    run_command curl -sSL --retry 3 -o "$DOTNET_DIR/dotnet-install.sh" https://dot.net/v1/dotnet-install.sh
    chmod +x "$DOTNET_DIR/dotnet-install.sh"
    run_command "$DOTNET_DIR/dotnet-install.sh" --jsonfile "$REPO_ROOT/global.json" --install-dir "$DOTNET_DIR" --no-path
  fi

  export DOTNET_ROOT="$DOTNET_DIR"
  export PATH="$DOTNET_DIR:$PATH"
fi

run_command rm -rf "$RESULTS_DIR"
run_command "$DOTNET" build "$PROJECT" -c "$CONFIGURATION"
run_command "$DOTNET" test --project "$PROJECT" -c "$CONFIGURATION" --no-build \
  --results-directory "$RESULTS_DIR" --report-trx --hangdump --hangdump-timeout 20m --hangdump-type Mini

echo "Success!"
