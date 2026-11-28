# Create an Amazon S3 feed

This quick start creates a public NuGet v3 feed in Amazon S3. It covers the Sleet config, the first package push, and the bucket policy change that avoids 403 responses for missing packages.

## Prerequisites

- An AWS account with permission to create or write to the bucket.
- Sleet installed. See [Install Sleet](install.md).
- AWS credentials configured for Sleet. See [AWS authentication](auth-aws.md).

Use bucket names without dots. Dots in bucket names can break TLS with virtual-hosted S3 URLs.

## Create the config file

Create a starter `sleet.json` file:

```bash
sleet create-config --s3
```

The generated source looks like this:

```json
{
  "username": "",
  "useremail": "",
  "sources": [
    {
      "name": "myAmazonS3Feed",
      "type": "s3",
      "bucketName": "bucketname",
      "region": "us-east-1",
      "profileName": "credentialsFileProfileName"
    }
  ]
}
```

Edit it for your bucket and region. Set `path` explicitly to the public URL that NuGet clients will use:

sleet.json:

```json
{
  "sources": [
    {
      "name": "feed",
      "type": "s3",
      "bucketName": "my-bucket-feed",
      "region": "us-west-2",
      "path": "https://my-bucket-feed.s3.us-west-2.amazonaws.com/",
      "profileName": "sleetProfile"
    }
  ]
}
```

You can also use [.netconfig](client-settings.md#netconfig):

```ini
[sleet "feed"]
    type = s3
    bucketName = my-bucket-feed
    region = us-west-2
    path = https://my-bucket-feed.s3.us-west-2.amazonaws.com/
    profileName = sleetProfile
```

For the full property list, see [Amazon S3 properties](client-settings.md#amazon-s3-properties).

| Property | Use |
| --- | --- |
| `bucketName` | The S3 bucket that stores the feed. |
| `region` | The AWS Region for the bucket. Use this for AWS S3. |
| `path` | The public feed root URL. Set it explicitly. |
| `profileName` | The AWS shared profile Sleet should use. |
| `baseURI` | Optional URL written into feed JSON, such as a CDN URL. |

## Push packages

Push one package or a folder of packages:

```bash
sleet push ./nupkgs
```

On the first push, Sleet creates the bucket if it does not exist, removes the bucket public access block, sets Object Ownership to `BucketOwnerPreferred`, and adds a public-read bucket policy for `s3:GetObject` on `arn:aws:s3:::my-bucket-feed/*`. If you set `acl`, Sleet also applies that canned bucket ACL.

Sleet doesn't change the policy or public access settings of an existing bucket. If something else creates the bucket while Sleet is creating it, Sleet logs a warning and leaves the bucket's settings unchanged. Sleet can't always detect this. In us-east-1, Amazon S3 reports success when you create a bucket that you already own, and Amazon S3 can report a bucket that was just created as missing for a short time. In that case Sleet only leaves the settings unchanged if the bucket already contains files, so an empty bucket created at the same moment can still be made public. Create private buckets yourself before the first push, not during it.

> [!WARNING]
> Account-level S3 Block Public Access can still block the public bucket policy. If bucket creation fails, Sleet logs: `Unable to update S3 bucket. Ensure that the login info used has AmazonS3FullAccess and that the AWS account allows public access buckets: https://docs.aws.amazon.com/AWSCloudFormation/latest/UserGuide/aws-properties-s3-bucket-publicaccessblockconfiguration.html`.

The first push initializes the feed with the default [feed settings](feed-settings.md). To start with different settings, run [init](commands.md#init) before the first push. `init` creates the bucket the same way. To turn on the catalog:

```bash
sleet init --with-catalog
```

To create the feed without the [package gallery](gallery.md):

```bash
sleet init --set galleryenabled:false
```

## Fix 403 responses for missing packages

Sleet's auto-created public policy grants `s3:GetObject`, but not `s3:ListBucket`. With S3, anonymous requests for missing keys return 403 unless the caller also has `s3:ListBucket`. NuGet queries every configured source for every package. A 403 from one source can make restore fail or prompt for credentials instead of continuing to the next source.

Add `s3:ListBucket` if this is a public feed:

```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Sid": "AllowPublicRead",
      "Effect": "Allow",
      "Principal": "*",
      "Action": [
        "s3:GetObject"
      ],
      "Resource": [
        "arn:aws:s3:::my-bucket-feed/*"
      ]
    },
    {
      "Sid": "AllowPublicListForMissingPackage404",
      "Effect": "Allow",
      "Principal": "*",
      "Action": [
        "s3:ListBucket"
      ],
      "Resource": [
        "arn:aws:s3:::my-bucket-feed"
      ]
    }
  ]
}
```

> [!WARNING]
> `s3:ListBucket` makes the object listing public. Do not add it to buckets that must hide object names.

## Create the bucket yourself

If you create the bucket before running Sleet, create it in the target Region, allow public bucket policies, and apply the full public-read policy above. Save the policy as `public-feed-policy.json` first.

For Regions other than `us-east-1`:

```bash
aws s3api create-bucket \
  --bucket my-bucket-feed \
  --region us-west-2 \
  --create-bucket-configuration LocationConstraint=us-west-2

aws s3api put-public-access-block \
  --bucket my-bucket-feed \
  --public-access-block-configuration BlockPublicAcls=false,IgnorePublicAcls=false,BlockPublicPolicy=false,RestrictPublicBuckets=false

aws s3api put-bucket-policy \
  --bucket my-bucket-feed \
  --policy file://public-feed-policy.json
```

For `us-east-1`, omit `--create-bucket-configuration`.

## Use the feed

The package source URL is:

```text
https://my-bucket-feed.s3.us-west-2.amazonaws.com/index.json
```

Add it to NuGet clients. See [Use a feed with NuGet](consume-feed.md).

## Browse the feed

Sleet writes a [package gallery](gallery.md) to the feed, a web page for browsing its packages. Open it in a browser at the bucket URL plus `index.html`:

```text
https://my-bucket-feed.s3.us-west-2.amazonaws.com/index.html
```

The S3 endpoint doesn't serve a default page, so include `index.html` in the URL. To open the gallery from the bucket URL alone, see [serve the gallery from the root URL](gallery.md#serve-the-gallery-from-the-root-url).

## Next steps

- [AWS authentication](auth-aws.md)
- [Private feed options](private-feeds.md)
- [Private feed on AWS](private-feed-s3.md)
- [S3-compatible storage](s3-compatible.md)
- [Multiple feeds](multiple-feeds.md)
- [CDN and caching](cdn-caching.md)
- [Amazon S3 properties](client-settings.md#amazon-s3-properties)
