# Sleet.MinioS3.Tests

Set the following environment variables:

* SLEET_FEED_ACCESSKEYID
* SLEET_FEED_SECRETACCESSKEY
* SLEET_FEED_REGION
* SLEET_FEED_SERVICEURL
* SLEET_FEED_COMPRESS
* SLEET_FEED_TYPE

If you are testing, using a containerized MinIO Server as described in [getting-started-with-minio](../../doc/getting-started-with-minio.md), the environment variable values should be:

* SLEET_FEED_ACCESSKEYID="Q3AM3UQ867SPQQA43P2F"
* SLEET_FEED_SECRETACCESSKEY="zuf+tfteSlswRu7BJ86wekitnifILbZam1KYY3TG"
* SLEET_FEED_REGION="us-east-1"
* SLEET_FEED_SERVICEURL="http://localhost:9000"
* SLEET_FEED_COMPRESS="false"
* SLEET_FEED_TYPE="minio"

export SLEET_FEED_ACCESSKEYID="Q3AM3UQ867SPQQA43P2F"
export SLEET_FEED_SECRETACCESSKEY="zuf+tfteSlswRu7BJ86wekitnifILbZam1KYY3TG"
export SLEET_FEED_REGION="us-east-1"
export SLEET_FEED_SERVICEURL="http://localhost:9000"
export SLEET_FEED_COMPRESS="false"
export SLEET_FEED_TYPE="minio"
