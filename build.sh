#!/usr/bin/env bash

# No options are needed to run a basic build and unit tests
# To run functional tests against azure and or aws, use the following options:
VALID_ARGS=$(getopt -o : --long azure-conn:,aws-key:,aws-secret:,aws-region: -- "$@")
if [[ $? -ne 0 ]]; then
    exit 1;
fi

eval set -- "$VALID_ARGS"
while [ : ]; do
  case "$1" in
    --azure-conn)
        export SLEET_TEST_ACCOUNT=$2
        shift 2
        ;;
    --aws-key)
        export AWS_ACCESS_KEY_ID=$2
        shift 2
        ;;
    --aws-secret)
        export AWS_SECRET_ACCESS_KEY=$2
        shift 2
        ;;
    --aws-region)
        export AWS_DEFAULT_REGION=$2
        shift 2
        ;;
    --) shift; 
        break 
        ;;
  esac
done

RESULTCODE=0
pushd $(pwd)

# Download dotnet cli and run tests
. build/common/common.sh
run_standard_tests

popd
exit $RESULTCODE
