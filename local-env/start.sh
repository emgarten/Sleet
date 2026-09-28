#!/usr/bin/env bash
# Starts the local test environment with Docker and waits until it's ready.
#
#   ./local-env/start.sh            all services in docker-compose.yml
#   ./local-env/start.sh azurite    only the listed services
set -euo pipefail

COMPOSE_FILE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/docker-compose.yml"

if ! command -v docker > /dev/null 2>&1; then
  echo "Docker is required for the local test environment. Install it from https://docs.docker.com/get-started/get-docker/" >&2
  exit 1
fi

if ! docker info > /dev/null 2>&1; then
  echo "Docker isn't running. Start Docker Desktop or the Docker service, then try again." >&2
  exit 1
fi

echo ">> docker compose -f $COMPOSE_FILE up --detach --wait --wait-timeout 120 $*"

if ! docker compose -f "$COMPOSE_FILE" up --detach --wait --wait-timeout 120 "$@"; then
  docker compose -f "$COMPOSE_FILE" logs "$@" || true
  echo "The local test environment didn't start, see the logs above." >&2
  exit 1
fi

docker compose -f "$COMPOSE_FILE" ps "$@"
