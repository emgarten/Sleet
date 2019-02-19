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

## Initialize the feed

Now initialize the feed, this creates the basic files needed to get started. The `source` value here corresponds to the `name` property used in `sleet.json`.

``sleet init --source feed``

## Adding packages

Add packages to the feed with the push command, this can be used with either a path to a single nupkg or a folder of nupkgs.

``sleet push d:\nupkgsToPush --source feed``

## Using the feed

Add the feed as a source to your `NuGet.Config` file. In the example above the package source URL is ``https://yourStorageAccount.blob.core.windows.net/feed/index.json``
