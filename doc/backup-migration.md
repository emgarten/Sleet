# Backup and migration

Use `sleet download` to back up package files and to move packages to a new feed. This page also covers URL changes, feed upgrades, and migration from other NuGet servers.

> [!WARNING]
> `sleet destroy` deletes every file under the feed root. On Azure and S3 feeds without `feedSubPath`, that can mean every object in the container or bucket, including unrelated files and other feeds. `sleet recreate` calls destroy after downloading packages.

## Back up packages

Download all packages to a local folder:

```bash
sleet download -o ./backup
```

The output layout is:

```text
{output}/{id-lowercase}/{file-name}
```

The download includes regular `.nupkg` files and `.symbols.nupkg` files listed in the feed's package index.

Useful options:

| Option | Description |
| --- | --- |
| `--skip-existing` | Skip files that already exist in the output folder. Use this for incremental backups. |
| `--no-lock` | Skip locking the feed and client-version verification. Use only when you know no writes are happening. |
| `--ignore-errors` | Report download errors as warnings and continue. |

`sleet download` backs up package files. It does not write a copy of feed settings from `sleet.settings.json`, and it does not preserve catalog history as catalog pages. Use `sleet feed-settings --get-all` to record feed settings separately.

## Storage-native copies

Tools such as `azcopy` and `aws s3 sync` can copy the raw feed files. A raw copy keeps JSON files exactly as they are, including absolute URLs written from `baseURI`. A raw copy is directly usable only at the same public URL. If the URL changes, run `sleet recreate` after the copy to rewrite feed JSON with the new `baseURI`. For a local feed whose `baseURI` is different from `path`, follow [rebuild a local feed without recreate](#rebuild-a-local-feed-without-recreate) instead.

## Restore to a new feed

Use this flow to restore or migrate to a new account, bucket, container, path, or URL:

1. Record feed settings from the old feed.
2. Download packages from the old feed.
3. Initialize the new feed.
4. Reapply feed settings on the new feed.
5. Push the downloaded packages.

Example with two sources in `sleet.json` named `old` and `new`:

```bash
sleet feed-settings --source old --get-all
sleet download --source old -o ./backup
sleet init --source new
sleet feed-settings --source new --set badgesenabled:true
sleet push --source new ./backup --skip-existing
```

If the old feed used the catalog or the symbol server, add `--with-catalog` or `--with-symbols` to the `init` command, for example `sleet init --source new --with-symbols`. Run `init` only once on the new feed.

Reapply each setting that `--get-all` printed before you push. The `badgesenabled` line in the example is only needed if the old feed had badges turned on. Settings such as badges and retention are applied while packages are pushed.

`push` accepts directories and searches them recursively for `*.nupkg` files, so the `./backup` folder can be pushed directly.

## Change baseURI or path in place

Sleet validates normal write commands against the `baseURI` stored in `index.json`. After you change `baseURI` or `path` in `sleet.json`, commands such as `push`, `delete`, and `validate` can fail with:

```text
The path or baseURI set in sleet.json does not match the URIs found in index.json.
```

`recreate` does not run that baseURI validation. If the existing feed files are still available at the configured `path`, you can update `baseURI` in `sleet.json` and run:

```bash
sleet recreate
```

This downloads packages from the current feed path, destroys the feed files at that path, initializes the feed with the new settings, and pushes the packages back. It rewrites feed JSON with the new `baseURI`.

Use this same approach after a storage-native copy to a new `path`: update `path` and `baseURI` to the copied location, then run `sleet recreate` against the copied feed.

If the new `path` does not already contain a raw copy of the old feed, do not use `recreate` against the new source. Download from the old source, initialize the new source, and push the backup folder instead.

For a local feed whose `baseURI` is different from `path`, don't use `recreate`. Update `sleet.json`, then follow [rebuild a local feed without recreate](#rebuild-a-local-feed-without-recreate).

## Recreate safety

`recreate` downloads all packages to a cache folder or to `--nupkg-path`, destroys the feed, initializes it again, pushes the packages, and validates the result. If the command fails after packages have been downloaded, Sleet keeps the downloaded nupkgs as a backup and prints the folder path. When `--nupkg-path` is supplied, that folder is cleaned up only after a successful recreate.

## Rebuild a local feed without recreate

`recreate` fails on local feeds whose `baseURI` is different from `path`, such as a folder that a web server serves at `https://example.com/feed/`. It stops with this error and leaves the feed without its packages:

```text
Something went wrong when recreating the feed. Feed validation has failed.
```

Run the same steps as separate commands instead. Keep the backup folder outside the feed folder, because `destroy` deletes everything in the feed folder.

```bash
sleet feed-settings --get-all
sleet download -o ./backup
sleet destroy
sleet init
sleet push ./backup
sleet validate
```

Before the `push` step, reapply each setting that `--get-all` printed with `sleet feed-settings --set key:value`. If the feed used the catalog or the symbol server, add `--with-catalog` or `--with-symbols` to `init`.

If a failed `recreate` already removed the packages, push the backup folder that the error message printed.

## Upgrade older feeds

Sleet checks feed compatibility before most operations. Feeds created by Sleet older than 2.2.0 use an older schema and must be recreated. Commands on these feeds fail with:

```text
uses an older version of Sleet ... Upgrade the feed ... by running 'Sleet recreate'
```

`recreate` runs the same check, so add `--force` to skip it:

```bash
sleet recreate --force
```

Feeds can also declare `sleet:requiredVersion`; if your client does not satisfy it, upgrade the Sleet client. See the [release notes](https://github.com/emgarten/Sleet/blob/main/ReleaseNotes.md) for version changes.

## Enable catalog or symbols on an existing feed

For catalog:

```bash
sleet feed-settings --set catalogenabled:true
sleet recreate
```

For symbols:

```bash
sleet feed-settings --set symbolsfeedenabled:true
sleet recreate
```

The setting enables the feature for future operations. `recreate` adds the catalog or symbol server to `index.json` and indexes packages already on the feed. Until you recreate, clients can't find the new resources. For a local feed whose `baseURI` is different from `path`, use [rebuild a local feed without recreate](#rebuild-a-local-feed-without-recreate) with `init --with-catalog` or `init --with-symbols`.

## Move packages from another server

To migrate from NuGet.Server, BaGet, Azure Artifacts, GitHub Packages, or another server, export or download the `.nupkg` files with that server's tools. Then initialize a Sleet feed and push the folder:

```bash
sleet init
sleet push ./nupkgs --skip-existing
```

Keep any server-specific metadata separately if you need it. Sleet rebuilds the static NuGet v3 feed from the package files.
