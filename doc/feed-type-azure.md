# Creating an azure feed

This guide is used to setup a new feed hosted on azure storage.

## Creating a config for azure feed

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
      "container": "feed",
      "connectionString": "DefaultEndpointsProtocol=https;AccountName=;AccountKey=;BlobEndpoint="
    }
  ]
}
```

For `.netconfig`, just create or edit the file directly in the [desired location](https://dotnetconfig.org/#what):

```gitconfig
[sleet "feed"]
    type = azure
    container = feed
    connectionString = "DefaultEndpointsProtocol=https;AccountName=;AccountKey=;BlobEndpoint="
```

## Adding packages

Add packages to the feed with the push command, this can be used with either a path to a single nupkg or a folder of nupkgs.

``sleet push d:\nupkgsToPush``

## Initializing the feed

For a new feed the first push will do the following:

* Create the container and set access to public read
* Initialize the feed with the default settings

If the container already exists the access will *not* be modified. Private feeds should set up the container before pushing for the first time.

To create a feed with custom feed settings, such as with a catalog or symbols feed, use the `init` first.

## Using the feed

Add the feed as a source to your `NuGet.Config` file. In the example above the package source URL is ``https://yourStorageAccount.blob.core.windows.net/feed/index.json``
