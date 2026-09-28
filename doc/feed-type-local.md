# Create a local feed

A local feed writes the Sleet feed to a folder on disk. To use that feed over the network, serve the folder with a web server and set `baseURI` to the public URL. If `baseURI` is omitted, Sleet writes `file:///` URLs into the feed JSON.

## Create a local configuration

Run `createconfig` to create a starter `sleet.json`:

```powershell
sleet createconfig --local
```

The local template uses the current directory plus `myfeed` for `path`, and `https://example.com/feed/` for `baseURI`:

```json
{
  "username": "",
  "useremail": "",
  "sources": [
    {
      "name": "myLocalFeed",
      "type": "local",
      "path": "C:\\path\\to\\current-directory\\myfeed",
      "baseURI": "https://example.com/feed/"
    }
  ]
}
```

Edit the file before you push packages:

- Set `path` to the folder Sleet should read and write.
- Set `baseURI` to the URL that will serve `index.json`, such as `https://example.com/feed/`.

> [!TIP]
> Set `baseURI` before the first push. Sleet writes it into the feed files, so changing it later means rebuilding the feed. The `recreate` command doesn't work when `baseURI` is different from `path`. See [rebuild a local feed without recreate](backup-migration.md#rebuild-a-local-feed-without-recreate).

Relative `path` values are resolved relative to the folder that contains `sleet.json`. Relative paths only work when Sleet loaded a settings file. If you configure a local feed from environment variables or command-line properties and use a relative path, Sleet fails with:

```text
Cannot use a relative 'path' without a sleet.json file.
```

In a `.netconfig` file, use an absolute `path`. A relative path there is resolved from the current folder, not from the folder of the `.netconfig` file.

If the settings file contains more than one source, pass `-s` or `--source` with the source name.

## Initialize and push packages

You can initialize the feed explicitly:

```powershell
sleet init -c C:\feeds\sleet.json -s myLocalFeed
```

This step is optional for normal publishing. `push` creates the folder and initializes the feed on first use.

Push one package or a directory of packages:

```powershell
sleet push C:\packages -c C:\feeds\sleet.json -s myLocalFeed --skip-existing
```

Sleet searches directory inputs recursively for `*.nupkg` files. Sleet does not expand wildcards itself. Pack to a folder, then push that folder.

## Serve the folder

A local feed is a static set of files. You can copy or sync the output folder to another machine or a static host, then serve it from the `baseURI` URL.

Do not copy or sync the folder while a `push`, `delete`, `destroy`, or `recreate` command is running. Wait for the feed lock to be released so clients do not see a partially updated feed.

See [Web servers and static hosting](static-hosting.md) for IIS, nginx, Apache, and static-host requirements.

## Use the feed with NuGet

When the folder is served over HTTPS, add the Sleet service index as the NuGet source. See [Use a feed with NuGet](consume-feed.md) for more options.

```xml
<configuration>
  <packageSources>
    <add key="sleet-local" value="https://example.com/feed/index.json" />
  </packageSources>
</configuration>
```

For local HTTP testing, see [local testing](static-hosting.md#test-a-local-server).

## Direct disk access

NuGet can't restore packages straight from a Sleet local feed folder. Using the feed folder, its `flatcontainer` folder, or a `file:///` URL to `index.json` as a package source doesn't work. NuGet reports `NU1101` for the folders and `NU1301` for the `file:///` URL. Serve the folder over HTTPS instead.

If you only need a folder or file share of packages, NuGet's built-in [local feeds](https://learn.microsoft.com/en-us/nuget/hosting-packages/local-feeds) are simpler and don't need Sleet.

## Locking

Local feeds use a `.lock` file in the feed root. Sleet creates it before changing the feed and removes it when the command completes.

If a process stops while holding the lock, see [Feed locking](locking.md).
