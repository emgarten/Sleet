#!/usr/bin/env bash
# Stops the local test environment and removes its containers and data.
#
#   ./local-env/stop.sh
set -euo pipefail

COMPOSE_FILE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/docker-compose.yml"

if ! command -v docker > /dev/null 2>&1; then
  echo "Docker is required for the local test environment. Install it from https://docs.docker.com/get-started/get-docker/" >&2
  exit 1
fi

# --volumes removes the anonymous data volumes that the images declare
echo ">> docker compose -f $COMPOSE_FILE down --volumes --remove-orphans"
docker compose -f "$COMPOSE_FILE" down --volumes --remove-orphans
