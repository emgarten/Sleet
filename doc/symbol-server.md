# Symbol server (removed)

Sleet 8.0 removed the symbol server. Older versions could serve `.pdb` files to debuggers from a `symbols/` folder on the feed. The .NET SDK now makes this unnecessary: PDBs can be embedded in the assemblies of a package, and [Source Link](https://learn.microsoft.com/en-us/dotnet/standard/library-guidance/sourcelink) lets debuggers download the matching source code.

## Add symbols to your packages

Embed the PDB in each assembly. Set `DebugType` in the project file, or in `Directory.Build.props` to cover every project:

```xml
<PropertyGroup>
  <DebugType>embedded</DebugType>
</PropertyGroup>
```

Debuggers such as Visual Studio read the embedded PDB from the `.dll`, so projects that use the package don't need a symbol server. The .NET 8 SDK and later add Source Link information for repositories on GitHub, Azure Repos, GitLab, and Bitbucket. To step into package code in Visual Studio, turn off [Just My Code](https://learn.microsoft.com/en-us/visualstudio/debugger/just-my-code?view=visualstudio).

For other ways to ship symbols, such as `.pdb` files next to the `.dll` in the package, or `.snupkg` packages on nuget.org, see Microsoft's guidance on [symbol packages](https://learn.microsoft.com/en-us/dotnet/standard/library-guidance/nuget#symbol-packages). Sleet doesn't support `.snupkg` packages.

## Feeds that use the symbol server

Feeds created with the symbol server keep working with NuGet. Most Sleet commands print this warning for them:

```text
This feed has the symbol server enabled. Sleet no longer supports the symbol server and files under symbols/ are no longer updated. Run 'sleet recreate' to remove it from the feed. See: https://github.com/emgarten/Sleet/blob/main/doc/symbol-server.md
```

Until you [remove the symbol server from the feed](#remove-the-symbol-server-from-a-feed):

- The `symbols/` folder and the symbol server entries in `index.json` stay on the feed, so debuggers can still load symbols for packages pushed with older versions of Sleet.
- Sleet doesn't add symbols for new packages. `delete` and [retention](retention.md) don't remove symbols files.
- `push` skips `.symbols.nupkg` files with a warning.
- `download` still downloads the `.symbols.nupkg` files that are on the feed.
- Sleet ignores the `symbolsfeedenabled` [feed setting](feed-settings.md).

## Remove the symbol server from a feed

Run [recreate](commands.md#recreate). The new feed doesn't have the `symbols/` folder, the symbol server entries in `index.json`, or the `symbolsfeedenabled` setting.

If the feed has `.symbols.nupkg` files, `recreate` stops before it changes the feed, because the new feed can't keep them:

```text
The feed contains 2 symbols packages. Sleet no longer supports symbols packages and recreating the feed would remove them. Back them up with 'sleet download', then run recreate with --force.
```

Back up the feed, then recreate it with `--force`:

```bash
sleet download -o ./backup
sleet recreate --force
```

> [!WARNING]
> `--force` also makes `recreate` continue past other errors, such as a package that fails to download. Keep the backup until you've checked the new feed. Don't pass the backup folder to `--nupkg-path`, because `recreate` deletes that folder when it succeeds.

`recreate` doesn't work on a local feed whose `baseURI` is different from `path`. Follow [rebuild a local feed without recreate](backup-migration.md#rebuild-a-local-feed-without-recreate) instead. `push` skips the `.symbols.nupkg` files in the backup folder.
