#!/usr/bin/env bash

RESULTCODE=0

pushd $(pwd)

# Download dotnet cli
DOTNET="$(pwd)/.cli/dotnet"


if [ ! -f $DOTNET ]; then
    echo "Installing dotnet"
    mkdir -p .cli
    curl -o .cli/dotnet-install.sh https://raw.githubusercontent.com/dotnet/cli/c497bf498fd4e964b00e2ee44bd840f2a269ea6c/scripts/obtain/dotnet-install.sh

    # Run install.sh
    chmod +x .cli/dotnet-install.sh
    .cli/dotnet-install.sh -i .cli -c 2.0 -v 2.0.0
fi

# Display info
$DOTNET --info

# clean
rm -r -f $(pwd)/artifacts

# Clean projects and write out git info
$DOTNET msbuild build/build.proj /t:Clean\;WriteGitInfo /p:Configuration=Release /nologo /v:m

if [ $? -ne 0 ]; then
    echo "Clean;WriteGitInfo FAILED!"
    exit 1
fi

# restore
$DOTNET msbuild build/build.proj /t:Restore /p:Configuration=Release /nologo /v:m

if [ $? -ne 0 ]; then
    echo "Restore FAILED!"
    exit 1
fi

# build
$DOTNET msbuild build/build.proj /t:Build\;Test\;Pack /p:Configuration=Release /nologo /v:m

if [ $? -ne 0 ]; then
    echo "Build FAILED!"
    exit 1
fi

popd

exit $RESULTCODE
