# Azure authentication

Sleet needs write access to the Azure Storage container that holds the feed. This page covers the ways to give it access, starting with the recommended one.

## How Sleet picks a credential

Sleet looks at two source properties:

| Settings | What Sleet uses |
| --- | --- |
| `path` set, no `connectionString` | Microsoft Entra ID, through `DefaultAzureCredential` from the Azure Identity library. Sleet takes the storage account URL from `path`. |
| `connectionString` set | The connection string. `path` is optional, and is only checked against the container URL. |

Use `path` without a connection string whenever you can. Tokens from Entra ID expire on their own, and access is controlled with Azure roles instead of a shared account key.

## Microsoft Entra ID

`DefaultAzureCredential` tries these credentials in order, and uses the first one that works:

1. Environment variables for a service principal.
1. Workload identity, for example on Azure Kubernetes Service.
1. Managed identity, on Azure hosts such as virtual machines and App Service.
1. Visual Studio sign-in.
1. Azure CLI sign-in (`az login`).
1. Azure PowerShell sign-in (`Connect-AzAccount`).
1. Azure Developer CLI sign-in (`azd auth login`).

Sleet never opens a browser to sign in. See Microsoft's [credential chains](https://learn.microsoft.com/en-us/dotnet/azure/sdk/authentication/credential-chains) article for details on each credential.

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

## Assign a data role

The identity that runs Sleet needs the **Storage Blob Data Contributor** role. This role lets Sleet read, write, and delete blobs, list the container, and take the lease that [locks the feed](locking.md).

> [!NOTE]
> The Owner and Contributor roles manage the storage account, but they don't grant access to blob data through Entra ID. Assign Storage Blob Data Contributor even if you own the account.

Assign the role on the storage account:

```bash
az role assignment create \
  --assignee <user-or-app-id> \
  --role "Storage Blob Data Contributor" \
  --scope /subscriptions/<subscription-id>/resourceGroups/my-resource-group/providers/Microsoft.Storage/storageAccounts/myaccount
```

To limit access to the feed container, add `/blobServices/default/containers/feed` to the end of the scope. The container must exist before you can assign a role on it, so create it first.

New role assignments can take a few minutes to apply. Until then, Sleet fails with a 403 `AuthorizationPermissionMismatch` error.

## Sign in on your machine

Sign in with the Azure CLI, then run Sleet in the same shell:

```bash
az login
sleet push ./nupkgs
```

A Visual Studio or Azure PowerShell sign-in also works.

## Service principal

For a build server that isn't on Azure, create a service principal (an app registration) and set these environment variables. Sleet passes them to the Azure Identity library.

| Variable | Value |
| --- | --- |
| `AZURE_TENANT_ID` | The Entra ID tenant ID. |
| `AZURE_CLIENT_ID` | The app's client ID. |
| `AZURE_CLIENT_SECRET` | A client secret for the app. |
| `AZURE_CLIENT_CERTIFICATE_PATH` | Path to a certificate, instead of a client secret. |
| `AZURE_CLIENT_CERTIFICATE_PASSWORD` | Password for the certificate, if it has one. |

Store the secret in your CI secret store. A certificate or a federated credential is safer than a client secret. For the full list of variables, see the [Azure Identity README](https://github.com/Azure/azure-sdk-for-net/blob/main/sdk/identity/Azure.Identity/README.md#environment-variables).

## Managed identity

When Sleet runs on an Azure host with a managed identity, such as a virtual machine or a self-hosted build agent, no secrets are needed. Assign the role to the managed identity.

For a user-assigned managed identity, set `AZURE_CLIENT_ID` to the identity's client ID so Sleet uses the right one.

## CI systems

- GitHub Actions: sign in with `azure/login` and OpenID Connect, then run Sleet in a later step. See [GitHub Actions to Azure with OIDC](ci-server.md#github-actions-to-azure-with-oidc).
- Azure Pipelines: run Sleet inside an `AzureCLI@2` task with an Azure Resource Manager service connection. See [Azure Pipelines](ci-server.md#azure-pipelines-to-azure-with-a-service-connection).

## Connection strings

A connection string with an account key also works. The key gives full access to the whole storage account, and it doesn't expire, so use it only when Entra ID isn't an option.

```text
DefaultEndpointsProtocol=https;AccountName=myaccount;AccountKey=<account-key>;EndpointSuffix=core.windows.net
```

Don't put the key in a file that is checked in. Use one of these instead:

- A [token](client-settings.md#tokens) in `sleet.json`, such as `"connectionString": "$AZURE_STORAGE_CONNECTION_STRING$"`.
- The `SLEET_FEED_CONNECTIONSTRING` [environment variable](environment-variables.md#feed-variables) with `--config none`.

Sleet logs a warning about connection strings at the `detailed` verbosity level. You can ignore it if you chose this option on purpose.

Sleet doesn't read a shared access signature (SAS) from `path`. Only the storage account URL in `path` is used.

## Local testing with Azurite

[Azurite](https://learn.microsoft.com/en-us/azure/storage/common/storage-use-azurite) is a local emulator for Azure Storage. Use the `UseDevelopmentStorage=true` connection string to point Sleet at it:

```json
{
  "sources": [
    {
      "name": "test",
      "type": "azure",
      "container": "feed",
      "connectionString": "UseDevelopmentStorage=true"
    }
  ]
}
```

## Troubleshooting

| Error | Fix |
| --- | --- |
| `AuthorizationPermissionMismatch` or `This request is not authorized to perform this operation using this permission.` | The identity is missing Storage Blob Data Contributor, or the role assignment hasn't applied yet. |
| `DefaultAzureCredential failed to retrieve a token` | No credential in the chain could sign in. Run `az login`, or set the service principal variables. |
| `Missing connectionString for azure account.` | Set `path`, or remove the empty `connectionString` property. |
| `Invalid connectionString for azure account.` | The connection string is still the template value from `createconfig`. Remove it and use `path`, or fill it in. |
| `Invalid feed path. Azure container ... does not match the provided URI` | `path` must start with the container URL, for example `https://myaccount.blob.core.windows.net/feed/`. |
| `PublicAccessNotPermitted` | The storage account doesn't allow anonymous access. See [allow anonymous read access](feed-type-azure.md#allow-anonymous-read-access). |

For other problems, see [troubleshooting](troubleshooting.md).
