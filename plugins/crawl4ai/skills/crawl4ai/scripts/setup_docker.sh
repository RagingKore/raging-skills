#!/usr/bin/env bash
# Setup Crawl4AI via Docker — pull image and start container.
# Usage: bash scripts/setup_docker.sh [--port PORT] [--env-file PATH]

set -euo pipefail

PORT="${1:-11235}"
ENV_FILE=""
CONTAINER_NAME="crawl4ai"
IMAGE="unclecode/crawl4ai:latest"

# Parse args
while [[ $# -gt 0 ]]; do
    case $1 in
        --port) PORT="$2"; shift 2 ;;
        --env-file) ENV_FILE="$2"; shift 2 ;;
        *) shift ;;
    esac
done

echo "==> Checking Docker..."
if ! command -v docker &>/dev/null; then
    echo "ERROR: Docker is not installed. Install Docker first: https://docs.docker.com/get-docker/"
    exit 1
fi

# Stop existing container if running
if docker ps -q --filter "name=$CONTAINER_NAME" | grep -q .; then
    echo "==> Stopping existing $CONTAINER_NAME container..."
    docker stop "$CONTAINER_NAME" && docker rm "$CONTAINER_NAME"
elif docker ps -aq --filter "name=$CONTAINER_NAME" | grep -q .; then
    echo "==> Removing stopped $CONTAINER_NAME container..."
    docker rm "$CONTAINER_NAME"
fi

echo "==> Pulling $IMAGE..."
docker pull "$IMAGE"

echo "==> Starting container on port $PORT..."
DOCKER_ARGS=(-d -p "$PORT:11235" --name "$CONTAINER_NAME" --shm-size=1g --restart unless-stopped)

if [[ -n "$ENV_FILE" ]]; then
    DOCKER_ARGS+=(--env-file "$ENV_FILE")
    echo "    Using env file: $ENV_FILE"
fi

docker run "${DOCKER_ARGS[@]}" "$IMAGE"

echo "==> Waiting for health check..."
for i in $(seq 1 30); do
    if curl -sf "http://localhost:$PORT/health" >/dev/null 2>&1; then
        echo "==> Crawl4AI is running!"
        echo "    API:        http://localhost:$PORT"
        echo "    Dashboard:  http://localhost:$PORT/dashboard"
        echo "    Playground: http://localhost:$PORT/playground"
        echo "    MCP (SSE):  http://localhost:$PORT/mcp/sse"
        echo "    Health:     http://localhost:$PORT/health"
        exit 0
    fi
    sleep 2
done

echo "WARNING: Container started but health check not responding after 60s."
echo "Check logs: docker logs $CONTAINER_NAME"
exit 1
