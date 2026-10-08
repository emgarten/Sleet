# Commands

This page lists every Sleet command and its options. Run `sleet --help` for the list of commands, and `sleet <command> --help` for the options of a single command.

## Common options

Every command except `createconfig` works against a feed, and accepts these options.

| Option | Description |
| --- | --- |
| `-c`, `--config` | Path to a sleet.json or .netconfig file to read sources and settings from. Use `none` to skip loading files and configure the feed only with [environment variables](environment-variables.md). |
| `-s`, `--source` | Name of the source to use. Required when the settings contain more than one source. |
| `-p`, `--property` | A `key=value` pair. Used to fill in `$tokens$` in the settings, or in place of an environment variable. Can be repeated. |
| `-V`, `--verbosity` | Console output level: `quiet`, `minimal`, `normal`, `detailed`, or `diagnostic`. The default is `normal`. See [logging verbosity](#logging-verbosity). |
| `-h`, `--help` | Show help for the command. |

When `--config` isn't set, Sleet searches for settings in this order: a sleet.json file in the current folder or any parent folder, then .netconfig files, then environment variables. For details see [how Sleet finds settings](client-settings.md#how-sleet-finds-settings).

To print the Sleet version, run `sleet --version`.

## Exit codes

| Code | Meaning |
| --- | --- |
| `0` | The command succeeded. |
| `1` | The command failed. The error is written to the console. Running `sleet` with no command, or with invalid options, also shows help and returns `1`. |

## Logging verbosity

`-V` or `--verbosity` controls how much output a command writes. Lower levels are useful for scripts that run Sleet many times.

| Level | Output |
| --- | --- |
| `quiet` | Warnings and errors only. |
| `minimal` | The main actions and the final result. |
| `normal` | The default output. |
| `detailed` | Additional messages. `--verbose` is a short alias for this level. |
| `diagnostic` | Full debug output. |

```bash
sleet push ./nupkgs --verbosity minimal
```

To get debug output from every command without changing the command line, set the environment variable `SLEET_DEBUG` to `1`. It overrides `--verbosity`. See [debug output](troubleshooting.md#debug-output).

## createconfig

Creates a sleet.json template file that you then edit with your own feed details.

```text
sleet createconfig [--azure | --s3 | --local] [--provider <name>] [--output <path>]
```

| Option | Description |
| --- | --- |
| `--azure` | Add a template source for an Azure Storage feed. |
| `--s3` | Add a template source for an Amazon S3 feed. |
| `--provider` | Add a template source for an S3 feed on the given service: `aws`, `r2` for Cloudflare R2, or `self-hosted` for S3 servers that you run yourself, such as MinIO and RustFS. The default is `aws`. Implies `--s3`, and can't be used with `--azure` or `--local`. See [S3-compatible storage](s3-compatible.md#provider-settings). |
| `--local` | Add a template source for a local folder feed. |
| `--output` | Folder or file path to write. The default is sleet.json in the current folder. |
| `-V`, `--verbosity` | Console output level. |

The template contains one source. If you pass more than one type option, only one is used. If you pass none, the template has an empty `type` for you to fill in. The command fails if the output file already exists.

The Azure template contains a `connectionString` placeholder. The recommended setup uses `path` with Microsoft Entra ID instead, see [Azure authentication](auth-azure.md).

```bash
sleet createconfig --azure
```

## init

Initializes a new, empty feed.

```text
sleet init [options]
```

| Option | Description |
| --- | --- |
| `--with-catalog` | Enable the catalog, which records the history of every change to the feed. |
| `--with-symbols` | Enable the [symbol server](symbol-server.md). |

The [common options](#common-options) are also supported.

You don't need to run `init` for most feeds, because `push` creates and initializes a new feed automatically. Use `init` when you want to enable the catalog or the symbol server from the start. Both can also be turned on later with [feed settings](feed-settings.md).

`init` creates the container, bucket, or folder if it doesn't exist.

> [!WARNING]
> Don't run `init` on a feed that already exists. Current versions of Sleet don't stop you, and `init` resets parts of the feed: it turns the catalog and the symbol server on or off to match the options you pass, and it switches search back to the static search files even if [external search](external-search.md) is set. Packages aren't removed. To change settings on an existing feed, use [feed-settings](#feed-settings).

```bash
sleet init --source feed --with-symbols
```

## push

Adds packages to a feed.

```text
sleet push <path> [<path>...] [options]
```

| Option | Description |
| --- | --- |
| `<path>` | One or more `.nupkg` files or folders. Folders are searched recursively for `*.nupkg` files. |
| `-f`, `--force` | Replace packages that already exist on the feed. |
| `--skip-existing` | Skip packages that already exist on the feed. |

The [common options](#common-options) are also supported.

On the first push to a new feed, Sleet creates the container, bucket, or folder if needed and initializes the feed. Check what that means for [Azure](feed-type-azure.md) and [Amazon S3](feed-type-s3.md) before you push to a feed that should be private.

Pushing a version that is already on the feed fails with `Package already exists` unless you use `--skip-existing` or `--force`. If both are set, `--skip-existing` wins. Use `--skip-existing` in CI so a rerun of the same build doesn't fail.

Sleet doesn't expand wildcards such as `*.nupkg`. Pass a folder instead. Some shells expand wildcards before Sleet runs, which works, but a folder is portable across shells and operating systems.

Files that end in `.symbols.nupkg` are only used by the [symbol server](symbol-server.md). If the symbol server is off, Sleet skips them with a warning. The `.snupkg` format isn't supported.

If [retention](retention.md) is set up on the feed, push removes old versions of the package ids it pushed. It never removes the packages it just pushed.

Examples:

```bash
# Push a single package
sleet push ./nupkgs/MyPackage.1.0.0.nupkg

# Push every package under two folders to the source named "feed"
sleet push ./nupkgs ./more-nupkgs --source feed

# Rerun-safe push for CI
sleet push ./nupkgs --skip-existing --verbosity minimal
```

## delete

Removes a package version, or every version of a package id, from a feed.

```text
sleet delete --id <id> [--version <version>] [options]
```

| Option | Description |
| --- | --- |
| `-i`, `--id` | Package id to delete. Required. |
| `-v`, `--version` | Package version to delete. If you leave it out, all versions of the package are deleted, including symbols packages. |
| `-f`, `--force` | Don't fail if the package isn't on the feed. |
| `-r`, `--reason` | Accepted, but not stored in the feed by current versions of Sleet. |

The [common options](#common-options) are also supported.

> [!WARNING]
> In this command lowercase `-v` means `--version`. Use uppercase `-V` for verbosity.

Deleted packages are removed from the feed. Consumers that depend on a deleted version can no longer restore it, unless NuGet already cached it on their machine. Sleet doesn't support unlisting, see [limitations](limitations.md#unlisting).

```bash
# Delete one version
sleet delete --id MyPackage --version 1.0.1-beta

# Delete all versions
sleet delete --id MyPackage
```

## stats

Shows the number of packages on a feed.

```text
sleet stats [options]
```

Only the [common options](#common-options) are supported. Like the other feed commands, `stats` locks the feed while it runs, so it needs write access. See [feed locking](locking.md).

## validate

Checks that every package listed in the feed index is present in all feed resources, such as the registration and flat container files. Run it when a package is missing, or when a package shows up that shouldn't.

```text
sleet validate [options]
```

Only the [common options](#common-options) are supported. If `validate` reports problems, [recreate](#recreate) rebuilds the feed from its packages.

## download

Downloads every package and symbols package on a feed to a local folder.

```text
sleet download --output-path <folder> [options]
```

| Option | Description |
| --- | --- |
| `-o`, `--output-path` | Folder to write the packages to. Required. |
| `--skip-existing` | Skip packages that already exist in the output folder. |
| `--no-lock` | Don't lock the feed, and don't check that this Sleet version is compatible with the feed. |
| `--ignore-errors` | Continue when a package fails to download. |

The [common options](#common-options) are also supported.

Packages are written to `<folder>/<id>/<file>`, with the id in lowercase. Use `download` for [backups and migrations](backup-migration.md).

```bash
sleet download --output-path ./backup --skip-existing
```

## destroy

Deletes all files from a feed. This can't be undone.

```text
sleet destroy [options]
```

Only the [common options](#common-options) are supported.

> [!WARNING]
> `destroy` deletes every file under the feed root, not only the files Sleet created. If the feed doesn't use `feedSubPath`, that is every file in the Azure container or S3 bucket, including other feeds that use a sub path in the same container or bucket. For a local feed it also deletes every subfolder. Run [download](#download) first if you might need the packages.

## recreate

Rebuilds a feed from its own packages. Sleet downloads all packages, deletes the feed, and pushes the packages again. Use it to fix a broken feed, to upgrade a feed created by an old Sleet version, or to apply feed settings to packages that are already on the feed.

```text
sleet recreate [options]
```

| Option | Description |
| --- | --- |
| `--nupkg-path` | Folder to keep the downloaded packages in during the rebuild. By default a temporary folder is used. The folder is deleted when the command succeeds, and kept as a backup if it fails. |
| `-f`, `--force` | Continue past errors while recreating the feed, and skip the check that the feed was created by a compatible version of Sleet. Required to [upgrade very old feeds](backup-migration.md#upgrade-older-feeds). |

The [common options](#common-options) are also supported.

`recreate` has the same scope as [destroy](#destroy). Use `--nupkg-path` for large feeds so you know where the backup is if the command fails.

> [!WARNING]
> `recreate` doesn't work on a local feed whose `baseURI` is different from its `path`, which is the usual setup when a web server hosts the folder. The command fails with `Feed validation has failed` and leaves the feed without its packages. The downloaded packages are kept, and the error shows where. Rebuild these feeds with the steps in [rebuild a local feed without recreate](backup-migration.md#rebuild-a-local-feed-without-recreate) instead.

## feed-settings

Reads or changes the settings stored in the feed's `sleet.settings.json` file. These settings apply to everyone who pushes to the feed. See [feed settings](feed-settings.md) for the list of keys.

```text
sleet feed-settings [--get <key>] [--get-all] [--set <key>:<value>] [--unset <key>] [--unset-all] [options]
```

| Option | Description |
| --- | --- |
| `--get` | Show a setting. Can be repeated. |
| `--get-all` | Show all settings. |
| `--set` | Add or change a setting, in the form `key:value`. Can be repeated. |
| `--unset` | Remove a setting. Can be repeated. |
| `--unset-all` | Remove all settings. |

The [common options](#common-options) are also supported. You can't combine a get option with a set or unset option in the same command.

```bash
# Show all settings
sleet feed-settings --get-all

# Turn on the symbol server
sleet feed-settings --set symbolsfeedenabled:true

# Turn on badges
sleet feed-settings --set badgesenabled:true
```

The command always ends with the message `Run 'recreate' to rebuild the feed with the new settings.` You only need to run `recreate` for settings that must be applied to packages already on the feed. [Feed settings](feed-settings.md) says which settings those are.

## retention settings

Stores package retention limits in the feed. After this, every push removes old versions of the package ids it pushed. See [package retention](retention.md).

```text
sleet retention settings --stable <count> --prerelease <count> [--release-labels <count>] [options]
sleet retention settings --disable [options]
```

| Option | Description |
| --- | --- |
| `--stable` | Number of stable versions to keep per package id. Required unless `--disable` is set. |
| `--prerelease` | Number of prerelease versions to keep per package id. Required unless `--disable` is set. |
| `--release-labels` | Group prerelease versions by their first *n* release labels, and keep the `--prerelease` count in each group. Optional. |
| `--disable` | Turn off retention. Can't be combined with `--stable` or `--prerelease`. |

The [common options](#common-options) are also supported.

```bash
sleet retention settings --stable 5 --prerelease 2
```

## retention prune

Removes old package versions from the feed once.

```text
sleet retention prune [--stable <count>] [--prerelease <count>] [options]
```

| Option | Description |
| --- | --- |
| `--stable` | Number of stable versions to keep per package id. Defaults to the feed's retention settings. |
| `--prerelease` | Number of prerelease versions to keep per package id. Defaults to the feed's retention settings. |
| `--release-labels` | Group prerelease versions by their first *n* release labels. Optional. |
| `--package` | Only prune this package id. Can be repeated. |
| `--dry-run` | List the versions that would be deleted, without deleting them. |

The [common options](#common-options) are also supported. Both a stable and a prerelease limit greater than 0 are required, either from the options or from the feed settings.

```bash
# See what would be deleted
sleet retention prune --stable 5 --prerelease 2 --dry-run

# Prune two packages
sleet retention prune --package PackageA --package PackageB --stable 2 --prerelease 1
```
