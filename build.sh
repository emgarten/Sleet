#!/usr/bin/env bash
set -e

# No options are needed to run a basic build and unit tests
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

pushd $(pwd)

# Download dotnet cli and run tests
. build/common/common.sh
run_standard_tests