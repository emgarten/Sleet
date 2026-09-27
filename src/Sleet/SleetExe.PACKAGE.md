# SleetExe

SleetExe contains *Sleet.exe*, a self-contained single-file build of [Sleet](https://github.com/emgarten/Sleet) for Windows x64 that doesn't require .NET to be installed. On other platforms, or when the .NET SDK is available, use the [Sleet .NET tool](https://www.nuget.org/packages/Sleet) instead.

## Use from MSBuild

Add the package to a project:

```
dotnet add package SleetExe
```

The package sets the `$(Sleet)` MSBuild property to the full path of *Sleet.exe*:

```xml
<Target Name="PublishToFeed">
  <Exec Command="&quot;$(Sleet)&quot; push nupkgs --skip-existing" />
</Target>
```

## Use directly

Download the package from NuGet.org and extract *tools/Sleet.exe*.

## Documentation

* [Commands](https://github.com/emgarten/Sleet/blob/main/doc/commands.md)
* [Release notes](https://github.com/emgarten/Sleet/blob/main/ReleaseNotes.md)

Source code and issues: [github.com/emgarten/Sleet](https://github.com/emgarten/Sleet)
