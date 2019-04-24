## Build Status

| AppVeyor | Travis | Visual Studio Online |
| --- | --- | --- |
| [![AppVeyor](https://ci.appveyor.com/api/projects/status/cuhdeq60c3ogy7pa?svg=true)](https://ci.appveyor.com/project/emgarten/sleet) | [![Travis](https://travis-ci.org/emgarten/Sleet.svg?branch=master)](https://travis-ci.org/emgarten/Sleet) | [![VSO](https://hackamore.visualstudio.com/_apis/public/build/definitions/abbff132-0981-4267-a80d-a6e7682a75a9/2/badge)](https://github.com/emgarten/sleet) |

# What is Sleet?

Sleet is a static NuGet package feed generator.

* **Serverless**. Create static feeds directly on *Azure Storage* or *Amazon S3*. No compute required.
* **Cross platform**. Sleet is built in .NET, it can run on *.NET Framework*, *Mono*, or [dotnet CLI](https://github.com/dotnet/cli)
* **Fast.** Static feeds are created using the [NuGet v3 feed format](https://docs.microsoft.com/en-us/nuget/api/overview).
* **Symbol server.** Assemblies and pdb files from packages are automatically indexed and provided as a [symbol server](doc/symbol-server.md).
* **Simple.** Sleet is a simple command line tool that can add, remove, and update packages.
* **Flexible.** Feeds can be written to disk and hosted with a web server to support authentication. Use the command line tool or a library to run Sleet programmatically.

## Getting Sleet

### Manually getting sleet.exe (Windows and Mono)
1. Download the latest nupkg from [NuGet.org](https://www.nuget.org/packages/Sleet)
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

Check out the full getting started guide [here](http://emgarten.com/2016/04/25/how-to-host-a-nuget-v3-feed-on-azure-storage/).

## CI builds

CI builds are located on the following NuGet feed:

``https://nuget.blob.core.windows.net/packages/index.json``

The list of packages on this feed is [here](https://nuget.blob.core.windows.net/packages/sleet.packageindex.json).

## Sleet is..

Cold static packages from the cloud. ☁️ + 📦 = ❄️

## Related projects

* [Sleet.Azure](https://github.com/kzu/Sleet.Azure) provides MSBuild props/targets for running Sleet.

## License

[MIT License](https://github.com/emgarten/Sleet/blob/master/LICENSE.md)
