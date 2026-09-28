# Limitations

A Sleet feed is a set of static files, with no server behind it. That keeps feeds cheap and simple, but some features of nuget.org and hosted package servers aren't possible. This page lists what Sleet can't do, and the known issues in the current version.

## Search results

Search on a Sleet feed is a static file, `search/query`, that lists every package and version. Sleet can't run a query, so every search returns every package:

- The Visual Studio **Browse** tab lists the same packages, whatever you type.
- `dotnet package search` returns all packages for any search term.
- Paging and the prerelease filter don't apply.

On a large feed the file gets big, and Visual Studio can show incomplete results. To get real search, set up an [external search](external-search.md) service.

Restore doesn't use search, so it isn't affected.

## Unlisting

Sleet can't unlist or deprecate a package. The only way to hide a version is to [delete](commands.md#delete) it, which removes it from the feed. Projects that use that version can't restore it anymore, unless NuGet already has it in the local cache.

For the same reason, [package retention](retention.md) deletes old versions instead of unlisting them.

## Publishing

- `dotnet nuget push` and `nuget push` don't work, because there's no server to take the upload. Use [sleet push](commands.md#push).
- `dotnet nuget delete` and `nuget delete` don't work either. Use [sleet delete](commands.md#delete).
- Only one client can change a feed at a time. Others wait for the [feed lock](locking.md).

## Symbol packages

The [symbol server](symbol-server.md) reads `.symbols.nupkg` packages, and `.pdb` files inside normal packages. It doesn't support the `.snupkg` format.

## Authentication

NuGet can't sign in to Azure Storage or Amazon S3, so a feed is either public or behind something that handles sign-in for it. See [private feeds](private-feeds.md).

## nuget.org features

A Sleet feed doesn't have download counts, package owners, deprecation data, or vulnerability data. NuGet can still check your packages for known vulnerabilities with data from nuget.org, if nuget.org is one of your package sources or audit sources.

## Large feeds

A few files list every package on the feed, and every push downloads and uploads them. Pushes get slower as a feed grows. See [files that grow with the feed](how-it-works.md#files-that-grow-with-the-feed).

To keep a feed fast, use [package retention](retention.md) to remove old versions, or split packages into [multiple feeds](multiple-feeds.md).

## Changing the feed URL

The feed URL is written into the feed files. To move a feed or change its `baseURI`, you need to rebuild it. See [backup and migration](backup-migration.md#change-baseuri-or-path-in-place).

## Configuration

- Azure feeds always store JSON files gzipped. Only Amazon S3 sources have a `compress` setting to turn it off. See [compression](how-it-works.md#compression).
- [Environment variables](environment-variables.md) can define only one feed. To use several feeds, use a settings file. See [multiple feeds](multiple-feeds.md).

## Delays after a push

Clients can take a while to see a new version. NuGet caches the version list of a source for about 30 minutes, and a CDN can cache files for longer. See [see new versions right away](consume-feed.md#see-new-versions-right-away) and [CDN and caching](cdn-caching.md).

## Known issues

These are problems in the current version of Sleet. Each one links to its issue on GitHub, where you can follow the fix.

- `recreate` fails on local feeds that have a `baseURI` different from `path`, and leaves the feed without its packages ([#257](https://github.com/emgarten/Sleet/issues/257)). Use [rebuild a local feed without recreate](backup-migration.md#rebuild-a-local-feed-without-recreate) instead.
- `destroy` and `recreate` delete every file under the feed root, not only the feed's own files. For a feed at the root of an Azure container or S3 bucket, that includes other feeds in sub folders ([#256](https://github.com/emgarten/Sleet/issues/256)). See [what feeds in the same container or bucket share](multiple-feeds.md#what-feeds-in-the-same-container-or-bucket-share).
- `init` doesn't stop you from running it on an existing feed, and it resets some [feed settings](feed-settings.md#settings-and-init) when you do ([#251](https://github.com/emgarten/Sleet/issues/251)).
- Feeds made with Sleet versions older than 2.2.0 need `recreate --force` to upgrade, even though the error message says to run `recreate` ([#259](https://github.com/emgarten/Sleet/issues/259)). See [upgrade older feeds](backup-migration.md#upgrade-older-feeds).
- `delete --reason` is accepted, but the reason isn't saved ([#255](https://github.com/emgarten/Sleet/issues/255)).
- Amazon S3 buckets that Sleet creates return 403 instead of 404 for packages that aren't on the feed, which can break restore ([#250](https://github.com/emgarten/Sleet/issues/250)). See [fix 403 responses for missing packages](feed-type-s3.md#fix-403-responses-for-missing-packages).
- The Amazon S3 lock isn't atomic, doesn't expire, and is shared by all feeds in a bucket ([#252](https://github.com/emgarten/Sleet/issues/252)). See [Amazon S3 locks](locking.md#amazon-s3-locks).
- An S3 source with `serviceURL` and no `path` fails with `Object reference not set to an instance of an object` ([#253](https://github.com/emgarten/Sleet/issues/253)). Always set `path`. See [S3-compatible storage](s3-compatible.md#configure-the-source).
- In `.netconfig` files, setting the same property of a source more than once fails with an error ([#254](https://github.com/emgarten/Sleet/issues/254)). A relative `path` is resolved from the current folder, not from the folder of the file ([#258](https://github.com/emgarten/Sleet/issues/258)). See [.netconfig](client-settings.md#netconfig).
