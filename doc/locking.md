# Feed locking

Sleet takes a lock on the feed before a command reads or changes it, so that two clients never write to the same feed at once. If another client has the lock, Sleet waits until it's free. This page explains how the lock works on each storage type, and how to remove a lock that was left behind.

## How the lock works

Every command that works with a feed takes the lock, except `init`, and `download` with `--no-lock`. NuGet clients don't use the lock. They can read the feed at any time, even during a push. See [what a push does](how-it-works.md#what-a-push-does).

When a command starts, Sleet writes a short message into the lock: `Push of {id} {version}` for a push, or the command name, such as `Delete` or `Recreate`. Another client that finds the lock prints the message. For a local feed, it looks like this:

```text
Waiting to obtain feed lock. Feed is locked by: Push of My.Package 1.0.0 since: 2025-01-15T18:04:12.3456789Z Delete .lock to forcibly unlock the feed.
```

Sleet tries again every 30 seconds, or every 200 milliseconds for a local feed, and prints the message again every 5 minutes. If the lock holder uses a Sleet version that doesn't write a message, the output says `Client holding the lock did not provide a message, it may be using an older version of Sleet.`

## Wait time

By default, Sleet waits for the lock forever. To stop after a while, set `feedLockTimeoutMinutes` in the [global settings](client-settings.md#global-settings):

```json
{
  "config": {
    "feedLockTimeoutMinutes": 10
  },
  "sources": []
}
```

When the time runs out, Sleet fails with `Unable to obtain a lock on the feed. Try again later.` With a value of `0`, Sleet tries once and fails right away if the feed is locked.

In CI, it's better to run one publish job at a time than to have jobs wait on each other. See [concurrency](ci-server.md#concurrency).

## Azure locks

On Azure, Sleet takes a lease on a blob named `feedlock` in the feed folder. The lease lasts 60 seconds, and Sleet renews it every 15 seconds while the command runs. The lock message goes in a blob named `feedlock-message`.

When the command ends, Sleet releases the lease and deletes `feedlock-message`. The `feedlock` blob stays in the container, and that's expected.

If Sleet stops without releasing the lease, the lease runs out within a minute, and the next command gets the lock. You never need to remove an Azure lock by hand.

If Sleet can't renew the lease, for example after a network problem, it warns `Failed to renew lock on feed. If another client takes the lock conflicts could occur.`

Each feed in a sub folder of a container has its own lock.

## Amazon S3 locks

On Amazon S3, the lock is an object named `.feedlock` at the root of the bucket. It holds the lock message. Sleet deletes it when the command ends.

Things to know about S3 locks:

- There's one lock per bucket. Feeds in sub folders of the same bucket share it, so a push to one of them waits for a push to another.
- The lock doesn't expire. If Sleet stops before it deletes `.feedlock`, for example because the CI job was canceled, the next commands wait forever. See [remove a stuck lock](#remove-a-stuck-lock).
- Sleet needs `s3:ListBucket`, `s3:GetObject`, `s3:PutObject`, and `s3:DeleteObject` for the lock. Without `s3:DeleteObject`, Sleet warns `Unable to clean up lock` at the end of a command, and the lock stays.
- Sleet checks that `.feedlock` doesn't exist, then writes it. Two clients that start at the same moment could both get the lock. Run one publish job at a time to avoid this.

Some [S3-compatible](s3-compatible.md) providers behave differently from Amazon S3. Test the lock with two clients before you rely on it.

## Local folder locks

For a local feed, the lock is a file named `.lock` in the feed folder. Sleet creates it only if it doesn't exist, so two clients can't both get it. The file holds the lock message, the date, and the process id. Sleet deletes it when the command ends.

If Sleet stops before it deletes `.lock`, the next commands wait forever.

## Remove a stuck lock

Only remove a lock when you're sure no Sleet command is running against the feed. Check the message and the date that Sleet prints, and check your CI jobs. Removing the lock of a running command can break the feed.

When Sleet is waiting, the message ends with the steps for your storage type:

- **Local folder**: delete `.lock` from the feed folder.
- **Amazon S3**: delete `.feedlock` from the bucket root:

  ```bash
  aws s3 rm s3://my-bucket-feed/.feedlock
  ```

- **Azure**: wait a minute for the lease to run out. If Sleet still waits, another client is running.

After a command was stopped partway, run [validate](commands.md#validate) to check the feed.
