# External search service

Sleet uses a static search resources by default which returns all packages on the feed. Dynamic search results can be provided through an external service.

## Setting an external search provider

To update a feed set *externalsearch* to the url of the external search provider 
using the feed settings command.

```
feed-settings --set "externalsearch:http://example.org/search/query"
```

The above command will store the external url in *sleet.settings.json* and update
the service index in the main *index.json* for the feed. NuGet will discover the
url through the service index and send along the query, prerelease, skip, and take 
parameters to the new endpoint.


## Reverting to static search
If you no longer wish to use the external search service you can switch back
by unsetting *externalsearch*

```
feed-settings --unset "externalsearch"
```

## Example external search providers

* [Sleet.Search](https://github.com/emgarten/Sleet.Search) - Dockerized azure function that providers filtering for sleet feeds

