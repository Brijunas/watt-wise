#!/bin/sh
# Start guard of the frontend and admin images (deploy/docs/images.md, "Runtime settings").
# Runs from /docker-entrypoint.d/ before the envsubst script renders /config.js and index.html. The
# entry point runs under `set -e`, so a non-zero exit stops the container before nginx starts.
#
# Each setting named in WATTWISE_SETTINGS must have the shape `checkOrigin` in
# packages/core/src/runtime-config/origin.ts requires: https://host[:port], or http:// only for
# localhost, 127.0.0.1 or [::1]; no path (not even a trailing /), query, fragment or credentials.
# This is a shape check, not URL parsing (it accepts some hosts `new URL` rejects, such as
# https://999.1.1.1): the browser's readRuntimeConfig stays the full check of API_BASE_URL and
# FRONTEND_URL. What the guard adds is that the value holds no ", <, \ or $, so it is safe in the
# rendered JS string and HTML attribute.
set -eu

host='([A-Za-z0-9]([A-Za-z0-9.-]*[A-Za-z0-9])?|\[[0-9A-Fa-f:.]+\])'
loopback='(localhost|127\.0\.0\.1|\[::1\])'
port='(:[0-9]{1,5})?'
newline='
'

fail() {
  echo "05-check-settings.sh: $1" >&2
  exit 1
}

for name in ${WATTWISE_SETTINGS:-}; do
  value="$(printenv "$name" || true)"
  [ -n "$value" ] || fail "$name is not set. Pass it to the container (docker run -e $name=<origin>)."
  case "$value" in
    *"$newline"*) fail "$name must be a single-line origin." ;;
  esac
  if ! printf '%s\n' "$value" | grep -Eq "^(https://$host$port|http://$loopback$port)\$"; then
    fail "$name must be an origin: https://host[:port] (http only for localhost, 127.0.0.1 or [::1]), with no path, query, fragment or credentials."
  fi
  portnumber="${value##*:}"
  case "$portnumber" in
    *[!0-9]*) ;;
    *) [ "$portnumber" -le 65535 ] || fail "$name has a port above 65535." ;;
  esac
done
