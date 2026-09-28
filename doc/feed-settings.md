# Feed settings

Feed settings are stored in the feed, in a file named `sleet.settings.json`. They turn feed features on and off for everyone who writes to the feed. They're separate from the [configuration](client-settings.md) in `sleet.json`, which only says where the feed is and how to sign in.

## Manage settings

Use the [feed-settings](commands.md#feed-settings) command:

```bash
# Show all settings
sleet feed-settings --get-all

# Change a setting
sleet feed-settings --set badgesenabled:true

# Remove a setting, which restores its default
sleet feed-settings --unset badgesenabled
```

Keys aren't case-sensitive. Sleet doesn't check key names, so a misspelled key is saved and has no effect. Run `--get-all` after a change to check it.

`feed-settings` always ends with `Run 'recreate' to rebuild the feed with the new settings.` Only some settings need `recreate`. The table below says when each one takes effect.

## Settings

| Key | Default | When it takes effect |
| --- | --- | --- |
| [`catalogenabled`](#catalogenabled) | `false` | After `recreate`. |
| [`catalogpagesize`](#catalogpagesize) | `1024` | On the next push. |
| [`symbolsfeedenabled`](#symbolsfeedenabled) | `false` | After `recreate`. |
| [`badgesenabled`](#badgesenabled) | `false` | On the next push. Run `recreate` to add badges for packages already on the feed. |
| [`externalsearch`](#externalsearch) | Not set | Right away. |
| [`retentionmaxstableversions`](#retention-settings) | Not set | On the next push. |
| [`retentionmaxprereleaseversions`](#retention-settings) | Not set | On the next push. |
| [`retentiongroupbyfirstprereleaselabelcount`](#retention-settings) | Not set | On the next push. |

`recreate` doesn't work on local feeds whose `baseURI` is different from `path`. For those feeds, use [rebuild a local feed without recreate](backup-migration.md#rebuild-a-local-feed-without-recreate) wherever this page says to run `recreate`.

## catalogenabled

When `true`, Sleet keeps a catalog: a log of every package added to or removed from the feed, in the [NuGet catalog format](https://learn.microsoft.com/en-us/nuget/api/catalog-resource). Tools that copy or mirror feeds can read it. NuGet doesn't need it to restore packages, and it adds files to every push.

To turn it on for a new feed, run `sleet init --with-catalog` before the first push. For an existing feed, set `catalogenabled:true` and run `recreate`. `recreate` adds the catalog to `index.json`, and adds the packages already on the feed to it. See [enable catalog or symbols on an existing feed](backup-migration.md#enable-catalog-or-symbols-on-an-existing-feed).

## catalogpagesize

The number of entries in each catalog page. Sleet starts a new page when the latest one is full. Only used when the catalog is on.

## symbolsfeedenabled

When `true`, the feed has a [symbol server](symbol-server.md). Sleet accepts `.symbols.nupkg` files, and indexes the `.dll` and `.pdb` files in packages under `symbols/`. When `false`, `push` skips symbol packages with a warning.

To turn it on for a new feed, run `sleet init --with-symbols` before the first push. For an existing feed, set `symbolsfeedenabled:true` and run `recreate`, so that `index.json` lists the symbol server and existing packages are indexed.

## badgesenabled

When `true`, `push` and `delete` update the [version badge](badges.md) files under `badges/`. Badges are off on new feeds. After you turn them on, run `recreate` to create badges for the packages that are already on the feed.

## externalsearch

The URL of an [external search](external-search.md) service. When you set it, Sleet points the search resource in `index.json` to that URL right away. Unset it to go back to the built-in static search:

```bash
sleet feed-settings --set externalsearch:https://search.example.com/query
sleet feed-settings --unset externalsearch
```

`recreate` keeps the external search URL. `init` doesn't, see [settings and init](#settings-and-init).

## Retention settings

These keys control [package retention](retention.md), which deletes old versions on each push:

| Key | Description |
| --- | --- |
| `retentionmaxstableversions` | The number of stable versions to keep for each package id. |
| `retentionmaxprereleaseversions` | The number of prerelease versions to keep for each package id. |
| `retentiongroupbyfirstprereleaselabelcount` | Groups prerelease versions by their first *n* release labels, and keeps `retentionmaxprereleaseversions` in each group. |

Retention only runs when both limits are set and greater than 0. Use the [retention settings](commands.md#retention-settings) command to change them, since it sets all three keys at once:

```bash
sleet retention settings --stable 5 --prerelease 2
```

## Settings and init

`init` writes `sleet.settings.json`. If you run it on a feed that already has settings, it:

- Sets `catalogenabled` and `symbolsfeedenabled` to match the `--with-catalog` and `--with-symbols` options, which are off unless you pass them.
- Points search back to the static search file, even if `externalsearch` is set. Run `feed-settings --set externalsearch:<url>` again to fix it.
- Keeps `badgesenabled`, `catalogpagesize`, and the retention settings.
- Removes keys that Sleet doesn't know.

Only run `init` on a new feed. `recreate` keeps all settings.
