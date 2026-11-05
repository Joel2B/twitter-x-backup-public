#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
COMPOSE_BASE_FILE="${COMPOSE_BASE_FILE:-$SCRIPT_DIR/compose.yml}"
COMPOSE_OVERRIDE_FILE="${COMPOSE_OVERRIDE_FILE:-$SCRIPT_DIR/compose.linux.yml}"
ENV_FILE="${ENV_FILE:-$SCRIPT_DIR/.env}"
COMPOSE=(
  docker compose
  --project-directory "$SCRIPT_DIR"
  --env-file "$ENV_FILE"
  -f "$COMPOSE_BASE_FILE"
  -f "$COMPOSE_OVERRIDE_FILE"
)
STOP_TIMEOUT_SECONDS="${STOP_TIMEOUT_SECONDS:-120}"

if [[ ! "$STOP_TIMEOUT_SECONDS" =~ ^[1-9][0-9]*$ ]]; then
  echo "ERROR: STOP_TIMEOUT_SECONDS must be a positive integer." >&2
  exit 2
fi

usage() {
  echo "Usage: $0 [cli|api|all] [deploy|start|stop|restart|logs]" >&2
}

resolve_service() {
  case "$1" in
    cli) echo "twitter-x-backup-cli" ;;
    api) echo "twitter-x-backup-api" ;;
    *) return 1 ;;
  esac
}

resolve_targets() {
  case "$1" in
    cli|api) echo "$1" ;;
    all) printf '%s\n' cli api ;;
    *) return 1 ;;
  esac
}

stop_service() {
  local service="$1"
  local container_id
  container_id="$("${COMPOSE[@]}" ps -a -q "$service")"

  if [[ -z "$container_id" ]]; then
    container_id="$(docker container inspect --format '{{.Id}}' "$service" 2>/dev/null || true)"
  fi

  if [[ -z "$container_id" ]]; then
    echo "==> Stop skipped: $service container does not exist"
    return
  fi

  if [[ "$(docker inspect --format '{{.State.Running}}' "$container_id")" != "true" ]]; then
    echo "==> Stop skipped: $service already stopped"
    return
  fi

  echo "==> Stop: sending SIGINT to $service"
  docker kill --signal=SIGINT "$container_id" >/dev/null

  local deadline=$((SECONDS + STOP_TIMEOUT_SECONDS))
  while [[ "$(docker inspect --format '{{.State.Running}}' "$container_id")" == "true" ]]; do
    if (( SECONDS >= deadline )); then
      echo "ERROR: $service did not stop within ${STOP_TIMEOUT_SECONDS}s; no SIGKILL sent." >&2
      return 1
    fi
    sleep 1
  done

  local exit_code
  exit_code="$(docker inspect --format '{{.State.ExitCode}}' "$container_id")"
  echo "==> Stopped $service: exit code $exit_code"

  if [[ "$exit_code" != "0" ]]; then
    "${COMPOSE[@]}" logs --tail 100 "$service" >&2
    return 1
  fi
}

remove_service_container() {
  local service="$1"
  local container_id
  container_id="$(docker container inspect --format '{{.Id}}' "$service" 2>/dev/null || true)"

  if [[ -z "$container_id" ]]; then
    return
  fi

  if [[ "$(docker inspect --format '{{.State.Running}}' "$container_id")" == "true" ]]; then
    echo "ERROR: refusing to remove running container for $service." >&2
    return 1
  fi

  echo "==> Remove stopped container: $service"
  docker rm "$container_id" >/dev/null
}

target="${1:-all}"
action="${2:-deploy}"

case "$target" in
  deploy|start|stop|restart|logs)
    if [[ $# -gt 1 ]]; then
      usage
      exit 2
    fi
    action="$target"
    target="all"
    ;;
esac

if [[ $# -gt 2 ]] || ! resolve_targets "$target" >/dev/null; then
  usage
  exit 2
fi

case "$action" in
  deploy|start|stop|restart|logs) ;;
  *)
    usage
    exit 2
    ;;
esac

for file in "$COMPOSE_BASE_FILE" "$COMPOSE_OVERRIDE_FILE" "$ENV_FILE"; do
  if [[ ! -f "$file" ]]; then
    echo "ERROR: missing $file" >&2
    exit 1
  fi
done

mapfile -t targets < <(resolve_targets "$target")
services=()
for current_target in "${targets[@]}"; do
  services+=("$(resolve_service "$current_target")")
done

available_services_output="$("${COMPOSE[@]}" config --services)"
mapfile -t available_services <<<"$available_services_output"
for service in "${services[@]}"; do
  service_found=false
  for available_service in "${available_services[@]}"; do
    available_service="${available_service%$'\r'}"
    if [[ "$available_service" == "$service" ]]; then
      service_found=true
      break
    fi
  done

  if [[ "$service_found" != "true" ]]; then
    echo "ERROR: service '$service' is not defined in Compose configuration." >&2
    exit 1
  fi
done

case "$action" in
  stop)
    for service in "${services[@]}"; do
      stop_service "$service"
    done
    ;;
  start)
    echo "==> Up: ${services[*]}"
    "${COMPOSE[@]}" up -d "${services[@]}"
    ;;
  restart)
    for service in "${services[@]}"; do
      stop_service "$service"
      remove_service_container "$service"
    done
    echo "==> Up: ${services[*]}"
    "${COMPOSE[@]}" up -d "${services[@]}"
    ;;
  logs)
    "${COMPOSE[@]}" logs -f --tail 10 "${services[@]}"
    ;;
  deploy)
    for service in "${services[@]}"; do
      stop_service "$service"
      remove_service_container "$service"
    done
    echo "==> Pull: ${services[*]}"
    "${COMPOSE[@]}" pull "${services[@]}"
    echo "==> Up: ${services[*]}"
    "${COMPOSE[@]}" up -d "${services[@]}"
    "${COMPOSE[@]}" logs -f --tail 10 "${services[@]}"
    ;;
esac
