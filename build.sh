#!/usr/bin/env bash

RESULTCODE=0

# increase open file limit for osx
ulimit -n 2048

# Download dotnet cli
echo "Installing dotnet"
mkdir -p .cli
curl -o .cli/dotnet-install.sh https://raw.githubusercontent.com/dotnet/cli/f4ceb1f2136c5b0be16a7b551d28f5634a6c84bb/scripts/obtain/dotnet-install.sh

# Run install.sh
chmod +x .cli/dotnet-install.sh
.cli/dotnet-install.sh -i .cli -c preview -v 1.0.0-preview2-003121

# Display info
DOTNET="$(pwd)/.cli/dotnet"
$DOTNET --info

# restore
$DOTNET restore

for testProj in `find test -type f -name project.json`
do
    echo "Running tests for $testProj"

    $DOTNET test $testProj -f netcoreapp1.0

    if [ $? -ne 0 ]; then
        echo "$testProj FAILED"
        RESULTCODE=1
    fi
done

exit $RESULTCODE
