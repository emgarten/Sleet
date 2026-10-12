# Use SleetLib from .NET

SleetLib is the library behind the Sleet CLI. Use it from custom tools, MSBuild tasks, services, or tests that need to create or update Sleet feeds without starting a separate process.

## Package and target frameworks

Add the `SleetLib` NuGet package to your project. The current project targets:

- `net8.0`
- `net9.0`
- `net10.0`

SleetLib follows Sleet releases. The repository release notes do not describe a separate API stability policy for the library, so treat public APIs as tied to the Sleet package version you reference.

## Push packages from code

This example loads a `sleet.json` file, creates the configured file system for the `feed` source, and pushes packages from `./nupkgs`.

```csharp
using System;
using System.Collections.Generic;
using NuGet.Common;
using Sleet;

var log = NullLogger.Instance;
var settings = LocalSettings.Load("sleet.json");

using var cache = new LocalCache();
var fileSystem = await FileSystemFactory.CreateFileSystemAsync(settings, cache, "feed", log)
    ?? throw new InvalidOperationException("Unable to find source 'feed'.");

var success = await PushCommand.RunAsync(
    settings,
    fileSystem,
    new List<string> { "./nupkgs" },
    force: false,
    skipExisting: true,
    log);

if (!success)
{
    throw new InvalidOperationException("Push failed.");
}
```

## Build settings in code

You can also build the same settings object from JSON. This is useful for generated local feeds or tests.

```csharp
using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using NuGet.Common;
using Sleet;

var json = JObject.Parse(
    """
    {
      "sources": [
        {
          "name": "feed",
          "type": "local",
          "path": "C:\\feeds\\myfeed"
        }
      ]
    }
    """);

var log = NullLogger.Instance;
var settings = LocalSettings.Load(json);

using var cache = new LocalCache();
var fileSystem = await FileSystemFactory.CreateFileSystemAsync(settings, cache, "feed", log)
    ?? throw new InvalidOperationException("Unable to find source 'feed'.");

await StatsCommand.RunAsync(settings, fileSystem, log);
```

## Run other commands

Most CLI operations have matching command classes in SleetLib. For example, delete a package version with `DeleteCommand`:

```csharp
var deleted = await DeleteCommand.RunAsync(
    settings,
    fileSystem,
    packageId: "My.Package",
    version: "1.2.3",
    reason: null,
    force: false,
    log);
```

The command classes use the same feed behavior as the CLI. See the [command reference](commands.md) for operation behavior and [client settings](client-settings.md#sleetjson) for `sleet.json` source settings.
