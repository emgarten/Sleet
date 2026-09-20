# Creating a Cloudflare R2 feed

This guide is used to setup a new feed hosted on [Cloudflare R2](https://developers.cloudflare.com/r2/) storage.

R2 implements the S3 API, but it does not support S3 bucket policies or ACLs. Public access is
enabled through Cloudflare instead. Setting `"provider": "r2"` on an `s3` feed applies the R2
specific settings for you.

## Before you start

You will need the following from the Cloudflare dashboard:

* Your **account id**, shown on the R2 overview page.
* An **R2 API token**, which provides the *access key id* and *secret access key*. Use a token with
  *Object Read & Write* permissions to push packages, or *Admin Read & Write* if you want Sleet to
  create the bucket for you. See [R2 authentication](https://developers.cloudflare.com/r2/api/tokens/).
* A **public url** for the bucket. R2 buckets are private by default and the public url cannot be
  determined automatically, so it must be set in the config. See [public buckets](#making-the-bucket-public) below.

## Creating a config for a Cloudflare R2 feed

Create a `sleet.json` config file to define a new package feed hosted on R2.

``sleet createconfig --s3 --provider r2``

Edit `sleet.json` using your editor of choice to set your account id, bucket, and credentials.

``notepad sleet.json``

For `.netconfig`, just create or edit the file directly in the [desired location](https://dotnetconfig.org/#what).

`sleet.json`:
```json
{
  "sources": [
    {
      "name": "feed",
      "type": "s3",
      "provider": "r2",
      "accountId": "cloudflareAccountId",
      "bucketName": "my-bucket-feed",
      "accessKeyId": "R2_ACCESS_KEY_ID",
      "secretAccessKey": "R2_SECRET_ACCESS_KEY",
      "baseURI": "https://nuget.example.com/"
    }
  ]
}
```

`.netconfig`:
```gitconfig
[sleet "feed"]
    type = s3
    provider = r2
    accountId = cloudflareAccountId
    bucketName = my-bucket-feed
    accessKeyId = R2_ACCESS_KEY_ID
    secretAccessKey = R2_SECRET_ACCESS_KEY
    baseURI = https://nuget.example.com/
```

`accountId` is used to build the R2 S3 API endpoint. To connect to a specific
[jurisdiction](https://developers.cloudflare.com/r2/reference/data-location/) add
`"jurisdiction": "eu"`. Valid values are `default`, `eu`, `us`, and `fedramp`. Set `serviceURL`
directly to use an endpoint Sleet does not build for you.

### Keeping credentials out of sleet.json

Leave `accessKeyId` and `secretAccessKey` out of the config to read the credentials from the
`AWS_ACCESS_KEY_ID` and `AWS_SECRET_ACCESS_KEY` environment variables instead.

Sleet also supports `$token$` replacement in config values, see [client settings](client-settings.md).

## Making the bucket public

NuGet clients read the feed over HTTP, so the bucket must be publicly readable. R2 does not allow
this to be configured through the S3 API, so it has to be done in Cloudflare.

There are two options, both described in the
[Cloudflare public buckets guide](https://developers.cloudflare.com/r2/buckets/public-buckets/):

* **Custom domain** *(recommended)* — connect a domain in your Cloudflare account to the bucket.
  This is the only option that supports caching, WAF rules, and access controls.
* **Managed `r2.dev` development url** — Cloudflare hosts the bucket at a `https://pub-<id>.r2.dev`
  address. This is rate limited and Cloudflare does not recommend it for production use.

Set `baseURI` to whichever url you choose. Sleet writes this url into the feed json files so that
NuGet clients read from the public address rather than the R2 S3 API endpoint, which is not
publicly readable.

## Caching

Cloudflare does not cache `.json` or `.nupkg` files by default, so no extra configuration is needed.

If you add a *Cache Everything* rule to a custom domain, set `mutableCacheControl` so that feed
metadata is revalidated, otherwise clients may read a stale `index.json` after a push:

```json
{
  "immutableCacheControl": "public, max-age=31536000, immutable",
  "mutableCacheControl": "no-cache"
}
```

### Additional feed settings

Help on additional feed settings such as *baseURI* and *feedSubPath* can be found under [Sleet client settings](client-settings.md)

## Adding packages

Add packages to the feed with the push command, this can be used with either a path to a single nupkg or a folder of nupkgs.

``sleet push d:\nupkgsToPush``

## Initializing the feed

For a new feed the first push will create the bucket if it does not already exist and initialize
the feed with the default settings. Creating a bucket requires an *Admin Read & Write* token.

Sleet cannot make the bucket public, so follow [making the bucket public](#making-the-bucket-public)
after the bucket has been created.

To create a feed with custom feed settings, such as with a catalog or symbols feed, use the `init` command first.

## Using the feed

Add the feed as a source to your `NuGet.Config` file. In the example above the package source URL is ``https://nuget.example.com/index.json``

## Unsupported options

The following S3 feed options cannot be used with `"provider": "r2"` and will fail with an error:

| Property | Reason |
| --- | ------ |
| acl | R2 accepts S3 ACL headers but ignores them. Use a public bucket instead. |
| serverSideEncryptionMethod | R2 always encrypts objects at rest. |

R2 buckets are signed with the `auto` region, so `region` does not need to be set. If it is set it
will be used as the signing region.
