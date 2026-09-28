# Contributing to Sleet

Thanks for helping improve Sleet! Bug reports, fixes, features, and documentation changes are all welcome.

## Report a problem

Search the [existing issues](https://github.com/emgarten/Sleet/issues) first. When you open a new issue, include:

- The output of `sleet --version`.
- The storage type: Azure, Amazon S3, an S3-compatible provider, or a local folder.
- The command you ran, and its output with `--verbosity diagnostic`.

Remove connection strings, access keys, and other secrets from the settings and the output before you post them. See [troubleshooting](doc/troubleshooting.md) for common problems.

## Propose a change

For a large change or a new feature, open an issue first to discuss it. Small fixes can go straight to a pull request.

1. Fork the repository and create a branch.
1. Make the change, and add or update tests.
1. Add a short, user-focused entry to [ReleaseNotes.md](ReleaseNotes.md) for changes that users will notice. Internal changes don't need an entry.
1. Update the documentation in [/doc](doc) if the change affects how Sleet is used.
1. Run the build and tests, then open a pull request.

## Build and test

The build scripts install the .NET SDK from [global.json](global.json) and the runtimes the tests need into `.dotnet` if they aren't already installed. Then they build, pack, and run all tests.

On Windows:

```powershell
./build.ps1
```

On Linux and macOS:

```bash
./build.sh
```

The packages are written to `artifacts/nupkgs`, and the test results to `artifacts/TestResults`.

### Functional tests

Tests that use Azure Storage or Amazon S3 are skipped unless you set their environment variables:

| Variable | Tests |
| --- | --- |
| `SLEET_TEST_ACCOUNT` | Azure Storage tests. Set it to a storage account connection string, or to `UseDevelopmentStorage=true` to use [Azurite](https://learn.microsoft.com/azure/storage/common/storage-use-azurite). |
| `AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY`, `AWS_DEFAULT_REGION` | Amazon S3 tests. The region defaults to `us-east-1`. |

The build scripts can set them for you:

```powershell
# Azure tests against Azurite, which must be running
./build.ps1 -UseDevStorage

# Azure and Amazon S3 tests against real accounts
./build.ps1 -StorageTestAccount "<connection string>" -AWSAccessKeyId "<key id>" -AWSSecretAccessKey "<secret>" -AWSDefaultRegion us-east-1
```

```bash
./build.sh --azure-conn "<connection string>" --aws-key "<key id>" --aws-secret "<secret>" --aws-region us-east-1
```

> [!WARNING]
> The functional tests create and delete containers and buckets named `sleet-test-{guid}`. Use a test account, not one that holds production feeds.

### Project conventions

- Package versions are managed centrally in [Directory.Packages.props](Directory.Packages.props). Don't add versions to `PackageReference` items.
- Shared build settings are in [build/common.props](build/common.props) and [build/test.props](build/test.props).
- Tests use xUnit v3 on Microsoft.Testing.Platform with AwesomeAssertions. Pass `TestContext.Current.CancellationToken` to async APIs called from tests.
- Follow the coding style of the surrounding code and the [.editorconfig](.editorconfig) settings.

## Documentation

The documentation is the Markdown files in [/doc](doc). It's published to [emgarten.github.io/Sleet](https://emgarten.github.io/Sleet/) with [Material for MkDocs](https://squidfunk.github.io/mkdocs-material/) when changes are merged to `main`. The pages also render on GitHub, so both must work.

### Preview the site

Install the site tools into a Python virtual environment, then start the preview server from the repository root:

```bash
python -m venv .venv
source .venv/bin/activate
pip install -r doc/requirements.txt
mkdocs serve
```

In PowerShell on Windows, activate the environment with `.venv\Scripts\Activate.ps1` instead.

Open `http://127.0.0.1:8000/Sleet/` to see the site. It reloads when you save a file.

Before you open a pull request, run the same check as CI. It fails on broken links and missing anchors:

```bash
mkdocs build --strict
```

### Add a page

Put new pages in `/doc`, with no sub folders, and add them to `nav` in [mkdocs.yml](mkdocs.yml). Link to the page from related pages so readers can find it.

### Writing style

- Start each page with one `#` heading and a short introduction that says what the page covers.
- Write headings in sentence case. Keep them unique on the page, and don't use backticks, `/`, `&`, or `+` in them, so the anchors are the same on GitHub and on the site.
- Use relative links to `.md` files, such as `[feed locking](locking.md#amazon-s3-locks)`. Link to files outside `/doc`, such as `ReleaseNotes.md`, with a full GitHub URL.
- Use only the `> [!NOTE]`, `> [!TIP]`, and `> [!WARNING]` callouts.
- Give every code block a language, such as `bash`, `powershell`, `json`, `ini` for `.netconfig`, or `text` for command output.
- Write commands as `sleet`, not `sleet.exe`.
- Use the example names from the other pages: the Azure storage account `myaccount` with the container `feed`, and the S3 bucket `my-bucket-feed` in `us-west-2`.
- Check commands and settings against the code or a real run before you document them.

## License

By contributing, you agree that your contributions are licensed under the [MIT License](LICENSE.md).
