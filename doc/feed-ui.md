# Feed UI

Sleet writes a static browsing UI to `index.html` at the root of the feed. It is a single
self contained HTML file with no external dependencies and no build step. Opening it in a
browser lists every package on the feed and provides client side search.

The page reads the feed's own JSON files at runtime:

* `search/query` for package metadata (id, version, description, authors, tags, and links).
* `sleet.packageindex.json` as a fallback when `search/query` is unavailable, which lists
  ids and versions only.

All URLs in the page are relative, so the same file works from any host name the feed is
served under and never requires CORS configuration.

The page is written by `sleet init`, by `sleet recreate`, and by the automatic
initialization that happens on the first push to a new feed. Later pushes and all other
commands leave it alone and never read it.

## Feed UI URL

| feed type | url |
| --------- | --- |
| Azure Storage | `https://{account}.blob.core.windows.net/{container}/index.html` |
| Amazon S3 | `https://{bucket}.s3.{region}.amazonaws.com/index.html` |
| Any feed | `{feed url}/index.html` |

Object storage REST endpoints do not support index documents, so the file name must be
included in the URL. Requesting the bare feed root returns an error from the storage
service, not the UI.

To serve the UI from the bare root URL a static website endpoint or CDN is required:

* **Azure Storage** &mdash; enable the [static website endpoint](https://learn.microsoft.com/azure/storage/blobs/storage-blob-static-website)
  and set the index document to `index.html`. The feed is then browsable at
  `https://{account}.z00.web.core.windows.net/`. Note that CORS is not supported on the
  static website endpoint, so NuGet clients should continue to use the blob endpoint.
* **Amazon S3** &mdash; enable [static website hosting](https://docs.aws.amazon.com/AmazonS3/latest/userguide/WebsiteHosting.html)
  with `index.html` as the index document. S3 website endpoints serve HTTP only, so front
  the bucket with CloudFront for HTTPS.
* **CloudFront** &mdash; set the *Default Root Object* to `index.html`. This applies to the
  distribution root only, not to sub paths.
* **Cloudflare R2** &mdash; R2 has no index document feature. Use a Worker in front of the
  bucket to rewrite `/` to `/index.html`, or link directly to `index.html`.

## Customizing the UI

Sleet only creates `index.html` when it does not already exist, so a custom page can be
used instead. Upload your own `index.html` to the feed root and Sleet will leave it alone.

Note that `sleet recreate` rebuilds the feed from scratch and will replace a custom page
with the default one.

## How to disable the feed UI

The UI can be disabled in the feed settings using the following command

`sleet feed-settings --set feeduienabled:false`

Run `sleet init` afterwards to remove the existing `index.html` from the feed.

The feed UI is enabled by default for new feeds.
