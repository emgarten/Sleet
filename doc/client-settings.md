# Configuration

Sleet reads feed settings from a `sleet.json` file, a `.netconfig` file, or [environment variables](environment-variables.md). This page lists every setting and explains how Sleet finds them.

## sleet.json

Create a starter file in the current folder with `createconfig`:

```bash
sleet createconfig --azure
```

Use `--s3`, `--local`, or `--none` for other templates. `createconfig` fails if `sleet.json` already exists.

A `sleet.json` file has a list of sources, plus optional global and proxy settings:

```json
{
  "config": {
    "feedLockTimeoutMinutes": 10
  },
  "proxy": {
    "useDefaultCredentials": false
  },
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

Files made by `createconfig` also have `username` and `useremail` properties. Sleet doesn't use them, so you can remove them.

Property names aren't case-sensitive.

## .netconfig

Sleet can also read settings from a [.netconfig](https://github.com/dotnetconfig/dotnet-config) file. It uses the same format as git config files, and several .NET tools can share one file. Each `[sleet "name"]` section is a source, and the name in quotes is the source name:

```ini
[sleet]
    feedLockTimeoutMinutes = 10

[sleet "feed"]
    type = azure
    container = feed
    path = https://myaccount.blob.core.windows.net/feed/
```

The `[sleet]` section holds the [global settings](#global-settings) and the [proxy setting](#proxy-settings). The source properties are the same as in `sleet.json`.

The format has a few rules to watch for:

- `;` and `#` start a comment. Put values that contain them, such as connection strings, in double quotes.
- `\` starts an escape sequence. Write Windows paths with `/` or `\\`, for example `C:/feeds/main` or `C:\\feeds\\main`.
- A property with no value means `true`. Otherwise, write `true` or `false`.
- Use an absolute `path` for local feeds. A relative path starts from the current folder, not from the folder that holds `.netconfig`.

### Where Sleet looks for .netconfig

Sleet reads every `.netconfig` file it finds, in this order:

1. `.netconfig.user` and `.netconfig` in the current folder, then in each parent folder.
2. `.netconfig` in your user profile folder.
3. `.netconfig` in the system folder.

This lets you define a feed once in your user profile and use it from any folder.

You can't set the same property of the same source in two files. If you do, Sleet fails with `Can not add property ... Property with the same name already exists on object.` You can split one source across files, though. For example, keep the source in `.netconfig` in source control, and put its secret in `.netconfig.user`, which most `.gitignore` files already exclude:

```ini
# .netconfig
[sleet "feed"]
    type = azure
    container = feed
```

```ini
# .netconfig.user
[sleet "feed"]
    connectionString = "DefaultEndpointsProtocol=https;AccountName=myaccount;AccountKey=...;EndpointSuffix=core.windows.net"
```

## Common source properties

Every source has these properties:

| Property | Description |
| --- | --- |
| `name` | Required. The name you pass to `--source`. In `.netconfig`, the name comes from the section header. |
| `type` | Required. `azure`, `s3`, or `local`. |
| `path` | Where Sleet writes the feed. For Azure and S3, this is the URL of the container, bucket, or sub folder. For local feeds, it's a folder path. |
| `baseURI` | Optional. The URL that Sleet writes into the feed files, and that NuGet uses to read them. Set it when clients read the feed from a different URL than `path`, such as a CDN, proxy, or web server. Defaults to `path`. |
| `feedSubPath` | Optional. Azure and S3 only. Puts the feed in a sub folder. See [multiple feeds](multiple-feeds.md). |

Set `baseURI` before the first push. Sleet writes it into every feed file, and changing it later needs a rebuild. See [change baseURI or path in place](backup-migration.md#change-baseuri-or-path-in-place).

## Azure properties

| Property | Description |
| --- | --- |
| `container` | Required. The blob container name. |
| `path` | The container URL, for example `https://myaccount.blob.core.windows.net/feed/`. It must start with the container URL. Add a folder to the end to put the feed in a sub folder. When you set `path` without `connectionString`, Sleet signs in with Microsoft Entra ID. |
| `connectionString` | A storage account connection string. Sleet uses it instead of Entra ID. Without `path`, Sleet uses the container URL from the connection string. |
| `feedSubPath` | A sub folder for the feed. A sub folder in `path` takes precedence. |
| `immutableCacheControl` | See [cache-control properties](#cache-control-properties). |
| `mutableCacheControl` | See [cache-control properties](#cache-control-properties). |

Set `path`, `connectionString`, or both. See [Azure authentication](auth-azure.md) for the sign-in options.

```json
{
  "name": "feed",
  "type": "azure",
  "container": "feed",
  "path": "https://myaccount.blob.core.windows.net/feed/"
}
```

## Amazon S3 properties

| Property | Description |
| --- | --- |
| `bucketName` | Required. The bucket name. |
| `region` | The AWS Region of the bucket, such as `us-west-2`. Set `region` or `serviceURL`, but not both. |
| `serviceURL` | The S3 API endpoint, for S3-compatible storage. Set `region` or `serviceURL`, but not both. See [S3-compatible storage](s3-compatible.md). |
| `path` | The bucket URL that clients use, such as `https://my-bucket-feed.s3.us-west-2.amazonaws.com/`. Always set it. Without it, Sleet builds a path-style URL from `region`. |
| `feedSubPath` | A sub folder for the feed. When you set it, `path` must end with the same folder. See [multiple feeds](multiple-feeds.md). |
| `profileName` | A profile in your AWS credentials or config file. |
| `accessKeyId` | An access key ID. Use it with `secretAccessKey`. |
| `secretAccessKey` | The secret for `accessKeyId`. |
| `serverSideEncryptionMethod` | `None` or `AES256`. The default is `None`, which uses the bucket's default encryption. |
| `compress` | `true` or `false`. When `true`, Sleet gzips JSON files. The default is `true`. See [compression](how-it-works.md#compression). |
| `acl` | A canned ACL, such as `public-read`, for each uploaded file. By default, Sleet doesn't set one. When Sleet creates the bucket, it also sets this ACL on the bucket. Buckets with ACLs turned off, which is the default for buckets you create yourself, reject uploads that set an ACL. |
| `disablePayloadSigning` | `true` or `false`. Set it to `true` for S3-compatible storage that doesn't support payload signing. The default is `false`. |
| `immutableCacheControl` | See [cache-control properties](#cache-control-properties). |
| `mutableCacheControl` | See [cache-control properties](#cache-control-properties). |

If you don't set `profileName` or the access keys, Sleet uses the AWS environment variables, then the container credentials, then the default AWS credential chain. See [AWS authentication](auth-aws.md).

```json
{
  "name": "feed",
  "type": "s3",
  "bucketName": "my-bucket-feed",
  "region": "us-west-2",
  "path": "https://my-bucket-feed.s3.us-west-2.amazonaws.com/",
  "profileName": "sleetProfile"
}
```

## Local folder properties

| Property | Description |
| --- | --- |
| `path` | Required. The feed folder. A relative path starts from the folder that holds `sleet.json`. |
| `baseURI` | The URL of the web server that serves the folder. Without it, the feed only works as a file path. |

Local feeds don't support `feedSubPath` or the cache-control properties.

```json
{
  "name": "feed",
  "type": "local",
  "path": "C:\\feeds\\main",
  "baseURI": "https://packages.example.com/"
}
```

You can't use a relative `path` with [environment variables](environment-variables.md). Sleet fails with `Cannot use a relative 'path' without a sleet.json file.`

## Cache-control properties

Azure and S3 sources can set the `Cache-Control` header that Sleet uses for each file it uploads:

| Property | Description |
| --- | --- |
| `immutableCacheControl` | For files that don't change after they're pushed, such as `.nupkg` and `.nuspec` files. The default is `no-store`. |
| `mutableCacheControl` | For files that change, such as `index.json` and the package lists. The default is `no-store`. |

Sleet only sets the header when it uploads a file. See [CDN and caching](cdn-caching.md) for the files in each group and the values to use.

## Tokens

Any string value in `sleet.json` or `.netconfig` can use `$NAME$` tokens. Sleet replaces each token with a [command line property](environment-variables.md#command-line-properties) passed with `-p NAME=value`, or else with the environment variable `NAME`. Use tokens to keep secrets out of the file:

```json
{
  "sources": [
    {
      "name": "feed",
      "type": "azure",
      "container": "feed",
      "connectionString": "$SLEET_CONNECTION_STRING$"
    }
  ]
}
```

```bash
sleet push ./nupkgs -p "SLEET_CONNECTION_STRING=DefaultEndpointsProtocol=https;AccountName=myaccount;AccountKey=...;EndpointSuffix=core.windows.net"
```

How tokens work:

- A value can hold several tokens, mixed with text, like `https://$ACCOUNT$.blob.core.windows.net/feed/`.
- If a token's value has tokens of its own, Sleet replaces those too.
- If Sleet can't find a value, or the environment variable is empty, the token stays as it is.
- To write a `$` sign, use `$$`.

## Global settings

These settings apply to every source in the file:

| sleet.json | .netconfig | Description |
| --- | --- | --- |
| `config.feedLockTimeoutMinutes` | `feedLockTimeoutMinutes` in `[sleet]` | How long Sleet waits for another client to release the [feed lock](locking.md), in whole minutes. By default, Sleet waits forever. |

You can't set `feedLockTimeoutMinutes` with environment variables.

## Proxy settings

If your network uses a proxy that needs your Windows sign-in, turn on `useDefaultCredentials`:

```json
{
  "proxy": {
    "useDefaultCredentials": true
  },
  "sources": []
}
```

In `.netconfig`, set `proxy-useDefaultCredentials = true` in the `[sleet]` section. With environment variables, set `SLEET_FEED_PROXY_USEDEFAULTCREDENTIALS=true`.

Sleet uses the system proxy settings. This setting only makes it send your default credentials to the proxy.

## How Sleet finds settings

Sleet uses the first settings it finds, in this order:

1. The file passed with `--config`. It can be a JSON file with any name, or a `.netconfig` file.
2. A `sleet.json` file in the current folder or one of its parent folders.
3. `.netconfig` files, if any of them has a `sleet` section. See [where Sleet looks for .netconfig](#where-sleet-looks-for-netconfig).
4. [Environment variables](environment-variables.md), if `SLEET_FEED_TYPE` is set as an environment variable or a `-p` property.

Sleet doesn't combine these. When it finds a file, it ignores the `SLEET_FEED_*` variables. Environment variables and `-p` properties still fill in [tokens](#tokens).

A `sleet.json` file in a parent folder takes precedence over a `.netconfig` file in the current folder.

To skip all files and use only environment variables, pass `--config none`.

## Choose a source

If there's only one source, Sleet uses it. If there are several, pass the source name with `--source`:

```bash
sleet push ./nupkgs --source feed
```

Source names aren't case-sensitive. These errors mean Sleet couldn't pick a source:

| Error | Fix |
| --- | --- |
| `The local settings file contains multiple sources. Use --source to specify the feed to use.` | Pass `--source`. |
| `Unable to find source. Verify that the --source parameter is correct and that sleet.json contains the named source.` | Check the name, and check which file Sleet loaded. |
| `Unable to find source settings. Specify the path to a sleet.json settings file.` | Sleet found no file and no `SLEET_FEED_TYPE` variable. |
| `Unable to find source settings. File not found '...'.` | Fix the `--config` path. |
