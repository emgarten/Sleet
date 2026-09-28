# Troubleshooting

This page lists common errors from Sleet and from NuGet clients that use a Sleet feed, and how to fix them. For sign-in problems, see [Azure authentication](auth-azure.md#troubleshooting) and [AWS authentication](auth-aws.md#troubleshooting).

## Debug output

Run the command with `--verbosity diagnostic` to see every file Sleet reads and writes, and the full stack trace of an error:

```bash
sleet push ./nupkgs --verbosity diagnostic
```

To turn on debug output for every command without changing the command line, set `SLEET_DEBUG` to `1`. This is useful in CI. It overrides `--verbosity`.

```bash
# bash
export SLEET_DEBUG=1
```

```powershell
# PowerShell
$env:SLEET_DEBUG = "1"
```

Run `sleet --version` to check which version you have.

## Settings errors

### Unable to find source settings

```text
Unable to find source settings. Specify the path to a sleet.json settings file.
```

Sleet didn't find any settings. It looks for `sleet.json` in the current folder and its parent folders, then for Sleet settings in `.netconfig` files, then for the `SLEET_FEED_TYPE` environment variable. Run Sleet from the folder that holds `sleet.json`, or pass the file with `--config`. See [how Sleet finds settings](client-settings.md#how-sleet-finds-settings).

### Multiple sources in the settings file

```text
The local settings file contains multiple sources. Use --source to specify the feed to use.
```

Pass the name of the source to use, for example `sleet push ./nupkgs --source feed`. Sleet picks a source by itself only when there's exactly one.

### Unable to find source

```text
Unable to find source. Verify that the --source parameter is correct and that sleet.json contains the named source.
```

No source has the name passed to `--source`. Names aren't case-sensitive. Check that Sleet reads the settings file you expect: a `sleet.json` in a parent folder is used before a `.netconfig` in the current folder.

If the settings come from environment variables, the source name is `envirnoment_feed`, with that spelling. See [feed variables](environment-variables.md#feed-variables).

### No sources in the settings file

```text
The local settings file is missing or does not contain any sources. Use 'CreateConfig' to add a source.
```

The `sources` list is empty. If the `sources` property is missing, the error is `Invalid config. No sources found.` Add a source, or run `sleet createconfig` to make a new file. See [sleet.json](client-settings.md#sleetjson).

### Relative path without a sleet.json file

```text
Cannot use a relative 'path' without a sleet.json file.
```

A local feed defined with environment variables needs an absolute `SLEET_FEED_PATH`.

### S3 source with serviceURL and no path

```text
[System.NullReferenceException] Object reference not set to an instance of an object.
```

An S3 source that sets `serviceURL` also needs `path`, the public URL of the bucket. See [S3-compatible storage](s3-compatible.md#configure-the-source).

### Problems with .netconfig values

| Error | Cause |
| --- | --- |
| `No valid combination of account information found.` | A connection string that isn't in double quotes. `;` starts a comment in `.netconfig`, so the value gets cut off. |
| `Missing path for account.` | A Windows path with single backslashes. Use `/` or `\\`. |
| `Can not add property ... Property with the same name already exists on object.` | The same property of a source is set in two `.netconfig` files. |

See [.netconfig](client-settings.md#netconfig).

## Feed errors

### Path or baseURI mismatch

```text
The path or baseURI set in sleet.json does not match the URIs found in index.json. To fix this update sleet.json with the correct settings, or recreate the feed to apply the new settings. Local settings: https://example.com/other/ Feed settings: https://example.com/feed/
```

The feed was created with a different `baseURI` or `path`. If you changed the settings by mistake, change them back. To move the feed to the new URL, see [change baseURI or path in place](backup-migration.md#change-baseuri-or-path-in-place).

### Feed uses an older version of Sleet

```text
https://myaccount.blob.core.windows.net/feed/ uses an older version of Sleet: 2.1.0. Upgrade the feed to 7.2.0 by running 'Sleet recreate' against this feed.
```

Feeds made by Sleet versions older than 2.2.0 must be rebuilt. `recreate` runs the same check, so use `sleet recreate --force`. See [upgrade older feeds](backup-migration.md#upgrade-older-feeds).

### Feed requires a newer version of Sleet

```text
https://myaccount.blob.core.windows.net/feed/ requires a newer version of Sleet. Upgrade your Sleet client to work with this feed.
```

A newer version of Sleet changed the feed. The error can also say `requires Sleet version: ...` with the versions that work. Update Sleet, for example with `dotnet tool update -g sleet`. In CI, check the version that the pipeline installs. See [install Sleet](install.md).

### Feed is missing Sleet files

```text
https://myaccount.blob.core.windows.net/feed/ is missing sleet files. Use 'sleet.exe init' to create a new feed.
```

The storage exists, but there's no `index.json` at the feed root. Only `push` and `init` set up a new feed. If the feed should already exist, check `path` and `feedSubPath`, since they may point to the wrong folder.

### Unable to use feed

```text
Unable to use feed. Create the appropriate feed folder/container/bucket to continue.
```

The folder, container, or bucket doesn't exist. The line before the error shows which one. Only `push` and `init` create it. Check the name in the settings, or run `sleet init`.

### Feed sub path mismatch

```text
When using FeedSubPath the Path property must end with the sub path.
```

When a source sets `feedSubPath`, the feed URL must end with the same sub folder. That's `baseURI` if it's set, and `path` otherwise. See [sub folders and baseURI](multiple-feeds.md#sub-folders-and-baseuri).

## Push errors

### Package already exists

```text
Package already exists: My.Package 1.0.0.
```

The version is already on the feed. Use `--skip-existing` so a rerun of the same build doesn't fail, or change the package version.

`--force` replaces the package, but NuGet clients that already restored that version keep using their cached copy. It's safer to push a new version.

### Unable to find nupkgs

```text
Unable to find nupkgs in 'C:\build\nupkgs'.
```

The folder has no `.nupkg` files in it or in its sub folders. For a single file, the error is `Unable to find '...'`. Check the path. Sleet doesn't expand wildcards, so pass a folder instead of `*.nupkg`.

### Symbols packages are skipped

```text
Skipping My.Package 1.0.0 Symbols, to push symbols packages enable the symbols server on this feed.
```

The feed doesn't have the symbol server turned on. See [enable symbols](symbol-server.md#enable-symbols).

## Waiting to obtain a feed lock

```text
Waiting to obtain feed lock. Feed is locked by: Push of My.Package 1.0.0 since: 2025-01-15T18:04:12.3456789Z
```

Another command is working on the feed, and Sleet waits until it's done. If no other command is running, a command that stopped before it finished left the lock behind. This happens on local and Amazon S3 feeds. Azure locks expire by themselves within a minute.

On Amazon S3, a lock also stays behind when the credentials don't allow `s3:DeleteObject`. The command that left it prints `Unable to clean up lock`.

See [remove a stuck lock](locking.md#remove-a-stuck-lock). To stop waiting after a set time, see [wait time](locking.md#wait-time).

## Recreate fails

```text
Something went wrong when recreating the feed. Feed validation has failed.
```

On a local feed with a `baseURI` that's different from `path`, `recreate` always fails with this error. Use [rebuild a local feed without recreate](backup-migration.md#rebuild-a-local-feed-without-recreate) instead.

When `recreate` fails after it has downloaded the packages, it keeps them and prints the folder:

```text
Encountered an error while recreating the feed. You may need to manually create the feed and push the nupkgs again. Nupkgs have been saved to: ...
```

Push that folder to the feed to add the packages back.

## NuGet can't read the feed

Start by opening the feed's `index.json` with curl. `-i` shows the status code:

```bash
curl --compressed -i https://myaccount.blob.core.windows.net/feed/index.json
```

A working feed returns `200` and a JSON document. Check that the URL is the one in [find the source URL](consume-feed.md#find-the-source-url).

### Unable to load the service index

```text
error NU1301: Unable to load the service index for source https://myaccount.blob.core.windows.net/feed/index.json.
```

NuGet couldn't get `index.json`. Common causes:

- The URL is wrong, or it's missing `index.json` at the end.
- The feed isn't set up yet. Push a package or run `sleet init`.
- On Azure, the storage account or the container doesn't allow anonymous access. The error response has a code such as `ResourceNotFound` or `PublicAccessNotPermitted`. See [allow anonymous read access](feed-type-azure.md#allow-anonymous-read-access).
- On Amazon S3, the bucket policy doesn't allow public reads, or S3 Block Public Access is on. See [create an Amazon S3 feed](feed-type-s3.md).
- A proxy or firewall blocks the request.

### NuGet returns 403 or asks for credentials

NuGet treats `401` and `403` responses as a sign-in problem. Restore fails, and Visual Studio asks for a user name and password.

- **Public S3 feed**: without `s3:ListBucket`, S3 returns `403` instead of `404` for files that don't exist. NuGet checks every source for every package, so this breaks restores of packages that aren't on the Sleet feed. See [fix 403 responses for missing packages](feed-type-s3.md#fix-403-responses-for-missing-packages).
- **CloudFront in front of a private bucket**: CloudFront passes on the same `403` for missing files. See [convert origin 403 responses](private-feed-s3.md#convert-origin-403-responses).
- **Feed behind a proxy that asks for a password**: NuGet needs credentials for the source. See [sources that need credentials](consume-feed.md#sources-that-need-credentials).

NuGet can't sign in to Azure Storage or Amazon S3 itself. See [private feeds](private-feeds.md).

### HTTP source errors

```text
error NU1302: You are running the 'restore' operation with an 'HTTP' source: http://localhost:8080/index.json. NuGet requires HTTPS sources.
```

Serve the feed over HTTPS. See [HTTP sources](consume-feed.md#http-sources).

### New versions are missing after a push

NuGet caches the version list of each package for about 30 minutes, so restore can fail with `NU1102` for a version you just pushed. Clear the HTTP cache:

```bash
dotnet nuget locals http-cache --clear
```

If the feed is behind a CDN, the CDN can also serve old files. See [CDN and caching](cdn-caching.md).

### Search shows every package

This is expected. Search on a Sleet feed is a static file. See [search results](limitations.md#search-results).

## Report a problem

If you can't fix the problem, search the [issues on GitHub](https://github.com/emgarten/Sleet/issues), or open a new one. Include:

- The output of `sleet --version`.
- The storage type: Azure, Amazon S3, an S3-compatible provider, or a local folder.
- The command you ran, and the output with `--verbosity diagnostic`.

Remove connection strings, access keys, and any other secrets from the settings and the output before you post them.
