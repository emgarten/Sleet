# Creating a MinIO S3 feed

This guide is used to setup a new feed hosted on [MinIO](https://min.io/) S3 storage.

## Creating a config for Minio S3 feed

Create a `sleet.json` config file to define a new package feed hosted on MinIO S3 storage.

``sleet createconfig --minio``

Edit `sleet.json` using your editor of choice to set the url of your MinIO s3 bucket and access key.

*Windows users*
``notepad sleet.json``

*Mac/Linux users*
``vi sleet.json``

### Using accessKeyId and secretAccessKey in sleet.json
This configuration strategy specifies the access key id and secret key directly in sleet.json. Replace `IAM_ACCESS_KEY_ID` and `IAM_SECRET_ACCESS_KEY` with the valid values.

*WARNING: Although you can specify IAM secrets in the feed configuration file, it is probably not the most secure solution. If you do choose this option you must secure this file and treat it like any other secret.*

*sleet.json schema*
```json
{
  "sources": [
    {
      "name": "feed",
      "type": "minio",
      "serviceURL": "MINIO_SERVER_URL",
      "bucketName": "MINIO_BUCKET_NAME",
      "region": "MINIO_REGION_NAME",
      "accessKeyId": "IAM_ACCESS_KEY_ID",
      "secretAccessKey": "IAM_SECRET_ACCESS_KEY"
    }
  ]
}
```

*Example local configuration*
```json
{
  "sources": [
    {
      "name": "feed",
      "type": "minio",
      "serviceURL": "http://localhost:9000",
      "bucketName": "my-bucket-feed",
      "region": "us-east-1",
      "accessKeyId": "Q3AM3UQ867SPQQA43P2F",
      "secretAccessKey": "zuf+tfteSlswRu7BJ86wekitnifILbZam1KYY3TG"
    }
  ]
}
```

### Using AWS_* environment variables and sleet.config
To use [AWS environment variables](https://docs.aws.amazon.com/cli/latest/userguide/cli-configure-envvars.html) create a `minio` feed config without an `accessKeyId` and `secretAccessKey`. Sleet will attempt to automatically configure the feed based on the environment.

*sleet.json schema*
```json
{
  "sources": [
    {
      "name": "feed",
      "type": "minio",
      "serviceURL": "MINIO_SERVER_URL",
      "bucketName": "MINIO_BUCKET_NAME",
      "region": "MINIO_REGION_NAME"
    }
  ]
}
```

```terminal
export AWS_ACCESS_KEY_ID="IAM_ACCESS_KEY_ID"
export AWS_SECRET_ACCESS_KEY="IAM_SECRET_ACCESS_KEY"
```

*Example local configuration*
```json
{
  "sources": [
    {
      "name": "feed",
      "type": "minio",
      "serviceURL": "http://localhost:9000",
      "bucketName": "my-bucket-feed",
      "region": "us-east-1",

    }
  ]
}
```

```terminal
export AWS_ACCESS_KEY_ID="Q3AM3UQ867SPQQA43P2F"
export AWS_SECRET_ACCESS_KEY="zuf+tfteSlswRu7BJ86wekitnifILbZam1KYY3TG"
```

*NOTE: Setting secrets in environment variables can be a very secure strategy. There are many ways to configure environment variables, some more secure than others.*


### Additional feed settings

Help on additional feed settings such as *baseURI* and *feedSubPath* can be found under [Sleet client settings](client-settings.md)

## Initializing the feed

For a new feed the first push will do the following:

* Create the bucket and set the policy to public read-only
* Initialize the feed with the default settings

If the bucket already exists the policy will *not* be modified. Private feeds should set up the bucket before pushing for the first time.

To create a feed with custom feed settings, such as with a catalog or symbols feed, use the `init` first.

## Using the feed

Add the feed as a source to your `NuGet.Config` file. In the example above the package source URL is ``https://localhost:9000/my-bucket-feed/index.json``

## Adding packages

Add packages to the feed with the push command, this can be used with either a path to a single nupkg or a folder of nupkgs.

``sleet push d:\nupkgsToPush``

## Additional references
* [Getting Started with MinIO Server](getting-started-with-minio.md)

### External references
* [AWS environment variables](https://docs.aws.amazon.com/cli/latest/userguide/cli-configure-envvars.html)
* [MinIO website](https://min.io/)
* [Official MinIO documentation](https://docs.min.io/)
* [Official MinIO Docker Quickstart](https://docs.min.io/docs/minio-docker-quickstart-guide.html)
* [Official MinIO Docker image on Dockerhub](https://hub.docker.com/r/minio/minio)