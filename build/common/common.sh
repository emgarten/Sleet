#!/usr/bin/env bash

run_standard_tests()
{
  pushd $(pwd)

  # Download dotnet cli
  REPO_ROOT="$(pwd)"
  DOTNET="$(pwd)/.cli/dotnet"
  DOTNET_TOOLS="$(pwd)/.nuget/tools"
  DOTNET_FORMAT="$(DOTNET_TOOLS)/dotnet-format"

  if [ ! -f $DOTNET ]; then
    echo "Installing dotnet"
    mkdir -p .cli
    curl -o .cli/dotnet-install.sh https://raw.githubusercontent.com/dotnet/cli/1f4478755d57ed37058096ed739bbdf9b3d2eb3c/scripts/obtain/dotnet-install.sh

    # Run install.sh
    chmod +x .cli/dotnet-install.sh
    .cli/dotnet-install.sh -i .cli -c 3.0 -v 3.0.100
  fi

  # Display info
  $DOTNET --info

  # install dotnet-format
  if [ ! -d $DOTNET_TOOLS ]; then
    echo "Installing dotnet tools"
    mkdir -p .nuget/tools
    
    $DOTNET tool install --tool-path $DOTNET_TOOLS --ignore-failed-sources dotnet-format --version 3.1.37601
  fi

  $DOTNET_FORMAT -w $REPO_ROOT

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