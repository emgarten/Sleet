#!/usr/bin/env bash

RESULTCODE=0

# Download dotnet cli
DOTNET="$(pwd)/.cli/dotnet"

if [ ! -f $DOTNET ]; then
    echo "Installing dotnet"
    mkdir -p .cli
    curl -o .cli/dotnet-install.sh https://raw.githubusercontent.com/dotnet/cli/58b0566d9ac399f5fa973315c6827a040b7aae1f/scripts/obtain/dotnet-install.sh

    # Run install.sh
    chmod +x .cli/dotnet-install.sh
    .cli/dotnet-install.sh -i .cli -c preview -v 1.0.1
fi

# Display info
$DOTNET --info

# clean
$DOTNET msbuild build/build.proj /t:Clean

if [ $? -ne 0 ]; then
    echo "Clean FAILED!"
    exit 1
fi

# restore
$DOTNET msbuild build/build.proj /t:Restore

if [ $? -ne 0 ]; then
    echo "Restore FAILED!"
    exit 1
fi

# build
$DOTNET msbuild build/build.proj

if [ $? -ne 0 ]; then
    echo "Build FAILED!"
    exit 1
fi

exit $RESULTCODE

