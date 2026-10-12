# Environment variables

Sleet can read a feed's settings from environment variables instead of a file. This is handy in CI, where secrets are already stored as variables. This page also covers `-p` command line properties and the `SLEET_DEBUG` variable.

## Feed variables

Set `SLEET_FEED_TYPE` to `azure`, `s3`, or `local`. Each other `SLEET_FEED_` variable sets the source property with the same name. Names aren't case-sensitive, so `SLEET_FEED_BUCKETNAME` sets `bucketName`. For the list of properties, see [configuration](client-settings.md#common-source-properties).

An Azure feed that signs in with Microsoft Entra ID:

| Variable | Value |
| --- | --- |
| `SLEET_FEED_TYPE` | `azure` |
| `SLEET_FEED_CONTAINER` | `feed` |
| `SLEET_FEED_PATH` | `https://myaccount.blob.core.windows.net/feed/` |

An Amazon S3 feed that uses the AWS credentials from the environment:

| Variable | Value |
| --- | --- |
| `SLEET_FEED_TYPE` | `s3` |
| `SLEET_FEED_BUCKETNAME` | `my-bucket-feed` |
| `SLEET_FEED_REGION` | `us-west-2` |
| `SLEET_FEED_PATH` | `https://my-bucket-feed.s3.us-west-2.amazonaws.com/` |

A local feed needs an absolute `SLEET_FEED_PATH`.

Then run Sleet with `--config none`:

```bash
export SLEET_FEED_TYPE=azure
export SLEET_FEED_CONTAINER=feed
export SLEET_FEED_PATH=https://myaccount.blob.core.windows.net/feed/
sleet push ./nupkgs --config none
```

In PowerShell:

```powershell
$env:SLEET_FEED_TYPE = "azure"
$env:SLEET_FEED_CONTAINER = "feed"
$env:SLEET_FEED_PATH = "https://myaccount.blob.core.windows.net/feed/"
sleet push ./nupkgs --config none
```

Things to know:

- Sleet only reads these variables when it finds no settings file. If a `sleet.json` file is in the current folder or a parent folder, Sleet uses it and ignores the `SLEET_FEED_` variables. Pass `--config none` to skip the files.
- The variables define one feed, so you don't need `--source`. Sleet names it `envirnoment_feed`, and ignores `SLEET_FEED_NAME`.
- Set `SLEET_FEED_PROXY_USEDEFAULTCREDENTIALS=true` to turn on the [proxy setting](client-settings.md#proxy-settings).
- `feedLockTimeoutMinutes` can't be set with variables. Use a settings file for it.
- Values can use [tokens](client-settings.md#tokens), such as `$ACCOUNT_KEY$`.

## Credential variables

Storage credentials can be feed variables too, such as `SLEET_FEED_CONNECTIONSTRING` or `SLEET_FEED_SECRETACCESSKEY`. It's usually better to leave them out, and let the Azure and AWS libraries read their own variables. Those work with or without a settings file:

- Azure: `AZURE_TENANT_ID`, `AZURE_CLIENT_ID`, and related variables. See [service principal](auth-azure.md#service-principal).
- AWS: `AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY`, and `AWS_SESSION_TOKEN`. See [AWS environment variables](auth-aws.md#aws-environment-variables).

## Command line properties

Pass `-p` or `--property` with a `key=value` pair. You can pass it more than once. A property works like an environment variable with the same name, and takes precedence over it:

- `-p SLEET_FEED_...` sets a feed variable.
- `-p NAME=value` fills in `$NAME$` [tokens](client-settings.md#tokens) in a settings file.

This command creates a local feed without a settings file:

```bash
sleet init --config none -p SLEET_FEED_TYPE=local -p SLEET_FEED_PATH=/srv/feeds/main
```

Property names aren't case-sensitive. If you pass the same name twice, Sleet uses the first value.

> [!WARNING]
> Values on the command line can show up in shell history, process lists, and CI logs. Pass secrets with environment variables from your secret store instead.

## SLEET_DEBUG

Set `SLEET_DEBUG` to `1` to turn on debug output for every command. It overrides `--verbosity`, and adds error details such as stack traces.

```bash
SLEET_DEBUG=1 sleet push ./nupkgs
```

See [debug output](troubleshooting.md#debug-output).
