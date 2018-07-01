# Sleet commands

## Help
The help parameter may be applied to any command to see a description of all parameters.

``sleet.exe --help`` 

## CreateConfig
All commands require a *sleet.json* config file to provide source settings. Before creating a new source the *createconfig* command may be used to output a *sleet.json* template file that may be filled in with your own settings.

``Usage: sleet createconfig [options]``

### Options

| Parameter | Description |
| --- | ------ |
| azure | Add a template entry for an azure storage feed. | 
| s3 | Add a template entry for an Amazon S3 storage feed. |
| local | Add a template entry for a local folder feed. |
| output | Output path. If not specified the file will be created in the working directory. |

At least one feed type must be specified.

## Init

Init is used to initialize a new feed. This is only needed once. Calling this method on an already created feed will fail.

After running this command you will have a complete feed with zero packages.

``Usage: sleet init [options]``

### Options

| Parameter | Description |
| --- | ------ |
| config | Optional path to *sleet.json* where the source information is contained. | 
| source | Source name from *sleet.json*. *Required* |
| with-catalog | Enable the feed catalog and all change history tracking. |
| with-symbols | Enable symbols server. |

## Push

Push adds packages to your feed. It can used to add individual packages or complete directories of packages.

``Usage: sleet push [nupkg or folder paths] [options]``

### Options

| Parameter | Description |
| --- | ------ |
| config | Optional path to *sleet.json* where the source information is contained. | 
| source | Source name from *sleet.json*. *Required* |
| force | Overwrite existing packages. Defaults to *false* |

### Examples

Pushing a single nupkg

``sleet push path/to/mynupkg.nupkg --source myFeed --force``

Pushing multiple directories of nupkgs

``sleet push /my/nupkgs1/ /my/nupkgs2/ --source myFeed``

## Delete

Delete removes packages from your feed. It can be used to remove a single version of a package, or all versions of a package using a given id.

``Usage: sleet delete [options]``

### Options

| Parameter | Description |
| --- | ------ |
| id | Package id to delete from the feed. |
| version | Package version to delete. If not specified all versions will be deleted. |
| reason | Reason for deleting the package(s). This will be stored in the catalog. |
| config | Optional path to *sleet.json* where the source information is contained. | 
| source | Source name from *sleet.json*. *Required* |
| force | Ignore missing packages. Defaults to *false* |

### Examples

Delete a single package

``sleet delete --id myNupkg --version 1.0.1-beta ``

Delete all version of a package

``sleet delete --id myNupkg``

## Stats

Stats provides a count of the number of packages on the feed.

``Usage: sleet stats [options]``

### Options

| Parameter | Description |
| --- | ------ |
| config | Optional path to *sleet.json* where the source information is contained. | 
| source | Source name from *sleet.json*. *Required* |

## Validate

Validate is a built in helper to verify that all packages contained in the index exist for all resources. If you are running into any issues such as an extra package showing up, or a missing package this is a good way to start troubleshooting.

``Usage: sleet validate [options]``

### Options

| Parameter | Description |
| --- | ------ |
| config | Optional path to *sleet.json* where the source information is contained. | 
| source | Source name from *sleet.json*. *Required* |

## Properties and settings

All feed related commands allow passing *--property* to specify properties on the command line. These properties can be used to override env vars or populate tokens in sleet.json.

For more information see [setting](settings.md)