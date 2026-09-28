# Multiple feeds

A `sleet.json` file can hold several sources, and one Azure container or S3 bucket can hold several feeds in sub folders. Use separate feeds to split stable and nightly packages, or to give teams their own feed.

## Several sources in one file

Give each source a unique name:

```json
{
  "sources": [
    {
      "name": "release",
      "type": "azure",
      "container": "release",
      "path": "https://myaccount.blob.core.windows.net/release/"
    },
    {
      "name": "nightly",
      "type": "azure",
      "container": "nightly",
      "path": "https://myaccount.blob.core.windows.net/nightly/"
    }
  ]
}
```

When the file has more than one source, pass `--source` to every command. Source names aren't case-sensitive.

```bash
sleet push ./nupkgs --source nightly
```

Without `--source`, Sleet fails with `The local settings file contains multiple sources. Use --source to specify the feed to use.`

[Environment variables](environment-variables.md) can only define one feed. To use several feeds in CI, check in a `sleet.json` file and use [tokens](client-settings.md#tokens) for the secrets.

## Several feeds in one Azure container

Set `path` to a sub folder of the container. Sleet stores the feed files under that folder:

```json
{
  "sources": [
    {
      "name": "team-a",
      "type": "azure",
      "container": "feed",
      "path": "https://myaccount.blob.core.windows.net/feed/team-a/"
    },
    {
      "name": "team-b",
      "type": "azure",
      "container": "feed",
      "path": "https://myaccount.blob.core.windows.net/feed/team-b/"
    }
  ]
}
```

The package source for the first feed is `https://myaccount.blob.core.windows.net/feed/team-a/index.json`.

Azure also accepts a `feedSubPath` property, but a sub folder in `path` takes precedence over it.

## Several feeds in one S3 bucket

Set `feedSubPath`, and set `path` to the bucket URL plus the same sub folder:

```json
{
  "sources": [
    {
      "name": "team-a",
      "type": "s3",
      "bucketName": "my-bucket-feed",
      "region": "us-west-2",
      "path": "https://my-bucket-feed.s3.us-west-2.amazonaws.com/team-a/",
      "feedSubPath": "team-a"
    }
  ]
}
```

Always set both. Without `feedSubPath`, Sleet writes the files to the root of the bucket, even if `path` has a sub folder.

## Sub folders and baseURI

When a feed uses a sub folder, the feed URL must end with it. If you set `baseURI`, for example for a CDN, include the same sub folder:

```json
"baseURI": "https://packages.example.com/team-a/"
```

If the URL doesn't end with the sub folder, Sleet fails with `When using FeedSubPath the Path property must end with the sub path.`

## Several local feeds

Local sources ignore `feedSubPath`. Give each feed its own folder, and keep the folders side by side, not inside each other. Commands such as `destroy` delete everything in a feed folder, including other feeds inside it.

## What feeds in the same container or bucket share

- **Deletes**: a feed at the root of a container or bucket owns every file in it. Running [destroy](commands.md#destroy) or [recreate](commands.md#recreate) on a root feed deletes the feeds in sub folders too. Don't put a root feed and sub folder feeds in the same container or bucket.
- **Locks**: Azure locks each feed on its own. Amazon S3 uses one lock file at the root of the bucket, so a push to one feed waits for a push to another feed in the same bucket. See [feed locking](locking.md#amazon-s3-locks).
- **Access**: Azure anonymous access applies to the whole container, so feeds that need different access need different containers. On S3, a bucket policy can grant access to each sub folder separately.
