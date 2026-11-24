#!/usr/bin/env bash
# Asserts the behaviour of a frontend or admin runtime image (deploy/docs/images.md, "What CI builds
# and asserts"). Run from anywhere with Docker running:
#   bash .github/scripts/check-web-image.sh <image> frontend|admin
# The containers publish on 127.0.0.1 with free ports and are removed on exit.
set -euo pipefail

image="${1:?usage: check-web-image.sh <image> frontend|admin}"
app="${2:?usage: check-web-image.sh <image> frontend|admin}"
case "$app" in frontend | admin) ;; *) echo "The app must be frontend or admin." >&2; exit 2 ;; esac

work="$(mktemp -d)"
containers=()
cleanup() {
  if [ "${#containers[@]}" -gt 0 ]; then docker rm -f "${containers[@]}" > /dev/null 2>&1 || true; fi
  rm -rf "$work"
}
trap cleanup EXIT

passed=0
fail() {
  echo "FAIL: $*" >&2
  exit 1
}
pass() {
  passed=$((passed + 1))
  echo "ok: $*"
}
expect_eq() { # description, expected, actual
  [ "$2" = "$3" ] || fail "$1: expected '$2', got '$3'"
  pass "$1"
}

api_a='http://127.0.0.1:5999'
api_b='http://127.0.0.1:5998'
app_url='http://127.0.0.1:5997'
frontend_url='http://127.0.0.1:5996'

env_args() { # api base url
  local args=(-e "API_BASE_URL=$1" -e "APP_URL=$app_url")
  if [ "$app" = admin ]; then args+=(-e "FRONTEND_URL=$frontend_url"); fi
  printf '%s\n' "${args[@]}"
}

start() { # extra docker run args come first, then the env lines through env_args
  local name="$1" api="$2"
  shift 2
  local args=()
  while IFS= read -r line; do args+=("$line"); done < <(env_args "$api")
  local port
  port="$(free_port)"
  echo "$port" > "$work/port-$name"
  docker run -d --name "$name" -p "127.0.0.1:$port:8080" "${args[@]}" "$@" "$image" > /dev/null
  containers+=("$name")
}

# A free loopback port below the ephemeral range, picked here: on rootless Docker `docker port`
# reports a random port that isn't open, and so does any port in the ephemeral range.
free_port() {
  python3 -c '
import random, socket
while True:
    port = random.randint(20000, 29999)
    s = socket.socket()
    try:
        s.bind(("127.0.0.1", port))
    except OSError:
        continue
    print(port)
    break
'
}

base_url() { echo "http://127.0.0.1:$(cat "$work/port-$1")"; }

wait_ready() { # container
  local url
  url="$(base_url "$1")"
  for _ in $(seq 1 40); do
    if curl -fs -o /dev/null "$url/index.html"; then return 0; fi
    sleep 0.5
  done
  docker logs "$1" >&2 || true
  fail "$1 never answered /index.html"
}

# get <base> <path>: sets $status, writes $work/headers and $work/body.
get() {
  status="$(curl -s -D "$work/headers" -o "$work/body" -w '%{http_code}' "$1$2")"
}
header() { # name: the value of a response header of the last get, without the CR
  awk -v name="$(printf '%s' "$1" | tr '[:upper:]' '[:lower:]')" -F': ' \
    'tolower($1) == name { sub(/\r$/, "", $2); print $2 }' "$work/headers" | head -n 1
}

# 1. Users.
expect_eq "the image's user" 101 "$(docker image inspect -f '{{.Config.User}}' "$image")"

# 2. A health check exists.
health="$(docker image inspect -f '{{json .Config.Healthcheck}}' "$image")"
[ "$health" != null ] || fail "the image has no health check"
pass "the image has a health check"

# 3. The runtime user can't change the files it serves or runs.
for path in /etc/nginx /etc/nginx/conf.d /etc/nginx/templates /docker-entrypoint.d \
  /etc/nginx/conf.d/default.conf /etc/nginx/templates/index.html.template \
  /etc/nginx/templates/config.js.template /etc/nginx/nginx.conf /docker-entrypoint.d/05-check-settings.sh \
  /usr/share/nginx/html; do
  docker run --rm --entrypoint sh -e CHECK_PATH="$path" "$image" \
    -c 'test -e "$CHECK_PATH" && test ! -w "$CHECK_PATH"' || fail "the runtime user can write (or lacks) $path"
done
pass "the runtime user can't write the config, the templates, the guard, the html root or their folders"

# Nothing the runtime user serves or runs is writable by it. The built files keep their source
# modes (no --chmod), so a world-writable file in public/ would otherwise ship writable.
writable="$(docker run --rm --entrypoint sh "$image" -c \
  'find /usr/share/nginx/html /etc/nginx /docker-entrypoint.d -writable 2>/dev/null; true')"
[ -z "$writable" ] || fail "the runtime user can write: $writable"
pass "the runtime user can write nothing under the html root, /etc/nginx or /docker-entrypoint.d"

# The runtime user can read every served file and enter every directory (a stripped x bit on a
# directory makes nginx answer 404 for what is inside). `-readable` and `-executable` are GNU find.
unreadable="$(docker run --rm --entrypoint sh "$image" -c \
  'dirs="/usr/share/nginx/html /etc/nginx /docker-entrypoint.d"; find $dirs ! -readable 2>/dev/null; find $dirs -type d ! -executable 2>/dev/null; true')"
if [ -n "$unreadable" ]; then
  docker run --rm --entrypoint ls "$image" -laR /usr/share/nginx/html /etc/nginx >&2 || true
  fail "the runtime user can't read or enter: $unreadable"
fi
pass "the runtime user can read every served file and enter every directory"

# 4. Two containers of one image, two APIs.
start web-a "$api_a"
start web-b "$api_b"
wait_ready web-a
wait_ready web-b
expect_eq "the container's user id" 101 "$(docker exec web-a id -u)"
a="$(base_url web-a)"
b="$(base_url web-b)"

expected_config() {
  if [ "$app" = admin ]; then
    printf 'window.__WATTWISE_CONFIG__ = {"apiBaseUrl":"%s","frontendUrl":"%s"};' "$1" "$frontend_url"
  else
    printf 'window.__WATTWISE_CONFIG__ = {"apiBaseUrl":"%s"};' "$1"
  fi
}
for pair in "$a $api_a" "$b $api_b"; do
  set -- $pair
  get "$1" /config.js
  expect_eq "/config.js status of $2" 200 "$status"
  expect_eq "/config.js body of $2" "$(expected_config "$2")" "$(cat "$work/body")"
  expect_eq "/config.js cache of $2" no-cache "$(header cache-control)"
  case "$(header content-type)" in
    text/javascript* | application/javascript*) pass "/config.js content type of $2" ;;
    *) fail "/config.js content type: '$(header content-type)'" ;;
  esac
done

# 5. index.html, rendered with the container's APP_URL, for / , /index.html and client routes.
get "$a" /index.html
expect_eq "/index.html status" 200 "$status"
expect_eq "/index.html cache" no-cache "$(header cache-control)"
cp "$work/body" "$work/index.html"
grep -qF "<div id=\"root\">" "$work/index.html" || fail "/index.html is not the app's page"
grep -qF "property=\"og:url\" content=\"$app_url/\"" "$work/index.html" || fail "og:url is not $app_url/"
grep -qF "property=\"og:image\" content=\"$app_url/social-card.png\"" "$work/index.html" || fail "og:image is not $app_url/social-card.png"
grep -qF "name=\"twitter:image\" content=\"$app_url/social-card.png\"" "$work/index.html" || fail "twitter:image is not $app_url/social-card.png"
if grep -qF '${' "$work/index.html"; then fail "/index.html still holds a placeholder"; fi
pass "og:url, og:image and twitter:image hold the container's APP_URL"
for path in / /settings /a/b/c; do
  get "$a" "$path"
  expect_eq "$path status" 200 "$status"
  expect_eq "$path cache" no-cache "$(header cache-control)"
  cmp -s "$work/body" "$work/index.html" || fail "$path is not the rendered index.html"
  pass "$path serves the rendered index.html"
done

# nginx's own error page is gone from the image: /50x.html is just another client route.
get "$a" /50x.html
expect_eq "/50x.html status" 200 "$status"
cmp -s "$work/body" "$work/index.html" || fail "/50x.html is not the app's index.html"
pass "/50x.html falls back to the app's index.html"

# 6. Assets and the files that revalidate.
get "$a" /assets/missing-0000.js
expect_eq "a missing hashed asset" 404 "$status"
asset="$(grep -oE '/assets/[^"]+\.js' "$work/index.html" | head -n 1)"
[ -n "$asset" ] || fail "index.html names no hashed script"
get "$a" "$asset"
if [ "$status" != 200 ]; then
  docker exec web-a ls -la /usr/share/nginx/html /usr/share/nginx/html/assets >&2 || true
  docker logs web-a >&2 || true
fi
expect_eq "$asset status" 200 "$status"
expect_eq "$asset cache" "public, max-age=31536000, immutable" "$(header cache-control)"
get "$a" /sw.js
expect_eq "/sw.js status" 200 "$status"
expect_eq "/sw.js cache" no-cache "$(header cache-control)"
get "$a" /manifest.webmanifest
expect_eq "/manifest.webmanifest status" 200 "$status"
expect_eq "/manifest.webmanifest cache" no-cache "$(header cache-control)"
case "$(header content-type)" in
  application/manifest+json*) pass "/manifest.webmanifest content type" ;;
  *) fail "/manifest.webmanifest content type: '$(header content-type)'" ;;
esac

# 7. The container turns healthy.
for _ in $(seq 1 60); do
  [ "$(docker inspect -f '{{.State.Health.Status}}' web-a)" = healthy ] && break
  sleep 2
done
expect_eq "the container's health" healthy "$(docker inspect -f '{{.State.Health.Status}}' web-a)"

# 8. A start without a valid setting stops the container, with the setting named.
check_stops() { # description, then the environment as -e arguments
  local description="$1"
  shift
  local code=0 output
  output="$(timeout 60 docker run --rm "$@" "$image" 2>&1)" || code=$?
  [ "$code" -ne 0 ] || fail "$description: the container started"
  [ "$code" -ne 124 ] || fail "$description: the container did not stop"
  pass "$description: stops with exit code $code"
  printf '%s\n' "$output" | grep -q '05-check-settings.sh' || fail "$description: no message from the guard: $output"
}
good=(-e "API_BASE_URL=$api_a" -e "APP_URL=$app_url")
if [ "$app" = admin ]; then good+=(-e "FRONTEND_URL=$frontend_url"); fi
settings=(API_BASE_URL APP_URL)
if [ "$app" = admin ]; then settings+=(FRONTEND_URL); fi
for name in "${settings[@]}"; do
  without=()
  for ((i = 0; i < ${#good[@]}; i += 2)); do
    case "${good[i + 1]}" in "$name="*) ;; *) without+=("${good[i]}" "${good[i + 1]}") ;; esac
  done
  check_stops "$name missing" "${without[@]}"
  for bad in 'http://example.test' 'https://x.test/path' 'https://x.test/' 'https://u:p@x.test' 'https://x.test?q=1'; do
    with_bad=()
    for ((i = 0; i < ${#good[@]}; i += 2)); do
      case "${good[i + 1]}" in "$name="*) with_bad+=(-e "$name=$bad") ;; *) with_bad+=("${good[i]}" "${good[i + 1]}") ;; esac
    done
    check_stops "$name=$bad" "${with_bad[@]}"
  done
  empty=()
  for ((i = 0; i < ${#good[@]}; i += 2)); do
    case "${good[i + 1]}" in "$name="*) empty+=(-e "$name=") ;; *) empty+=("${good[i]}" "${good[i + 1]}") ;; esac
  done
  check_stops "$name empty" "${empty[@]}"
done

# 9. Without the entry point nothing is rendered: /config.js is a 404, not index.html.
port="$(free_port)"
echo "$port" > "$work/port-web-bare"
docker run -d --name web-bare -p "127.0.0.1:$port:8080" --entrypoint nginx "$image" -g 'daemon off;' > /dev/null
containers+=(web-bare)
bare="$(base_url web-bare)"
for _ in $(seq 1 20); do curl -s -o /dev/null "$bare/config.js" && break; sleep 0.5; done
get "$bare" /config.js
expect_eq "/config.js without rendering" 404 "$status"

echo "All $passed checks passed for $image ($app)."
