# Version badges

Sleet can write a small JSON file with the latest version of each package. Use these files with shields.io to show the latest stable or prerelease version from a public feed.

## Enable badges

Badges are off on new feeds. Turn them on with the `badgesenabled` [feed setting](feed-settings.md#badgesenabled):

```bash
sleet feed-settings --set badgesenabled:true
```

Sleet updates a badge when a push or delete changes the latest version of a package. To create badges for packages that are already on the feed, run `sleet recreate` after you turn badges on. For a local feed with a `baseURI`, use [rebuild a local feed without recreate](backup-migration.md#rebuild-a-local-feed-without-recreate) instead.

To stop updating badges, set the value back to `false`:

```bash
sleet feed-settings --set badgesenabled:false
```

Turning badges off doesn't delete the badge files that already exist.

## Badge files

Sleet writes these files when badges are enabled:

| Badge | Feed file |
| --- | --- |
| Stable version | `badges/v/{id}.json` |
| Prerelease version | `badges/vpre/{id}.json` |

The package id is lowercased. Sleet no longer writes SVG badge files. When an old SVG file exists for a package id that no longer has a badge version, Sleet deletes the old SVG and JSON badge files.

For the stable badge, Sleet uses the newest stable version. If the package has no stable version, it falls back to the newest prerelease version. For the prerelease badge, Sleet uses the newest version including prerelease versions.

## JSON format

Sleet writes Shields endpoint JSON like this:

```json
{
  "schemaVersion": 1,
  "label": "nuget",
  "message": "v1.2.3",
  "color": "#007ec6"
}
```

Stable versions use color `#007ec6`. Prerelease versions use color `#dfb317`.

## Shields endpoint URLs

Use the feed root URL, not the `index.json` URL. The feed root includes the Azure container, S3 bucket, local web root, and any `feedSubPath`.

Stable badge:

```text
https://img.shields.io/endpoint?url={feed-root}/badges/v/{id}.json
```

Prerelease badge:

```text
https://img.shields.io/endpoint?url={feed-root}/badges/vpre/{id}.json
```

Markdown example:

```markdown
[![MyPackage](https://img.shields.io/endpoint?url=https://myaccount.blob.core.windows.net/feed/badges/v/mypackage.json)](https://myaccount.blob.core.windows.net/feed/index.json)
```

Shields supports query parameters such as `style` and `label`. See the [Shields endpoint badge documentation](https://shields.io/badges/endpoint-badge).

## Public access requirement

shields.io must be able to fetch the JSON file anonymously. Private feeds cannot use shields.io-hosted badges unless the badge JSON endpoint is public.
