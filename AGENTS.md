## Build and test

### Windows

Run `build.ps1` to build and run all unit test and validations.

### Linux

Run `build.sh` to build and run all unit tests and validations.

### Functional tests

The build scripts don't run the Azure and Amazon S3 functional tests. When you change the Azure or Amazon S3 code, also run `functional-tests.ps1` on Windows or `functional-tests.sh` on Linux. They need Docker to run the tests against the local emulators in `local-env`.

## Rules

All builds and tests must pass successfully before stopping. All errors or test failures must be fixed.

## Project setup

* Package versions are managed centrally in `Directory.Packages.props`, do not add versions to `PackageReference` items.
* Shared build settings are in `build/common.props` and `build/test.props`, projects import one of them before `Sdk.props`.
* Tests use xUnit v3 on Microsoft.Testing.Platform with AwesomeAssertions. Pass `TestContext.Current.CancellationToken` to async APIs called from tests.

## Style

Follow existing patterns in the repository for both structure, coding style, and tests.

## Release notes

Update ReleaseNotes.md to provide a concise summary of any functionality changes. These notes should be aimed at users. Internal fixes do not need to be noted.