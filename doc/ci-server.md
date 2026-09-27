# Integration with CI Server

It is really common to publish nuget package by CI flow.

You can check this out for "How to publish your nuget packages by CI to the sleet supported server".

## Build and push by GitHub Action

Github Action is free and common CI/CD tools to developer.

Now we take example to publish some nuget package in Github Action.

You can make it ok with other CI/CD tools like Jeknins, Gitlab Jobs, etc.

Now, we are going to publish nuget package to Azure by Github Action.

You can create a yml file named 'push_nuget_to_azure_by_sleet.yml' at `.github/workflows` in your GitHub repository.

And type something as below:

```yml
name: Publish dev nuget package to azure

on:
  push:
    branches:
      - v*
```

It will run this action if you push code to the branch named like `v*`. e.g. `v1`, `v2`

And,

```yml
jobs:
  build:
    runs-on: ubuntu-latest

    steps:
      - uses: actions/checkout@v4
      - name: pack
        env:
          NUGET_PACKAGE_VERSION: 1.0.0
        run: |
          cd $GITHUB_WORKSPACE/src
          mkdir pkgs
          dotnet pack --configuration Release -o ./pkgs -p:PackageVersion=$NUGET_PACKAGE_VERSION
      - name: Push nuget package to Azure storage
        env:
          SLEET_FEED_TYPE: azure
          SLEET_FEED_CONTAINER: feed
          SLEET_FEED_CONNECTIONSTRING: ${{secrets.SLEET_CONNECTIONSTRING}}
        run: |
          cd $GITHUB_WORKSPACE/src
          dotnet tool install -g sleet --version "7.*"
          sleet push ./pkgs --skip-existing
```

It means:

1. It will run CI action in ubuntu-latest OS
2. Step named `pack` will `cd` to the `src` directory and try to pack nuget packages versioned `1.0.0`
3. Step named `Push nuget package to Azure storage` will publish packages placed in `pkgs` directory to azure
4. \${{secrets.SLEET_CONNECTIONSTRING}} is a secret, you can add it in you repository settings tab.

Put it together as below:

```yml
name: Publish dev nuget package to azure

on:
  push:
    branches:
      - v*

jobs:
  build:
    runs-on: ubuntu-latest

    steps:
      - uses: actions/checkout@v4
      - name: pack
        env:
          NUGET_PACKAGE_VERSION: 1.0.0
        run: |
          cd $GITHUB_WORKSPACE/src
          mkdir pkgs
          dotnet pack --configuration Release -o ./pkgs -p:PackageVersion=$NUGET_PACKAGE_VERSION
      - name: Push nuget package to Azure storage
        env:
          SLEET_FEED_TYPE: azure
          SLEET_FEED_CONTAINER: feed
          SLEET_FEED_CONNECTIONSTRING: ${{secrets.SLEET_CONNECTIONSTRING}}
        run: |
          cd $GITHUB_WORKSPACE/src
          dotnet tool install -g sleet --version "7.*"
          sleet push ./pkgs --skip-existing
```

### Some tips

#### Pin the Sleet version

The examples above pin Sleet to a major version with `--version "7.*"`, so a new major release can't change your build unexpectedly.

To pin an exact version, use a [local tool manifest](https://learn.microsoft.com/dotnet/core/tools/local-tools-how-to-use) instead. Run `dotnet new tool-manifest` and `dotnet tool install sleet` in your repository, commit the *dotnet-tools.json* file they create, and replace the install step with:

```yml
        run: |
          cd $GITHUB_WORKSPACE/src
          dotnet tool restore
          dotnet sleet push ./pkgs --skip-existing
```

#### Please pack nuget package to out it to specific directory

Maybe you publish nuget package via `dotnet` or `nuget` command as script below:

```shell
dotnet nuget push **/*.nupkg
```

Unfortunately, It is not support to publish package via `sleet push **/*.nupkg`.

So, please pack your nuget package to some directory like `pkg` as example above.
