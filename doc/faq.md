# FAQ

Short answers to common questions about Sleet, with links to the pages that explain more. For errors, see [troubleshooting](troubleshooting.md).

## Do NuGet clients need Sleet?

No. A Sleet feed is a standard NuGet v3 feed, and `dotnet`, Visual Studio, and `nuget.exe` read it like any other package source. Only the machines that push packages need Sleet. See [use a feed with NuGet](consume-feed.md).

## What does a feed cost to run?

There's no server, so you pay only your storage provider or web host for storage, requests, and data transfer. Check your provider's pricing page for the rates.

## Can I make a feed private?

Yes, but NuGet can't sign in to Azure Storage or Amazon S3 itself. Put a proxy or CDN that handles sign-in in front of the storage, or allow access only from your network. See [private feeds](private-feeds.md), and [private feed on AWS](private-feed-s3.md) for a full example.

## Can I use dotnet nuget push?

No. There's no server to take the upload. Use [sleet push](commands.md#push) instead. For the same reason, `dotnet nuget delete` doesn't work, so use [sleet delete](commands.md#delete).

## Why does search return every package?

Search on a Sleet feed is a static file, so it can't filter by the search term. See [search results](limitations.md#search-results). To get real search, set up an [external search](external-search.md) service.

## Can I unlist or deprecate a package?

No. You can only delete a package, which removes it from the feed. See [unlisting](limitations.md#unlisting).

## Does Sleet support snupkg files?

No. The [symbol server](symbol-server.md) reads `.symbols.nupkg` packages, and `.pdb` files inside normal packages.

## Can several builds push at the same time?

Yes. Sleet [locks the feed](locking.md), so the pushes run one after the other. It's still better to run one publish job at a time. See [concurrency](ci-server.md#concurrency).

## Can clients restore during a push?

Yes. NuGet clients don't use the lock, and Sleet uploads package files before the files that list them. See [what a push does](how-it-works.md#what-a-push-does).

## How do I publish from CI?

See [publish from CI](ci-server.md) for GitHub Actions, Azure Pipelines, and GitLab CI examples. You can define the feed with [environment variables](environment-variables.md) instead of a settings file.

## Can I host several feeds in one container or bucket?

Yes. See [multiple feeds](multiple-feeds.md).

## Can I use a CDN or my own domain?

Yes. Set `baseURI` to the URL that clients use before the first push. See [CDN and caching](cdn-caching.md) and [private feeds](private-feeds.md#point-baseuri-at-the-front-end).

## Which S3-compatible services work?

Sleet can use services that support the Amazon S3 API, such as Cloudflare R2, MinIO, DigitalOcean Spaces, and Backblaze B2. The Sleet maintainers don't test them, so check the provider notes in [S3-compatible storage](s3-compatible.md).

## How do I move a feed?

Download the packages from the old feed, and push them to the new one. See [backup and migration](backup-migration.md). The same page covers moving packages from another NuGet server.

## How do I keep a feed small?

Use [package retention](retention.md) to delete old versions when you push new ones. See [large feeds](limitations.md#large-feeds).

## How do I update Sleet?

Run `dotnet tool update -g sleet`, and check the [release notes](https://github.com/emgarten/Sleet/blob/main/ReleaseNotes.md) for changes. A new version of Sleet works with your existing feed, unless the feed was made by a version older than 2.2.0. See [upgrade older feeds](backup-migration.md#upgrade-older-feeds).

## Can I use Sleet from .NET code?

Yes. The SleetLib package has the same features as the CLI. See [SleetLib](sleetlib.md).
