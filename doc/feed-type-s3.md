# Creating an Amazon S3 feed

This guide is used to setup a new feed hosted on Amazon S3 storage.

## Creating a config for Amazon S3 feed

Create a `sleet.json` config file to define a new package feed hosted on Amazon S3 storage.

``sleet createconfig --s3``

Edit `sleet.json` using your editor of choice to set the url of your s3 bucket and access key.

``notepad sleet.json``

### Using an AWS credentials file

```json
{
  "sources": [
    {
      "name": "feed",
      "type": "s3",
      "path": "https://s3.amazonaws.com/my-bucket-feed/",
      "profileName": "sleetProfile",
      "bucketName": "my-bucket-feed",
      "region": "us-west-2",
      "baseURI": "https://tempuri.org/",
      "feedSubPath": "a/b/c/"
    }
  ]
}
```

For details on creating a credentials file go [here](https://docs.aws.amazon.com/sdk-for-net/v2/developer-guide/net-dg-config-creds.html#creds-file)

### Using accessKeyId and secretAccessKey in sleet.json

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
      "secretAccessKey": "IAM_SECRET_ACCESS_KEY",
      "baseURI": "https://tempuri.org/",
      "feedSubPath": "a/b/c/"
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

Add the feed as a source to your `NuGet.Config` file. In the example above the package source URL is ``https://s3.amazonaws.com/my-bucket-feed/index.json``
