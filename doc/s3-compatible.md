# S3-compatible storage

Sleet can publish to storage providers that expose an S3-compatible API. This page explains the Sleet settings that differ from AWS S3 and gives community-reported provider examples.

> [!NOTE]
> These configurations are not tested by the Sleet maintainers. They are based on provider documentation and community reports. Send a PR if a provider changes its endpoint format or public-access model.

## Configure the source

Use `serviceURL` instead of `region` for non-AWS S3 APIs. Sleet requires exactly one of those properties.

Always set `path` to the public URL that NuGet clients will use. Sleet's built-in default path is built from an AWS Region, such as `https://s3-us-west-2.amazonaws.com/my-bucket-feed/`. With only `serviceURL`, the AWS Region can be null, and that default is wrong for other providers.

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

## Signing and compression

Some providers reject AWS SigV4 payload signing. Set `disablePayloadSigning` to `true` for those providers. Cloudflare R2 commonly needs this setting.

```json
{
  "disablePayloadSigning": true
}
```

By default, Sleet gzips JSON files and uploads them with `Content-Encoding: gzip`. Set `compress` to `false` if the provider or its public endpoint mishandles gzipped JSON.

```json
{
  "compress": false
}
```

See [Compression](how-it-works.md#compression) for how Sleet writes feed files.

## Create buckets outside Sleet

Sleet's automatic bucket creation uses AWS-specific APIs. It removes the bucket public access block, sets S3 Object Ownership, and writes an AWS bucket policy. Many S3-compatible providers do not support all of those APIs.

Create the bucket with the provider's tools first. Configure public read access for public feeds, or private access for private feeds, before you run `sleet push`.

## Provider examples

| Provider | `serviceURL` | `path` for clients | Notes |
| --- | --- | --- | --- |
| Cloudflare R2 | `https://<account-id>.r2.cloudflarestorage.com` | `https://<public-id>.r2.dev/` or a custom domain | Public access uses an R2 public bucket URL or custom domain. Set `disablePayloadSigning` to `true`. See [R2 S3 API](https://developers.cloudflare.com/r2/api/s3/api/) and [public buckets](https://developers.cloudflare.com/r2/buckets/public-buckets/). |
| MinIO | Your MinIO API endpoint, such as `https://minio.example.com` | The public URL for the bucket, such as `https://minio.example.com/my-bucket-feed/` or a reverse-proxied custom domain | Configure anonymous read access with MinIO policy tools. See [MinIO anonymous access](https://docs.min.io/aistor/reference/cli/mc-anonymous/) and [policy-based access control](https://min.io/docs/minio/linux/administration/identity-access-management/policy-based-access-control.html). |
| DigitalOcean Spaces | `https://<region>.digitaloceanspaces.com` | `https://my-bucket-feed.<region>.digitaloceanspaces.com/`, a CDN endpoint, or a custom domain | Use the Spaces public endpoint that clients can reach. See [Spaces API](https://docs.digitalocean.com/reference/api/spaces/) and [Spaces docs](https://docs.digitalocean.com/products/spaces/). |
| Backblaze B2 | `https://s3.<region>.backblazeb2.com` | `https://my-bucket-feed.s3.<region>.backblazeb2.com/` | Use the endpoint for the bucket's region. See [Backblaze S3-compatible API](https://www.backblaze.com/docs/cloud-storage-call-the-s3-compatible-api). |
| Wasabi | `https://s3.<region>.wasabisys.com` | `https://my-bucket-feed.s3.<region>.wasabisys.com/` | Some regions also have legacy endpoint aliases. See [Wasabi service URLs](https://docs.wasabi.com/docs/service-url-endpoints). |
| Yandex Object Storage | `https://storage.yandexcloud.net` | `https://my-bucket-feed.storage.yandexcloud.net/` | Use the Object Storage S3 API endpoint. Configure public access with Yandex Cloud tools. See [Yandex S3 API](https://yandex.cloud/en/docs/storage/s3/). |
| Scaleway Object Storage | `https://s3.<region>.scw.cloud` | `https://my-bucket-feed.s3.<region>.scw.cloud/` | Region examples include `fr-par`, `nl-ams`, `pl-waw`, and `it-mil`. See [Scaleway endpoints](https://www.scaleway.com/en/docs/object-storage/concepts/). |

## Feed locks

Sleet writes the S3 lock file as `.feedlock` at the bucket root. The lock is shared by all feeds in the same bucket, including feeds separated with `feedSubPath`. See [Amazon S3 locks](locking.md#amazon-s3-locks).
