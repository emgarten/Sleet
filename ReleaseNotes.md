# Release Notes

## 5.1.0
* Added net7.0 support
* Update AWS SDK
* Added AWS SSO profile support

## 5.0.6
* Updated NuGet.* packages to 6.2.1
* Fixed deleted AWS bucket handling [PR](https://github.com/emgarten/Sleet/pull/161)

## 5.0.0
* Added net6.0 for LTS support, removed net5.0
* Changed azure container default name to lowercase [PR](https://github.com/emgarten/Sleet/pull/156)

## 4.1.0
* Sleet.exe is now produced by dotnet publish as a standalone file instead of by ILMerge
* Removed net472 support from SleetLib

## 4.0.0
* Added net5.0 support
* Dropped netcoreapp2.1 and netcoreapp3.1 support
* Badges are now enabled by default
* SVG badges have been removed in favor of using shields.io via json files from the feed
* Added prune by release labels option to package retention
* Added package icon support. Icons will be added to flatcontainer
* Removed iconUrl support
* Added external search support and feed setting

## 3.2.1
* Added badge json for shields.io support [PR](https://github.com/emgarten/Sleet/pull/133)
* Removed gzip compression for badges

## 3.2.0
* dotnet config support [PR](https://github.com/emgarten/Sleet/pull/128)

## 3.1.26
* Version badge svg support
* DefaultWebProxy support for authenticated proxies
* Updated S3 SDK
* S3 error handling and encryption fixes

## 3.1.0
* Package retention commands have been added to support pruning feed packages by version. [PR](https://github.com/emgarten/Sleet/pull/110)
* Fixed bug in specifying S3 feed type through env vars [PR](https://github.com/emgarten/Sleet/pull/108)
* S3 compatible storage support [PR](https://github.com/emgarten/Sleet/pull/99)

## 3.0.24
* netcoreapp3.0 -> netcoreapp3.1
* PDBs are now embedded in the dlls
* Updated package dependencies on NuGet
* Updated nuspec properties

## 3.0.19
* Improve feed lock error logging

## 3.0.14
* netcoreapp3.0 tool support

## 3.0.8
* Support for AWS environment variables and docker environments

## 3.0.0
* Moved Sleet.exe from the package 'Sleet' to 'SleetExe' *breaking change*
* Init command will now automatically create a public bucket/container if it does not exist already. *breaking change*
* Push command will now automatically create a public bucket/container and initialize a feed using the default settings if it does not exist already. *breaking change*
* Removed client/feed version compat checks based on the minor version of sleet.
* Added capabilities for client/feed compat checks.
* Remove netstandard1.0 *breaking change*

## 2.3.75
* Added Download command options: --no-lock --skip-existing --ignore-errors
* Skip package SHA512 hashing when the catalog is disabled. Package details blobs will no longer write this extra property.
* ISleetFile.Link support
* Nupkgs are no longer copied to the temp cache during push. This improves perf and saves disk space for large pushes.
* Reduced default log output, http get/push calls will now only be shown on verbose mode.
* Performance summary displays where time was spent during push operations.
* Push batch support. Instead of pushing possibly hundreds of thousands of packages at once push will now load nupkgs in batches and process them to avoid running out of memory.
* Files are now ordered during upload. Index files will be pushed last to help avoid conflicts on the client when the feed is still incomplete.
* Increased the delay for obtaining a file lock on azure feeds. Cleaned up file lock logging.

## 2.3.36
* Path property in sleet.json can now be a relative path for local feeds

## 2.3.35
* Performance improvements, packages are added in batch to reduce the number of file read/writes.
* Add/removes within a service are done in parallel where possible.
* nupkg files are read in parallel before locking the feed to reduce the amount of time spent in the lock. 

## 2.3.33
* Local feeds will contian baseURI by default when using createconfig.
* Local feeds will fail if path contains an http URI. baseURI should be used instead.

## 2.3.31
* Path property in sleet.json is now optional for azure and s3 feeds. If not provided it will be resolved from the container/bucket.
* Added support for tokenized sleet.json files
* Added --property support for passing in setting values
* Added SLEET_FEED_ env var support

## 2.3.0
* Added Amazon S3 support (skarllot)
* Fix for createconfig json formatting

## 2.2.0
* Fix for multiple catalog pages
* Sleet versions of the same semantic version patch will no longer require upgrading the feed to work together.

## 2.1.0
* Fix for race condition when reading symbols files
* Props path fix
* Improved exists check performance

## 2.0.0
* Improved console output progress display.
* Performance improvements, files are committed to the feed in parallel which cuts push time in half for some scenarios.
* Symbols feed support, symbols packages will be stored in the feed and dll and pdb files will be available for debuggers.
* Catalog is disabled by default, this improves perf for feeds which overwrite packages.
* Invalid command arguments now return a non-zero exit code.
* Adds package details from the catalog to the registration package details page when the catalog is disabled.
* Moved from netstandard1.3 to netstandard2.0
