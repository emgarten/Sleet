# Web servers and static hosting

Sleet feeds are static files. This page shows what any web server must provide when it serves a Sleet local feed folder.

## Hosting requirements

Serve the feed folder at the exact URL configured as `baseURI`. NuGet reads `{baseURI}index.json`, then follows the resource URLs written into that file.

A production feed should use HTTPS. With .NET 9 SDK and NuGet 6.12 or later, HTTP package sources fail with NU1302 unless the source has `allowInsecureConnections="true"` in `NuGet.Config`.

The server must serve:

- `.json` files as `application/json`.
- Extensionless JSON files as `application/json`.
- `.nupkg` files as `application/zip`.
- `.nuspec` and `.xml` files as `application/xml`.
- `.dll` and `.pdb` files as `application/octet-stream` when the symbol server is enabled.
- `/icon` files as `image/png` when packages contain embedded icons.
- `/readme` files as `text/markdown` when packages contain embedded readmes.

Sleet writes these extensionless files:

| Path pattern | Recommended content type |
| --- | --- |
| `search/query` | `application/json` |
| `autocomplete/query` | `application/json` |
| `flatcontainer/{id}/{version}/icon` | `image/png` |
| `flatcontainer/{id}/{version}/readme` | `text/markdown` |

Sleet sets the same content types when it uploads to Azure Blob Storage or Amazon S3. Local feeds store JSON uncompressed, so enabling on-the-fly compression for JSON responses is useful.

## Configure IIS

IIS serves only file types that have static content MIME mappings. You can add mappings in IIS Manager or in a `web.config` file.

A minimal `web.config` for most feeds is:

```xml
<configuration>
  <system.webServer>
    <staticContent>
      <mimeMap fileExtension=".nupkg" mimeType="application/zip" />
      <mimeMap fileExtension=".nuspec" mimeType="application/xml" />
      <mimeMap fileExtension=".pdb" mimeType="application/octet-stream" />
      <mimeMap fileExtension=".dll" mimeType="application/octet-stream" />
      <mimeMap fileExtension="." mimeType="application/json" />
    </staticContent>
    <urlCompression doStaticCompression="true" />
  </system.webServer>
</configuration>
```

Most IIS installations already map `.json`. The extensionless `.` mapping covers `search/query` and `autocomplete/query`, but it also maps extensionless `icon` and `readme` files to JSON. If your packages include embedded icons or readmes and clients need those URLs, configure path-specific handling outside this basic static MIME map or use a server that can map those paths separately.

To expose a feed as a virtual directory:

1. Open Internet Information Services Manager.
1. Select the site that owns the public host name.
1. Choose **Add Virtual Directory**.
1. Set **Alias** to the URL segment, such as `feed`.
1. Set **Physical path** to the local feed output folder.
1. Set the feed source `baseURI` to the public URL, such as `https://example.com/feed/`.

## Configure nginx

This example serves `/srv/feeds/myfeed` at `https://example.com/feed/`:

```nginx
server {
    listen 443 ssl;
    server_name example.com;
    # Add your ssl_certificate and ssl_certificate_key settings here.

    location /feed/ {
        alias /srv/feeds/myfeed/;
        types {
            application/json json;
            application/zip nupkg;
            application/xml nuspec xml;
            application/octet-stream dll pdb;
            image/svg+xml svg;
        }
        default_type application/octet-stream;
        gzip on;
        gzip_types application/json application/xml text/markdown image/svg+xml;
    }

    location ~ ^/feed/(search/query|autocomplete/query)$ {
        alias /srv/feeds/myfeed/$1;
        default_type application/json;
        gzip on;
    }

    location ~ ^/feed/(flatcontainer/.+/icon)$ {
        alias /srv/feeds/myfeed/$1;
        default_type image/png;
    }

    location ~ ^/feed/(flatcontainer/.+/readme)$ {
        alias /srv/feeds/myfeed/$1;
        default_type text/markdown;
        gzip on;
    }
}
```

Keep the `alias` path and `baseURI` path aligned. If the feed is available at `/feed/`, `baseURI` must end in `/feed/`.

## Configure Apache

This example serves `/srv/feeds/myfeed` at `/feed/`:

```apache
Alias "/feed/" "/srv/feeds/myfeed/"

<Directory "/srv/feeds/myfeed">
    Require all granted
    AddType application/json .json
    AddType application/zip .nupkg
    AddType application/xml .nuspec .xml
    AddType application/octet-stream .dll .pdb
    AddType image/svg+xml .svg

    <Files "query">
        ForceType application/json
    </Files>
    <Files "icon">
        ForceType image/png
    </Files>
    <Files "readme">
        ForceType text/markdown
    </Files>
</Directory>
```

Enable compression with Apache's compression module if it is available for your site.

## Keep server configuration outside the feed folder

> [!WARNING]
> `destroy` and `recreate` delete every file under the local feed root, including subfolders and files such as `web.config`.

Keep web server configuration in the parent site, virtual host, or server configuration when possible. If you must place configuration inside the feed folder, restore it after `destroy` or `recreate`.

## Test a local server

For a quick local test, serve the feed folder over HTTP:

```powershell
python -m http.server 8080 --directory C:\feeds\myfeed
```

Set the feed `baseURI` to the test URL before you create the feed, such as `http://localhost:8080/`. For HTTP sources, add `allowInsecureConnections="true"`:

```xml
<configuration>
  <packageSources>
    <add key="sleet-test" value="http://localhost:8080/index.json" allowInsecureConnections="true" />
  </packageSources>
</configuration>
```

If the feed already exists with a different `baseURI`, rebuild it with the steps in [rebuild a local feed without recreate](backup-migration.md#rebuild-a-local-feed-without-recreate). The `recreate` command doesn't work for local feeds whose `baseURI` is different from `path`.

## Other static hosts

Static hosts can work if they preserve the folder layout, allow the required content types, and serve extensionless files. Check size limits before publishing large `.nupkg` files. Some CDNs and static hosts cache by extension by default, so configure cache rules for JSON and extensionless query files instead of relying on defaults.
