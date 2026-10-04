#!/usr/bin/env bash
# Runs the functional tests against the local storage emulators, or against Azure and Amazon S3 accounts.
# Run ./functional-tests.sh --help for the targets.
set -euo pipefail

usage()
{
  cat << 'EOF'
Usage: ./functional-tests.sh [--target <target>[,<target>...]]

Targets:
  emulators  The emulator targets. This is the default and needs Docker.
  cloud      The cloud targets. They need the account environment variables.
  all        All targets.
  azurite    The Azure tests against Azurite.
  rustfs     The Amazon S3 tests against RustFS.
  azure      The Azure tests against SLEET_TEST_ACCOUNT, an Azure Storage connection string.
  aws        The Amazon S3 tests against SLEET_TEST_S3_ACCESS_KEY_ID and SLEET_TEST_S3_SECRET_ACCESS_KEY.
             SLEET_TEST_S3_REGION defaults to us-east-1. Set SLEET_TEST_S3_SERVICE_URL to test S3 compatible
             storage instead of Amazon S3.

The emulator targets start the local test environment in local-env with Docker, and stop it
afterwards if it wasn't already running.

The test results are written to artifacts/TestResults/functional.

Examples:
  ./functional-tests.sh
  SLEET_TEST_ACCOUNT="<connection string>" ./functional-tests.sh --target azure
EOF
}

TARGET_ARG="emulators"

while [[ $# -gt 0 ]]; do
  case "$1" in
    -t|--target)
        if [[ $# -lt 2 ]]; then
          echo "Missing value for $1" >&2
          exit 1
        fi
        TARGET_ARG="$2"
        shift 2
        ;;
    -h|--help)
        usage
        exit 0
        ;;
    *)
        echo "Unknown option: $1" >&2
        usage >&2
        exit 1
        ;;
  esac
done

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DOTNET_DIR="$REPO_ROOT/.dotnet"
LOCAL_ENV_DIR="$REPO_ROOT/local-env"
COMPOSE_FILE="$LOCAL_ENV_DIR/docker-compose.yml"
RESULTS_DIR="$REPO_ROOT/artifacts/TestResults/functional"
CONFIGURATION="Release"
# The emulator targets run against local-env
EMULATOR_TARGETS="azurite rustfs"
CLOUD_TARGETS="azure aws"
ALL_TARGETS="$EMULATOR_TARGETS $CLOUD_TARGETS"

# Emulator targets list the account env vars to unset so the tests use local-env.
# Cloud targets list the env vars they need.
target_project()
{
  case "$1" in
    azurite|azure) echo "test/Sleet.Azure.Tests/Sleet.Azure.Tests.csproj" ;;
    rustfs|aws) echo "test/Sleet.AmazonS3.Tests/Sleet.AmazonS3.Tests.csproj" ;;
  esac
}

target_unset_env()
{
  case "$1" in
    azurite) echo "SLEET_TEST_ACCOUNT" ;;
    rustfs) echo "SLEET_TEST_S3_ACCESS_KEY_ID SLEET_TEST_S3_SECRET_ACCESS_KEY" ;;
  esac
}

target_required_env()
{
  case "$1" in
    azure) echo "SLEET_TEST_ACCOUNT" ;;
    aws) echo "SLEET_TEST_S3_ACCESS_KEY_ID SLEET_TEST_S3_SECRET_ACCESS_KEY" ;;
  esac
}

REQUESTED=""

for name in ${TARGET_ARG//,/ }; do
  case "$name" in
    emulators) REQUESTED="$REQUESTED $EMULATOR_TARGETS" ;;
    cloud) REQUESTED="$REQUESTED $CLOUD_TARGETS" ;;
    all) REQUESTED="$REQUESTED $ALL_TARGETS" ;;
    azurite|rustfs|azure|aws) REQUESTED="$REQUESTED $name" ;;
    *)
        echo "Unknown target: $name. Use emulators, cloud, all, azurite, rustfs, azure, or aws." >&2
        exit 1
        ;;
  esac
done

SELECTED=""
USE_LOCAL_ENV=0
PROJECTS=""
MISSING=""

for name in $ALL_TARGETS; do
  if [[ " $REQUESTED " != *" $name "* ]]; then
    continue
  fi

  SELECTED="$SELECTED $name"

  if [[ " $EMULATOR_TARGETS " == *" $name "* ]]; then
    USE_LOCAL_ENV=1
  fi

  project="$(target_project "$name")"
  [[ " $PROJECTS " == *" $project "* ]] || PROJECTS="$PROJECTS $project"

  for var in $(target_required_env "$name"); do
    [[ -n "${!var:-}" ]] || MISSING="$MISSING $var"
  done
done

if [[ -z "$SELECTED" ]]; then
  echo "No targets to run, see ./functional-tests.sh --help" >&2
  exit 1
fi

if [[ -n "$MISSING" ]]; then
  MISSING="${MISSING# }"
  echo "The cloud tests need these environment variables: ${MISSING// /, }" >&2
  echo "Set them to a test account, or run ./functional-tests.sh without --target to test against the local emulators." >&2
  exit 1
fi

cd "$REPO_ROOT"

export DOTNET_NOLOGO=1
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export MSBUILDDISABLENODEREUSE=1

run_command()
{
  echo ">> $*"
  "$@"
}

# True if dotnet has the SDK from global.json. The functional tests don't need other runtimes.
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

STARTED_ENV=0

cleanup()
{
  local exit_code=$?

  if [[ $STARTED_ENV -eq 1 ]]; then
    # Keep the container logs with the test results
    mkdir -p "$RESULTS_DIR"
    docker compose -f "$COMPOSE_FILE" logs --no-color --timestamps > "$RESULTS_DIR/local-env.log" 2>&1 || true
    "$LOCAL_ENV_DIR/stop.sh" || true
  fi

  exit $exit_code
}

trap cleanup EXIT
trap 'exit 130' INT
trap 'exit 143' TERM

if [[ $USE_LOCAL_ENV -eq 1 ]]; then
  # Start every service, including ones the tests don't use yet, so CI checks that they all start. Leave the
  # environment running afterwards if it's already running.
  if command -v docker > /dev/null 2>&1 && RUNNING="$(docker compose -f "$COMPOSE_FILE" ps --services --status running 2> /dev/null)" && [[ -z "$RUNNING" ]]; then
    STARTED_ENV=1
  fi

  "$LOCAL_ENV_DIR/start.sh"
fi

for project in $PROJECTS; do
  run_command "$DOTNET" build "$project" -c "$CONFIGURATION"
done

FAILED=0
SUMMARY=""

for name in $SELECTED; do
  ENV_ARGS=""

  for var in $(target_unset_env "$name"); do
    ENV_ARGS="$ENV_ARGS -u $var"
  done

  # --fail-skips makes the run fail if the tests are skipped instead of run, such as when they can't find local-env
  # shellcheck disable=SC2086 # ENV_ARGS is a list of env options
  if run_command env $ENV_ARGS "$DOTNET" test --project "$(target_project "$name")" -c "$CONFIGURATION" --no-build \
      --results-directory "$RESULTS_DIR/$name" --report-trx --hangdump --hangdump-timeout 20m --hangdump-type Mini --fail-skips on; then
    SUMMARY="$SUMMARY$(printf '  %-10s %s' "$name" passed)"$'\n'
  else
    SUMMARY="$SUMMARY$(printf '  %-10s %s' "$name" failed)"$'\n'
    FAILED=1
  fi
done

echo ""
echo "Functional test results:"
printf '%s' "$SUMMARY"

if [[ $FAILED -ne 0 ]]; then
  exit 1
fi

echo "Success!"
