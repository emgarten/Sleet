# CDN and caching

Sleet feeds are read much more often than they are written. A CDN can reduce latency and origin cost, but cache settings decide how quickly clients see new feed data.

## Cache-control properties

Azure and Amazon S3 feeds support these source properties:

| Property | Default | Used for |
| --- | --- | --- |
| `immutableCacheControl` | `no-store` | Versioned package files and package assets. |
| `mutableCacheControl` | `no-store` | Feed indexes and other JSON files that change over time. |

Local feeds do not set HTTP headers. Configure `Cache-Control` in your web server or CDN.

Sleet sets headers when a file is uploaded. Changing these properties affects files written later. To apply new headers everywhere, run `recreate` so Sleet downloads the packages, destroys the feed, initializes it again, and pushes the packages back.

## Files that use each header

Azure Blob Storage and Amazon S3 use the same cache-control groups:

| File type or path | Content type | Cache-control property |
| --- | --- | --- |
| `.nupkg` | `application/zip` | `immutableCacheControl` |
| `.nuspec`, `.xml` | `application/xml` | `immutableCacheControl` |
| `.dll`, `.pdb` | `application/octet-stream` | `immutableCacheControl` |
| Paths ending in `/icon` | `image/png` | `immutableCacheControl` |
| Paths ending in `/readme` | `text/markdown` | `immutableCacheControl` |
| `.json` files | `application/json` | `mutableCacheControl` |
| Extensionless JSON files such as `search/query` and `autocomplete/query` | `application/json` | `mutableCacheControl` |
| `.svg` files | `image/svg+xml` | `mutableCacheControl` |

Unknown file types keep `Cache-Control: no-store` and Sleet logs a warning.

## Recommended values

A common starting point is:

```json
{
  "sources": [
    {
      "name": "feed",
      "type": "s3",
      "bucketName": "my-bucket-feed",
      "region": "us-west-2",
      "path": "https://my-bucket-feed.s3.us-west-2.amazonaws.com/",
      "immutableCacheControl": "public, max-age=31536000, immutable",
      "mutableCacheControl": "public, max-age=300"
    }
  ]
}
```

Use a long lifetime for immutable files because package versions are normally not changed. Use a short lifetime, such as five minutes, for mutable files so new versions appear quickly. `no-cache` is also reasonable for mutable files when you want the CDN or client to revalidate before reuse.

Long mutable caching means stale reads. NuGet may not discover a newly pushed version until the cached index expires or is purged.

Sleet uploads `.nupkg` files first, then `.nuspec` files, then other package data, then `index.json` files, then the search query file. This ordering helps clients avoid seeing an index entry before the package file exists.

Be careful with `push --force` and `delete` if immutable files have long cache lifetimes. A CDN can keep old bytes, and NuGet also keeps restored packages in the global packages folder.

## Compression

Azure Blob Storage uploads JSON as gzip-compressed content with `Content-Encoding: gzip`.

Amazon S3 uploads JSON as gzip-compressed content when `compress` is `true`, which is the default:

```json
{
  "name": "feed",
  "type": "s3",
  "bucketName": "my-bucket-feed",
  "region": "us-west-2",
  "path": "https://my-bucket-feed.s3.us-west-2.amazonaws.com/",
  "compress": true
}
```

The CDN should pass through `Content-Encoding: gzip`. Do not re-compress objects that are already stored compressed. Check headers after publishing:

```bash
curl -I https://packages.example.com/index.json
curl -I https://packages.example.com/search/query
```

## Put a CDN in front of a feed

1. Point the CDN origin at the Azure container, S3 bucket, or web server that hosts the feed.
1. Configure the CDN to cache `GET` responses and respect origin `Cache-Control` headers, or set equivalent CDN rules.
1. Set the Sleet source `baseURI` to the CDN URL with the same path structure.
1. Run `recreate` if the feed already exists with a different `baseURI`. For a local feed, use [rebuild a local feed without recreate](backup-migration.md#rebuild-a-local-feed-without-recreate) instead.

Changing `baseURI` or `path` on an existing feed fails until the feed is recreated:

```text
The path or baseURI set in sleet.json does not match the URIs found in index.json...
```

After a push, purge mutable files from the CDN if your `mutableCacheControl` value allows stale data longer than you want. Start with `index.json`, `sleet.packageindex.json`, `search/query`, `autocomplete/query`, `registration/*/index.json`, and `flatcontainer/*/index.json`.

## Provider notes

Azure Front Door Standard or Premium is the current Azure CDN option to consider for new deployments. Azure Front Door documentation describes cache behavior and notes that Azure Front Door classic retires on March 31, 2027. Microsoft also announced retirement for Azure CDN from Microsoft classic and Azure CDN from Edgio, so check the current Azure retirement guidance before creating a new profile.

Amazon CloudFront can use an S3 bucket as an origin. AWS recommends S3 regional bucket origin names such as `my-bucket-feed.s3.us-west-2.amazonaws.com`. For a private S3 feed behind CloudFront, see [Private feed on AWS](private-feed-s3.md).

Cloudflare respects origin `Cache-Control` headers for cacheable responses, but it does not cache JSON by file extension by default. Use Cache Rules if you want Cloudflare to cache Sleet JSON and extensionless query files.
