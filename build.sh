#!/usr/bin/env bash

RESULTCODE=0
pushd $(pwd)

# Download dotnet cli and run tests
. build/common/common.sh
run_standard_tests

popd
exit $RESULTCODE
