# Badges

Sleet supports generating package version badges similar to [shields.io](https://shields.io/) as of version 3.1.26

## How to enable badges on sleet
Badges can be enabled in the feed settings using the following command

`sleet feed-settings --set badgesenabled:true`

## Badge URLs

| version | url format |
| ------- | --- |
| stable |  `{feed url}/badges/v/{package id}.svg` |
| prerelease |  `{feed url}/badges/vpre/{package id}.svg` |

## Badge URLs using shields.io

Badges can also be generated from a json file on the feed using *shields.io* and customized.

| version | url format |
| ------- | --- |
| stable |  `https://img.shields.io/endpoint?url={feed url}/badges/v/{package id}.json` |
| prerelease |  `https://img.shields.io/endpoint?url={feed url}/badges/vpre/{package id}.json` |

See [shields.io](https://shields.io/) for further options on customizing the badge style.