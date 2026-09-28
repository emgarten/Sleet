# Symbol server

Sleet can build a static HTTP symbol store from packages on your feed. Add the Sleet symbol server URL to Visual Studio or another debugger to load PDB files for package dependencies.

## Enable symbols

Symbols are disabled on new feeds unless you enable them during initialization:

```bash
sleet init --with-symbols
```

For an existing feed, set the feed setting and recreate the feed:

```bash
sleet feed-settings --set symbolsfeedenabled:true
sleet recreate
```

The `feed-settings` command updates `sleet.settings.json`, but it doesn't add the symbol server to `index.json`. `recreate` downloads the packages, destroys the feed files, initializes the feed again with the current settings, and pushes the packages back. After that, `index.json` lists the symbol server and packages that were already on the feed are indexed.

For a local feed with a `baseURI`, `recreate` doesn't work. Follow [rebuild a local feed without recreate](backup-migration.md#rebuild-a-local-feed-without-recreate) and run `init --with-symbols`.

## Disable symbols

To stop indexing symbols for future pushes, set the feed setting to false:

```bash
sleet feed-settings --set symbolsfeedenabled:false
```

This setting change does not delete existing symbol files or remove the symbol server entry from `index.json` by itself. Run `sleet recreate` after disabling symbols if you want the feed rebuilt without the symbol server files and service-index entries.

## Package files Sleet indexes

Sleet indexes `.dll` files and matching `.pdb` files from `.nupkg` and `.symbols.nupkg` files. It does not index `.exe` or XML documentation files. It does not support `.snupkg` symbol packages.

You can provide symbols in either of these ways:

- Include PDB files next to the DLL files inside the main `.nupkg`.
- Push a legacy `.symbols.nupkg` package with the same package id and version.

To create a legacy `.symbols.nupkg` with the .NET SDK, use:

```bash
dotnet pack -p:IncludeSymbols=true -p:SymbolPackageFormat=symbols.nupkg
```

Microsoft documents `IncludeSymbols` and `SymbolPackageFormat` in [Creating symbol packages](https://learn.microsoft.com/en-us/nuget/create-packages/symbol-packages-snupkg). The newer `.snupkg` format is recommended for NuGet.org, but Sleet's symbol server reads only `.symbols.nupkg` files and PDBs inside normal `.nupkg` files.

## Symbol packages on the feed

When symbols are enabled, `.symbols.nupkg` files are stored under the feed's `symbols/packages/` area. NuGet restore still uses only the regular `.nupkg` from the package feed. The symbol package is for the debugger.

`sleet download` includes both regular packages and `.symbols.nupkg` files. `sleet recreate` also downloads symbol packages and reindexes them when symbols are enabled.

## Symbol server URL and layout

The symbol server URL is the feed root plus `symbols/`. If your NuGet source is:

```text
https://myaccount.blob.core.windows.net/feed/index.json
```

Use this symbol URL:

```text
https://myaccount.blob.core.windows.net/feed/symbols/
```

The same URL appears in `index.json` with this resource type:

```text
http://schema.emgarten.com/sleet#SymbolsServer/1.0.0
```

Sleet writes symbol files with this layout:

```text
symbols/{file-name}/{hash}/{file-name}
symbols/{file-name}/{hash}/packages.json
```

For example:

```text
symbols/MyLibrary.pdb/4B26B9A60D384F90855C3A6196C6C8781/MyLibrary.pdb
```

For DLL files, the hash is built from the PE timestamp and image size. For portable PDBs, Sleet reads the CodeView debug entry from the matching assembly and uses the PDB GUID plus `ffffffff`. For Windows PDBs, it uses the PDB GUID plus the PDB age.

## Configure Visual Studio

In Visual Studio, open **Tools** > **Options** > **Debugging** > **Symbols**, add the Sleet `symbols/` URL, enable it, and choose a local symbol cache. See Microsoft's current guide to [configure symbol locations and loading options](https://learn.microsoft.com/en-us/visualstudio/debugger/specify-symbol-dot-pdb-and-source-files-in-the-visual-studio-debugger?view=visualstudio).

To step into package code, you may also need to disable Just My Code. See Microsoft's guide to [Just My Code](https://learn.microsoft.com/en-us/visualstudio/debugger/just-my-code?view=visualstudio). If your packages include Source Link information, Visual Studio can use the PDB to find source files.

## Private feeds

The debugger must be able to reach the symbol URL. Authentication support depends on the debugger and symbol-loading path, so test private feeds with the debugger you plan to use.
