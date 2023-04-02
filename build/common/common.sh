#!/usr/bin/env bash

run_standard_tests()
{
  pushd $(pwd)

  # Download dotnet cli
  REPO_ROOT=$(pwd)
  DOTNET=$(pwd)/.cli/dotnet
  DOTNET_TOOLS=$(pwd)/.nuget/tools
  DOTNET_FORMAT=$DOTNET_TOOLS/dotnet-format

  if [ ! -f $DOTNET ]; then
    echo "Installing dotnet"
    mkdir -p .cli
    curl -L -o .cli/dotnet-install.sh https://dot.net/v1/dotnet-install.sh

    # Run install.sh
    chmod +x .cli/dotnet-install.sh
    .cli/dotnet-install.sh -i .cli --channel 6.0
    .cli/dotnet-install.sh -i .cli --channel 7.0
  fi

  # Display info
  $DOTNET --info

  # install dotnet-format
  if [ ! -d $DOTNET_TOOLS ]; then
    echo "Installing dotnet tools"
    mkdir -p .nuget/tools
    
    $DOTNET tool install --tool-path $DOTNET_TOOLS --ignore-failed-sources dotnet-format --version 5.1.250801
  fi

  $DOTNET_FORMAT --fix-whitespace --fix-style warn

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
  $DOTNET msbuild build/build.proj /t:Build\;Publish\;Test\;Pack /p:Configuration=Release /nologo /v:m

  if [ $? -ne 0 ]; then
    echo "Build FAILED!"
    exit 1
  fi

  popd
}