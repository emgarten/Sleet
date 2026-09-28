# AWS authentication

This page explains how Sleet chooses AWS credentials for Amazon S3 feeds. It also shows IAM policies for publishing to an existing bucket and for letting Sleet create a public bucket.

## Credential order

Sleet tries S3 credentials in this order.

### AWS profile

Set `profileName` to use the AWS shared credentials and config files. Sleet checks the shared credentials file first, then the AWS SDK profile chain, including SSO profiles.

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

For SSO profiles, sign in before running Sleet:

```bash
aws sso login --profile sleetProfile
```

Sleet does not prompt for SSO login. `profileName` takes precedence over keys and environment variables. If the profile is missing, Sleet fails with a message that starts: `The specified AWS profileName ... could not be found.`

### Access keys in Sleet config

You can set `accessKeyId` and `secretAccessKey` in `sleet.json`, but do not commit real keys. Use [$TOKEN$ tokens](client-settings.md#tokens) or environment variables for shared files.

```json
{
  "sources": [
    {
      "name": "feed",
      "type": "s3",
      "bucketName": "my-bucket-feed",
      "region": "us-west-2",
      "path": "https://my-bucket-feed.s3.us-west-2.amazonaws.com/",
      "accessKeyId": "$AWS_ACCESS_KEY_ID$",
      "secretAccessKey": "$AWS_SECRET_ACCESS_KEY$"
    }
  ]
}
```

### AWS environment variables

If no `profileName` or explicit keys are set, Sleet checks the standard AWS access key variables:

```powershell
$env:AWS_ACCESS_KEY_ID = "<access-key-id>"
$env:AWS_SECRET_ACCESS_KEY = "<secret-access-key>"
$env:AWS_SESSION_TOKEN = "<session-token>"
```

`AWS_SESSION_TOKEN` is used by the AWS SDK when the credentials are temporary.

### ECS task role

If the access key variables are not set, Sleet checks the `AWS_CONTAINER_CREDENTIALS_RELATIVE_URI` environment variable and uses the ECS container credentials provider. You normally don't set this variable yourself. ECS sets it for tasks that have a task role.

### AWS SDK default chain

If none of the earlier options apply, Sleet uses the AWS SDK default credential chain. This can include EC2 instance profiles, web identity credentials, and other SDK-supported sources. Sleet first calls STS `GetCallerIdentity` to verify that credentials can be found. If that fails, Sleet reports: `Failed to determine AWS identity - ensure you have an IAM role set, have set up default credentials or have specified a profile/key pair.`

## GitHub Actions with OIDC

Use [`aws-actions/configure-aws-credentials@v6`](https://github.com/aws-actions/configure-aws-credentials) to assume an AWS role with GitHub OIDC. The action exports AWS environment variables that Sleet picks up.

```yaml
name: publish

on:
  workflow_dispatch:

permissions:
  id-token: write
  contents: read

jobs:
  publish:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v7
        with:
          persist-credentials: false
      - uses: aws-actions/configure-aws-credentials@v6
        with:
          role-to-assume: arn:aws:iam::123456789012:role/sleet-publisher
          aws-region: us-west-2
      - run: dotnet tool install -g sleet --version "7.*"
      - run: sleet push ./nupkgs
```

See [Publish from CI](ci-server.md) for full workflow examples.

## IAM policy for publishing

For publishing to an existing bucket, grant Sleet read, write, delete, and list access to the feed bucket. Sleet writes a `.feedlock` file before commands that lock the feed. This includes read-oriented commands such as `stats`, `validate`, and `download` unless you pass `--no-lock` to `download`.

```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Sid": "SleetObjectAccess",
      "Effect": "Allow",
      "Action": [
        "s3:GetObject",
        "s3:PutObject",
        "s3:DeleteObject",
        "s3:AbortMultipartUpload"
      ],
      "Resource": "arn:aws:s3:::my-bucket-feed/*"
    },
    {
      "Sid": "SleetBucketList",
      "Effect": "Allow",
      "Action": [
        "s3:ListBucket"
      ],
      "Resource": "arn:aws:s3:::my-bucket-feed"
    }
  ]
}
```

Add `s3:PutObjectAcl` to the object statement only if your Sleet source sets `acl`.

If Sleet should create a public bucket, also grant these bucket-level actions:

```json
{
  "Sid": "SleetCreatePublicBucket",
  "Effect": "Allow",
  "Action": [
    "s3:CreateBucket",
    "s3:PutBucketPublicAccessBlock",
    "s3:PutBucketOwnershipControls",
    "s3:PutBucketPolicy"
  ],
  "Resource": "arn:aws:s3:::my-bucket-feed"
}
```

Add `s3:PutBucketAcl` only if your Sleet source sets `acl`. Account-level S3 Block Public Access must also allow public bucket policies.

## Troubleshooting

### Failed to determine AWS identity

This means Sleet reached the SDK default-chain path and STS `GetCallerIdentity` could not find usable credentials. Set `profileName`, run `aws sso login --profile <name>` for SSO profiles, export AWS environment variables, or run Sleet on a host with an IAM role.

### Wrong region

The bucket Region must match the `region` value in `sleet.json`. If you use S3-compatible storage, use `serviceURL` instead of `region`. Do not set both.

### SSO token expired

Run `aws sso login --profile <name>` again. Sleet does not refresh SSO tokens interactively.

### Waiting to obtain feed lock

Sleet writes `.feedlock` at the bucket root and deletes it when the command ends. If the credentials lack `s3:DeleteObject`, Sleet warns `Unable to clean up lock` and the lock stays, so later commands wait forever. The same happens when a command is stopped before it finishes. See [Amazon S3 locks](locking.md#amazon-s3-locks) and [waiting to obtain a feed lock](troubleshooting.md#waiting-to-obtain-a-feed-lock).
