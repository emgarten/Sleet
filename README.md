# Sleet

A static NuGet package feed generator. ☁️ + 📦 = ❄️

[![NuGet](https://img.shields.io/nuget/v/sleet.svg)](https://www.nuget.org/packages/sleet) [![.NET test](https://github.com/emgarten/Sleet/actions/workflows/dotnet.yml/badge.svg)](https://github.com/emgarten/Sleet/actions/workflows/dotnet.yml) [![Functional Tests](https://github.com/emgarten/Sleet/actions/workflows/functional.yml/badge.svg)](https://github.com/emgarten/Sleet/actions/workflows/functional.yml)

Sleet creates and updates NuGet v3 feeds as static files on Azure Blob Storage, Amazon S3, S3-compatible storage, or a local folder. NuGet clients restore packages straight from those files, so there is no server to run.

**Documentation: [emgarten.github.io/Sleet](https://emgarten.github.io/Sleet/)**

## Table of Contents

- [Sleet](#sleet)
  - [Table of Contents](#table-of-contents)
  - [Features](#features)
  - [Why use static feeds?](#why-use-static-feeds)
  - [Getting Sleet](#getting-sleet)
    - [Install as a dotnet global tool (recommended)](#install-as-a-dotnet-global-tool-recommended)
    - [Install as a local tool](#install-as-a-local-tool)
    - [Run without installing](#run-without-installing)
    - [Sleet.exe for Windows](#sleetexe-for-windows)
    - [Using SleetLib as a library](#using-sleetlib-as-a-library)
  - [Quick start](#quick-start)
  - [Commands](#commands)
  - [Documentation](#documentation)
  - [Contributing](#contributing)
  - [History](#history)
    - [How was sleet named?](#how-was-sleet-named)
  - [Related projects](#related-projects)
  - [License](#license)

## Features

* **Serverless.** Create static feeds directly on *Azure Storage*, *Amazon S3*, or any [S3-compatible storage](doc/s3-compatible.md) (Cloudflare R2, MinIO, Backblaze B2, etc.). No compute required.
* **Cross platform.** Sleet is built in .NET and runs on Linux, macOS, and Windows.
* **Fast.** Static feeds use the [NuGet v3 feed format](https://learn.microsoft.com/en-us/nuget/api/overview) so clients resolve packages with simple HTTP requests.
* **Simple.** A straightforward command line tool to add, remove, and update packages.
* **Flexible.** Configure credentials via [files](doc/client-settings.md), [environment variables](doc/environment-variables.md), command line args, .netconfig, or AWS-specific patterns to fit any workflow.
* **Symbol server.** Serve `.pdb` files to debuggers from the same feed. See [symbol server](doc/symbol-server.md).
* **Package retention.** Automatically prune old package versions with configurable stable/prerelease limits and release label grouping. See [package retention](doc/retention.md).
* **Version badges.** Generate [shields.io](https://shields.io/)-compatible [version badges](doc/badges.md) for your packages.
* **External search.** Plug in a custom [search endpoint](doc/external-search.md) for dynamic query results.
* **Cache control.** Set CDN-friendly `Cache-Control` headers for immutable and mutable feed files. See [CDN and caching](doc/cdn-caching.md).

## Why use static feeds?

* Package binaries are typically kept outside of git repos — static feeds provide a long term storage solution that can be paired with checked in code.
* NuGet feeds are read for restore far more often than they are updated.
* Cloud storage accounts are a cheap and secure way to share nupkgs for public or private feeds.
* You keep full control of your packages.

## Getting Sleet

Sleet requires the [.NET 8 SDK](https://dotnet.microsoft.com/download) or later. See [Install Sleet](doc/install.md) for all options.

### Install as a dotnet global tool (recommended)

```bash
dotnet tool install -g sleet
```

`sleet` should now be on your *PATH*.

In CI scripts, pin a major version so a new major release can't change your build unexpectedly:

```bash
dotnet tool install -g sleet --version "7.*"
```

### Install as a local tool

A [local tool manifest](https://learn.microsoft.com/dotnet/core/tools/local-tools-how-to-use) pins an exact Sleet version for a repository:

```bash
dotnet new tool-manifest
dotnet tool install sleet
```

Commit the *dotnet-tools.json* file it creates, then run `dotnet tool restore` and use `dotnet sleet`.

### Run without installing

With the .NET 10 SDK or later, [dnx](https://learn.microsoft.com/dotnet/core/tools/dotnet-tool-exec) downloads and runs Sleet in one step:

```bash
dnx sleet createconfig
```

### Sleet.exe for Windows

The [SleetExe](https://www.nuget.org/packages/SleetExe) package contains a self-contained *Sleet.exe* for Windows x64 that doesn't need .NET installed.

1. Download the latest SleetExe nupkg from [NuGet.org](https://www.nuget.org/packages/SleetExe).
1. Extract *tools/Sleet.exe* to a local folder and run it.

### Using SleetLib as a library

Install the [SleetLib](https://www.nuget.org/packages/SleetLib) NuGet package to access Sleet functionality programmatically from your own .NET applications. See [SleetLib](doc/sleetlib.md).

## Quick start

Create a feed on Azure Storage in three steps:

```bash
# 1. Generate a config file
sleet createconfig --azure

# 2. Edit sleet.json with your storage account URL and credentials

# 3. Push packages (the feed is created automatically on first push)
sleet push mypackage.1.0.0.nupkg
```

Then add the feed's `index.json` URL as a package source in NuGet. See [Use a feed with NuGet](doc/consume-feed.md).

For other storage backends see the [documentation](#documentation).

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

The documentation is published at [emgarten.github.io/Sleet](https://emgarten.github.io/Sleet/). The source files are in [/doc](doc/index.md).

Get started:

* [Install Sleet](doc/install.md)
* [Create an Azure feed](doc/feed-type-azure.md)
* [Create an Amazon S3 feed](doc/feed-type-s3.md)
* [Create a local feed](doc/feed-type-local.md)
* [Use a feed with NuGet](doc/consume-feed.md)

Guides:

* [Azure authentication](doc/auth-azure.md) and [AWS authentication](doc/auth-aws.md)
* [Publish from CI](doc/ci-server.md)
* [Private feeds](doc/private-feeds.md), including a [private feed on AWS](doc/private-feed-s3.md) with S3, CloudFront, and Lambda@Edge
* [S3-compatible storage](doc/s3-compatible.md), [web servers and static hosting](doc/static-hosting.md), [CDN and caching](doc/cdn-caching.md), and [multiple feeds](doc/multiple-feeds.md)
* [Package retention](doc/retention.md) and [backup and migration](doc/backup-migration.md)

Reference and help:

* [Commands](doc/commands.md), [configuration](doc/client-settings.md), [environment variables](doc/environment-variables.md), and [feed settings](doc/feed-settings.md)
* [How Sleet works](doc/how-it-works.md), [feed locking](doc/locking.md), and [limitations](doc/limitations.md)
* [Troubleshooting](doc/troubleshooting.md) and [FAQ](doc/faq.md)
* [Release notes](ReleaseNotes.md)

Also see this [getting started blog post](https://emgarten.com/posts/how-to-host-a-nuget-v3-feed-on-azure-storage) for a walkthrough.

## Contributing

We welcome contributions! If you are interested in contributing to Sleet, report an issue or open a pull request to propose a change. See [CONTRIBUTING.md](CONTRIBUTING.md) for how to build, test, and preview the documentation.

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
