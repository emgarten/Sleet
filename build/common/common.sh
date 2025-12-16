#!/usr/bin/env bash

# Helper function to run a command with logging
run_command()
{
  echo ">> $@"
  "$@"
}

run_standard_tests()
{
  pushd $(pwd)

  # Download dotnet cli
  REPO_ROOT=$(pwd)
  DOTNET=$(pwd)/.cli/dotnet

  if [ ! -f $DOTNET ]; then
    echo ""
    echo "===> Installing .NET SDK..."
    echo ""
    mkdir -p .cli
    run_command curl -L -o .cli/dotnet-install.sh https://dot.net/v1/dotnet-install.sh

    # Run install.sh
    chmod +x .cli/dotnet-install.sh
    run_command .cli/dotnet-install.sh -i .cli --channel 8.0
    run_command .cli/dotnet-install.sh -i .cli --channel 9.0
    run_command .cli/dotnet-install.sh -i .cli --channel 10.0
  fi

  # Display info
  echo ""
  echo "===> Displaying .NET SDK info..."
  echo ""
  run_command $DOTNET --info

  # clean
  echo ""
  echo "===> Cleaning artifacts directory..."
  echo ""
  run_command rm -r -f $(pwd)/artifacts

  # Clean projects and write out git info
  echo ""
  echo "===> Cleaning projects and writing git info..."
  echo ""
  run_command $DOTNET msbuild build/build.proj /t:Clean\;WriteGitInfo /p:Configuration=Release /nologo /v:m

  if [ $? -ne 0 ]; then
    echo "Clean;WriteGitInfo FAILED!"
    exit 1
  fi

  # restore
  echo ""
  echo "===> Restoring NuGet packages..."
  echo ""
  run_command $DOTNET msbuild build/build.proj /t:Restore /p:Configuration=Release /nologo /v:m

  if [ $? -ne 0 ]; then
    echo "Restore FAILED!"
    exit 1
  fi

  # build
  echo ""
  echo "===> Building projects..."
  echo ""
  run_command $DOTNET msbuild build/build.proj /t:Build /p:Configuration=Release /nologo /v:m

  if [ $? -ne 0 ]; then
    echo "Build FAILED!"
    exit 1
  fi

  # publish
  echo ""
  echo "===> Publishing projects..."
  echo ""
  run_command $DOTNET msbuild build/build.proj /t:Publish /p:Configuration=Release /nologo /v:m

  if [ $? -ne 0 ]; then
    echo "Publish FAILED!"
    exit 1
  fi

  # pack
  echo ""
  echo "===> Creating NuGet packages..."
  echo ""
  run_command $DOTNET msbuild build/build.proj /t:Pack /p:Configuration=Release /nologo /v:m

  if [ $? -ne 0 ]; then
    echo "Pack FAILED!"
    exit 1
  fi

  # test
  echo ""
  echo "===> Running tests..."
  echo ""
  
  # Find all solution files in the repo root and run dotnet test on each
  SLN_FILES=$(find "$REPO_ROOT" -maxdepth 1 -name "*.sln")
  
  if [ -z "$SLN_FILES" ]; then
    echo "No solution files found in $REPO_ROOT. Missing solution for tests!"
    exit 1
  fi
  
  for sln in $SLN_FILES; do
    echo "Running tests for solution: $sln"
    run_command $DOTNET test "$sln" --configuration Debug
    
    if [ $? -ne 0 ]; then
      echo "Test FAILED for $sln!"
      exit 1
    fi
  done

  popd
}
