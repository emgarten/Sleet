#!/usr/bin/env bash
set -euo pipefail

# No options are needed to run a basic build and unit tests.
# To run functional tests against azure and or aws, use the following options:
while [[ $# -gt 0 ]]; do
  case "$1" in
    --azure-conn)
        export SLEET_TEST_ACCOUNT="$2"
        shift 2
        ;;
    --aws-key)
        export AWS_ACCESS_KEY_ID="$2"
        shift 2
        ;;
    --aws-secret)
        export AWS_SECRET_ACCESS_KEY="$2"
        shift 2
        ;;
    --aws-region)
        export AWS_DEFAULT_REGION="$2"
        shift 2
        ;;
    --)
        shift
        break
        ;;
    --*)
        echo "Unknown option: $1" >&2
        exit 1
        ;;
    *)
        break
        ;;
  esac
done

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ARTIFACTS_DIR="$REPO_ROOT/artifacts"
DOTNET_DIR="$REPO_ROOT/.dotnet"
DOTNET="$DOTNET_DIR/dotnet"
CONFIGURATION="Release"

cd "$REPO_ROOT"

# Run a command with logging
run_command()
{
  echo ">> $*"
  "$@"
}

# Returns success if the .NET SDK from global.json and the runtimes needed by the tests are installed
dotnet_is_installed()
{
  [ -x "$DOTNET" ] || return 1
  "$DOTNET" --version > /dev/null 2>&1 || return 1

  local runtimes
  runtimes="$("$DOTNET" --list-runtimes)"
  grep -q "^Microsoft.NETCore.App 8\." <<< "$runtimes" && grep -q "^Microsoft.NETCore.App 9\." <<< "$runtimes"
}

# Install the .NET SDK and runtimes to .dotnet
if ! dotnet_is_installed; then
  echo ""
  echo "===> Installing .NET SDK..."
  echo ""
  mkdir -p "$DOTNET_DIR"
  run_command curl -sSL --retry 3 -o "$DOTNET_DIR/dotnet-install.sh" https://dot.net/v1/dotnet-install.sh
  chmod +x "$DOTNET_DIR/dotnet-install.sh"
  run_command "$DOTNET_DIR/dotnet-install.sh" --jsonfile "$REPO_ROOT/global.json" --install-dir "$DOTNET_DIR" --no-path
  run_command "$DOTNET_DIR/dotnet-install.sh" --runtime dotnet --channel 8.0 --install-dir "$DOTNET_DIR" --no-path
  run_command "$DOTNET_DIR/dotnet-install.sh" --runtime dotnet --channel 9.0 --install-dir "$DOTNET_DIR" --no-path
fi

# Use the repo local SDK for the build and any dotnet processes started by the tests
export DOTNET_ROOT="$DOTNET_DIR"
export PATH="$DOTNET_DIR:$PATH"
export DOTNET_NOLOGO=1
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export TESTINGPLATFORM_TELEMETRY_OPTOUT=1
export MSBUILDDISABLENODEREUSE=1

echo ""
echo "===> Displaying .NET SDK info..."
echo ""
run_command "$DOTNET" --info

echo ""
echo "===> Cleaning artifacts directory..."
echo ""
run_command rm -rf "$ARTIFACTS_DIR"

echo ""
echo "===> Writing git version info..."
echo ""
run_command "$DOTNET" msbuild build/version.proj -t:WriteGitInfo -nologo -v:m

echo ""
echo "===> Restoring NuGet packages..."
echo ""
run_command "$DOTNET" restore Sleet.slnx

echo ""
echo "===> Building projects..."
echo ""
run_command "$DOTNET" build Sleet.slnx -c "$CONFIGURATION" --no-restore

echo ""
echo "===> Creating NuGet packages..."
echo ""
run_command "$DOTNET" pack Sleet.slnx -c "$CONFIGURATION" --no-build

echo ""
echo "===> Running tests..."
echo ""
run_command "$DOTNET" test --solution Sleet.slnx -c "$CONFIGURATION" --no-build --results-directory "$ARTIFACTS_DIR/TestResults" --report-trx --hangdump --hangdump-timeout 20m --hangdump-type Mini

echo "Success!"