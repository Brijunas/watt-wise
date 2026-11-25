#!/usr/bin/env bash
# Asserts the debugger in a backend dev image (deploy/docs/debugging.md): vsdbg is there, pinned to the
# version in the Dockerfile, runnable but not changeable by the image's non-root user, it answers the
# Debug Adapter Protocol, and launch.json attaches to the right container. Run from the repo root
# with Docker running:
#   bash .github/scripts/check-debugger.sh <dev-image> <dockerfile> <service> [<image without vsdbg>...]
# The expected version is the Dockerfile's `ARG VSDBG_VERSION=` line, so the pin lives in one place.
# <service> is the Compose service (api or jobs). Every further image (the runtime and build stages)
# must hold no /vsdbg. The container is named and removed on exit.
set -euo pipefail

usage='usage: check-debugger.sh <dev-image> <dockerfile> <service> [<image without vsdbg>...]'
image="${1:?$usage}"
dockerfile="${2:?$usage}"
service="${3:?$usage}"
shift 3

error() {
  echo "::error::$*"
  exit 1
}

[ -f "$dockerfile" ] || error "$dockerfile does not exist."
version="$(sed -n 's/^ARG VSDBG_VERSION=//p' "$dockerfile" | head -n 1)"
[ -n "$version" ] || error "$dockerfile has no 'ARG VSDBG_VERSION=' line."

work="$(mktemp -d)"
dap_container="check-debugger-$$"
cleanup() {
  docker rm -f "$dap_container" > /dev/null 2>&1 || true
  rm -rf "$work"
}
trap cleanup EXIT

# 1. The image runs as a non-root user.
user="$(docker image inspect -f '{{.Config.User}}' "$image")"
case "$user" in
  '' | root | 0 | 0:* | root:*) error "$image runs as '$user', not as a non-root user." ;;
esac
echo "ok: the image's user is $user"

# 2. vsdbg is in the image, and the image's user can run it.
if ! docker run --rm --entrypoint test "$image" -x /vsdbg/vsdbg; then
  error "$image has no executable /vsdbg/vsdbg."
fi
if ! docker run --rm --entrypoint /vsdbg/vsdbg "$image" --help > /dev/null; then
  error "/vsdbg/vsdbg does not run as user $user in $image."
fi
echo "ok: vsdbg runs as $user"

# 3. That user can't change it. A failing find is an error, not an empty list.
if ! writable="$(docker run --rm --entrypoint find "$image" /vsdbg -writable)"; then
  error "Listing the writable paths under /vsdbg failed in $image."
fi
[ -z "$writable" ] || error "The image's user can write in /vsdbg: $writable"
echo "ok: nothing under /vsdbg is writable by $user"

# 4. The pinned version, exactly (the line is "<version> commit:<hash>").
actual="$(docker run --rm --entrypoint head "$image" -n 1 /vsdbg/version.txt)"
actual="${actual%% *}"
[ "$actual" = "$version" ] || error "/vsdbg/version.txt names '$actual', expected '$version' (from $dockerfile)."
echo "ok: vsdbg is $version"

# 5. It answers the Debug Adapter Protocol's initialize request. vsdbg keeps running after it
# answers, so stdin is a FIFO held open here, the output is polled, and the container is removed
# as soon as the answer is in (or after 60 seconds).
body='{"seq":1,"type":"request","command":"initialize","arguments":{"clientID":"vscode","adapterID":"coreclr","pathFormat":"path","linesStartAt1":true,"columnsStartAt1":true}}'
mkfifo "$work/in"
exec 3<> "$work/in"
docker run -i --rm --name "$dap_container" --entrypoint /vsdbg/vsdbg "$image" --interpreter=vscode < "$work/in" > "$work/out" 2>&1 &
printf 'Content-Length: %s\r\n\r\n%s' "${#body}" "$body" >&3
answered=false
for _ in $(seq 1 120); do
  if grep -q '"command":"initialize"' "$work/out"; then
    answered=true
    break
  fi
  sleep 0.5
done
docker rm -f "$dap_container" > /dev/null 2>&1 || true
exec 3>&-
[ "$answered" = true ] || error "vsdbg did not answer initialize within 60 seconds: $(cat "$work/out")"
grep -q '"success":true' "$work/out" || error "vsdbg answered initialize without success: $(cat "$work/out")"
echo "ok: vsdbg answers the Debug Adapter Protocol"

# 6. launch.json attaches to this service's container with this image's debugger.
project="$(sed -n 's/^name: *//p' deploy/docker-compose.development.yml | head -n 1)"
[ -n "$project" ] || error "deploy/docker-compose.development.yml has no 'name:' line."
container="$project-$service-1"
if ! jq -e --arg container "$container" '
  [.configurations[]
   | select(.type == "coreclr" and .pipeTransport.debuggerPath == "/vsdbg/vsdbg"
            and (.pipeTransport.pipeArgs | index($container)) != null)] | length == 1' .vscode/launch.json > /dev/null; then
  error ".vscode/launch.json has no single coreclr configuration with debuggerPath /vsdbg/vsdbg that attaches to $container."
fi
echo "ok: launch.json attaches to $container"

# 7. The stages that ship hold no debugger.
for other in "$@"; do
  if ! docker run --rm --entrypoint sh "$other" -c 'test ! -e /vsdbg'; then
    error "$other holds /vsdbg."
  fi
  echo "ok: $other holds no /vsdbg"
done

echo "The debugger in $image passed."
