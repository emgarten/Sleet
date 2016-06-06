#!/usr/bin/env bash

RESULTCODE=0

# increase open file limit for osx
ulimit -n 2048

# Download dotnet cli
echo "Installing dotnet"
mkdir -p .cli
curl -o .cli/dotnet-install.sh https://raw.githubusercontent.com/dotnet/cli/7652335195989b2c8c9c7aa705d89b0cd4af3551/scripts/obtain/dotnet-install.sh

# Run install.sh
chmod +x .cli/dotnet-install.sh
.cli/dotnet-install.sh -i .cli -c beta -v 1.0.0-preview1-002702

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
