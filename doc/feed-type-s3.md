# Creating an Amazon S3 feed

This guide is used to setup a new feed hosted on Amazon S3 storage.

## Creating a config for Amazon S3 feed

Create a `sleet.json` config file to define a new package feed hosted on Amazon S3 storage.

``sleet createconfig --s3``

Edit `sleet.json` using your editor of choice to set the url of your s3 bucket and access key.

``notepad sleet.json``

For `.netconfig`, just create or edit the file directly in the [desired location](https://dotnetconfig.org/#what).

### Using an AWS credentials file

`sleet.json`:
```json
{
  "sources": [
    {
      "name": "feed",
      "type": "s3",
      "path": "https://s3.amazonaws.com/my-bucket-feed/",
      "profileName": "sleetProfile",
      "bucketName": "my-bucket-feed",
      "region": "us-west-2"
    }
  ]
}
```

`.netconfig`:
```gitconfig
[sleet "feed"]
    type = s3
    path = https://s3.amazonaws.com/my-bucket-feed/
    profileName = sleetProfile
    bucketName = my-bucket-feed
    region = us-west-2
```

For details on creating a credentials file go [here](https://docs.aws.amazon.com/sdk-for-net/v2/developer-guide/net-dg-config-creds.html#creds-file)

#### Using SSO profiles

If you are using an SSO profile, you must first log in using the AWS CLI before running sleet to allow SSO profiles to be used.

Sleet will not prompt for SSO login.

```
aws sso login --profile my-sso-profile
```


### Using accessKeyId and secretAccessKey in sleet.json

`sleet.json`:
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

`.netconfig`:
```gitconfig
[sleet "feed"]
    type = s3
    path = https://s3.amazonaws.com/my-bucket-feed/
    bucketName = my-bucket-feed
    region = us-east-1
    accessKeyId = IAM_ACCESS_KEY_ID
    secretAccessKey = IAM_SECRET_ACCESS_KEY
```

This example specifies the access key id and secret key directly in sleet.json/.netconfig.

### Using an EC2 instance profile

`sleet.json`:
```json
{
  "sources": [
    {
      "name": "feed",
      "type": "s3",
      "path": "https://s3.amazonaws.com/my-bucket-feed/",
      "bucketName": "my-bucket-feed",
      "region": "us-west-2"
    }
  ]
}
```

`.netconfig`:
```gitconfig
[sleet "feed"]
    type = s3
    path = https://s3.amazonaws.com/my-bucket-feed/
    bucketName = my-bucket-feed
    region = us-west-2
```

To use [AWS environment variables](https://docs.aws.amazon.com/cli/latest/userguide/cli-configure-envvars.html) create an s3 feed config without an *accessKeyId* or *secretAccessKey*. Sleet will attempt to automatically configure the feed based on the environment.

### Using S3 compatible storage

`sleet.json`:
```json
{
  "sources": [
    {
      "name": "feed",
      "type": "s3",
      "path": "https://nupkg.website.yandexcloud.net/",
      "bucketName": "nupkg",
      "serviceURL": "https://storage.yandexcloud.net",
      "accessKeyId": "IAM_ACCESS_KEY_ID",
      "secretAccessKey": "IAM_SECRET_ACCESS_KEY"
    }
  ]
}
```

`.netconfig`:
```gitconfig
[sleet "feed"]
    type = s3
    path = https://nupkg.website.yandexcloud.net/
    bucketName = nupkg
    serviceURL = https://storage.yandexcloud.net
    accessKeyId = IAM_ACCESS_KEY_ID
    secretAccessKey = IAM_SECRET_ACCESS_KEY
```

To use S3 compatible storage create an s3 feed config with *serviceURL* instead of *region*.

### Additional feed settings

Help on additional feed settings such as *baseURI* and *feedSubPath* can be found under [Sleet client settings](client-settings.md)

## Adding packages

Add packages to the feed with the push command, this can be used with either a path to a single nupkg or a folder of nupkgs.

``sleet push d:\nupkgsToPush``

## Initializing the feed

For a new feed the first push will do the following:

* Create the bucket and set the policy to public read-only
* Initialize the feed with the default settings

If the bucket already exists the policy will *not* be modified. Private feeds should set up the bucket before pushing for the first time.

To create a feed with custom feed settings, such as with a catalog or symbols feed, use the `init` first.

## Using the feed

Add the feed as a source to your `NuGet.Config` file. In the example above the package source URL is ``https://s3.amazonaws.com/my-bucket-feed/index.json``

## Creating a private S3 feed

Private feeds can be created by creating a lambda function to authenticate clients. 

For help setting up S3 go [here](private-feed-s3.md)

In *sleet.json* set *baseURI* for the feed to the CloudFront address, this will write the CloudFront URI to the feed json files instead of the restricted S3 bucket which the client cannot access.

