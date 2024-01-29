# Private NuGet feeds on AWS S3

This guide will walk through how to create a private feed with basic authentication on AWS.

* S3 is used to hold the static json files and nupkgs for the feed.
* CloudFront proxies requests to S3 and serves as a CDN for the private bucket.
* Lambda functions run custom code to enforce basic authentication to restrict feed access.

## Architecture

<a href="images/private-feed-s3-arch.svg"><img src="images/private-feed-s3-arch.svg" alt="arch diagram" width="800"/></a>

## S3 bucket creation

First we will need to create a bucket to store the feed in. By default Sleet will create a bucket with a public ACL so that clients can access it directly.

Since we are making a private feed we need to create the bucket ourselves and ensure that it has a private ACL. You can do this using the S3 console, AWS CLI, or [AWS CloudShell](https://aws.amazon.com/cloudshell/).

This guide will use the s3 bucket name `sleet-private-feed`. **Replace this with your own bucket name.**

```bash
# Create the s3 bucket
[cloudshell]$ aws s3api create-bucket --bucket sleet-private-feed --region us-east-1 --acl private

# Output
{
    "Location": "/sleet-private-feed"
}
```

## Create a cloudfront distribution

Create a CloudFront distribution using the AWS console.

1. Select `Create Distribution`
1. Under `Origin Domain` select the private s3 bucket that was just created.
1. Under `Origin access` select `Origin access control settings`
1. `Origin access control` should also be set to the s3 bucket.
1. Under `Viewer protocol policy` select `HTTPS only` to avoid clients passing credentials over HTTP.
1. Choose `CachingDisabled` under `Cache key and origin requests`
1. `Restrict viewer access` should be set to `No`

<a href="images/private-feed-s3-cloudfront-create.png"><img src="images/private-feed-s3-cloudfront-create.png" alt="create cloudfront" width="800"/></a>

## Update s3 bucket policy

Creating the distrubution will tell you that you need to update the s3 bucket policy. The AWS console will guide you through this process if you follow the instructions it provides when creating the distribution.

1. Copy the policy given by the CloudFront distribution.
1. Open the s3 bucket in the AWS console.
1. Update the policy to the one given by CloudFront.

This will allow the CloudFront distribution to access the bucket since the bucket is private.


## Create lambda functions

Next we will create two lambda functions to enforce basic authentication and convert S3 403s to 404s.

Create a Lambda function using the AWS console.

1. Select `Create function`
1. Choose `Use a blueprint` at the top
1. Under *Blueprint name* search for `cloudfront` and select `Modify HTTP response header`
1. Name the function `sleet-private-feed-basic-auth`
1. Under *Excecution role* select `Create a new role from AWS policy templates`
1. Name the role `sleet-private-feed-basic-auth-role`
1. `Basic Lamdba@Edge permissions` should be automatically added as part of the template

<a href="images/private-feed-s3-lambda-create.png"><img src="images/private-feed-s3-lambda-create.png" alt="create lambda" width="800"/></a>

### Basic auth lambda function

Now that the  `sleet-private-feed-basic-auth` lambda has been created, update the code to the following.

This function will change incoming cloudfront requests and fail them with 401 if they do not have the correct basic auth credentials.

**IMPORTANT: Change authUser and authPass**

```javascript
'use strict';

// Lamba function to enforce basic auth by checking the username and password sent
// Use this in the CloudFront behavior as the 'Viewer request
export const handler = async(event, context, callback) => {
    // UPDATE THESE VALUES!!!!!!!!!
    // Set username and password
    const authUser = 'sleet';
    const authPass = 'test';

    // Get cloudfront request and headers
    const request = event.Records[0].cf.request;
    const headers = request.headers;
 
    // Construct the Basic Auth string
    const authString = 'Basic ' + new Buffer(authUser + ':' + authPass).toString('base64');
 
    // Require Basic authentication to match the credentials above
    if (typeof headers.authorization == 'undefined' || headers.authorization[0].value != authString) {
        const body = 'Unauthorized';
        const response = {
            status: '401',
            statusDescription: 'Unauthorized',
            body: body,
            headers: {
                'www-authenticate': [{key: 'WWW-Authenticate', value:'Basic'}]
            },
        };
        callback(null, response);
    } else {
        // Continue request processing if authentication passed
        callback(null, request);
    }
};
```

### Credentials as environment variables 

Once you get the basics working the username and password should be moved to lambda environment variables to secure them.

### 404 lambda function

CloudFront calls to S3 return 403 if the file does not exist in the bucket. NuGet expects a 404 when it searches all feeds for a package, if a 403 is returned NuGet will try to prompt for credentials to fix the problem.

To solve this we will create a lambda function that converts 403s to 404s.

Follow the steps from *Create lambda functions* and create a 2nd lambda function named `sleet-private-feed-origin`.

```javascript
'use strict';

// Lamba function to convert 403s from S3 to 404s so NuGet works properly
// Use this in the CloudFront behavior as the 'Origin response'
export const handler = async(event, context, callback) => {
    // Read response from S3
    const response = event.Records[0].cf.response;
    
    // Check if the response is a 403
    if (response.status == 403)
    {
        response.status = 404;
        response.statusDescription = 'NOT FOUND';
        response.body = '';
    }

    // Allow everything else
    callback(null, response);
};
```

### Deploying functions

For each function do the following to allow CloudFront to trigger the functions.

1. Update the code to match the above functions
1. Deploy the function
1. Publish a new version
1. Use `Deploy to Lambda@Edge`
1. Select `Configure new CloudFront trigger`
1. Select the CloudFront distribution that was created earlier
1. Use `*` for the cache behavior
1. See the table below for the `CloudFront event` type to use for each function
1. Confirm deploy and then deploy it

<a href="images/private-feed-s3-lambda-deploy-auth.png"><img src="images/private-feed-s3-lambda-deploy-auth.png" alt="deploy lambda" width="800"/></a>

### Cloud front event types

Ensure that the functions have the correct event type, otherwise they will not get called.

| Function | CloudFront event |
| --- | --- |
| sleet-private-feed-basic-auth | Viewer request |
| sleet-private-feed-origin | Origin response |

Functions do not need the request or response body, they work on the headers.

## Verifying functions from CloudFront

In the AWS console under `CloudFront > Distributions` select the distribution that was created earlier.

1. Select the `behaviors` tab
1. There should be an entry for `*` with the functions created above.
1. Verify that the functions show up under the correct event type and that they are using the latest version of the functions.

<a href="images/private-feed-s3-behavior.png"><img src="images/private-feed-s3-behavior.png" alt="deploy lambda" width="800"/></a>

If you do not see an entry under behaviors you can create a new behavior and add in the functions manually from the CloudFront side.


## Create the sleet feed

### Finding the cloudfront url

In the AWS console under `CloudFront > Distrubutions` find the `Domain name` for your distribution. 

If you have added an alternatte domain name you can use that instead.

This guide uses the cloudfront url `https://d1cdxzxbqv5kg2.cloudfront.net/` **Replace this with your own cloudfront url.**

### Create the sleet config 

Create a sleet.json config file to reference the bucket that we just created. There are many ways to configure the feed and credentials, below is a basic example to help understand the settings. For the full list of options see [S3 feeds](feed-type-s3.md).

The CloudFront domain is added under `baseURI`. Sleet will rewrite all urls to use this domain instead of the s3 bucket domain. CloudFront does not have to rewrite urls for the clients reading the feed, Sleet handles this.

```json
{
    "sources": [
      {
        "name": "feed",
        "type": "s3",
        "path": "https://s3.amazonaws.com/sleet-private-feed/",
        "bucketName": "sleet-private-feed",
        "region": "us-east-1",
        "baseURI": "https://d1cdxzxbqv5kg2.cloudfront.net/",
        "accessKeyId": <your access key id>,
        "secretAccessKey": <your secret access key>
      }
    ]
  }
```

### Initialize the feed

```bash
# Initialize the feed using sleet.json
$ sleet init -c sleet.json

# Output
Initializing https://d1cdxzxbqv5kg2.cloudfront.net/
Verifying sleet-private-feed exists.
Successfully initialized https://d1cdxzxbqv5kg2.cloudfront.net/ 
```

### Push a package

```bash
# Push a package to the feed
$ sleet push newtonsoft.json.13.0.3.nupkg -c sleet.json

# Output
Reading feed https://d1cdxzxbqv5kg2.cloudfront.net/
Reading feed
Add new package: Newtonsoft.Json 13.0.3
Processing feed changes
Committing changes to https://d1cdxzxbqv5kg2.cloudfront.net/
Successfully pushed packages.
```

## Configuring NuGet

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
    <packageSources>
        <clear />
        <!-- Set to your cloudfront domain -->
        <add key="PrivateFeed" value="https://d1cdxzxbqv5kg2.cloudfront.net/index.json" />
    </packageSources>
    <packageSourceCredentials>
        <PrivateFeed>
            <!-- Change these to match your lambda function -->
            <add key="Username" value="sleet" />
            <add key="ClearTextPassword" value="test" />
        </PrivateFeed>
    </packageSourceCredentials>
</configuration>
```


## Testing it out

Create a `test.proj` file with a package that doesn't exist. This will ensure we are not getting the package from the cache.

```xml
<Project ToolsVersion="15.0">
  <PropertyGroup>
    <TargetFrameworks>net8.0</TargetFrameworks>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="SleetTestDoesNotExist" Version="10.0.5" />
  </ItemGroup>
  
  <Import Project="Sdk.targets" Sdk="Microsoft.NET.Sdk" />
</Project>
```

Run `dotnet restore test.proj` and you should see the following output.

```bash
# Ensure NuGet.config is in the current directory
$ dotnet restore test.proj
```

### Diagnosing problems

#### Verify urls manually

Try to open the feed url in your browser using.

```
# Template
https://<user>:<pass>@<cloudfront domain>/index.json

# Url example for this guide
https://sleet:test@d1cdxzxbqv5kg2.cloudfront.net/index.json
```

You should be able to see the json file with the feed config in the browser if everything is working.

You can also verify that 404s work correctly

```
# Template
https://<user>:<pass>@<cloudfront domain>/does-not-exist.json

# Url example for this guide
https://sleet:test@d1cdxzxbqv5kg2.cloudfront.net/does-not-exist.json
```

If you get a 503 or 403 instead of a 404 then the origin lambda function is not working correctly.

#### 401 errors

If things are not setup correctly you will a 401 error saying that the credentials are invalid.
`Response status code does not indicate success: 401 (Unauthorized). [test.proj] `

#### Invalid domain

An invalid domain error may also occur if the wrong domain or sleet settings were used. Check the baseURI in the sleet config and verify it matches the CloudFront domain. Also verify that index.json uses the correct domain.


### Success

If everything is successful you will see an error saying the package does not exist 
`error NU1101: Unable to find package SleetTestDoesNotExist`

When pushing a real package to the sleet feed it should restore successfully.

## Final steps

Once you have everything working you can start using the feed settings from NuGet.config as part of your build and local VS NuGet config.

## Improve this doc

Has AWS changed? Is something unclear? Typos? Send a PR to improve it!