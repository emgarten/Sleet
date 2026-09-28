# Local test environment

This folder runs local versions of the storage services that Sleet supports, so you can run the functional tests without a cloud account. It needs [Docker](https://docs.docker.com/get-started/get-docker/), such as Docker Desktop on Windows and macOS, or Docker Engine on Linux.

## Run the tests

Start Docker, then run the functional tests from the repository root:

```powershell
./functional-tests.ps1
```

```bash
./functional-tests.sh
```

The script starts the environment, builds and runs the tests against it, and stops the environment when the tests finish. If the environment was already running, it's left running. The test results and the container logs are written to `artifacts/TestResults/functional`.

The same script runs the tests against Azure and Amazon S3 accounts. See [functional tests](../CONTRIBUTING.md#functional-tests) for all the targets.

## Services

| Service | Emulates | Endpoint | Settings |
| --- | --- | --- | --- |
| `azurite` | Azure Blob Storage | `http://127.0.0.1:10000` | Connection string `UseDevelopmentStorage=true` |
| `rustfs` | Amazon S3 | `http://127.0.0.1:9100` | Access key `rustfsadmin`, secret key `rustfsadmin` |

The Amazon S3 tests don't use RustFS yet. They need [#239](https://github.com/emgarten/Sleet/pull/239) to work with it.

The services keep their data in the containers, so it's removed when the environment stops.

## Start and stop the environment

Start the environment yourself to keep it running between test runs, or to run the tests from an IDE:

```powershell
# All services, or only the ones you list
./local-env/start.ps1
./local-env/start.ps1 azurite

./local-env/stop.ps1
```

```bash
# All services, or only the ones you list
./local-env/start.sh
./local-env/start.sh azurite

./local-env/stop.sh
```

The start script waits until each service is healthy. The stop script removes the containers and their data.

To run the Azure tests from an IDE or with `dotnet test`, start the environment and set `SLEET_TEST_ACCOUNT` to `UseDevelopmentStorage=true`. Without it, the tests are skipped.

## CI

GitHub Actions runs the same scripts, so a run that passes locally should pass in CI:

- [functional.yml](../.github/workflows/functional.yml) runs the emulator tests and the cloud tests for pushes to `main` and for pull requests from branches in this repository.
- [functional-emulators.yml](../.github/workflows/functional-emulators.yml) runs only the emulator tests for pull requests from forks and Dependabot, which can't use the repository secrets.
- [functional-manual.yml](../.github/workflows/functional-manual.yml) lets a maintainer run both for a pull request from a fork.

The emulator tests run on Linux because the GitHub hosted macOS and Windows runners can't run Linux containers.

## Add a service

1. Add the service to [docker-compose.yml](docker-compose.yml). Pin the image version, bind the ports to `127.0.0.1`, and add a healthcheck so the start script can wait for it.
1. Add a target for it to [functional-tests.ps1](../functional-tests.ps1) and [functional-tests.sh](../functional-tests.sh), with the services it uses and the environment variables that point the tests at it. Add the target to the `emulators` group.
1. Add the service to the table above and the target to [functional tests](../CONTRIBUTING.md#functional-tests).

Dependabot keeps the image versions in `docker-compose.yml` up to date.
