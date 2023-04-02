## Build Status

| AppVeyor | Travis | Azure Pipelines |
| --- | --- | --- |
| [![AppVeyor](https://ci.appveyor.com/api/projects/status/cuhdeq60c3ogy7pa?svg=true)](https://ci.appveyor.com/project/emgarten/sleet) | [![VSO](https://hackamore.visualstudio.com/_apis/public/build/definitions/abbff132-0981-4267-a80d-a6e7682a75a9/2/badge)](https://github.com/emgarten/sleet) |

# What is Sleet?

Sleet is a static NuGet package feed generator.

* **Serverless**. Create static feeds directly on *Azure Storage*, *Amazon S3* or another S3 compatible storage. No compute required.
* **Cross platform**. Sleet is built in .NET, it can run on *.NET Framework*, *Mono*, or [dotnet CLI](https://github.com/dotnet/cli)
* **Fast.** Static feeds are created using the [NuGet v3 feed format](https://docs.microsoft.com/en-us/nuget/api/overview).
* **Simple.** Sleet is a simple command line tool that can add, remove, and update packages.
* **Flexible.** Configuration and credentials can be set using files, env vars, command line args, or AWS specific patterns to support a variety of workflows and CI builds.

## Why use static feeds?

* Package binaries are typically kept outside of git repos, static feeds provide a long term storage solution that can be paired with checked in code.
* NuGet feeds are typically read for restore far more than they are updated.
* Cloud storage accounts are a cheap and secure way to share nupkgs for public feeds.
* You keep full control of your packages.

## Getting Sleet

### Manually getting sleet.exe (Windows and Mono)
1. Download the latest SleetExe nupkg from [NuGet.org](https://www.nuget.org/packages/SleetExe)
1. Extract *tools/Sleet.exe* to a local folder and run it.

### Install dotnet global tool
1. `dotnet tool install -g sleet`
1. `sleet` should now be on your *PATH*

## Read the guides

Documentation can be found in this repo under [/doc](doc/index.md)

### Quick start guides

These provide a walk through on the basics of configuring sleet, creating, and using a feed.

* [Setting up an Azure feed](doc/feed-type-azure.md)
* [Setting up an AWS S3 feed](doc/feed-type-s3.md)
* [Setting up a local feed with IIS hosting](doc/feed-type-local.md)
* [Integration with CI Server](doc/ci-server.md)

Check out the full getting started guide [here](http://emgarten.com/2016/04/25/how-to-host-a-nuget-v3-feed-on-azure-storage/).

## CI builds

CI builds are located on the following NuGet feed:

``https://nuget.blob.core.windows.net/packages/index.json``

The list of packages on this feed is [here](https://nuget.blob.core.windows.net/packages/sleet.packageindex.json).

## Contributing

We welcome contributions. If you are interested in contributing to Sleet report an issue or open a pull request to propose a change.

## Sleet is..

Cold static packages from the cloud. ☁️ + 📦 = ❄️

## History

Sleet was created to achieve the original goals of the NuGet v3 feed format: Provide maximum availability and performance for NuGet restore by using only static files.

The v3 feed format was designed to do all compute when pushing a new package since updates are infrequent compared to the number of times a package is read for restore. Static files also remove the need to run a specific server to host the feed, allowing a simple file service to handle it.

## Related projects

* [Sleet.Azure](https://github.com/kzu/Sleet.Azure) provides MSBuild props/targets for running Sleet.
* [Sleet.Search](https://github.com/emgarten/Sleet.Search) provides a search service for Sleet feeds.

## License

[MIT License](https://github.com/emgarten/Sleet/blob/main/LICENSE.md)
