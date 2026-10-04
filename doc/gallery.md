# Package gallery

Sleet feeds include a package gallery, a web page for browsing the packages on a feed. It
lists every package with its icon, latest version, and number of versions, filters the
list by package id, and opens a package to show its versions and the details of each
version, including its dependencies, readme, and download links.

The gallery is a single `index.html` file that Sleet writes to the root of the feed, next
to `index.json`. It reads everything it shows from the feed's own files, so it needs no
server or search service and is served by the same storage as the feed, such as Azure
Storage, Amazon S3, or Cloudflare R2.

The gallery is enabled by default and is turned on and off with the `galleryenabled` feed
setting, see [enabling and disabling the gallery](#enabling-and-disabling-the-gallery).

The gallery has no sign in of its own. The browser reads the feed's files directly, so
the gallery is intended for feeds that are publicly readable.

## Opening the gallery

The gallery is at `index.html` in the same location as the feed's `index.json`. Take the
feed's package source URL and replace `index.json` with `index.html`.

| feed type | gallery URL |
| --------- | ----------- |
| Azure Storage | `https://{account}.blob.core.windows.net/{container}/index.html` |
| Amazon S3 | `https://{bucket}.s3.{region}.amazonaws.com/index.html` |
| Any feed | `{feed url}/index.html` |

Include `index.html` in the URL. Storage endpoints do not serve a default page, so opening
the bare feed URL does not show the gallery. To serve the gallery from the bare URL see
[serving the gallery from the root URL](#serving-the-gallery-from-the-root-url).

The page must be opened over http or https. Browsers do not let a page opened from disk
read other files, so opening `index.html` directly from a local feed folder does not work.

## Browsing packages

The gallery has three views. Each view has its own URL, so it can be bookmarked, shared,
and returned to with the browser's back button.

| View | URL | Shows |
| --- | --- | --- |
| Packages | `index.html` | Package ids in alphabetical order, 20 per page, each with its icon, latest version, and number of versions |
| Package | `index.html#package/{id}` | Every version of one package with its publish date |
| Version | `index.html#package/{id}/{version}` | The details of one version |

The filter box at the top of the page shows only the packages whose id contains the text
typed, ignoring case. The filter and the page number are kept in the URL, for example
`index.html#q=storage&page=2`.

The latest version is the newest stable version, or the newest prerelease when a package
has no stable version. Packages without an icon show a generic one.

A version page shows the package's title, description, authors, publish date, tags,
project and license links, links to download the `.nupkg` and view the `.nuspec`, the
dependencies for each target framework, and the readme. Dependencies that are on the same
feed link to their package. Readmes are shown as plain text rather than rendered markdown.

Ids and versions in a URL match regardless of case, and versions match in any form NuGet
treats as equal, such as `1.0` for `1.0.0`. If the version in a URL is not on the feed,
the page shows the latest version instead and says so.

## Enabling and disabling the gallery

The gallery is controlled by the `galleryenabled` feed setting. Feed settings are stored
in `sleet.settings.json` on the feed itself, so the setting applies to everyone who pushes
to the feed. It defaults to `true`, and a feed that does not have the setting, such as one
created by an older version of Sleet, is treated as enabled.

Sleet applies the setting only when it creates or rebuilds the feed:

* `sleet init`
* the first `sleet push` to a new feed, which creates the feed
* `sleet recreate`

Changing the setting does not add or remove the gallery by itself, and pushing to an
existing feed never adds, updates, or removes it. Run `sleet recreate` after changing the
setting. `sleet recreate` downloads every package, deletes the feed, and creates it again
from the same packages and settings, so it can take a while on a large feed.

### Checking the setting

`sleet feed-settings --get galleryenabled`

The output `galleryenabled : not found!` means the setting is not set and the gallery is
enabled.

### Disabling the gallery

```
sleet feed-settings --set galleryenabled:false
sleet recreate
```

This removes `index.html` and the package links from `index.json`. Later pushes do not add
them back.

To start a new feed without the gallery, create the feed with `sleet init` before pushing
any packages, then disable the gallery and recreate the empty feed:

```
sleet init
sleet feed-settings --set galleryenabled:false
sleet recreate
```

### Enabling the gallery

```
sleet feed-settings --set galleryenabled:true
sleet recreate
```

A feed created with an older version of Sleet does not have the setting, so running
`sleet recreate` is enough to add the gallery.

## Updating the gallery

Sleet writes `index.html` when it creates the feed and does not change it afterwards. The
footer of the page shows the version of Sleet that wrote it. To get the gallery from a
newer version of Sleet, run `sleet recreate` with that version.

Running `sleet recreate` with a version of Sleet that does not include the gallery removes
the gallery from the feed.

## Package links in Visual Studio

While the gallery is enabled, the feed's `index.json` includes a
`PackageDetailsUriTemplate` resource that points at `index.html#package/{id}/{version}`.
Visual Studio and other NuGet clients use it to link each package to its page in the
gallery. NuGet only shows these links for feeds served over https.

## Serving the gallery from the root URL

Object storage REST endpoints do not support index documents. To open the gallery from the
bare feed URL a static website endpoint or CDN is required:

* **Azure Storage** &mdash; the [static website endpoint](https://learn.microsoft.com/azure/storage/blobs/storage-blob-static-website)
  only serves the `$web` container. For a feed in `$web`, enable static website hosting
  with `index.html` as the index document, and the gallery opens at the primary endpoint
  shown in the portal, for example `https://{account}.z13.web.core.windows.net/`. NuGet
  clients continue to use the feed's blob endpoint.
* **Amazon S3** &mdash; enable [static website hosting](https://docs.aws.amazon.com/AmazonS3/latest/userguide/WebsiteHosting.html)
  with `index.html` as the index document. S3 website endpoints serve HTTP only, so front
  the bucket with CloudFront for HTTPS.
* **CloudFront** &mdash; set the *Default Root Object* to `index.html`. This applies to the
  distribution root only, not to sub paths.
* **Cloudflare R2** &mdash; R2 has no index document feature. On a custom domain, add a
  [URL Rewrite Rule](https://developers.cloudflare.com/rules/transform/url-rewrite/) that
  rewrites `/` to `/index.html`. The `r2.dev` development URL does not support rules, so
  link directly to `index.html` there.

## Using a custom page

Sleet only writes `index.html` when the feed does not already have one, so you can replace
the gallery with your own page. Upload your own `index.html` to the root of the feed and
Sleet leaves it in place when pushing. While the gallery is enabled, NuGet clients link
packages to `index.html#package/{id}/{version}`, so your page receives those links too.

`sleet recreate` deletes the feed's files, including a custom `index.html`, so upload the
page again after running it.

## How the gallery reads the feed

The gallery is a single self contained HTML file with no external dependencies. It reads
these files from the feed it was opened from:

* `sleet.packageindex.json`, once when the page loads. It lists every package id and
  version, which is all the package list, the filter, and paging need.
* `registration/{id}/index.json`, when a package is opened, for its versions, metadata,
  and dependencies. Browsing the package list never downloads these.
* `flatcontainer/{id}/{version}/icon` and `flatcontainer/{id}/{version}/readme`, when a
  package has them.

The search endpoint is not used. It is a single large document covering the whole feed,
and `externalsearch` can point it at a different service.

All requests use URLs relative to the page, so it works under any host name that serves
the feed and never needs CORS configuration. A query string on the page URL, such as an
Azure SAS token, is not passed on to these requests.

If the page cannot read `sleet.packageindex.json` it shows *Unable to read this feed*,
which usually means the feed is not publicly readable. If a single package cannot be read,
the error is shown on that package's page and the rest of the feed stays browsable.
