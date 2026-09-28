# Publish from CI

Use CI to pack packages into a folder, then push that folder with Sleet. This page shows secure patterns for Azure, Amazon S3, and generic CI systems.

## Principles

Pack to a known output folder, then push the folder:

```bash
dotnet pack --configuration Release --output ./artifacts/packages
sleet push ./artifacts/packages --skip-existing
```

Sleet accepts files and directories. Directory inputs are searched recursively for `*.nupkg`. Sleet does not expand wildcards itself, although your shell might.

Use these defaults in CI:

- Pin Sleet to the current major version, such as `dotnet tool install -g sleet --version "7.*"`.
- For exact pinning, commit a local tool manifest and run `dotnet tool restore`, then `dotnet sleet ...`. See [Install Sleet](install.md).
- Use `--skip-existing` so reruns do not fail after a package was already published.
- Keep secrets in the CI secret store.
- Prefer workload identity or OIDC over account keys.
- Avoid checked-in `sleet.json` files that contain secrets. Use `SLEET_FEED_*` environment variables or `$TOKEN$` tokens instead. See [configuration](client-settings.md) for tokens and feed settings.
- Consider `--config none` when configuring only with environment variables. This prevents a stray `sleet.json` from being discovered.

## Concurrency

Sleet protects each feed with a lock. Parallel pushes wait for that lock, but it is still better to use your CI system's concurrency control so jobs do not pile up.

For GitHub Actions, use `concurrency`:

```yaml
concurrency:
  group: sleet-feed
  cancel-in-progress: false
```

You can limit lock waiting with `config.feedLockTimeoutMinutes` in `sleet.json`:

```json
{
  "config": {
    "feedLockTimeoutMinutes": 30
  },
  "sources": []
}
```

This setting is not available through `SLEET_FEED_*` environment variables.

## GitHub Actions to Azure with OIDC

This is the preferred Azure pattern. `azure/login@v3` signs in with GitHub OIDC. Sleet uses `DefaultAzureCredential` because the feed has `path` and no connection string.

The Azure identity needs Storage Blob Data Contributor on the storage account or container. See [Azure authentication](auth-azure.md).

```yaml
name: publish-packages

on:
  push:
    branches: [main]

permissions:
  id-token: write
  contents: read

concurrency:
  group: sleet-feed
  cancel-in-progress: false

jobs:
  publish:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v7
        with:
          persist-credentials: false

      - uses: actions/setup-dotnet@v6
        with:
          dotnet-version: 8.0.x

      - uses: azure/login@v3
        with:
          client-id: ${{ secrets.AZURE_CLIENT_ID }}
          tenant-id: ${{ secrets.AZURE_TENANT_ID }}
          subscription-id: ${{ secrets.AZURE_SUBSCRIPTION_ID }}

      - name: Pack
        run: dotnet pack --configuration Release --output ./artifacts/packages

      - name: Push to Sleet
        env:
          SLEET_FEED_TYPE: azure
          SLEET_FEED_CONTAINER: feed
          SLEET_FEED_PATH: https://myaccount.blob.core.windows.net/feed/
        run: |
          dotnet tool install -g sleet --version "7.*"
          sleet push ./artifacts/packages --skip-existing --config none
```

## GitHub Actions to Azure with a connection string

This is simpler to set up, but less secure than OIDC because it stores an account secret in GitHub Actions.

```yaml
name: publish-packages

on:
  push:
    branches: [main]

permissions:
  contents: read

concurrency:
  group: sleet-feed
  cancel-in-progress: false

jobs:
  publish:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v7
        with:
          persist-credentials: false

      - uses: actions/setup-dotnet@v6
        with:
          dotnet-version: 8.0.x

      - name: Pack
        run: dotnet pack --configuration Release --output ./artifacts/packages

      - name: Push to Sleet
        env:
          SLEET_FEED_TYPE: azure
          SLEET_FEED_CONTAINER: feed
          SLEET_FEED_CONNECTIONSTRING: ${{ secrets.SLEET_AZURE_CONNECTION_STRING }}
        run: |
          dotnet tool install -g sleet --version "7.*"
          sleet push ./artifacts/packages --skip-existing --config none
```

## GitHub Actions to Amazon S3 with OIDC

Use `aws-actions/configure-aws-credentials@v6` to assume an AWS role. The action exports temporary credentials as `AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY`, and `AWS_SESSION_TOKEN` environment variables, which Sleet picks up. See [AWS authentication](auth-aws.md).

```yaml
name: publish-packages

on:
  push:
    branches: [main]

permissions:
  id-token: write
  contents: read

concurrency:
  group: sleet-feed
  cancel-in-progress: false

jobs:
  publish:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v7
        with:
          persist-credentials: false

      - uses: actions/setup-dotnet@v6
        with:
          dotnet-version: 8.0.x

      - uses: aws-actions/configure-aws-credentials@v6
        with:
          role-to-assume: ${{ secrets.AWS_ROLE_TO_ASSUME }}
          aws-region: us-west-2

      - name: Pack
        run: dotnet pack --configuration Release --output ./artifacts/packages

      - name: Push to Sleet
        env:
          SLEET_FEED_TYPE: s3
          SLEET_FEED_BUCKETNAME: my-bucket-feed
          SLEET_FEED_REGION: us-west-2
          SLEET_FEED_PATH: https://my-bucket-feed.s3.us-west-2.amazonaws.com/
        run: |
          dotnet tool install -g sleet --version "7.*"
          sleet push ./artifacts/packages --skip-existing --config none
```

Use bucket names without dots if you plan to use virtual-hosted HTTPS URLs.

## Azure Pipelines to Azure with a service connection

Run Sleet inside the `AzureCLI@2` task so `DefaultAzureCredential` can use the Azure CLI login from the Azure Resource Manager service connection.

```yaml
trigger:
  branches:
    include:
      - main

pool:
  vmImage: ubuntu-latest

steps:
  - task: UseDotNet@2
    inputs:
      packageType: sdk
      version: 8.0.x

  - script: dotnet pack --configuration Release --output $(Build.ArtifactStagingDirectory)/packages
    displayName: Pack

  - task: AzureCLI@2
    displayName: Push to Sleet
    inputs:
      azureSubscription: my-azure-service-connection
      scriptType: bash
      scriptLocation: inlineScript
      inlineScript: |
        dotnet tool install -g sleet --version "7.*"
        export PATH="$PATH:$HOME/.dotnet/tools"
        export SLEET_FEED_TYPE=azure
        export SLEET_FEED_CONTAINER=feed
        export SLEET_FEED_PATH=https://myaccount.blob.core.windows.net/feed/
        sleet push "$(Build.ArtifactStagingDirectory)/packages" --skip-existing --config none
```

On Linux and macOS agents, global .NET tools are installed under `$HOME/.dotnet/tools`. Add that folder to `PATH` if `sleet` is not found.

## GitLab CI or a generic shell

Use your CI variable store for the values. This example uses Azure, but the same pattern works for S3.

```yaml
publish-packages:
  image: mcr.microsoft.com/dotnet/sdk:8.0
  stage: deploy
  variables:
    SLEET_FEED_TYPE: azure
    SLEET_FEED_CONTAINER: feed
    SLEET_FEED_PATH: https://myaccount.blob.core.windows.net/feed/
  script:
    - dotnet pack --configuration Release --output ./artifacts/packages
    - dotnet tool install -g sleet --version "7.*"
    - export PATH="$PATH:$HOME/.dotnet/tools"
    - sleet push ./artifacts/packages --skip-existing --config none
```

For Entra ID outside Azure, store the [service principal variables](auth-azure.md#service-principal) as masked CI variables. If you use access keys, store them in masked CI variables and expose them as `SLEET_FEED_CONNECTIONSTRING` for Azure or `SLEET_FEED_ACCESSKEYID` and `SLEET_FEED_SECRETACCESSKEY` for S3.
