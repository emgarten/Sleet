## Build and test

### Windows

Run `build.ps1` to build and run all unit test and validations.

### Linux

Run `build.sh` to build and run all unit tests and validations.

### Gallery UX tests

The browser tests for the package gallery (`src/SleetLib/compiler/resources/templateGallery.html`) are not run by the build scripts. When changing the gallery or the feed files it reads, also run `ux-tests.ps1` on Windows or `ux-tests.sh` on Linux and macOS. These need Google Chrome or Microsoft Edge.

## Rules

All builds and tests must pass successfully before stopping. All errors or test failures must be fixed.

## Style

Follow existing patterns in the repository for both structure, coding style, and tests.

## Release notes

Update ReleaseNotes.md to provide a concise summary of any functionality changes. These notes should be aimed at users. Internal fixes do not need to be noted.