# What is Sleet?

Sleet is a static NuGet package feed generator.

* **Serverless**. Create static feeds directly on *Azure Storage* or *Amazon S3*. No compute required.
* **Cross platform**. Sleet is built in .NET, it can run on *.NET Framework*, *Mono*, or [dotnet CLI](https://github.com/dotnet/cli)
* **Fast.** Static feeds are created using the [NuGet v3 feed format](https://docs.microsoft.com/en-us/nuget/api/overview).
* **Symbol server.** Assemblies and pdb files from packages are automatically indexed and provided as a [symbol server](https://msdn.microsoft.com/en-us/library/windows/desktop/ms680693.aspx).
* **Simple.** Sleet is a simple command line tool that can add, remove, and update packages.
* **Flexible.** Feeds can be written to disk and hosted with a web server to support authentication. Use the command line tool or a library to run Sleet programmatically.

## Getting Sleet

### Manually getting sleet.exe (Windows and Mono)
1. Download the latest nupkg from [NuGet.org](https://www.nuget.org/packages/Sleet)
1. Extract *tools/Sleet.exe* to a local folder and run it.

### Install global tool (dotnet CLI >= 2.1.300-preview2)
1. `dotnet tool install -g sleet`
1. `sleet` should now be on your *PATH*

### Manually run sleet.dll (dotnet CLI cross platform)
1. Download the latest nupkg from [NuGet.org](https://www.nuget.org/packages/Sleet)
1. Extract the nupkg to a local folder
1. `dotnet <PathToNupkg>/tools/netcoreapp2.0/any/Sleet.dll`

## Build Status

| AppVeyor | Travis | Visual Studio Online |
| --- | --- | --- |
| [![AppVeyor](https://ci.appveyor.com/api/projects/status/cuhdeq60c3ogy7pa?svg=true)](https://ci.appveyor.com/project/emgarten/sleet) | [![Travis](https://travis-ci.org/emgarten/Sleet.svg?branch=master)](https://travis-ci.org/emgarten/Sleet) | [![VSO](https://hackamore.visualstudio.com/_apis/public/build/definitions/abbff132-0981-4267-a80d-a6e7682a75a9/2/badge)](https://github.com/emgarten/sleet) |

## CI builds

CI builds are located on the following NuGet feed:

``https://nuget.blob.core.windows.net/packages/index.json``

The list of packages on this feed is [here](https://nuget.blob.core.windows.net/packages/sleet.packageindex.json).

# Quick start

#### Windows
On Windows use *Sleet.exe*

#### Cross platform
OSX, Linux, and other OSes download and extract *Sleet.tar.gz*. To use Sleet run ``dotnet Sleet.dll``

## Creating an azure feed

This guide is used to setup a new feed hosted on azure storage.

### Creating a config for azure feed

Create a `sleet.json` config file to define a new package feed hosted on azure storage.

``sleet createconfig --azure``

Edit `sleet.json` using your editor of choice to set the url of your storage account and the connection string.

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

Now initialize the feed, this creates the basic files needed to get started. The `source` value here corresponds to the `name` property used in `sleet.json`.

``sleet init --source feed``

### Adding packages

Add packages to the feed with the push command, this can be used with either a path to a single nupkg or a folder of nupkgs.

``sleet push d:\nupkgsToPush --source feed``

### Using the feed

Add the feed as a source to your `NuGet.Config` file. In the example above the package source URL is ``https://yourStorageAccount.blob.core.windows.net/feed/index.json``

## Creating an Amazon S3 feed

This guide is used to setup a new feed hosted on Amazon S3 storage.

### Creating a config for Amazon S3 feed

Create a `sleet.json` config file to define a new package feed hosted on azure storage.

``sleet createconfig --s3``

Edit `sleet.json` using your editor of choice to set the url of your s3 bucket and access key.

``notepad sleet.json``

```json
{
  "sources": [
    {
      "name": "feed",
      "type": "s3",
      "path": "https://s3.amazonaws.com/my-bucket-feed/",
      "bucketName": "my-bucket-feed",
      "region": "us-east-1",
      "accessKeyId": "IAM_ACCESS_KEY_ID",
      "secretAccessKey": "IAM_SECRET_ACCESS_KEY"
    }
  ]
}
```

### Initialize the feed

Now initialize the feed, this creates the basic files needed to get started. The `source` value here corresponds to the `name` property used in `sleet.json`.

``sleet init --source feed``

### Adding packages

Add packages to the feed with the push command, this can be used with either a path to a single nupkg or a folder of nupkgs.

``sleet push d:\nupkgsToPush --source feed``

### Using the feed

Add the feed as a source to your `NuGet.Config` file. In the example above the package source URL is ``https://s3.amazonaws.com/my-bucket-feed/index.json``

## Creating a locally hosted feed

This guide is used to setup a new feed hosted on a local IIS Webserver.

### Creating a config for local feed

Create a `sleet.json` config file to define a new package feed hosted on IIS.

``sleet createconfig --local``


Open `sleet.json` using your editor of choice, the file will look like similar to this

``notepad sleet.json``

```json
{
  "username": "",
  "useremail": "",
  "sources": [
    {
      "name": "myLocalFeed",
      "type": "local",
      "path": "C:\\myFeed"
    }
  ]
}
```

Edit the file so that `path` contains the address of your webserver and the URI users will map to use the feed.

For example, if you want the mapped feed address to be `https://example.com/feed/index.json` change `path` to:

```json
    "path": "https://example.com/feed"
```

### Initialize the feed

Now initialize the feed, this creates the basic files needed to get started.

* The `config` value here corresponds to the filesystem path to the `sleet.json` file.
* the `source` value here corresponds to the `name` property used in `sleet.json`

``sleet init --config C:\sleet.json --source myLocalFeed``

Sleet will create files for the feed in a new directory corresponding to the URI set in *path*, so if you changed *path* to `https://example.com/feed`,
the files will be created in a directory named feed on you `C:\` drive.

### Adding packages

Add packages to the feed with the push command, this can be used with either a path to a single nupkg or a folder of nupkgs.

``sleet push --config C:\sleet.json -s myLocalFeed C:\PackagesFolder``

### Creating the feed's ASP.NET project

Create an empty ASP.NET Website project.

In the projects' `web.config` file add the following lines:

```xml
<configuration>
   <system.webServer>
      <staticContent>
          <mimeMap fileExtensions=".nupkg" mimeType="application/zip"/>
          <mimeMap fileExtension="." mimeType="application/json"/>
      </staticContent>
   </system.webServer>
</configuration>
```

### Uploading the feed to IIS

Publish your ASP.NET website to your IIS server.

Copy the entire `C:\feed` directory to a path on your IIS server (including all subfolders).

### Exposing the feed with IIS

In `Internet Information Services Manager` open your website, right click and choose `Add Virtual Directory`

* In `Alias` enter the URI you want to expose - in our example it's `feed`
* In `Physical Path` enter the path on the server you copied your `C:\feed` directory to.

### Using the feed

Add the feed as a source to your NuGet.Config file. In the example above the package source URL is ``https://example.com/feed/index.json``

### Full guide

Check out the full getting started guide [here](http://emgarten.com/2016/04/25/how-to-host-a-nuget-v3-feed-on-azure-storage/).

### Related projects

* [Sleet.Azure](https://github.com/kzu/Sleet.Azure) provides MSBuild props/targets for running Sleet.

### License

[MIT License](https://github.com/emgarten/Sleet/blob/master/LICENSE.md)
