# Use a feed with NuGet

A Sleet feed is a standard NuGet v3 package source. Anyone who can read the feed over HTTPS can restore packages from it with `dotnet`, Visual Studio, or `nuget.exe`. Clients don't need Sleet.

## Find the source URL

The package source URL is `index.json` at the root of the feed. If the source has a `baseURI`, the feed root is `baseURI`. Otherwise it is `path`.

| Feed | Source URL |
| --- | --- |
| Azure | `https://myaccount.blob.core.windows.net/feed/index.json` |
| Amazon S3 | `https://my-bucket-feed.s3.us-west-2.amazonaws.com/index.json` |
| Local folder behind a web server | `https://example.com/feed/index.json` |
| Feed in a sub folder of a container or bucket | `https://myaccount.blob.core.windows.net/feed/team-a/index.json` |

Open the URL in a browser, or use curl, to check that it works. Azure and S3 feeds store JSON compressed, so tell curl to decompress it:

```bash
curl --compressed https://myaccount.blob.core.windows.net/feed/index.json
```

## Add the source with the dotnet CLI

```bash
dotnet nuget add source https://myaccount.blob.core.windows.net/feed/index.json --name sleet
```

This adds the source to your user-level NuGet.Config file. Add `--configfile ./NuGet.Config` to write to a file in your repository instead.

Check that NuGet can read the feed:

```bash
dotnet package search --source sleet
```

## Add the source in NuGet.Config

To use the feed in a repository, commit a `NuGet.Config` file next to the solution:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
    <add key="sleet" value="https://myaccount.blob.core.windows.net/feed/index.json" />
  </packageSources>
</configuration>
```

`<clear />` removes sources that come from user or machine settings, so every machine and build agent uses the same sources. Keep nuget.org in the list if your packages depend on packages from nuget.org. NuGet looks for each dependency in all sources, and a Sleet feed usually holds only your own packages.

For more options, see Microsoft's [nuget.config reference](https://learn.microsoft.com/en-us/nuget/reference/nuget-config-file).

## Map packages to sources

When you use more than one source, [package source mapping](https://learn.microsoft.com/en-us/nuget/consume-packages/package-source-mapping) tells NuGet which source each package comes from. It protects you from a package with the same id on another source.

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
    <add key="sleet" value="https://myaccount.blob.core.windows.net/feed/index.json" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="nuget.org">
      <package pattern="*" />
    </packageSource>
    <packageSource key="sleet">
      <package pattern="Contoso.*" />
    </packageSource>
  </packageSourceMapping>
</configuration>
```

The most specific pattern wins, so packages that start with `Contoso.` come only from the Sleet feed, and everything else comes from nuget.org.

## HTTP sources

NuGet requires HTTPS sources. With the .NET 9 SDK and later, restore fails for an HTTP source with this error:

```text
error NU1302: You are running the 'restore' operation with an 'HTTP' source: http://localhost:8080/index.json. NuGet requires HTTPS sources.
```

Serve the feed over HTTPS. For a local test only, you can allow HTTP for one source:

```xml
<add key="sleet-test" value="http://localhost:8080/index.json" allowInsecureConnections="true" />
```

The CLI equivalent is `dotnet nuget add source http://localhost:8080/index.json --name sleet-test --allow-insecure-connections`.

## Sources that need credentials

NuGet can't sign in to Azure Storage or Amazon S3. If the feed must be private, put a server in front of it that asks for a user name and password, as described in [private feeds](private-feeds.md). Then give NuGet the credentials:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
    <add key="sleet" value="https://packages.example.com/index.json" />
  </packageSources>
  <packageSourceCredentials>
    <sleet>
      <add key="Username" value="nuget" />
      <add key="ClearTextPassword" value="%SLEET_FEED_PASSWORD%" />
    </sleet>
  </packageSourceCredentials>
</configuration>
```

The element under `packageSourceCredentials` must match the source key. NuGet replaces `%SLEET_FEED_PASSWORD%` with the value of that environment variable, so the password stays out of the file.

You can also store credentials in your user-level NuGet.Config:

```bash
dotnet nuget add source https://packages.example.com/index.json --name sleet --username nuget --password <password>
```

On Windows, NuGet encrypts the password for the current user. On macOS and Linux, add `--store-password-in-clear-text`.

## Visual Studio

Visual Studio reads the `NuGet.Config` file in your solution folder. To add the source for your user account instead, go to **Tools** > **Options** > **NuGet Package Manager** > **Package Sources**, select the add button, and set **Source** to the `index.json` URL.

The **Browse** tab lists every package on a Sleet feed, whatever you type in the search box. Search on a Sleet feed is a static file, see [limitations](limitations.md#search-results).

## See new versions right away

NuGet caches the list of versions on a source for about 30 minutes. If a version you just pushed isn't found, clear the HTTP cache and restore again:

```bash
dotnet nuget locals http-cache --clear
dotnet restore
```

Feeds behind a CDN can also serve old files until the CDN cache expires. See [CDN and caching](cdn-caching.md).

## Publish packages

You can't publish to a Sleet feed with `dotnet nuget push` or `nuget push`, because the feed is only static files. Use [sleet push](commands.md#push) instead.
