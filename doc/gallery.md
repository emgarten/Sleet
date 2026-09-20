# Gallery

Sleet writes a package gallery to `index.html` at the root of the feed. It is a single
self contained HTML file with no external dependencies and no build step. Opening it in a
browser lists every package on the feed, filters by package id, and shows a detail page
for each package.

The page reads the feed's own JSON files at runtime:

* `sleet.packageindex.json` once on load. It holds every package id and version, which is
  all the list, the id filter, and paging need.
* `registration/{id}/index.json` for a package's metadata, version history and
  dependencies. This is requested only for the packages that are actually shown, so the
  cost of drawing the list does not grow with the size of the feed.
* `flatcontainer/{id}/{version}/icon` and `.../readme` when a package has them.

`search/query` is deliberately not used. It is a single large document covering the whole
feed, and `externalsearch` can point it at a different service.

All URLs in the page are relative, so the same file works from any host name the feed is
served under and never requires CORS configuration.

The page is written by `sleet init`, by `sleet recreate`, and by the automatic
initialization that happens on the first push to a new feed. Later pushes and all other
commands leave it alone and never read it.

## Browsing

The list shows 20 packages per page. Each row has the package icon, the latest version,
the number of versions, the publish date, a direct download link, and the description,
authors and tags. Packages without an embedded icon fall back to a generic one.

Both the current page and the id filter are kept in the URL fragment, so `#q=storage`,
`#page=3` and `#package/Newtonsoft.Json` can be bookmarked and shared, and the browser
back button works as expected.

A package page shows the description, authors, tags, publish date, project and license
links, the full version history with download links, and the dependencies of the selected
version. Dependencies that are also on the feed link to their own package page.

Selecting a version from the version history changes the URL to
`#package/Newtonsoft.Json/13.0.3`, so a specific version can be bookmarked and shared.
Leaving the version off addresses whichever version is the latest at the time the link is
opened. If a pinned version is no longer on the feed the page falls back to the latest
version and says so.

Readme files are shown as plain text rather than rendered markdown, since the content
comes from the package author.

## Gallery URL

| feed type | url |
| --------- | --- |
| Azure Storage | `https://{account}.blob.core.windows.net/{container}/index.html` |
| Amazon S3 | `https://{bucket}.s3.{region}.amazonaws.com/index.html` |
| Any feed | `{feed url}/index.html` |

Object storage REST endpoints do not support index documents, so the file name must be
included in the URL. Requesting the bare feed root returns an error from the storage
service, not the gallery.

To serve the gallery from the bare root URL a static website endpoint or CDN is required:

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

## Customizing the gallery

Sleet only creates `index.html` when it does not already exist, so a custom page can be
used instead. Upload your own `index.html` to the feed root and Sleet will leave it alone.

Note that `sleet recreate` rebuilds the feed from scratch and will replace a custom page
with the default one.

## How to disable the gallery

The gallery can be disabled in the feed settings using the following command

`sleet feed-settings --set galleryenabled:false`

Run `sleet init` afterwards to remove the existing `index.html` from the feed.

The gallery is enabled by default for new feeds.
