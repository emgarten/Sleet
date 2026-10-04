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
1. Run the build and tests, then open a pull request. If you changed the Azure or Amazon S3 code, run the [functional tests](#functional-tests) too.

## Build and test

The build scripts install the .NET SDK from [global.json](global.json) and the runtimes the tests need into `.dotnet` if they aren't already installed. Then they build, pack, and run the unit tests.

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

The functional tests run Sleet against Azure Storage and Amazon S3. They need Docker or a cloud account, so the build scripts don't run them.

By default, they run against local emulators, so you don't need a cloud account. Start [Docker](https://docs.docker.com/get-started/get-docker/), then run:

```powershell
./functional-tests.ps1
```

```bash
./functional-tests.sh
```

The script starts the [local test environment](local-env/README.md), builds and runs the tests, and then stops the environment. The test results and the container logs are written to `artifacts/TestResults/functional`.

To run the tests against your own accounts, set their environment variables and pick a cloud target:

```powershell
$env:SLEET_TEST_ACCOUNT = "<connection string>"
$env:SLEET_TEST_S3_ACCESS_KEY_ID = "<key id>"
$env:SLEET_TEST_S3_SECRET_ACCESS_KEY = "<secret>"
$env:SLEET_TEST_R2_ACCESS_KEY_ID = "<key id>"
$env:SLEET_TEST_R2_SECRET_ACCESS_KEY = "<secret>"
$env:SLEET_TEST_R2_SERVICE_URL = "https://<account id>.r2.cloudflarestorage.com"
./functional-tests.ps1 -Target cloud
```

```bash
export SLEET_TEST_ACCOUNT="<connection string>"
export SLEET_TEST_S3_ACCESS_KEY_ID="<key id>"
export SLEET_TEST_S3_SECRET_ACCESS_KEY="<secret>"
export SLEET_TEST_R2_ACCESS_KEY_ID="<key id>"
export SLEET_TEST_R2_SECRET_ACCESS_KEY="<secret>"
export SLEET_TEST_R2_SERVICE_URL="https://<account id>.r2.cloudflarestorage.com"
./functional-tests.sh --target cloud
```

| Target | Tests | Needs |
| --- | --- | --- |
| `emulators` | All the emulator targets. This is the default. | Docker |
| `cloud` | All the cloud targets. | The variables for `azure`, `aws`, and `r2` |
| `all` | All targets. | Docker and the variables for `azure`, `aws`, and `r2` |
| `azurite` | Azure Storage tests against Azurite. | Docker |
| `rustfs` | Amazon S3 tests against RustFS. | Docker |
| `azure` | Azure Storage tests against a storage account. | `SLEET_TEST_ACCOUNT`, set to a storage account connection string |
| `aws` | Amazon S3 tests against AWS. | `SLEET_TEST_S3_ACCESS_KEY_ID` and `SLEET_TEST_S3_SECRET_ACCESS_KEY`. `SLEET_TEST_S3_REGION` defaults to `us-east-1`. Set `SLEET_TEST_S3_SERVICE_URL` to test S3-compatible storage instead of AWS. |
| `r2` | Amazon S3 tests against Cloudflare R2. The tests that need a public bucket are skipped. | `SLEET_TEST_R2_ACCESS_KEY_ID` and `SLEET_TEST_R2_SECRET_ACCESS_KEY`, from an R2 API token with the **Admin Read & Write** permission so the tests can create buckets. `SLEET_TEST_R2_SERVICE_URL`, set to the S3 API URL of the account. |

To run more than one target, list them: `-Target azurite, azure` or `--target azurite,azure`. If a variable that a target needs isn't set, the script stops before it runs any tests. The emulator targets ignore the account variables, so they always run against the local test environment.

> [!WARNING]
> The cloud targets create and delete containers and buckets named `sleet-test-{guid}`. Use a test account, not one that holds production feeds.

You can also run the functional tests from an IDE or with `dotnet test`. They're skipped unless they have somewhere to run:

- The Azure tests run against `SLEET_TEST_ACCOUNT` if it's set, otherwise against Azurite in the local test environment if it's running. Start it with `./local-env/start.ps1` or `./local-env/start.sh`, see [start and stop the environment](local-env/README.md#start-and-stop-the-environment).
- The Amazon S3 tests run against the account in `SLEET_TEST_S3_ACCESS_KEY_ID` and `SLEET_TEST_S3_SECRET_ACCESS_KEY` if they're set, otherwise against RustFS in the local test environment if it's running. They don't use the standard `AWS_*` variables, so AWS credentials in your environment don't run them against your account. With `SLEET_TEST_S3_SERVICE_URL`, they use the `self-hosted` provider, or the provider in `SLEET_TEST_S3_PROVIDER`, such as `r2`.

CI runs the same scripts. Pull requests from branches in this repository run the emulator and the cloud tests. Pull requests from forks run only the emulator tests, and a maintainer can run the cloud tests for them with the [manual workflow](.github/workflows/functional-manual.yml).

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
