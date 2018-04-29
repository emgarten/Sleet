#!/usr/bin/env bash

run_standard_tests()
{
  pushd $(pwd)

  # Download dotnet cli
  DOTNET="$(pwd)/.cli/dotnet"


  if [ ! -f $DOTNET ]; then
    echo "Installing dotnet"
    mkdir -p .cli
    curl -o .cli/dotnet-install.sh https://raw.githubusercontent.com/dotnet/cli/485638376d20426a382705b9c546b47bea0da206/scripts/obtain/dotnet-install.sh

    # Run install.sh
    chmod +x .cli/dotnet-install.sh
    .cli/dotnet-install.sh -i .cli -v 2.1.300-rc1-008673
    .cli/dotnet-install.sh -i .cli -c 2.0 -v 2.0.3
    .cli/dotnet-install.sh -i .cli -c preview -v 1.0.4
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
  $DOTNET msbuild build/build.proj /t:Build\;Publish\;Test\;Pack /p:Configuration=Release /nologo /v:m

  if [ $? -ne 0 ]; then
    echo "Build FAILED!"
    exit 1
  fi

  popd
}