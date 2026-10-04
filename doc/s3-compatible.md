# S3-compatible storage

Sleet can publish to storage providers that expose an S3-compatible API. This page explains the Sleet settings that differ from AWS S3 and gives community-reported provider examples.

> [!NOTE]
> Sleet's functional tests run against RustFS. The other configurations are not tested by the Sleet maintainers. They are based on provider documentation and community reports. Send a PR if a provider changes its endpoint format or public-access model.

## Configure the source

Use `serviceURL` instead of `region` for non-AWS S3 APIs. If the provider needs a specific signing Region, also set `region`. With `serviceURL`, Sleet only uses `region` to sign requests.

Always set `path` to the public URL that NuGet clients will use. Without `path`, Sleet uses `serviceURL` followed by the bucket name, such as `https://s3.example.com/my-bucket-feed/`. For Cloudflare R2, set `baseURI` to the public URL instead, see [Create a Cloudflare R2 feed](feed-type-cloudflare.md).

sleet.json:

```json
{
  "sources": [
    {
      "name": "feed",
      "type": "s3",
      "bucketName": "my-bucket-feed",
      "serviceURL": "https://s3.example.com",
      "path": "https://packages.example.com/",
      "accessKeyId": "$S3_ACCESS_KEY_ID$",
      "secretAccessKey": "$S3_SECRET_ACCESS_KEY$"
    }
  ]
}
```

You can also omit `accessKeyId` and `secretAccessKey` and use `AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY`, and `AWS_SESSION_TOKEN` environment variables. See [AWS authentication](auth-aws.md).

## Provider settings

Set `provider` to use the defaults that a service needs. Settings in the source override these defaults.

| `provider` | Service | Defaults |
| --- | --- | --- |
| `aws` | Amazon S3, and providers that work with the AWS defaults. This is the default. | The AWS SDK defaults. |
| `r2` | Cloudflare R2 | `disablePayloadSigning` is `true` and `checksumMode` is `whenRequired`. `serviceURL` and `baseURI` are required. Sleet doesn't change public access on buckets it creates. See [Create a Cloudflare R2 feed](feed-type-cloudflare.md). |
| `minio` | MinIO | `forcePathStyle` is `true`, because MinIO only supports virtual-hosted-style URLs when `MINIO_DOMAIN` is set. `serviceURL` is required. |

For other providers, set `disablePayloadSigning`, `checksumMode`, or `forcePathStyle` yourself if the provider doesn't support the AWS defaults. See [Amazon S3 properties](client-settings.md#amazon-s3-properties).

`createconfig` creates a template for a provider:

```bash
sleet createconfig --provider minio
```

MinIO uses path-style URLs, so the bucket URL is the server URL followed by the bucket name. Set `region` if the MinIO server uses a Region other than `us-east-1`.

```json
{
  "sources": [
    {
      "name": "feed",
      "type": "s3",
      "provider": "minio",
      "bucketName": "my-bucket-feed",
      "serviceURL": "http://localhost:9000",
      "path": "http://localhost:9000/my-bucket-feed/",
      "accessKeyId": "$S3_ACCESS_KEY_ID$",
      "secretAccessKey": "$S3_SECRET_ACCESS_KEY$"
    }
  ]
}
```

## Signing and compression

Some providers reject AWS SigV4 payload signing. Set `disablePayloadSigning` to `true` for those providers. Some also reject the checksums that the AWS SDK adds to requests. Set `checksumMode` to `whenRequired` for those providers. The `r2` provider sets both for Cloudflare R2.

```json
{
  "disablePayloadSigning": true
}
```

By default, Sleet gzips JSON files and uploads them with `Content-Encoding: gzip`. Set `compress` to `false` if the provider or its public endpoint mishandles gzipped JSON.

For example, some S3-compatible services keep the `aws-chunked` encoding that the AWS SDK uses for uploads, and serve JSON files with `Content-Encoding: gzip, aws-chunked`. NuGet can't decompress those files, so it can't load the service index, and `curl --compressed` fails with `Unrecognized content encoding type`. Uncompressed files work with NuGet even if the service still adds `aws-chunked`. Check them with curl without `--compressed`.

```json
{
  "compress": false
}
```

See [Compression](how-it-works.md#compression) for how Sleet writes feed files.

## Create buckets outside Sleet

Sleet's automatic bucket creation uses AWS-specific APIs. It removes the bucket public access block, sets S3 Object Ownership, and writes an AWS bucket policy. Sleet skips the public access block and Object Ownership if the provider rejects them, but bucket creation fails if the provider rejects the bucket policy. With the `r2` provider, Sleet skips all three and the new bucket stays private.

Create the bucket with the provider's tools first. Configure public read access for public feeds, or private access for private feeds, before you run `sleet push`.

## Provider examples

| Provider | `serviceURL` | `path` for clients | Notes |
| --- | --- | --- | --- |
| Cloudflare R2 | `https://<account-id>.r2.cloudflarestorage.com` | `https://<public-id>.r2.dev/` or a custom domain | Set `provider` to `r2`, and set `baseURI` to the public URL. See [Create a Cloudflare R2 feed](feed-type-cloudflare.md), [R2 S3 API](https://developers.cloudflare.com/r2/api/s3/api/), and [public buckets](https://developers.cloudflare.com/r2/buckets/public-buckets/). |
| MinIO | Your MinIO API endpoint, such as `https://minio.example.com` | The public URL for the bucket, such as `https://minio.example.com/my-bucket-feed/` or a reverse-proxied custom domain | Set `provider` to `minio`. Configure anonymous read access with MinIO policy tools. See [MinIO anonymous access](https://docs.min.io/aistor/reference/cli/mc-anonymous/) and [policy-based access control](https://min.io/docs/minio/linux/administration/identity-access-management/policy-based-access-control.html). |
| RustFS | Your RustFS API endpoint, such as `https://rustfs.example.com` | `https://rustfs.example.com/my-bucket-feed/` | Set `forcePathStyle` to `true`, because RustFS only supports virtual-hosted-style URLs when `RUSTFS_SERVER_DOMAINS` is set. Sleet can create the bucket with public read access. See [RustFS virtual host style](https://docs.rustfs.com/en/integration/virtual). |
| DigitalOcean Spaces | `https://<region>.digitaloceanspaces.com` | `https://my-bucket-feed.<region>.digitaloceanspaces.com/`, a CDN endpoint, or a custom domain | Use the Spaces public endpoint that clients can reach. See [Spaces API](https://docs.digitalocean.com/reference/api/spaces/) and [Spaces docs](https://docs.digitalocean.com/products/spaces/). |
| Backblaze B2 | `https://s3.<region>.backblazeb2.com` | `https://my-bucket-feed.s3.<region>.backblazeb2.com/` | Use the endpoint for the bucket's region. See [Backblaze S3-compatible API](https://www.backblaze.com/docs/cloud-storage-call-the-s3-compatible-api). |
| Wasabi | `https://s3.<region>.wasabisys.com` | `https://my-bucket-feed.s3.<region>.wasabisys.com/` | Some regions also have legacy endpoint aliases. See [Wasabi service URLs](https://docs.wasabi.com/docs/service-url-endpoints). |
| Yandex Object Storage | `https://storage.yandexcloud.net` | `https://my-bucket-feed.storage.yandexcloud.net/` | Use the Object Storage S3 API endpoint. Configure public access with Yandex Cloud tools. See [Yandex S3 API](https://yandex.cloud/en/docs/storage/s3/). |
| Scaleway Object Storage | `https://s3.<region>.scw.cloud` | `https://my-bucket-feed.s3.<region>.scw.cloud/` | Region examples include `fr-par`, `nl-ams`, `pl-waw`, and `it-mil`. See [Scaleway endpoints](https://www.scaleway.com/en/docs/object-storage/concepts/). |

## Feed locks

Sleet writes the S3 lock file as `.feedlock` at the bucket root. The lock is shared by all feeds in the same bucket, including feeds separated with `feedSubPath`. See [Amazon S3 locks](locking.md#amazon-s3-locks).
