# Creating a locally hosted feed

This guide is used to setup a new feed hosted on a local IIS Webserver.

## Creating a config for local feed

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
      "path": "C:\\myFeed",
      "baseURI": "https://example.com/feed/"
    }
  ]
}
```

Set `path` to the local directory on disk where the feed json files will be written.

Change `baseURI` to the URI the http server will use to serve the feed.

## Initialize the feed

Now initialize the feed, this creates the basic files needed to get started.

* The `config` value here corresponds to the filesystem path to the `sleet.json` file.
* the `source` value here corresponds to the `name` property used in `sleet.json`

``sleet init --config C:\sleet.json --source myLocalFeed``

## Adding packages

Add packages to the feed with the push command, this can be used with either a path to a single nupkg or a folder of nupkgs.

``sleet push --config C:\sleet.json -s myLocalFeed C:\PackagesFolder``

## Creating the feed's ASP.NET project

Create an empty ASP.NET Website project.

In the projects' `web.config` file add the following lines:

```xml
<configuration>
   <system.webServer>
      <staticContent>
          <mimeMap fileExtension=".nupkg" mimeType="application/zip"/>
          <mimeMap fileExtension="." mimeType="application/json"/>
      </staticContent>
   </system.webServer>
</configuration>
```

## Uploading the feed to IIS

Publish your ASP.NET website to your IIS server.

Copy the entire local feed output folder to a path on your IIS server (including all subfolders).

## Exposing the feed with IIS

In `Internet Information Services Manager` open your website, right click and choose `Add Virtual Directory`

* In `Alias` enter the URI you want to expose - in our example it's `feed`
* In `Physical Path` enter the path on the server you copied your `path` output directory to.

## Using the feed

Add the feed as a source to your NuGet.Config file. In the example above the package source URL is ``https://example.com/feed/index.json``
