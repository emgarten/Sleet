# Badges

Sleet supports generating package version badges using [shields.io](https://shields.io/)

## Badge URLs

| version | url format |
| ------- | --- |
| stable |  `{feed url}/badges/v/{package id}.svg` |
| prerelease |  `{feed url}/badges/vpre/{package id}.svg` |

## Badge URLs using shields.io

Badges are generated from a json file on the feed using *shields.io*

| version | url format |
| ------- | --- |
| stable |  `https://img.shields.io/endpoint?url={feed url}/badges/v/{package id}.json` |
| prerelease |  `https://img.shields.io/endpoint?url={feed url}/badges/vpre/{package id}.json` |

See [shields.io](https://shields.io/) for further options on customizing the badge style.

Note that the feed url must include the S3 bucket or Azure container name in additional to the base url.

## How to disable badges on sleet
Badges can be disabled in the feed settings using the following command

`sleet feed-settings --set badgesenabled:false`

Badges are enabled by default for new feeds.
