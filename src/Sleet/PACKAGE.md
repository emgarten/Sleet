# Sleet

Sleet is a static NuGet package feed generator. It creates and updates NuGet v3 feeds on Azure Storage, Amazon S3, S3-compatible storage, or a local folder. No server is needed: NuGet clients restore packages directly from the static files.

## Install

Sleet is a .NET tool and requires the .NET 8 SDK or later.

```
dotnet tool install -g sleet
```

In CI scripts, pin a major version so a new major release can't change your build unexpectedly:

```
dotnet tool install -g sleet --version "7.*"
```

To pin an exact version for a repository, use a [local tool manifest](https://learn.microsoft.com/dotnet/core/tools/local-tools-how-to-use):

```
dotnet new tool-manifest
dotnet tool install sleet
```

Commit the *dotnet-tools.json* file it creates, then run `dotnet tool restore` and use `dotnet sleet`.

With the .NET 10 SDK or later, [dnx](https://learn.microsoft.com/dotnet/core/tools/dotnet-tool-exec) runs Sleet without installing it:

```
dnx sleet createconfig
```

## Quick start

```
# Create a sleet.json config file for an Azure Storage feed
sleet createconfig --azure

# Edit sleet.json with your storage account details, then push.
# The feed is created automatically on the first push.
sleet push ./packages
```

## Documentation

* [Documentation site](https://emgarten.github.io/Sleet/)
* [Commands](https://github.com/emgarten/Sleet/blob/main/doc/commands.md)
* [Azure Storage feeds](https://github.com/emgarten/Sleet/blob/main/doc/feed-type-azure.md)
* [Amazon S3 feeds](https://github.com/emgarten/Sleet/blob/main/doc/feed-type-s3.md)
* [Local feeds](https://github.com/emgarten/Sleet/blob/main/doc/feed-type-local.md)
* [CI integration](https://github.com/emgarten/Sleet/blob/main/doc/ci-server.md)
* [Troubleshooting](https://github.com/emgarten/Sleet/blob/main/doc/troubleshooting.md)
* [Release notes](https://github.com/emgarten/Sleet/blob/main/ReleaseNotes.md)

Source code and issues: [github.com/emgarten/Sleet](https://github.com/emgarten/Sleet)
