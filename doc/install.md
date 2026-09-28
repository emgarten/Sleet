# Install Sleet

Sleet is a .NET tool. This page covers the ways to install and run it, and how to pin a version for builds.

## Requirements

The Sleet .NET tool needs the .NET 8 SDK or later. It runs on Windows, Linux, and macOS. Check your SDK version with:

```bash
dotnet --version
```

If you can't install .NET, use [Sleet.exe](#sleetexe-for-windows) on Windows instead.

## Global tool

A global tool puts the `sleet` command on your `PATH` for your user account. This is the usual choice on a workstation.

```bash
dotnet tool install -g sleet
```

Check that it works:

```bash
sleet --version
```

On Linux and macOS, global tools are installed to `$HOME/.dotnet/tools`. If the shell can't find `sleet`, add that folder to your `PATH`.

Update or remove the tool with:

```bash
dotnet tool update -g sleet
dotnet tool uninstall -g sleet
```

## Pin a version in CI

In build scripts, pin the major version. A new major release can then never change your build without you knowing:

```bash
dotnet tool install -g sleet --version "7.*"
```

Check the [release notes](https://github.com/emgarten/Sleet/blob/main/ReleaseNotes.md) before you move to a new major version.

## Local tool

A local tool manifest pins an exact Sleet version for a repository, so every developer and build uses the same one.

```bash
dotnet new tool-manifest
dotnet tool install sleet
```

Commit the `.config/dotnet-tools.json` file this creates. After a clone, restore the tools and run Sleet through `dotnet`:

```bash
dotnet tool restore
dotnet sleet --version
```

Every `sleet` command in these docs also works as `dotnet sleet`. See Microsoft's guide to [local tools](https://learn.microsoft.com/en-us/dotnet/core/tools/local-tools-how-to-use) for more.

## Run without installing

With the .NET 10 SDK or later, [dnx](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-tool-exec) downloads Sleet and runs it in one step:

```bash
dnx sleet createconfig --azure
```

## Sleet.exe for Windows

The [SleetExe](https://www.nuget.org/packages/SleetExe) package contains `Sleet.exe`, a self-contained build for Windows x64. It doesn't need .NET to be installed.

1. Download the package from NuGet.org.
1. Open the `.nupkg` file as a zip file.
1. Extract `tools/Sleet.exe` to a folder and run it.

The commands are the same as for the .NET tool. Starting with Sleet 8.0, adding the SleetExe package to a project also sets the `$(Sleet)` MSBuild property to the full path of `Sleet.exe`:

```xml
<Target Name="PublishToFeed">
  <Exec Command="&quot;$(Sleet)&quot; push nupkgs --skip-existing" />
</Target>
```

## SleetLib for .NET code

The [SleetLib](https://www.nuget.org/packages/SleetLib) package contains the library behind the Sleet CLI. Use it to create and update feeds from your own .NET code. See [SleetLib](sleetlib.md).

```bash
dotnet add package SleetLib
```

## Next steps

Create a feed on [Azure](feed-type-azure.md), [Amazon S3](feed-type-s3.md), or a [local folder](feed-type-local.md).
