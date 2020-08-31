# Sleet client settings

# sleet.json

The standard way of setting up feeds is with a *sleet.json* file.

To get started use the *createconfig* command to generate a sample *sleet.json* file.

```
sleet createconfig --azure
```

The example file contains a set of sources. If only feed exists in the file sleet will automatically use it. Once there two or more sources the ``--source`` parameter will be required to select the correct source.

## Source properties

| Property | Description |
| --- | ------ |
| name | Feed name used for ``--source`` *[Required]* | 
| type | Feed type *[Required]*  |
| baseURI | Specify a URI to write to the feed json files instead of the container's URI. Useful if serving up the content from a different endpoint. |


## Azure specific properties

| Property | Description |
| --- | ------ |
| container | Name of an existing container in the storage account. *[Required]* |
| connectionString | Azure storage connection string. *[Required]* |
| path | Full URI of the azure storage container. If specified this value will be verified against the container's URI. |
| feedSubPath | Provides a sub directory path within the container where the feed should be added. This allows for multiple feeds within a single container. |

```json
{
  "sources": [
    {
      "name": "feed",
      "type": "azure",
      "container": "feed",
      "path": "https://yourStorageAccount.blob.core.windows.net/feed/",
      "connectionString": "DefaultEndpointsProtocol=https;AccountName=;AccountKey=;BlobEndpoint="
    }
  ]
}
```

## Amazon s3 specific properties

| Property | Description |
| --- | ------ |
| profileName | AWS [credentials file](https://docs.aws.amazon.com/sdk-for-net/v2/developer-guide/net-dg-config-creds.html#creds-file) profile name. *[Cannot be used with accessKeyId or secretAccessKey]* |
| accessKeyId | Access key id *[Cannot be used with profileName]* |
| secretAccessKey | Secret access key *[Cannot be used with profileName]* |
| bucketName | S3 bucket name *[Required]* |
| region | S3 region *[Cannot be used with serviceURL]* |
| serviceURL | S3 service URL *[Cannot be used with region]* |
| path | Full URI of the storage bucket. If not specified a default URI will be used. |
| feedSubPath | Provides a sub directory path within the bucket where the feed should be added. This allows for multiple feeds within a single bucket. |
| serverSideEncryptionMethod | The encryption to use for uploaded objects. Only `AES256` and `None` are currently supported. Default is `None` |
| compress | Compress JSON files with GZIP before uploading. Default is *true* |

Either `region` or `serviceURL` should be specified but not both.

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
      "region": "us-west-2"
    }
  ]
}
```

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
      "secretAccessKey": "IAM_SECRET_ACCESS_KEY"
    }
  ]
}
```

### Using AWS environments

```json
{
  "sources": [
    {
      "name": "feed",
      "type": "s3",
      "path": "https://s3.amazonaws.com/my-bucket-feed/",
      "bucketName": "my-bucket-feed",
      "region": "us-east-1"
    }
  ]
}
```

### Using serviceURL

```json
{
  "sources": [
    {
      "name": "feed",
      "type": "s3",
      "path": "https://s3.amazonaws.com/my-bucket-feed/",
      "bucketName": "my-bucket-feed",
      "serviceURL": "https://s3.us-east-1.amazonaws.com"
    }
  ]
}
```


When running Sleet with [AWS environment variables](https://docs.aws.amazon.com/cli/latest/userguide/cli-configure-envvars.html) leave accessKeyId, secretAccessKey, and profileName blank. If these properties are not set in sleet.json Sleet will try to set up the S3 feed using the environment.

## Folder feed specific properties

| Property | Description |
| --- | ------ |
| path | Path is the output directory of the feed. |

```json
{
  "name": "myLocalFeed",
  "type": "local",
  "path": "C:\\myFeed"
}
```

## Tokens in sleet.json

Property values in *sleet.json* can be tokenized similar to nuget *.pp* files.

Given an environment variable ``myKey`` the following file woudl replace `$myKey$` with the value of the environment variable if it exists.

```json
{
  "sources": [
    {
      "name": "feed",
      "type": "azure",
      "container": "feed",
      "connectionString": "DefaultEndpointsProtocol=https;AccountName=;AccountKey=$myKey$;BlobEndpoint="
    }
  ]
}
```

Tokens that resolve to a tokenized string will also be resolved, allowing environment variables to point to and combine additional environment variables.

To escape `$` use `$$`.

## Sleet.json loading order

1. If `--config` was passed the path given will be used.
1. If no config path was given sleet will search all parent directories starting with the working directory for sleet.json files.
1. Environment variables will be used if no sleet.json files were found.


# Environment variables

Feeds can be defined using only environment variables.

`SLEET_FEED_{property}` env vars will be treated the same as properties under a source.

Example of defining an azure feed using only environment variables:

| Property | Value |
| --- | ------ |
| `SLEET_FEED_TYPE` | `azure` |
| `SLEET_FEED_CONTAINER` | `feed` |
| `SLEET_FEED_CONNECTIONSTRING` | `DefaultEndpointsProtocol=https;AccountName=;AccountKey=;BlobEndpoint=` |

To avoid loading up any *sleet.json* files when using env vars pass `--config none` to block it from loading. 

Note that if *sleet.json* is used environment variables will be ignored. It is not possible to mix settings between the two input options.

# Command line properties

Key value pairs can be passed on the command line and are treated the same as environment variables would be.

Command line properties are favored over environment variables.

Properties can be passed with `-p` or `--property` with a format of `"key=value"`.

In this example a new feed is initialized *without* a sleet.json file. All values are passed in on the command line.

```
sleet init --config none -p SLEET_FEED_TYPE=azure -p SLEET_FEED_CONTAINER=feed \
 -p "SLEET_FEED_CONNECTIONSTRING=DefaultEndpointsProtocol=https;AccountName=;AccountKey=;BlobEndpoint="
```

# Network proxy settings

Authenticated proxy that use windows credentials should enable the following setting in *sleet.json*

```json
{
  "proxy": {
    "useDefaultCredentials": true
  },
  "sources": [
  ]
}
```

This setting can be set through an environment variable or command line property if *sleet.json* is not used.

| Property | Value |
| --- | ------ |
| `SLEET_FEED_PROXY_USEDEFAULTCREDENTIALS` | `true` |



