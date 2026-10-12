# Create a Cloudflare R2 feed

This quick start creates a public NuGet v3 feed in [Cloudflare R2](https://developers.cloudflare.com/r2/). R2 implements the S3 API, so an R2 feed is an `s3` source with `provider` set to `r2`. The `r2` provider applies the settings that R2 needs:

- `disablePayloadSigning` defaults to `true` and `checksumMode` defaults to `whenRequired`. R2 doesn't support the streaming uploads that the AWS SDK uses.
- `serviceURL` and `baseURI` are required.
- When Sleet creates a bucket, it doesn't try to make the bucket public. R2 doesn't support bucket policies, so you turn on public access in Cloudflare instead.

## Prerequisites

- Your Cloudflare **account ID**. The R2 S3 API URL is `https://<ACCOUNT_ID>.r2.cloudflarestorage.com`. Buckets created in a [jurisdiction](https://developers.cloudflare.com/r2/reference/data-location/#using-jurisdictions-with-the-s3-api) use a URL such as `https://<ACCOUNT_ID>.eu.r2.cloudflarestorage.com`.
- A bucket with public access turned on. See [make the bucket public](#make-the-bucket-public).
- R2 credentials, such as an **R2 API token**. See [Credentials](#credentials).
- Sleet installed. See [Install Sleet](install.md).

## Make the bucket public

NuGet clients read the feed anonymously, so the bucket must be publicly readable. Public access for R2 is turned on in Cloudflare, see [public buckets](https://developers.cloudflare.com/r2/buckets/public-buckets/). There are two options:

- **Custom domain** (recommended): connect a domain in your Cloudflare account to the bucket.
- **r2.dev URL**: Cloudflare serves the bucket from `https://pub-<id>.r2.dev`. This URL is rate limited and intended for development.

In the Cloudflare dashboard, open the bucket and select **Settings**. To connect a domain, select **Add** under **Custom Domains**. To turn on the r2.dev URL, select **Enable** under **Public Development URL**.

With [Wrangler](https://developers.cloudflare.com/workers/wrangler/install-and-update/):

```bash
# Custom domain
wrangler r2 bucket domain add my-bucket-feed --domain nuget.example.com --zone-id ZONE_ID

# r2.dev URL, the get command shows the URL
wrangler r2 bucket dev-url enable my-bucket-feed
wrangler r2 bucket dev-url get my-bucket-feed
```

To find `ZONE_ID`, see [find zone and account IDs](https://developers.cloudflare.com/fundamentals/account/find-account-and-zone-ids/). For a bucket in a jurisdiction, add `--jurisdiction`, such as `--jurisdiction eu`.

The r2.dev URL isn't known until the bucket exists, so create the bucket before the first push, for example with `wrangler r2 bucket create my-bucket-feed`.

Set `baseURI` to the public URL. Sleet writes this URL into the feed files. The S3 API URL is only used to upload files.

## Credentials

Sleet connects to R2 with an S3 access key ID and secret access key. Use one of these:

- **R2 API token**: [create an R2 API token](https://developers.cloudflare.com/r2/api/tokens/) and copy its access key ID and secret access key. *Object Read & Write* can push to an existing bucket. *Admin Read & Write* is needed if Sleet creates the bucket.
- **Cloudflare API token**: an API token with an R2 permission, such as *Workers R2 Storage Write*, also works. The access key ID is the token ID, and the secret access key is the hex-encoded SHA-256 hash of the token value. See [S3 API credentials from an API token](https://developers.cloudflare.com/r2/api/tokens/#get-s3-api-credentials-from-an-api-token).
- **Temporary credentials**: [temporary credentials](https://developers.cloudflare.com/r2/api/s3/temporary-credentials/) are short-lived and scoped to one bucket. Leave the keys out of the config, and set `AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY`, and `AWS_SESSION_TOKEN`. See [AWS environment variables](auth-aws.md#aws-environment-variables).

## Create the config file

Create a starter `sleet.json` file:

```bash
sleet createconfig --provider r2
```

Edit it to set the bucket name, account ID, public URL, and access key:

sleet.json:

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

You can also use [.netconfig](client-settings.md#netconfig):

```ini
[sleet "feed"]
    type = s3
    provider = r2
    bucketName = my-bucket-feed
    serviceURL = https://ACCOUNT_ID.r2.cloudflarestorage.com
    baseURI = https://nuget.example.com/
    accessKeyId = R2_ACCESS_KEY_ID
    secretAccessKey = R2_SECRET_ACCESS_KEY
```

R2 doesn't need `region`. To keep the access key out of the file, leave out `accessKeyId` and `secretAccessKey`, and set the `AWS_ACCESS_KEY_ID` and `AWS_SECRET_ACCESS_KEY` environment variables. See [AWS authentication](auth-aws.md).

Existing R2 feeds that set `disablePayloadSigning` instead of `provider` continue to work.

For the full property list, see [Amazon S3 properties](client-settings.md#amazon-s3-properties).

## Push packages

Push one package or a folder of packages:

```bash
sleet push ./nupkgs
```

The first push initializes the feed with the default settings. To turn on the catalog or the symbol server from the start, run [init](commands.md#init) first.

If the bucket doesn't exist, Sleet creates it, which needs an *Admin Read & Write* token. Sleet can't make the bucket public, so [make the bucket public](#make-the-bucket-public) after Sleet creates it.

## Caching

Sleet sets `Cache-Control: no-store` on feed files by default, so Cloudflare doesn't cache them. To cache packages on a custom domain, set `immutableCacheControl`, for example to `public, max-age=31536000, immutable`, and add a [cache rule](https://developers.cloudflare.com/cache/how-to/cache-rules/) for the domain, because Cloudflare doesn't cache `.nupkg` files by default. Feed JSON files change with every push, so keep `mutableCacheControl` as `no-store` or use a short `max-age`. See [CDN and caching](cdn-caching.md).

## Use the feed

The package source URL is `baseURI` followed by `index.json`. For the example above:

```text
https://nuget.example.com/index.json
```

Add it to NuGet clients. See [Use a feed with NuGet](consume-feed.md).

## Next steps

- [S3-compatible storage](s3-compatible.md)
- [AWS authentication](auth-aws.md)
- [CDN and caching](cdn-caching.md)
- [Amazon S3 properties](client-settings.md#amazon-s3-properties)
