# Release Notes

## 8.0.0
* Added a package gallery written to `index.html` at the root of the feed
  * Browse and filter packages, then drill down to a package's versions and a single version's details, dependencies, and readme
  * Readmes render as markdown, including GitHub tables, task lists, and alerts. HTML and script links in a readme are not rendered
  * Readme images from sites other than the feed and the badge hosts that nuget.org allows load only after selecting **Show images**
  * The page only runs its own scripts. CDN features that rewrite scripts, such as Cloudflare Rocket Loader, must be off for the feed
  * Every view is linkable, for example `index.html#package/Newtonsoft.Json/13.0.3`
  * Links to the gallery show a title and description in apps that preview links, such as Slack, Discord, and Teams
  * Dark mode by default, with a light mode toggle in the header
  * Visual Studio links packages to their gallery page on feeds served over https
  * Azure and S3 feeds store `index.html` gzipped, the same as JSON files. S3 feeds follow the `compress` setting
  * Run `sleet recreate` to add the gallery to an existing feed
  * Sleet does not overwrite an existing `index.html`, so a custom page can be used instead
  * Disable with `sleet recreate --set galleryenabled:false`, or create a new feed without it with `sleet init --set galleryenabled:false`. See [gallery docs](doc/gallery.md)
* Added `--set key:value` to `sleet init` and `sleet recreate` to choose [feed settings](doc/feed-settings.md) when a feed is created or rebuilt. Can be repeated, and checks keys and values before changing the feed
* A `badgesenabled` feed setting value other than `true` or `false` now leaves badges off instead of turning them on
* Removed net9.0 support. .NET 8 and 9 are out of support; the net8.0 build is kept for compatibility and also runs on .NET 9.
* Removed the symbol server. Embed PDBs in your packages instead. See [symbol server](doc/symbol-server.md)
  * Removed `sleet init --with-symbols` and the `symbolsfeedenabled` feed setting
  * `sleet push` skips `.symbols.nupkg` and `.snupkg` files with a warning
  * Feeds that use the symbol server keep working and show a warning. Files under `symbols/` are no longer updated
  * `sleet recreate` removes the symbol server from a feed. If the feed has `.symbols.nupkg` files, back them up with `sleet download` and run `sleet recreate --force`
  * `sleet download` still downloads `.symbols.nupkg` files
  * Removed the symbols APIs from SleetLib. `InitCommand.RunAsync` no longer takes `enableSymbols`
* Updated System.Security.Cryptography.Pkcs to 11.0.0 to address advisories GHSA-2fmv-rv2c-59x7 and GHSA-846f-f764-76mx
* Fixed `sleet` failing to start when installed with `dotnet tool install --allow-roll-forward` [Issue](https://github.com/emgarten/Sleet/issues/247)
* Added a `provider` setting for `s3` feeds hosted on S3 compatible storage
  * `aws` (default), `r2` for Cloudflare R2, and `self-hosted` for S3 servers you run yourself, such as MinIO and RustFS
  * `sleet create-config --provider <name>` creates a config template for the service
  * Buckets that Sleet creates with `r2` or `self-hosted` use the service's default location, instead of a region that the AWS SDK guesses from the bucket name or AWS settings
  * Added a [Cloudflare R2 setup guide](doc/feed-type-cloudflare.md)
* Added `forcePathStyle` and `checksumMode` settings for S3 compatible storage
* `region` can now be used with `serviceURL` to set the signing region
* `path` is no longer required when using `serviceURL` [Issue](https://github.com/emgarten/Sleet/issues/253)
* Credentials from the default AWS credential chain can now be used with `serviceURL`
* Fixed `sleet init` failing when the S3 bucket doesn't exist
* S3 feeds check that the bucket exists with `HeadBucket` instead of `GetBucketAcl`, which isn't supported by all S3 compatible services
* Creating a bucket on S3 compatible storage skips the Amazon S3 public access block and object ownership settings if the service does not support them
* Fixed the file path shown in verbose S3 log messages
* Renamed `createconfig` to `create-config` so all commands use dashes
  * `createconfig` still works but is no longer shown in help
* Renamed the `download` option `--output-path` to `--output`
  * `--output-path` still works but is no longer shown in help
* Added `-o` as a short form of `create-config --output`
* Added `-?` as an alias for `--help`

## 7.2.0
* Added `--verbosity <level>` (`-V`) option to control console output
* Adjusted minimal verbosity to show the main actions and final result
* Update NuGet.* packages to 7.6.0 to address advisory GHSA-g4vj-cjjj-v7hg

## 7.1.1
* Update NuGet.* packages to 7.3.0

## 7.1.0
* Added `immutableCacheControl` and `mutableCacheControl` configuration options for Azure Storage and Amazon S3 feeds
  * `immutableCacheControl` - Controls caching for versioned immutable files (.nupkg, .nuspec, .xml, .dll, .pdb, icons, readmes). Default: `no-store`
  * `mutableCacheControl` - Controls caching for mutable feed metadata (.json, .svg). Default: `no-store`
  * Both settings support standard Cache-Control header values (e.g., `public, max-age=31536000, immutable`)

## 7.0.0
* Add net10.0 support, remove netstandard2.0, net6.0 support
* Update NuGet.* packages to 7.0.1

## 6.5.13
* Update Azure.Identity to 1.15.0

## 6.5.10
* Fixed json parsing error on push for S3 [PR](https://github.com/emgarten/Sleet/pull/221)
* Updated McMaster.CommandLineUtils to the 4.1.1 and made several improvements to command line error handling
* Misc bug fixes

## 6.5.0
* Added README support. Flatcontainer will now place the README alongside the icon and nuspec files. Visual Studio package search will display README files the same as it does for nuget.org.

## 6.4.0
* Added disablePayloadSigning option for S3 feeds [PR](https://github.com/emgarten/Sleet/pull/211)

## 6.3.0
* Added net9.0 support
* Updated NuGet.* packages to 6.12.1

## 6.2.0
* S3 support for setting an ACL on upload. Feeds using scaleway object storage can now make files public during push.

## 6.1.0
* Updated AWS SDK packages to improve support for non AWS S3 storage
* Updated Azure Storage and Azure Identity packages to fix vulnerability

## 6.0.2
* Fixed a bug where Azure feeds failed when path was not set to the full container URI [Issue](https://github.com/emgarten/Sleet/issues/197)

## 6.0.0
* Moved from Microsoft.Azure.Storage.Blob to Azure.Storage.Blobs [PR](https://github.com/emgarten/Sleet/pull/191)
* Added support for Managed Identity and DefaultAzureCredential with Azure storage accounts [PR](https://github.com/emgarten/Sleet/pull/195)

## 5.2.12
* Update NuGet.* packages to 6.9.1 to address CVE-2024-0057 [Issue](https://github.com/emgarten/Sleet/issues/189)

## 5.2.11
* AWS S3 feeds with sub paths are now fully removed during destroy.

## 5.2.6
* Updated all AWS SDKs to the latest

## 5.2.5
* Added net8.0 support, removed net7.0
* Updated NuGet.* packages to 6.8.0
* Updated AWS SDK
* Fixed AWS S3 bucket public policy for new bucket creation [PR](https://github.com/emgarten/Sleet/pull/181)

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
