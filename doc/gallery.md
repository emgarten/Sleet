# Package gallery

The package gallery is a web page for browsing the packages on a feed. It lists every package with its icon, latest version, and number of versions, and filters the list by package id. Open a package to see its versions, and open a version to see its details, such as its dependencies, readme, and download links.

The gallery is a single `index.html` file that Sleet writes to the root of the feed, next to `index.json`. It reads everything it shows from the feed's own files, so it doesn't need a server or a search service. The storage that serves the feed, such as Azure Storage, Amazon S3, or Cloudflare R2, serves the gallery too.

The gallery is on by default. Turn it on or off with the `galleryenabled` [feed setting](feed-settings.md#galleryenabled), see [enable or disable the gallery](#enable-or-disable-the-gallery).

> [!NOTE]
> The gallery doesn't sign in. The browser reads the feed's files directly, so the gallery is meant for feeds that are publicly readable.

## Open the gallery

The gallery is at `index.html`, in the same place as the feed's `index.json`. Take the package source URL and replace `index.json` with `index.html`:

| Feed | Gallery URL |
| --- | --- |
| Azure | `https://myaccount.blob.core.windows.net/feed/index.html` |
| Amazon S3 | `https://my-bucket-feed.s3.us-west-2.amazonaws.com/index.html` |
| Cloudflare R2 | `https://nuget.example.com/index.html` |
| Local folder behind a web server | `https://example.com/feed/index.html` |

Azure, Amazon S3, and R2 don't serve a default page at these URLs, so include `index.html`. To open the gallery from the feed URL alone, see [serve the gallery from the root URL](#serve-the-gallery-from-the-root-url). Most web servers serve `index.html` as the default page of a folder, so on a web server the gallery also opens at the feed URL, such as `https://example.com/feed/`.

Open the gallery over http or https. Browsers don't let a page opened from disk read other files, so opening `index.html` straight from a local feed folder doesn't work. To serve a local feed for a quick look, see [test a local server](static-hosting.md#test-a-local-server).

## Browse packages

The gallery has four views. Each view has its own URL, so you can bookmark it, share it, and return to it with the browser's back button.

| View | URL | Shows |
| --- | --- | --- |
| Packages | `index.html` | Package ids in alphabetical order, 20 per page, each with its icon, latest version, and number of versions |
| Package | `index.html#package/{id}` | Every version of one package, with its publish date |
| Version | `index.html#package/{id}/{version}` | The details of one version |
| Nuspec | `index.html#package/{id}/{version}/nuspec` | The `.nuspec` file of one version, as text |

The filter box at the top of the page shows only the packages whose id contains the text you type, ignoring case. The URL keeps the filter and the page number, such as `index.html#q=storage&page=2`.

The latest version is the newest stable version, or the newest prerelease version when a package has no stable version. Packages without an icon show a generic one.

The gallery opens in dark mode. The button at the right of the header switches between dark and light mode, and the browser remembers the choice for the site.

A version page shows the package's title, description, authors, publish date, tags, a link to the project, links to download the `.nupkg` and view the `.nuspec`, the dependencies for each target framework, and the readme. The `.nuspec` opens in the gallery as text. The project link appears only when the package's project URL is an absolute `http` or `https` URL on another site. Dependencies on packages from the same feed link to those packages.

Ids and versions in a URL match regardless of case, and versions match in any form that NuGet treats as equal, such as `1.0` for `1.0.0`. If the version in a URL isn't on the feed, the page shows the latest version instead and says so.

### Readmes

The gallery renders a package's readme as markdown. It supports CommonMark and the GitHub additions that readmes use most: tables, task lists, strikethrough, alerts such as `> [!NOTE]`, and bare URLs that become links.

Anyone who can push to a feed controls its readmes, so the gallery limits what a readme can do:

- HTML isn't rendered. Formatting tags such as `<p>`, `<b>`, and `<a>` are removed and their text is kept, `<br>` and `<img>` work like their markdown versions, and other tags, such as `<script>`, show as text.
- Only absolute `http`, `https`, and `mailto` links to other sites are followed. Other links, including relative links such as `docs/usage.md` and links to the feed's own site, show as text.
- Only absolute `http` and `https` images are shown, and they load without sending the gallery's address. An image with a relative path, or one that fails to load, shows its alt text.
- Images from the feed's own site, and from the [image and badge hosts that nuget.org allows](https://learn.microsoft.com/nuget/nuget-org/package-readme-on-nuget-org#allowed-domains-for-images-and-badges), such as `img.shields.io`, load with the readme. Loading an image tells its site that someone is reading the readme, so images from other sites show their alt text until you select **Show images** above the readme.
- Readmes larger than 1 MB show as plain text.

### Link previews

When you share a link to the gallery in an app that previews links, such as Slack, Discord, or Microsoft Teams, the preview shows the title *Sleet package feed* and the description *Browse the NuGet packages on this feed.* Links to a package or a version get the same preview. The part of a URL after `#` isn't sent to the server, and preview services don't run the page's script, so every view looks the same to them. The preview has no image, and it appears only when the feed is publicly readable.

## Enable or disable the gallery

The `galleryenabled` feed setting controls the gallery. Feed settings are stored in `sleet.settings.json` on the feed, so the setting applies to everyone who pushes to the feed. It defaults to `true`, and a feed that doesn't have the setting, such as a feed created by an older version of Sleet, is treated as `true`.

Sleet applies the setting only when it creates the feed, with `sleet init`, the first `sleet push` to a new feed, or `sleet recreate`. Changing the setting doesn't add or remove the gallery by itself, and pushing to an existing feed never adds, updates, or removes it. Run `sleet recreate` after you change the setting. `recreate` downloads every package, deletes the feed, and creates it again from the same packages and settings, so it can take a while on a large feed.

### Check the setting

```bash
sleet feed-settings --get galleryenabled
```

The output `galleryenabled : not found!` means the setting isn't set, so the gallery is on.

### Disable the gallery

```bash
sleet feed-settings --set galleryenabled:false
sleet recreate
```

This removes `index.html` and the package links from `index.json`. Later pushes don't add them back.

To start a new feed without the gallery, create the feed with `sleet init` before you push any packages. Then turn off the gallery and recreate the empty feed:

```bash
sleet init
sleet feed-settings --set galleryenabled:false
sleet recreate
```

### Enable the gallery

```bash
sleet feed-settings --set galleryenabled:true
sleet recreate
```

A feed created by an older version of Sleet doesn't have the setting, so `sleet recreate` alone adds the gallery.

## Update the gallery

Sleet writes `index.html` when it creates the feed, and doesn't change it after that. The footer of the page shows the version of Sleet that wrote it. To get the gallery from a newer version of Sleet, run `sleet recreate` with that version.

Running `sleet recreate` with a version of Sleet that doesn't include the gallery removes the gallery from the feed.

## Package links in Visual Studio

While the gallery is on, the feed's `index.json` has a `PackageDetailsUriTemplate` resource that points to `index.html#package/{id}/{version}`. Visual Studio and other NuGet clients use it to link each package to its page in the gallery. NuGet shows these links only for feeds that use https.

## Serve the gallery from the root URL

The storage REST endpoints don't support index documents. To open the gallery from the feed URL alone, use a static website endpoint, a CDN, or a rewrite rule:

- **Azure Storage**: the [static website endpoint](https://learn.microsoft.com/azure/storage/blobs/storage-blob-static-website) serves only the `$web` container. For a feed in `$web`, turn on static website hosting with `index.html` as the index document. The gallery then opens at the primary endpoint shown in the portal, such as `https://myaccount.z13.web.core.windows.net/`. NuGet clients keep using the feed's blob endpoint.
- **Amazon S3**: turn on [static website hosting](https://docs.aws.amazon.com/AmazonS3/latest/userguide/WebsiteHosting.html) with `index.html` as the index document. S3 website endpoints serve only HTTP, so put CloudFront in front of the bucket for HTTPS.
- **CloudFront**: set the **Default root object** to `index.html`. This applies only to the root of the distribution, not to subpaths.
- **Cloudflare R2**: R2 doesn't have index documents. On a custom domain, add a [URL rewrite rule](https://developers.cloudflare.com/rules/transform/url-rewrite/) that rewrites `/` to `/index.html`. The r2.dev URL doesn't support rules, so link to `index.html` there.

## Use a custom page

Sleet writes `index.html` only when the feed doesn't already have one, so you can replace the gallery with your own page. Upload your own `index.html` to the root of the feed, and Sleet leaves it in place when it pushes. While the gallery is on, NuGet clients link packages to `index.html#package/{id}/{version}`, so your page receives those links too.

`sleet recreate` deletes the feed's files, including a custom `index.html`, so upload your page again after you run it.

## How the gallery reads the feed

The gallery is a single, self-contained HTML file with no external dependencies. It reads these files from the feed it was opened from:

- `sleet.packageindex.json`, once when the page loads. It lists every package id and version, which is all that the package list, the filter, and paging need.
- `registration/{id}/index.json`, when you open a package, for its versions, metadata, and dependencies. Browsing the package list never downloads these files.
- `flatcontainer/{id}/{version}/icon` and `flatcontainer/{id}/{version}/readme`, when a package has them.
- `flatcontainer/{id}/{version}/{id}.nuspec`, when you view a version's `.nuspec`.

The gallery doesn't use the search resource. It's a single large file that covers the whole feed, and [external search](external-search.md) can point it at a different service.

All requests for the feed's files use URLs relative to the page, so the gallery works under any host name that serves the feed and never needs CORS settings. A query string on the page URL, such as an Azure SAS token, isn't passed on to these requests. Images in a readme load from the sites in their URLs, see [readmes](#readmes).

The page has a Content-Security-Policy that lets it run only its own script and styles, and read files only from the site that serves it. Services that rewrite the scripts in a page as they serve it, such as Cloudflare Rocket Loader, stop the gallery from loading, so turn them off for the feed. Scripts that a service adds to the page, such as analytics, are blocked.

If the page can't read `sleet.packageindex.json`, it shows *Unable to read this feed*, which usually means that the feed isn't publicly readable. If a single package can't be read, the error shows on that package's page, and the rest of the feed stays browsable.
