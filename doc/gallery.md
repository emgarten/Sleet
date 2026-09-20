# Gallery

Sleet writes a package gallery to `index.html` at the root of the feed. It is a single
self contained HTML file with no external dependencies and no build step. Opening it in a
browser lists every package on the feed, filters by package id, and drills down from a
package to its versions and then to a single version.

The page reads the feed's own JSON files at runtime:

* `sleet.packageindex.json` once on load. It holds every package id and version, which is
  everything the top level list, the id filter, and paging need.
* `registration/{id}/index.json` for a package's version history, metadata and
  dependencies. This is requested only when a package is opened, so the cost of browsing
  the list does not grow with the size of the feed.
* `flatcontainer/{id}/{version}/icon` and `.../readme` when a package has them.

`search/query` is deliberately not used. It is a single large document covering the whole
feed, and `externalsearch` can point it at a different service.

All URLs in the page are relative, so the same file works from any host name the feed is
served under and never requires CORS configuration.

The page is written by `sleet init`, by `sleet recreate`, and by the automatic
initialization that happens on the first push to a new feed. Later pushes and all other
commands leave it alone and never read it.

## Browsing

The gallery has three levels, and the current level is always kept in the URL fragment so
that any view can be bookmarked, shared, and reached with the browser back button.

| View | URL | Shows |
| --- | --- | --- |
| Packages | `#` | 20 package ids per page, each with its icon, latest version, and total number of versions |
| Versions | `#package/Newtonsoft.Json` | Every version of one package with its publish date |
| Version | `#package/Newtonsoft.Json/13.0.3` | Everything the feed knows about one specific version |

The id filter and the current page are also kept in the fragment, as `#q=storage` and
`#page=3`, and the breadcrumb on a package page returns to the list page it was opened
from.

Latest means the newest stable version, or the newest prerelease when a package has no
stable version. Packages without an embedded icon fall back to a generic one.

A version page shows the description, authors, tags, publish date, project and license
links, a download link for the `.nupkg`, a link to the `.nuspec`, the dependencies of that
version grouped by target framework, and its readme. Dependencies that are also on the
feed link to their own package.

Leaving the version off a package URL addresses the package rather than a single version,
which keeps the link stable as new versions are pushed. If a pinned version is not on the
feed the page falls back to the latest version and says so.

If a package's registration cannot be read the error is shown on that package's page
rather than replacing the gallery, so the rest of the feed stays browsable.

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
