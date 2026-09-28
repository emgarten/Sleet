# Private feeds

Sleet writes static files, and NuGet reads them with plain HTTP requests. To keep a feed private, keep the storage private and put something in front of it that checks who is asking. This page explains why, and lists the options.

## Why NuGet can't use storage credentials

NuGet can send a user name and password to a package source, using basic or Windows authentication. Azure Storage and Amazon S3 don't accept those. They need Entra ID tokens, shared access signatures, or request signatures that NuGet can't create.

A shared access signature (SAS) in the source URL doesn't help either. NuGet reads `index.json` with it, but every other request uses the URLs written in the feed files, which don't include the signature.

The credentials in `sleet.json` are only used by Sleet to write the feed. They don't control who can read it.

## Options

| Option | Storage | How it works |
| --- | --- | --- |
| Proxy with basic authentication | Any | A small web app or reverse proxy checks a user name and password, then returns files from private storage. |
| CloudFront with Lambda@Edge | Amazon S3 | CloudFront reads a private bucket, and a Lambda@Edge function checks basic authentication. See [private feed on AWS](private-feed-s3.md). |
| Web server with authentication | Local folder | IIS, nginx, or Apache serves the folder and requires basic or Windows authentication. See [web servers and static hosting](static-hosting.md). |
| Network restrictions | Azure, Amazon S3 | Storage only accepts reads from your network, for example with the Azure Storage firewall or private endpoints, or with an S3 bucket policy that checks `aws:SourceIp` or `aws:SourceVpce`. Clients don't need credentials. |

For the proxy and CloudFront options, clients add credentials to their NuGet.Config. See [sources that need credentials](consume-feed.md#sources-that-need-credentials).

## Point baseURI at the front end

Sleet writes absolute URLs into the feed files, using `baseURI`. Set `baseURI` to the URL of the proxy or CDN, so that all NuGet requests go through it. Sleet still writes to storage through `path`.

```json
{
  "sources": [
    {
      "name": "feed",
      "type": "azure",
      "container": "feed",
      "path": "https://myaccount.blob.core.windows.net/feed/",
      "baseURI": "https://packages.example.com/"
    }
  ]
}
```

Set `baseURI` before the first push. To change it on an existing feed, see [change baseURI or path in place](backup-migration.md#change-baseuri-or-path-in-place).

## Keep the storage private

When Sleet creates a container or bucket on the first push, it makes it public. Create it yourself first:

- **Azure**: create the container before the first push. New containers are private by default. Sleet doesn't change the access level of a container that already exists.

  ```bash
  az storage container create --name feed --account-name myaccount --auth-mode login
  ```

- **Amazon S3**: create the bucket before the first push and leave Block Public Access on. Sleet doesn't change the policy or public access settings of a bucket that already exists. See [create the bucket yourself](feed-type-s3.md#create-the-bucket-yourself) for the commands, and skip the public policy.
- **Local folder**: make sure the web server only serves the feed folder to signed-in users.

## Features that need anonymous access

- [Badges](badges.md) are fetched by shields.io, which can't sign in. They only work if the `badges/` files are public.
- The [symbol server](symbol-server.md#private-feeds) must be reachable by your debugger. Check that your debugger can sign in to the front end.
- An [external search](external-search.md) service must be reachable by clients, with the same kind of authentication as the feed.
