# Creating a Cloudflare R2 feed

This guide is used to setup a new feed hosted on [Cloudflare R2](https://developers.cloudflare.com/r2/) storage.

R2 implements the S3 API, so an R2 feed is an `s3` feed with `"provider": "r2"`. This applies the settings R2 needs:

* `disablePayloadSigning` defaults to `true` and `checksumMode` defaults to `whenRequired`. R2 does not support the streaming uploads used by the AWS SDK.
* `serviceURL` and `baseURI` are required.
* When Sleet creates a new bucket it does not try to make it public. R2 does not support bucket policies, public access is enabled in Cloudflare instead.

## Before you start

You will need the following from the Cloudflare dashboard:

* Your **account id**. The R2 S3 API url is `https://<ACCOUNT_ID>.r2.cloudflarestorage.com`. Buckets created in a [jurisdiction](https://developers.cloudflare.com/r2/reference/data-location/#using-jurisdictions-with-the-s3-api) use a url such as `https://<ACCOUNT_ID>.eu.r2.cloudflarestorage.com`.
* A **bucket** with public access enabled, see [making the bucket public](#making-the-bucket-public).
* An **R2 API token**, which provides the access key id and secret access key. *Object Read & Write* is needed to push packages. See [R2 authentication](https://developers.cloudflare.com/r2/api/tokens/).

## Creating a config for a Cloudflare R2 feed

Create a `sleet.json` config file to define a new package feed hosted on R2.

``sleet createconfig --provider r2``

Edit `sleet.json` using your editor of choice to set the bucket name, account id, public url, and access key.

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
      "bucketName": "my-bucket-feed",
      "serviceURL": "https://ACCOUNT_ID.r2.cloudflarestorage.com",
      "baseURI": "https://nuget.example.com/",
      "accessKeyId": "R2_ACCESS_KEY_ID",
      "secretAccessKey": "R2_SECRET_ACCESS_KEY"
    }
  ]
}
```

`.netconfig`:
```gitconfig
[sleet "feed"]
    type = s3
    provider = r2
    bucketName = my-bucket-feed
    serviceURL = https://ACCOUNT_ID.r2.cloudflarestorage.com
    baseURI = https://nuget.example.com/
    accessKeyId = R2_ACCESS_KEY_ID
    secretAccessKey = R2_SECRET_ACCESS_KEY
```

`region` is not needed for R2.

To keep the access key out of the config, leave out `accessKeyId` and `secretAccessKey` and set the `AWS_ACCESS_KEY_ID` and `AWS_SECRET_ACCESS_KEY` environment variables.

Existing R2 feeds that set `disablePayloadSigning` instead of `provider` continue to work.

## Making the bucket public

NuGet clients read the feed anonymously, so the bucket must be publicly readable. Public access for R2 is enabled in Cloudflare, see [public buckets](https://developers.cloudflare.com/r2/buckets/public-buckets/). There are two options:

* **Custom domain** *(recommended)*: connect a domain in your Cloudflare account to the bucket.
* **r2.dev url**: Cloudflare serves the bucket from `https://pub-<id>.r2.dev`. This is rate limited and intended for development.

Set `baseURI` to the public url. Sleet writes this url into the feed json files, the S3 API url is only used to upload files.

## Caching

Sleet sets `Cache-Control: no-store` on feed files by default, so Cloudflare does not cache them. To cache packages on a custom domain set `immutableCacheControl`, for example to `public, max-age=31536000, immutable`, and add a [cache rule](https://developers.cloudflare.com/cache/how-to/cache-rules/) for the domain since Cloudflare does not cache `.nupkg` files by default. Feed json files change with every push, keep `mutableCacheControl` as `no-store` or use a short `max-age`.

### Additional feed settings

Help on additional feed settings such as *baseURI* and *feedSubPath* can be found under [Sleet client settings](client-settings.md)

## Adding packages

Add packages to the feed with the push command, this can be used with either a path to a single nupkg or a folder of nupkgs.

``sleet push d:\nupkgsToPush``

## Initializing the feed

For a new feed the first push will initialize the feed with the default settings.

If the bucket does not exist Sleet will create it, this requires an *Admin Read & Write* token. Sleet cannot make the bucket public, follow [making the bucket public](#making-the-bucket-public) after the bucket is created.

To create a feed with custom feed settings, such as with a catalog or symbols feed, use the `init` command first.

## Using the feed

Add the feed as a source to your `NuGet.Config` file. In the example above the package source URL is ``https://nuget.example.com/index.json``
