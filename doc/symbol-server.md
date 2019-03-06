# Setting up a symbol server with sleet

Sleet allows you to create an http [symbol store](https://msdn.microsoft.com/en-us/library/windows/desktop/ms680693.aspx) from dll and pdb files in nupkgs. Using a symbol store you can point Visual Studio to sleet feed's symbol folder and debug assemblies from package dependencies.

## Enabling the symbol store

By default the symbol store is disabled for new feeds. It can be turned on when creating a new feed with 

```
init --with-symbols
```

For an existing feed update the settings. This will modify 
*sleet.settings.json* in the root of the feed, which will enable symbols for all clients pushing to the feed.

```
feed-settings --set symbolsfeedenabled:true
```

Existing packages will **not** be indexed, to index nupkgs already on the feed run the *recreate* command to rebuild the feed using the new settings.

## Pushing packages

Files from .nupkg and .symbols.nupkg files will be indexed in the symbol store. 

Symbol nupkgs are uploaded to the feed, but they are not available for download through the NuGet client, only the non-symbols nupkg can be used in package restore.

Symbol nupkgs can be retrieved with the *download* command. They are also used when running *recreate*, all symbols nupkgs will be downloaded and reindexed.

## Debugging packages with Visual Studio

The symbol store is the url of the feed, excluding */index.json*, appended with `/symbols/`

The full url can be found in the feed's root `/index.json` under the type `http://schema.emgarten.com/sleet#SymbolsServer/1.0.0`

Instuctions on how to add the symbol store to Visual Studio can be found on [docs.microsoft.com](https://docs.microsoft.com/en-us/visualstudio/debugger/specify-symbol-dot-pdb-and-source-files-in-the-visual-studio-debugger?view=vs-2017#configure-symbol-locations-and-loading-options)

