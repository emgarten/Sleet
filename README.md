# Sleet

A static NuGet package feed generator. ☁️ + 📦 = ❄️

[![NuGet](https://img.shields.io/nuget/v/sleet.svg)](https://www.nuget.org/packages/sleet) [![.NET test](https://github.com/emgarten/Sleet/actions/workflows/dotnet.yml/badge.svg)](https://github.com/emgarten/Sleet/actions/workflows/dotnet.yml) [![Functional Tests](https://github.com/emgarten/Sleet/actions/workflows/functional.yml/badge.svg)](https://github.com/emgarten/Sleet/actions/workflows/functional.yml)

## Table of Contents

- [Sleet](#sleet)
  - [Table of Contents](#table-of-contents)
  - [Features](#features)
  - [Why use static feeds?](#why-use-static-feeds)
  - [Getting Sleet](#getting-sleet)
    - [Install as a dotnet global tool (recommended)](#install-as-a-dotnet-global-tool-recommended)
    - [Manually getting sleet.exe](#manually-getting-sleetexe)
    - [Using SleetLib as a library](#using-sleetlib-as-a-library)
  - [Quick start](#quick-start)
  - [Commands](#commands)
  - [Documentation](#documentation)
    - [Quick start guides](#quick-start-guides)
  - [Contributing](#contributing)
  - [History](#history)
    - [How was sleet named?](#how-was-sleet-named)
  - [Related projects](#related-projects)
  - [License](#license)

## Features

* **Serverless.** Create static feeds directly on *Azure Storage*, *Amazon S3*, or any S3-compatible storage (MinIO, Yandex Cloud, Scaleway, etc.). No compute required.
* **Cross platform.** Sleet is built in .NET and runs anywhere the [dotnet CLI](https://github.com/dotnet/cli) is supported — Linux, macOS, and Windows.
* **Fast.** Static feeds use the [NuGet v3 feed format](https://docs.microsoft.com/en-us/nuget/api/overview) so clients resolve packages with simple HTTP requests.
* **Simple.** A straightforward command line tool to add, remove, and update packages.
* **Flexible.** Configure credentials via files, environment variables, command line args, .netconfig, or AWS-specific patterns to fit any workflow.
* **Package retention.** Automatically prune old package versions with configurable stable/prerelease limits and release label grouping.
* **Version badges.** Generate [shields.io](https://shields.io/)-compatible version badges for your packages.
* **External search.** Plug in a custom search endpoint for dynamic query results.
* **Cache control.** Set CDN-friendly `Cache-Control` headers for immutable and mutable feed files.

## Why use static feeds?

* Package binaries are typically kept outside of git repos — static feeds provide a long term storage solution that can be paired with checked in code.
* NuGet feeds are read for restore far more often than they are updated.
* Cloud storage accounts are a cheap and secure way to share nupkgs for public or private feeds.
* You keep full control of your packages.

## Getting Sleet

Sleet requires [.NET 8.0](https://dotnet.microsoft.com/download) or later.

### Install as a dotnet global tool (recommended)

```
dotnet tool install -g sleet
```

`sleet` should now be on your *PATH*.

### Manually getting sleet.exe

1. Download the latest SleetExe nupkg from [NuGet.org](https://www.nuget.org/packages/SleetExe).
1. Extract *tools/Sleet.exe* to a local folder and run it.

### Using SleetLib as a library

Install the [SleetLib](https://www.nuget.org/packages/SleetLib) NuGet package to access Sleet functionality programmatically from your own .NET applications.

## Quick start

Create a feed on Azure Storage in three steps:

```bash
# 1. Generate a config file
sleet createconfig --azure

# 2. Edit sleet.json with your storage account URL and credentials

# 3. Push packages (the feed is created automatically on first push)
sleet push mypackage.1.0.0.nupkg
```

For other storage backends see the guides below.

## Commands

| Command | Description |
| --- | --- |
| `createconfig` | Create a new sleet.json config file |
| `init` | Initialize a new feed |
| `push` | Push packages to a feed |
| `delete` | Delete a package from a feed |
| `stats` | Display package count on a feed |
| `validate` | Verify all packages and resources are valid |
| `download` | Download all packages from a feed to a local folder |
| `destroy` | Delete all files from a feed |
| `recreate` | Rebuild a feed from its packages |
| `feed-settings` | Read or modify feed settings |
| `retention prune` | Apply package retention rules |
| `retention settings` | Configure package retention limits |

See [commands](doc/commands.md) for full details and options.

## Documentation

Full documentation can be found under [/doc](doc/index.md).

### Quick start guides

* [Setting up an Azure feed](doc/feed-type-azure.md)
* [Setting up an AWS S3 feed](doc/feed-type-s3.md)
* [Setting up a local feed with IIS hosting](doc/feed-type-local.md)
* [Integration with CI Server](doc/ci-server.md)
* [Setting up a private feed on AWS using S3 + CloudFront + Lambdas](doc/private-feed-s3.md)

Also see this [getting started blog post](https://emgarten.com/posts/how-to-host-a-nuget-v3-feed-on-azure-storage) for a walkthrough.

## Contributing

We welcome contributions! If you are interested in contributing to Sleet, report an issue or open a pull request to propose a change.

To build and run tests locally:

```bash
# Linux / macOS
./build.sh

# Windows
./build.ps1
```

CI runs on Linux, macOS, and Windows.

## History

Sleet was created to achieve the original goals of the NuGet v3 feed format: provide maximum availability and performance for NuGet restore by using only static files.

The v3 feed format was designed to do all compute when pushing a new package since updates are infrequent compared to the number of times a package is read for restore. Static files also remove the need to run a specific server to host the feed, allowing a simple file service to handle it.

### How was sleet named?

Sleet is.. cold static packages from the cloud. ☁️ + 📦 = ❄️

## Related projects

* [Sleet.Azure](https://github.com/kzu/Sleet.Azure) provides MSBuild props/targets for running Sleet.
* [Sleet.Search](https://github.com/emgarten/Sleet.Search) provides a search service for Sleet feeds.

## License

[MIT License](https://github.com/emgarten/Sleet/blob/main/LICENSE.md)
