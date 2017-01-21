#!/usr/bin/env bash

RESULTCODE=0

# increase open file limit for osx
ulimit -n 2048

# Download dotnet cli
echo "Installing dotnet"
mkdir -p .cli
curl -o .cli/dotnet-install.sh https://raw.githubusercontent.com/dotnet/cli/58b0566d9ac399f5fa973315c6827a040b7aae1f/scripts/obtain/dotnet-install.sh

# Run install.sh
chmod +x .cli/dotnet-install.sh
.cli/dotnet-install.sh -i .cli -c preview -v 1.0.0-preview4-004233

# Display info
DOTNET="$(pwd)/.cli/dotnet"
$DOTNET --info

# restore
$DOTNET restore Sleet.sln

$DOTNET test test/SleetLib.Tests/SleetLib.Tests.csproj -f netcoreapp1.0

if [ $? -ne 0 ]; then
    echo "SleetLib.Tests FAILED"
    RESULTCODE=1
fi

$DOTNET test test/Sleet.Integration.Tests/Sleet.Integration.Tests.csproj -f netcoreapp1.0

if [ $? -ne 0 ]; then
    echo "Sleet.Integration.Tests FAILED"
    RESULTCODE=1
fi

$DOTNET publish src/Sleet/Sleet.csproj -o $(pwd)/artifacts/publish/Sleet -f netcoreapp1.0 --configuration release

if [ $? -ne 0 ]; then
    echo "publish FAILED"
    RESULTCODE=1
fi

exit $RESULTCODE
