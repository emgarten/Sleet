# Sleet

Sleet is a cross platform command line tool to generate NuGet v3 static feeds.

## Build Status

| AppVeyor | Travis |
| --- | --- |
| [![AppVeyor](https://ci.appveyor.com/api/projects/status/cuhdeq60c3ogy7pa?svg=true)](https://ci.appveyor.com/project/emgarten/sleet) | [![Travis](https://travis-ci.org/emgarten/Sleet.svg?branch=master)](https://travis-ci.org/emgarten/Sleet) |

## Getting Sleet

* [Github releases](https://github.com/emgarten/Sleet/releases/latest)
* [NuGet package](https://www.nuget.org/packages/Sleet)
* [Nightly build](https://www.myget.org/F/sleet/api/v2/package/Sleet/)

## Features
* Add and remove packages from a feed.
* Fast and stable - Sleet uses compressed static files.
* Azure storage support - Feeds can work directly with an azure storage account.
* Local folder support - Feeds can be written to disk and hosted with a web server to support authentication. 

## Supported clients
* [NuGet 3.4.0+](https://www.nuget.org/downloads)
* [.NET Core tools](https://www.microsoft.com/net/core)

## Coding
This solution uses .NET Core, get the tools [here](http://dot.net/).

### License
[MIT License](https://github.com/emgarten/Sleet/blob/master/LICENSE.md)

# Quick start

Download the latest release from [github](https://github.com/emgarten/Sleet/releases/latest).

#### Windows
On Windows use *Sleet.exe*

#### Cross platform
OSX, Linux, and other OSes download and extract *Sleet.tar.gz*. To use Sleet run ``dotnet Sleet.dll``

### Creating a config

Create a *sleet.json* config file to define a new package feed hosted on azure storage.

``sleet createconfig --azure``

Edit *sleet.json* using your editor of choice to set the url of your storage account and the connection string.

``notepad sleet.json``

```json
{
  "sources": [
    {
      "name": "feed",
      "type": "azure",
      "path": "https://yourStorageAccount.blob.core.windows.net/feed/",
      "container": "feed",
      "connectionString": "DefaultEndpointsProtocol=https;AccountName=;AccountKey=;BlobEndpoint="
    }
  ]
}
```

### Initialize the feed

Now initialize the feed, this creates the basic files needed to get started. The source value here corresponds to the *name* property used in *sleet.json*.

``sleet init --source feed``

### Adding packages

Add packages to the feed with the push command, this can be used with either a path to a single nupkg or a folder of nupkgs.

``sleet push d:\nupkgsToPush --source feed``

### Using the feed

Add the feed as a source to your NuGet.Config file. In the example above the package source URL is ``https://yourStorageAccount.blob.core.windows.net/feed/index.json``

### Full guide

Check out the full getting started guide [here](http://emgarten.com/2016/04/25/how-to-host-a-nuget-v3-feed-on-azure-storage/).


