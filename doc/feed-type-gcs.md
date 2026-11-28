# Create a Google Cloud Storage feed

This quick start creates a public NuGet v3 feed in [Google Cloud Storage](https://cloud.google.com/storage/docs). Cloud Storage implements the S3 API in its [XML API](https://cloud.google.com/storage/docs/interoperability), so a Cloud Storage feed is an `s3` source with `provider` set to `gcs`. The `gcs` provider applies the settings that Cloud Storage needs:

- `serviceURL` defaults to `https://storage.googleapis.com`. Public reads use the same URL, so you don't need to set `path` or `baseURI`.
- `region` defaults to `auto`. Cloud Storage accepts any Region in request signatures, so you don't need to set it.
- `forcePathStyle` defaults to `true`, so bucket names that contain dots work.
- `disablePayloadSigning` defaults to `true` and `checksumMode` defaults to `whenRequired`. Cloud Storage rejects the checksums that the AWS SDK adds to uploads. With payload signing, it also keeps the SDK's `aws-chunked` encoding on uploaded files, and NuGet can't read them.
- Sleet downloads gzipped feed files as they're stored. Otherwise Cloud Storage [decompresses them](https://cloud.google.com/storage/docs/transcoding), and the download fails the AWS SDK's integrity check.
- When Sleet creates a bucket, it doesn't try to make the bucket public. Public access is an IAM policy, which the XML API can't set.

## Prerequisites

- A Google Cloud project, and the [gcloud CLI](https://cloud.google.com/sdk/docs/install) signed in to it. You can also do these steps in the Google Cloud console.
- A bucket with public read access. See [Create a public bucket](#create-a-public-bucket).
- An HMAC key. See [Credentials](#credentials).
- Sleet installed. See [Install Sleet](install.md).

## Create a public bucket

NuGet clients read the feed anonymously, so the bucket must be publicly readable. Create the bucket with [uniform bucket-level access](https://cloud.google.com/storage/docs/uniform-bucket-level-access), then let everyone read its objects:

```bash
gcloud storage buckets create gs://my-bucket-feed --location=us-central1 --uniform-bucket-level-access
gcloud storage buckets add-iam-policy-binding gs://my-bucket-feed --member=allUsers --role=roles/storage.objectViewer
```

Choose a [location](https://cloud.google.com/storage/docs/locations) near your users. [Public access prevention](https://cloud.google.com/storage/docs/public-access-prevention) and some organization policies block the second command. See [make data public](https://cloud.google.com/storage/docs/access-control/making-data-public).

Don't set `acl` in the source. Buckets with uniform bucket-level access reject uploads that set an ACL.

## Credentials

Sleet connects to Cloud Storage with an [HMAC key](https://cloud.google.com/storage/docs/authentication/hmackeys), which works like an S3 access key. Create a service account for Sleet, let it read and write the objects in the bucket, and create a key for it:

```bash
gcloud iam service-accounts create sleet-feed
gcloud storage buckets add-iam-policy-binding gs://my-bucket-feed --member=serviceAccount:sleet-feed@PROJECT_ID.iam.gserviceaccount.com --role=roles/storage.objectUser
gcloud storage hmac create sleet-feed@PROJECT_ID.iam.gserviceaccount.com
```

Replace `PROJECT_ID` with your project ID. The last command shows the key's `accessId` and `secret`. Save the secret, because Cloud Storage only shows it once. A new key can take up to a minute to work.

The *Storage Object User* role on the bucket is enough to push to it. If Sleet creates the bucket, the service account also needs permission to create buckets in the project, such as the *Storage Admin* role.

## Create the config file

Create a starter `sleet.json` file:

```bash
sleet create-config --provider gcs
```

Edit it to set the bucket name and HMAC key:

sleet.json:

```json
{
  "sources": [
    {
      "name": "feed",
      "type": "s3",
      "provider": "gcs",
      "bucketName": "my-bucket-feed",
      "accessKeyId": "HMAC_ACCESS_ID",
      "secretAccessKey": "HMAC_SECRET"
    }
  ]
}
```

You can also use [.netconfig](client-settings.md#netconfig):

```ini
[sleet "feed"]
    type = s3
    provider = gcs
    bucketName = my-bucket-feed
    accessKeyId = HMAC_ACCESS_ID
    secretAccessKey = HMAC_SECRET
```

You don't need to set `serviceURL`, `region`, or `path`. To keep the key out of the file, leave out `accessKeyId` and `secretAccessKey`, and set the `AWS_ACCESS_KEY_ID` and `AWS_SECRET_ACCESS_KEY` environment variables to the access ID and secret. See [AWS authentication](auth-aws.md).

For the full property list, see [Amazon S3 properties](client-settings.md#amazon-s3-properties).

## Push packages

Push one package or a folder of packages:

```bash
sleet push ./nupkgs
```

The first push initializes the feed with the default settings. To turn on the catalog from the start, run [init](commands.md#init) first.

If the bucket doesn't exist, Sleet creates it in the `US` multi-region, which needs permission to create buckets. Sleet can't make the bucket public, so [create the bucket](#create-a-public-bucket) yourself to choose its location and make it public.

## Caching

Cloud Storage [caches public files](https://cloud.google.com/storage/docs/caching) based on their `Cache-Control` header, and you can't purge this cache. Sleet sets `Cache-Control: no-store` on feed files by default, so Cloud Storage doesn't cache them. To cache packages, set `immutableCacheControl`, for example to `public, max-age=31536000, immutable`. Feed JSON files change with every push, so keep `mutableCacheControl` as `no-store` or use a short `max-age`. See [CDN and caching](cdn-caching.md).

## Use the feed

The package source URL is the bucket URL followed by `index.json`. For the example above:

```text
https://storage.googleapis.com/my-bucket-feed/index.json
```

Add it to NuGet clients. See [Use a feed with NuGet](consume-feed.md).

Cloud Storage doesn't serve custom domains over HTTPS. To use your own domain, put an [external Application Load Balancer with Cloud CDN](https://cloud.google.com/cdn/docs/setting-up-cdn-with-bucket) in front of the bucket, and set `baseURI` to the domain before the first push. See [Put a CDN in front of a feed](cdn-caching.md#put-a-cdn-in-front-of-a-feed).

## Next steps

- [S3-compatible storage](s3-compatible.md)
- [AWS authentication](auth-aws.md)
- [CDN and caching](cdn-caching.md)
- [Amazon S3 properties](client-settings.md#amazon-s3-properties)
