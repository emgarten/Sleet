# Sleet client settings

# sleet.json

The standard way of setting up feeds is with a *sleet.json* file.

To get started use the *createconfig* command to generate a sample *sleet.json* file.

```
sleet createconfig --azure
```

The example file contains a set of sources. If only feed exists in the file sleet will automatically use it. Once there two or more sources the ``--source`` parameter will be required to select the correct source.

# .netconfig

Additionally, Sleet supports configuration via [.netconfig](https://dotnetconfig.org) which provides a uniform way of configuring multiple tools with a single file and format. In addition, using `.netconfig` brings support for hierarchical configurations (i.e. reuse source configurations across the entire machine, with a single `.netconfig` in your user profile root directory).

## Source properties

| Property | Description |
| --- | ------ |
| name | Feed name used for ``--source`` *[Required]* |
| type | Feed type *[Required]*  |
| baseURI | Specify a URI to write to the feed json files instead of the container's URI. Useful if serving up the content from a different endpoint. |


## Azure specific properties

By default, the sleet will use the credentials types specified by [DefaultAzureCredential](https://docs.microsoft.com/en-us/dotnet/api/azure.identity.defaultazurecredential?view=azure-dotnet).
If you need to use a service principal credential type, set the `AZURE_TENANT_ID`, `AZURE_CLIENT_ID`, and `AZURE_CLIENT_CERTIFICATE_PATH` environment variables for the [EnvironmentalCredential](https://learn.microsoft.com/en-us/dotnet/api/azure.identity.environmentcredential?view=azure-dotnet).
More options can be found in the [Azure.Identity README](https://github.com/Azure/azure-sdk-for-net/blob/main/sdk/identity/Azure.Identity/README.md#environment-variables).


| Property | Description                                                                                                                                                                               |
| --- |-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| container | Name of an existing container in the storage account. *[Required]*                                                                                                                        |
| connectionString | Azure storage connection string. Note, either connectionString or path *must* be specified. *[Discouraged]*                                                                               |
| path | Full URI of the azure storage container. If specified this value will be verified against the container's URI. Note, either connectionString or path *must* be specified. *[Encouraged]*. |
| feedSubPath | Provides a sub directory path within the container where the feed should be added. This allows for multiple feeds within a single container.                                              |

`sleet.json`:
```json
{
  "sources": [
    {
      "name": "feed",
      "type": "azure",
      "container": "feed",
      "path": "https://yourStorageAccount.blob.core.windows.net/feed"
    }
  ]
}
```

`.netconfig`:

```gitconfig
[sleet "feed"]
    type = azure
    container = feed
    path = https://yourStorageAccount.blob.core.windows.net/feed/
```

## Amazon s3 specific properties

| Property                   | Description                                                                                                                                                                                                                                                                                                                  |
|----------------------------|------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| profileName                | AWS [credentials file](https://docs.aws.amazon.com/sdk-for-net/v2/developer-guide/net-dg-config-creds.html#creds-file) profile name. *[Cannot be used with accessKeyId or secretAccessKey]*                                                                                                                                  |
| accessKeyId                | Access key id *[Cannot be used with profileName]*                                                                                                                                                                                                                                                                            |
| secretAccessKey            | Secret access key *[Cannot be used with profileName]*                                                                                                                                                                                                                                                                        |
| bucketName                 | S3 bucket name *[Required]*                                                                                                                                                                                                                                                                                                  |
| provider                   | S3 compatible service to configure defaults for. Default is `aws`. See [S3 providers](#s3-providers).                                                                                                                                                                                                                       |
| region                     | S3 region. Selects the endpoint for Amazon S3. For other services it is the region used to sign requests, and must be used with `serviceURL`.                                                                                                                                                                                |
| serviceURL                 | S3 service URL. Required for non-AWS services unless the provider can determine it.                                                                                                                                                                                                                                          |
| path                       | Full URI of the storage bucket. If not specified a default URI will be used.                                                                                                                                                                                                                                                 |
| feedSubPath                | Provides a sub directory path within the bucket where the feed should be added. This allows for multiple feeds within a single bucket.                                                                                                                                                                                       |
| serverSideEncryptionMethod | The encryption to use for uploaded objects. Only `AES256` and `None` are currently supported. Default is `None`                                                                                                                                                                                                              |
| compress                   | Compress JSON files with GZIP before uploading. Default is *true*                                                                                                                                                                                                                                                            |
| acl                        | A acl can be set for uploaded files. By default, no specific canned acl is set and bucket defaults and/or policies are in effect. If the bucket is created by sleet and an acl is set, then the default bucket acl will be set to that acl. |
| disablePayloadSigning      | S3 payload signing is a requirement of AWS SigV4, some S3 compatible storage providers do not implement this. Defaults to the value for the selected `provider`.                                                                                                                                                              |
| forcePathStyle             | Address buckets as `serviceURL/bucket` instead of `bucket.serviceURL`. Required by services without wildcard DNS such as a local MinIO server. Defaults to the value for the selected `provider`.                                                                                                                             |
| checksumMode               | Controls the `x-amz-checksum-*` headers the AWS SDK adds to requests. `default` sends them on every request, `whenRequired` sends them only where the S3 API requires them. Services that reject unknown headers need `whenRequired`. Defaults to the value for the selected `provider`.                                       |
| authenticationRegion       | Region used to sign requests when it differs from `region`. Defaults to the value for the selected `provider`.                                                                                                                                                                                                               |
| publicAccess               | How the bucket is made publicly readable when Sleet creates it. One of `aws`, `bucketPolicy`, `cannedAcl`, or `external`. Defaults to the value for the selected `provider`.                                                                                                                                                  |

`region` and `serviceURL` may be used together. `serviceURL` selects the endpoint and `region` is used as the signing region. Every provider other than `aws` requires `serviceURL`, because `region` alone only identifies an Amazon endpoint.

### S3 providers

`provider` selects defaults for an S3 compatible service. Every default it sets can be overridden by
setting the individual property, so a provider is only a starting point. Use `generic` for a service
that is not listed.

| Provider | Service | Notes |
| --- | --- | --- |
| `aws` | Amazon S3 | Default. Also accepted as `amazon` or `s3`. |
| `r2` | Cloudflare R2 | Also accepted as `cloudflare`. See the [Cloudflare R2 guide](feed-type-cloudflare.md). |
| `minio` | MinIO | Uses path style addressing and a bucket policy for public read access. |
| `yandex` | Yandex Object Storage | Signs with `ru-central1` and skips optional checksums. |
| `scaleway` | Scaleway Object Storage | |
| `wasabi` | Wasabi | |
| `b2` | Backblaze B2 | Also accepted as `backblaze`. |
| `digitalocean` | DigitalOcean Spaces | Also accepted as `spaces`. |
| `generic` | Any other S3 compatible service | Grants public read with a bucket policy. No other defaults are applied. |

Defaults for `scaleway`, `wasabi`, `b2`, and `digitalocean` come from each service's published
documentation and have not been verified against a live account. If a feed on one of these services
needs a different setting, set it explicitly in `sleet.json` and please open an issue so the default
can be corrected.

#### Troubleshooting S3 compatible services

| Symptom | Setting to try |
| --- | --- |
| Uploads fail with a signature or `STREAMING-AWS4-HMAC-SHA256-PAYLOAD` error | `"disablePayloadSigning": true` |
| Requests are rejected for an unexpected `x-amz-checksum-crc32` or `x-amz-sdk-checksum-algorithm` header | `"checksumMode": "whenRequired"` |
| Requests go to `bucket.host` and fail to resolve, or a local server returns 404 for every path | `"forcePathStyle": true` |
| Signature errors mentioning a region that is not the one you configured | `authenticationRegion` |

`disablePayloadSigning` and `checksumMode` solve different problems and are not interchangeable.
`disablePayloadSigning` moves the checksum out of the signed payload and into an `aws-chunked`
trailer, so on a service that does not accept trailers it can make uploads worse rather than better.
Try `checksumMode` first for header and checksum rejections.


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
    region = us-west-2
    accessKeyId = IAM_ACCESS_KEY_ID
    secretAccessKey = IAM_SECRET_ACCESS_KEY
```

### Using AWS environments

`sleet.json`:
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

`.netconfig`:
```gitconfig
[sleet "feed"]
    type = s3
    path = https://s3.amazonaws.com/my-bucket-feed/
    bucketName = my-bucket-feed
    region = us-west-2
```

### Using serviceURL

`sleet.json`:
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

`.netconfig`:
```gitconfig
[sleet "feed"]
    type = s3
    path = https://s3.amazonaws.com/my-bucket-feed/
    bucketName = my-bucket-feed
    serviceURL = https://s3.us-east-1.amazonaws.com
```


When running Sleet with [AWS environment variables](https://docs.aws.amazon.com/cli/latest/userguide/cli-configure-envvars.html) leave accessKeyId, secretAccessKey, and profileName blank. If these properties are not set in sleet.json Sleet will try to set up the S3 feed using the environment.

## Folder feed specific properties

| Property | Description |
| --- | ------ |
| path | Path is the output directory of the feed. |

`sleet.json`:
```json
{
  "sources": [
    {
      "name": "myLocalFeed",
      "type": "local",
      "path": "C:\\myFeed"
    }
  ]
}
```

`.netconfig`:
```gitconfig
[sleet "myLocalFeed"]
    type = local
    path = C:\\myFeed
```

## Tokens in configuration

Property values in *sleet.json* and *.netconfig* can be tokenized similar to nuget *.pp* files.

Given an environment variable ``myKey`` the following file would replace `$myKey$` with the value of the environment variable if it exists.

`sleet.json`:
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

`.netconfig`:
```gitconfig
[sleet "feed"]
    type = azure
    container = feed
    connectionString = "DefaultEndpointsProtocol=https;AccountName=;AccountKey=$myKey$;BlobEndpoint="
```

Tokens that resolve to a tokenized string will also be resolved, allowing environment variables to point to and combine additional environment variables.

To escape `$` use `$$`.

## Caching configuration
It is possible to configure caching header for both Azure and S3 backed feeds. This is useful when serving the feed content through a CDN. By default, caching is disabled with a value of `no-store`. You can configure caching for both mutable and immutable files. Immutable files (files which aren't supposed to change, since they are stored per version - `.nupkg`, `/readme`, `/icon`, `.nuspec`, `.xml`, `.dll`, `.pdb`) should have a long cache lifetime (unless you are making changes to live packages). Mutable files (files which are expected to change, such as `index.json` and `flatcontainer/{id}/index.json`) should have a short cache lifetime (~1 hour or whatever you are comfortable with), since it can make clients see stale feed.

| Property                   | Description                                                                                                                                                          |
|----------------------------|----------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| immutableCacheControl      | Cache-Control header value for immutable versioned files (.nupkg, /readme, /icon, .nuspec, .xml, .dll, .pdb). Default is `no-store`. Example: `public, max-age=31536000, immutable`  |
| mutableCacheControl        | Cache-Control header value for mutable files (.json, .svg). Default is `no-store`. Example: `public, max-age=300, must-revalidate`                                   |

## Sleet.json loading order

1. If `--config` was passed the path given will be used.
1. If no config path was given sleet will search all parent directories starting with the working directory for sleet.json files.
1. Environment variables will be used if no sleet.json files were found.

## .netconfig loading order

1. If `--config` was passed the path given will be used.
1. Standard `.netconfig` probing happens next (current directory and all parent directories, plus [global](https://docs.microsoft.com/en-us/dotnet/api/system.environment.specialfolder?view=netstandard-2.0#fields) and [system](https://docs.microsoft.com/en-us/dotnet/api/system.environment.specialfolder?view=netstandard-2.0#fields) locations).
1. Environment variables will be used if no sleet setting is found.


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
