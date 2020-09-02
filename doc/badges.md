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
