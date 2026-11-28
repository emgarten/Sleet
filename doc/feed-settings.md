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

Keys aren't case-sensitive. `feed-settings` doesn't check key names, so a misspelled key is saved and has no effect. Run `--get-all` after a change to check it.

`feed-settings` always ends with `Run 'recreate' to rebuild the feed with the new settings.` Only some settings need `recreate`. The table below says when each one takes effect.

### Set settings with init and recreate

[init](commands.md#init) and [recreate](commands.md#recreate) also take settings, with `--set {key}:{value}`. Repeat `--set` for each setting:

- To give a new feed different settings from the start, pass them to `init` before the first push.
- To change a setting that needs `recreate` on an existing feed, pass it to `recreate`, which changes the setting and rebuilds the feed in one step.

```bash
# Create a feed with the catalog on and the package gallery off
sleet init --with-catalog --set galleryenabled:false

# Turn on badges, and add badges for the packages already on the feed
sleet recreate --set badgesenabled:true
```

Unlike `feed-settings`, `init` and `recreate` only accept the keys in the [settings](#settings) table, and check each value. If a key or value is wrong, they stop without changing the feed. `--set` can't remove a setting. Use `feed-settings --unset` for that.

## Settings

| Key | Default | When it takes effect |
| --- | --- | --- |
| [`catalogenabled`](#catalogenabled) | `false` | After `recreate`. |
| [`catalogpagesize`](#catalogpagesize) | `1024` | On the next push. |
| [`badgesenabled`](#badgesenabled) | `false` | On the next push. Run `recreate` to add badges for packages already on the feed. |
| [`galleryenabled`](#galleryenabled) | `true` | After `recreate`. |
| [`externalsearch`](#externalsearch) | Not set | Right away. |
| [`retentionmaxstableversions`](#retention-settings) | Not set | On the next push. |
| [`retentionmaxprereleaseversions`](#retention-settings) | Not set | On the next push. |
| [`retentiongroupbyfirstprereleaselabelcount`](#retention-settings) | Not set | On the next push. |

`recreate` doesn't work on local feeds whose `baseURI` is different from `path`. For those feeds, use [rebuild a local feed without recreate](backup-migration.md#rebuild-a-local-feed-without-recreate) wherever this page says to run `recreate`.

## catalogenabled

When `true`, Sleet keeps a catalog: a log of every package added to or removed from the feed, in the [NuGet catalog format](https://learn.microsoft.com/en-us/nuget/api/catalog-resource). Tools that copy or mirror feeds can read it. NuGet doesn't need it to restore packages, and it adds files to every push.

To turn it on for a new feed, run `sleet init --with-catalog` before the first push. For an existing feed, run `sleet recreate --set catalogenabled:true`. `recreate` adds the catalog to `index.json`, and adds the packages already on the feed to it. See [enable the catalog on an existing feed](backup-migration.md#enable-the-catalog-on-an-existing-feed).

## catalogpagesize

The number of entries in each catalog page. Sleet starts a new page when the latest one is full. Only used when the catalog is on.

## badgesenabled

When `true`, `push` and `delete` update the [version badge](badges.md) files under `badges/`. Badges are off on new feeds. To turn them on and create badges for the packages that are already on the feed, run `sleet recreate --set badgesenabled:true`.

## galleryenabled

When `true`, the feed has a [package gallery](gallery.md), a web page for browsing its packages. Sleet writes it to `index.html` at the root of the feed, and adds a resource to `index.json` that links packages to their page in the gallery. Feeds that don't have this setting, such as feeds created by older versions of Sleet, are treated as `true`.

Sleet applies the setting only when it creates the feed, with `init`, the first push to a new feed, or `recreate`. Pushing to an existing feed never adds or removes the gallery. To change it on an existing feed, run `sleet recreate --set galleryenabled:false` or `sleet recreate --set galleryenabled:true`. To create a new feed without the gallery, run `sleet init --set galleryenabled:false` before the first push. See [enable or disable the gallery](gallery.md#enable-or-disable-the-gallery).

## externalsearch

The URL of an [external search](external-search.md) service. When you set it, Sleet points the search resource in `index.json` to that URL right away. Unset it to go back to the built-in static search:

```bash
sleet feed-settings --set externalsearch:https://search.example.com/query
sleet feed-settings --unset externalsearch
```

`recreate` keeps the external search URL. `init` doesn't unless you pass it with `--set`, see [settings and init](#settings-and-init).

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

## Removed settings

`symbolsfeedenabled` turned on the [symbol server](symbol-server.md), which Sleet 8.0 removed. Sleet ignores it, and `recreate` removes it from the feed.

## Settings and init

`init` writes `sleet.settings.json`, with the values passed to `--set` applied. If you run it on a feed that already has settings, it:

- Sets `catalogenabled` to `false` unless you pass `--with-catalog` or `--set catalogenabled:true`.
- Points search back to the static search file, even if `externalsearch` is set. Pass `--set externalsearch:<url>` to keep external search, or run `feed-settings --set externalsearch:<url>` afterwards.
- Keeps `badgesenabled`, `galleryenabled`, `catalogpagesize`, and the retention settings, unless you change them with `--set`.
- Removes keys that Sleet doesn't know.

Only run `init` on a new feed. `recreate` keeps all settings that Sleet knows, and applies the values passed to its `--set` option.
