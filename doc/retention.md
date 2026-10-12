# Package retention

Package retention deletes older package versions from a feed. Use it to keep only the newest stable and prerelease versions for each package id.

> [!WARNING]
> Pruned packages are deleted from the feed. Sleet does not support unlisting. Deleting packages can break restores for consumers that pin those versions. Test with `--dry-run` first and take a [backup](backup-migration.md) before pruning.

## How retention works

Retention keeps the newest N stable versions and M prerelease versions per package id. Versions beyond those limits are removed from the feed, including matching symbol packages when present.

Automatic pruning runs on `sleet push` only when both retention settings are present and greater than zero:

- `retentionmaxstableversions`
- `retentionmaxprereleaseversions`

Packages pushed by the current `push` command are pinned for that push and are never pruned by the same push. Automatic pruning only checks package ids included in the push.

Sleet can also group prerelease versions by the first prerelease labels before applying the prerelease limit. That setting is stored as `retentiongroupbyfirstprereleaselabelcount`.

See [feed settings](feed-settings.md#retention-settings) for the setting keys.

## Retention settings

Use `sleet retention settings` to store automatic retention settings in the feed.

```bash
sleet retention settings --stable 5 --prerelease 2
```

This updates `sleet.settings.json`. Future pushes apply the settings automatically. Run `sleet retention prune` if you want to apply the settings to packages already on the feed immediately.

Disable automatic pruning:

```bash
sleet retention settings --disable
```

See the [command reference](commands.md#retention-settings) for all options.

## Retention prune

Use `sleet retention prune` for a one-off prune. If you omit `--stable`, `--prerelease`, or `--release-labels`, Sleet uses the values stored in feed settings.

Apply stored settings:

```bash
sleet retention prune
```

Override the limits for this run:

```bash
sleet retention prune --stable 5 --prerelease 2
```

Prune only selected package ids:

```bash
sleet retention prune --package My.Package --package Other.Package --stable 2 --prerelease 1
```

Preview changes without deleting packages:

```bash
sleet retention prune --dry-run --stable 5 --prerelease 2
```

See the [command reference](commands.md#retention-prune) for all options.

## Retain packages by release labels

Use `--release-labels` when prerelease versions contain labels for branches, channels, or build groups.

Example package versions:

```text
1.0.0-beta.branch.a.build.100
1.0.0-beta.branch.a.build.101
1.0.0-beta.branch.a.build.102
1.0.0-beta.branch.a.build.103
1.0.0-beta.branch.z.build.205
1.0.0-beta.branch.z.build.206
1.0.0-beta.branch.z.build.207
1.0.0-rc.branch.a.build.308
```

To keep one prerelease from each group based on the first three release labels, run:

```bash
sleet retention prune --stable 3 --prerelease 1 --release-labels 3
```

Or store the setting for future pushes:

```bash
sleet retention settings --stable 3 --prerelease 1 --release-labels 3
```

Sleet groups prerelease packages by `VERSION-FIRST.SECOND.THIRD.*`. The major, minor, and patch parts of the version are used for ordering, but not for choosing groups. In this example, all prerelease packages with `beta.branch.a` are grouped together and only the highest version remains.

After pruning, these versions remain:

```text
1.0.0-beta.branch.a.build.103
1.0.0-beta.branch.z.build.207
1.0.0-rc.branch.a.build.308
```
