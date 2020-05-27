# Getting Started with Minio
[MinIO](https://min.io/) is a high-performance, S3 API compliant, 100% open source, object storage solution. MinIO is a great storage solution for enterprises that need a secure and scalable data storage solution for on-premise, private cloud, and even public cloud deployments.

The purpose of this guide is to setup a local MinIO Server container for development, testing, and evaluation of [Sleet](https://github.com/emgarten/sleet).

## Preflight
Here's what you need to successfully follow this guide:

* Internet Access
* [DockerCE](https://docs.docker.com/engine/install/) or [Podman](http://docs.podman.io/en/latest/) are installed and configured for your OS.

*TIP: If you are running on Linux I would highly recommend using Podman as the more secure alternative to Docker.*

## Get the official MinIO container image
Pull the latest image from Dockerhub.

*Docker*
``docker pull minio/minio``
*NOTE: On Linux, if you are using Docker, you may need to `sudo` unless you have added your user to `docker` group.*

*Podman*
``podman pull minio/minio``

*WARNING: If you choose to pull an older image containing an older version of MinIO you many run into an eTag compatibility issue documented here: https://github.com/minio/minio/issues/7642 when using AWS S3 SDK to interact with MinIO Server. Recent versions (since April 2020) of MinIO Server contain the fix and work well with AWS S3 SDKs.*

## Run a MinIO container
### Run without persistent volume
MinIO needs a persistent volume to store configuration and application data. For testing purposes you can run a MinIO container with an ephemeral filesystem by specifying a directory such as `/data`. This directory gets created in the container filesystem at the time of container start. All the data is lost after container exits. The benefit of this configuration is that cleanup is easy; just destroy the container when you're finished with it!

*Docker*
``docker run -e "MINIO_ACCESS_KEY=Q3AM3UQ867SPQQA43P2F" -e "MINIO_SECRET_KEY=zuf+tfteSlswRu7BJ86wekitnifILbZam1KYY3TG" -p 9000:9000 --name minio1 minio/minio server /data``

*NOTE: On Linux, if you are using Docker, you may need to `sudo` unless you have added your user to `docker` group.*

*Podman*
``podman run -e "MINIO_ACCESS_KEY=Q3AM3UQ867SPQQA43P2F" -e "MINIO_SECRET_KEY=zuf+tfteSlswRu7BJ86wekitnifILbZam1KYY3TG" -p 9000:9000 --name minio1 minio/minio server /data``


### Run with persistent volume
To create a MinIO container with persistent storage, you need to map local persistent directories from the host OS to the container path `/data`.

*Docker*
``docker run -e "MINIO_ACCESS_KEY=Q3AM3UQ867SPQQA43P2F" -e "MINIO_SECRET_KEY=zuf+tfteSlswRu7BJ86wekitnifILbZam1KYY3TG" -p 9000:9000 --name minio1 -v /var/containers/minio/data:/data minio/minio server /data``


*Podman*
``podman run -e "MINIO_ACCESS_KEY=Q3AM3UQ867SPQQA43P2F" -e "MINIO_SECRET_KEY=zuf+tfteSlswRu7BJ86wekitnifILbZam1KYY3TG" -p 9000:9000 --name minio01 -v /var/containers/minio/data:/data:Z minio/minio server``

*WARNING: We are only passing secrets on the CLI for testing purposes only! Never do this in a production environment!*

### Expected results
Regardless of running MinIO in Docker or Podman, with persisted data or without, if the container started you will see standard ouput similar to:

```
Endpoint:  http://10.88.0.61:9000  http://127.0.0.1:9000    

Browser Access:
   http://10.88.0.61:9000  http://127.0.0.1:9000    

Object API (Amazon S3 compatible):
   Go:         https://docs.min.io/docs/golang-client-quickstart-guide
   Java:       https://docs.min.io/docs/java-client-quickstart-guide
   Python:     https://docs.min.io/docs/python-client-quickstart-guide
   JavaScript: https://docs.min.io/docs/javascript-client-quickstart-guide
   .NET:       https://docs.min.io/docs/dotnet-client-quickstart-guide

```

You can now launch your favorite web browser and go to `http://localhost:9000`.

### Additional configuration options
#### Port mapping
If your host port `9000` is already in use, map to an unused host port.

*example*

``podman run -p 9001:9000 --name minio1 minio/minio server /data``


#### Run container in detached mode
*example*

``podman run -d -p 9001:9000 --name minio1 minio/minio server /data``

#### Run MinIO in Docker as a rootless container
See *Run MinIO Docker as a regular user* section of the [Official MinIO Docker QuickStart Guide](https://docs.min.io/docs/minio-docker-quickstart-guide.html).

## Use MinIO
MinIO Server provides a web UI for human use. It does not provide any admin or configuration facility. Administration can be accomplished using [MinIO CLI client](https://docs.min.io/docs/minio-client-quickstart-guide) and any number of language specific SDKs.

Launch your favorite web browser and go to `http://localhost:9000`. Login using `minioadmin` and `1234567890`. Once authenticated, you can create buckets, upload files (objects), delete objects, download objects, and delete objects.

*NOTE: If you changed the values of `MINIO_ACCESS_KEY` and `MINIO_SECRET_KEY` you'll need to use those values to authenticate.*

## Next Steps
At this point you have a local, standalone MinIO container instance that will serve you well in your development, testing, and evaluation efforts. Now that you have MinIO running, try out [Sleet feeds on MinIO](feed-type-minio.md).

## Additional References
* [MinIO](https://min.io/)
* [Official MinIO documentation](https://docs.min.io/)
* [Official MinIO Docker QuickStart Guide](https://docs.min.io/docs/minio-docker-quickstart-guide.html)
* [Official DockerCE installation guide](https://docs.docker.com/engine/install/)
* [Official Podman documentation](http://docs.podman.io/en/latest/)
* [Official MinIO OCI image on Dockerhub](https://hub.docker.com/r/minio/minio)
* [Official MinIO CLI client Quickstart Guide](https://docs.min.io/docs/minio-client-quickstart-guide)

