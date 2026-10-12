# External search

Sleet includes a static search resource by default. You can replace it with an external NuGet Search API service when clients need server-side filtering and paging.

## Static search behavior

The built-in `search/query` resource is a static JSON file. Sleet writes every package id into one response with all versions for each id. It does not apply the NuGet search query parameters, so it does not filter by `q`, `skip`, `take`, `prerelease`, or `semVerLevel`.

This is enough for small feeds. On large feeds, clients such as Visual Studio can show incomplete browse or search results because they expect a paged search service.

## Set an external search URL

Set the `externalsearch` feed setting to the Search API endpoint:

```bash
sleet feed-settings --set externalsearch:https://example.org/search/query
```

Sleet stores the value in `sleet.settings.json` and immediately updates the `SearchQueryService/3.0.0-beta` resource in `index.json` to use that URL. `sleet recreate` is not needed for this setting, even though `feed-settings` prints a general reminder to run `recreate`.

## Revert to static search

Unset `externalsearch` to point the search resource back to Sleet's static `search/query` file:

```bash
sleet feed-settings --unset externalsearch
```

This also updates `index.json` immediately.

## External service requirements

The external service must implement the NuGet Search API. NuGet clients discover it from the service index and call it with query parameters such as:

| Parameter | Purpose |
| --- | --- |
| `q` | Search text. |
| `skip` | Number of results to skip. |
| `take` | Number of results to return. |
| `prerelease` | Whether prerelease packages are included. |
| `semVerLevel` | SemVer compatibility level requested by the client. |

See Microsoft's [NuGet Search API documentation](https://learn.microsoft.com/en-us/nuget/api/search-query-service-resource) for the full protocol. [Sleet.Search](https://github.com/emgarten/Sleet.Search) is an example external search service for Sleet feeds.

See [feed settings](feed-settings.md#externalsearch) for the setting reference.
