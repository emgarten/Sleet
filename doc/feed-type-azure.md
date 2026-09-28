# Create an Azure feed

This quick start creates a public NuGet v3 feed in an Azure Blob Storage container. Sleet signs in with Microsoft Entra ID, which is the recommended way to give it access to your storage account.

## Prerequisites

- An Azure storage account. The examples use an account named `myaccount` and a container named `feed`.
- Sleet installed. See [Install Sleet](install.md).
- An Entra ID identity with the **Storage Blob Data Contributor** role on the storage account. For your own user account, sign in with the Azure CLI (`az login`). See [Azure authentication](auth-azure.md) for role assignment, service principals, and managed identities.

## Allow anonymous read access

NuGet reads the feed anonymously, so the container must allow anonymous reads. Storage accounts created since November 2023 don't allow anonymous access by default.

To allow it in the Azure portal, open the storage account, go to **Settings** > **Configuration**, and set **Allow Blob anonymous access** to **Enabled**. With the Azure CLI:

```bash
az storage account update --name myaccount --resource-group my-resource-group --allow-blob-public-access true
```

If you skip this step, the first push fails with a `PublicAccessNotPermitted` error when Sleet tries to create the container.

> [!NOTE]
> Anonymous access on the account doesn't make any data public by itself. Each container also has an access level, and only containers set to allow anonymous access can be read without credentials. To keep the feed private instead, see [private feeds](private-feeds.md).

## Create the config file

Create a starter `sleet.json` file:

```bash
sleet createconfig --azure
```

The template uses a connection string. Replace the source with one that uses `path`, and delete the `connectionString` property:

sleet.json:

```json
{
  "sources": [
    {
      "name": "feed",
      "type": "azure",
      "container": "feed",
      "path": "https://myaccount.blob.core.windows.net/feed/"
    }
  ]
}
```

.netconfig:

```ini
[sleet "feed"]
    type = azure
    container = feed
    path = https://myaccount.blob.core.windows.net/feed/
```

| Property | Use |
| --- | --- |
| `container` | The container that stores the feed. |
| `path` | The container URL. Sleet uses it to find the storage account and to sign in with Entra ID. It must start with the URL of the container. |
| `baseURI` | Optional URL written into the feed files, such as a CDN URL. See [CDN and caching](cdn-caching.md). |

When `path` is set and `connectionString` isn't, Sleet signs in with Entra ID. If a `connectionString` property is present, even with the empty template value, Sleet uses it instead and fails with an error such as `Invalid connectionString for azure account.` For all properties, see [Azure properties](client-settings.md#azure-properties).

## Push packages

Sign in, then push a package or a folder of packages:

```bash
az login
sleet push ./nupkgs
```

On the first push, Sleet does the following:

- Creates the container if it doesn't exist, with the access level **Container**, which allows anonymous reads and blob listing.
- Initializes the feed with the default [feed settings](feed-settings.md).

If the container already exists, Sleet doesn't change its access level. Create the container yourself when you want a different level. **Blob** is enough for NuGet, and it doesn't allow anonymous listing.

To turn on the catalog or the [symbol server](symbol-server.md) from the start, run [init](commands.md#init) before the first push:

```bash
sleet init --with-symbols
```

## Use the feed

The package source URL is the container URL plus `index.json`:

```text
https://myaccount.blob.core.windows.net/feed/index.json
```

Check that it works. Sleet stores JSON files compressed, so tell curl to decompress:

```bash
curl --compressed https://myaccount.blob.core.windows.net/feed/index.json
```

Then add the source to NuGet. See [Use a feed with NuGet](consume-feed.md).

```bash
dotnet nuget add source https://myaccount.blob.core.windows.net/feed/index.json --name sleet
```

## Use a connection string

Sleet also accepts a storage account connection string with an account key. This is simpler to set up, but the key gives full access to the whole storage account, so prefer Entra ID. Sleet logs a warning about it when you run with `--verbosity detailed`.

Keep the key out of the file with a [token](client-settings.md#tokens). This example reads the value from the `AZURE_STORAGE_CONNECTION_STRING` environment variable:

```json
{
  "sources": [
    {
      "name": "feed",
      "type": "azure",
      "container": "feed",
      "connectionString": "$AZURE_STORAGE_CONNECTION_STRING$"
    }
  ]
}
```

See [connection strings](auth-azure.md#connection-strings) for details.

## Next steps

- [Azure authentication](auth-azure.md)
- [Publish from CI](ci-server.md)
- [Private feeds](private-feeds.md)
- [CDN and caching](cdn-caching.md)
- [Multiple feeds](multiple-feeds.md)
- [Azure properties](client-settings.md#azure-properties)
