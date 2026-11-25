#!/usr/bin/env bash
# Asserts the behaviour of deploy/provision/dev-vm.sh (deploy/docs/environments.md, "The check") on a
# throwaway Fedora container that runs systemd as PID 1 with sshd, firewalld and cockpit, like a Server
# install, and on two Docker networks that use only documentation address ranges. Run from anywhere
# with Docker running:
#   bash .github/scripts/check-dev-vm.sh
# It tests the committed HEAD of the repository this script is in (a clone), not the working tree. The
# SSH key is generated into a temporary folder and never leaves it. It needs a Docker that allows
# privileged containers: rootful (CI) or rootless (a developer machine) both do.
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
work="$(mktemp -d)"
suffix="$$"
vm="wattwise-dev-vm-check-$suffix"
lan_client="wattwise-dev-vm-lan-client-$suffix"
other_client="wattwise-dev-vm-other-client-$suffix"
lan_net="wattwise-dev-vm-lan-$suffix"
other_net="wattwise-dev-vm-other-$suffix"
home_volume="wattwise-dev-vm-home-$suffix"
image=wattwise-dev-vm-check
# The two networks. 192.0.2.0/24 and 198.51.100.0/24 are documentation ranges (RFC 5737).
lan_cidr=192.0.2.0/24
other_cidr=198.51.100.0/24
# A port in the fixed range, below the ephemeral one, that the check listens on to prove the firewall.
test_port=18080
dev_home=/home/dev
script="$dev_home/dev-vm.sh"

cleanup() {
  docker rm -f "$vm" "$lan_client" "$other_client" > /dev/null 2>&1 || true
  docker network rm "$lan_net" "$other_net" > /dev/null 2>&1 || true
  docker volume rm "$home_volume" > /dev/null 2>&1 || true
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

x_root() { docker exec "$vm" "$@"; }
x_dev() { docker exec -u dev -e HOME="$dev_home" -w "$dev_home" "$vm" "$@"; }
# `systemctl --user` and rootless Docker need the user's runtime folder and bus, which a login has.
x_user() {
  docker exec -u dev -e HOME="$dev_home" -w "$dev_home" -e XDG_RUNTIME_DIR=/run/user/1000 \
    -e DBUS_SESSION_BUS_ADDRESS=unix:path=/run/user/1000/bus "$vm" "$@"
}
fw() { x_root firewall-cmd "$@"; }
client_ssh() { # client container, then the ssh arguments; no prompt, no host key bookkeeping
  local client="$1"
  shift
  docker exec "$client" ssh -o BatchMode=yes -o StrictHostKeyChecking=no -o UserKnownHostsFile=/dev/null \
    -o LogLevel=ERROR -o ConnectTimeout=10 "$@"
}
port_open() { # client container, address, port
  docker exec "$1" nc -z -w 3 "$2" "$3" > /dev/null 2>&1
}
net_ip() { docker inspect -f "{{(index .NetworkSettings.Networks \"$2\").IPAddress}}" "$1"; }

run_script() { # log file, then dev-vm.sh's arguments; SSH_CONN, when set, is the session's SSH_CONNECTION
  local log="$1" env_args=()
  shift
  if [ -n "${SSH_CONN:-}" ]; then env_args=(-e "SSH_CONNECTION=$SSH_CONN"); fi
  docker exec -u dev -e HOME="$dev_home" -w "$dev_home" "${env_args[@]}" "$vm" bash "$script" "$@" < /dev/null > "$log" 2>&1
}
run_script_rc() { # log file, then arguments; prints the exit code
  local rc=0
  run_script "$@" || rc=$?
  echo "$rc"
}
no_change() { # description, log file
  if grep -E '^provision: .*: changed' "$2"; then fail "$1: a step reported 'changed' (lines above)"; fi
  pass "$1 reports no 'changed'"
}

snapshot() { # prints what a rerun must leave as it was
  # shellcheck disable=SC2016
  x_root bash -c '
    echo "rpm:"; rpm -qa | sort
    echo "repos:"; sha256sum /etc/yum.repos.d/* | sort
    echo "sshd_config.d:"; sha256sum /etc/ssh/sshd_config.d/* | sort; stat -c "%n %a %U %G" /etc/ssh/sshd_config.d/*
    echo "firewalld:"; find /etc/firewalld -type f -exec sha256sum {} + | sort
    echo "user units:"; find /home/dev/.config/systemd -type f -exec sha256sum {} + 2>/dev/null | sort
    echo "docker client:"; find /home/dev/.docker -type f -exec sha256sum {} + 2>/dev/null | sort
    echo "linger:"; ls /var/lib/systemd/linger 2>/dev/null
    echo "cockpit:"; systemctl is-enabled cockpit.socket 2>&1 || true
    echo "runtime firewall:"; firewall-cmd --get-default-zone; firewall-cmd --get-active-zones
    echo "services:"; systemctl is-active sshd firewalld 2>&1 || true
  ' 2>&1
}

wait_for() { # description, seconds, command...
  local description="$1" seconds="$2" i
  shift 2
  for ((i = 0; i < seconds; i++)); do
    if "$@" > /dev/null 2>&1; then return 0; fi
    sleep 1
  done
  fail "timed out waiting for $description"
}
booted() {
  case "$(x_root systemctl is-system-running 2> /dev/null || true)" in running | degraded) return 0 ;; *) return 1 ;; esac
}

# --- The machine ----------------------------------------------------------------------------------

echo "Building the dev-vm-check image"
# The committed HEAD, as a clone: this worktree's .git can be a pointer file.
git clone -q --no-local "$root" "$work/src"
chmod 755 "$work"
chmod -R a+rX "$work/src"
docker build -q -t "$image" - < "$work/src/.github/dev-vm-check/Dockerfile" > /dev/null
[ -x "$work/src/deploy/provision/dev-vm.sh" ] || fail "deploy/provision/dev-vm.sh is not executable in the committed HEAD"
pass "deploy/provision/dev-vm.sh is executable"

# A throwaway key; the VM accepts its public half.
ssh-keygen -q -t ed25519 -N "" -C dev-vm-check -f "$work/key"

docker network create --subnet "$lan_cidr" "$lan_net" > /dev/null
docker network create --subnet "$other_cidr" "$other_net" > /dev/null
docker volume create "$home_volume" > /dev/null
# systemd as PID 1 needs a privileged container with its own cgroup namespace, and tmpfs on /run and
# /tmp; /home is a volume so the nested Docker's storage isn't overlay on overlay.
docker run -d --name "$vm" --privileged --cgroupns=private --network "$lan_net" \
  --tmpfs /run:exec,mode=755 --tmpfs /tmp:exec,mode=1777 -v "$home_volume:/home" "$image" > /dev/null
docker network connect "$other_net" "$vm"
docker run -d --name "$lan_client" --network "$lan_net" "$image" sleep infinity > /dev/null
docker run -d --name "$other_client" --network "$other_net" "$image" sleep infinity > /dev/null
for client in "$lan_client" "$other_client"; do
  docker exec "$client" mkdir -p /root/.ssh
  docker cp "$work/key" "$client:/root/.ssh/id_ed25519"
  docker exec "$client" chmod 600 /root/.ssh/id_ed25519
done
wait_for "systemd to boot" 90 booted
pass "the machine boots with systemd as PID 1"

vm_lan_ip="$(net_ip "$vm" "$lan_net")"
vm_other_ip="$(net_ip "$vm" "$other_net")"
lan_client_ip="$(net_ip "$lan_client" "$lan_net")"
other_client_ip="$(net_ip "$other_client" "$other_net")"

install_authorized_keys() { # key file
  docker exec -i -u dev -e HOME="$dev_home" "$vm" sh -c 'umask 077 && mkdir -p ~/.ssh && cat > ~/.ssh/authorized_keys' < "$1"
}
install_authorized_keys "$work/key.pub"
docker cp "$work/src/deploy/provision/dev-vm.sh" "$vm:$script"
x_root chown dev:dev "$script"
expect_eq "cockpit.socket starts enabled, like a Server install" enabled "$(x_root systemctl is-enabled cockpit.socket)"
expect_eq "the firewall starts in the Server zone" FedoraServer "$(fw --get-default-zone)"

# --- Refusals: each stops before anything changes -----------------------------------------------

snap0="$(snapshot)"
expect_refused() { # description, expected exit code, message fragment, then dev-vm.sh's arguments
  local description="$1" want="$2" fragment="$3" log="$work/refusal.log" rc
  shift 3
  rc="$(run_script_rc "$log" "$@")"
  expect_eq "$description exits $want" "$want" "$rc"
  grep -qF -- "$fragment" "$log" || {
    cat "$log" >&2
    fail "$description: the message doesn't contain '$fragment'"
  }
  if grep -E '^provision: .*: changed' "$log"; then fail "$description: a step reported 'changed'"; fi
  expect_eq "$description changes nothing" "$snap0" "$(snapshot)"
}
expect_refused "no --lan" 2 "--lan is required"
expect_refused "--lan without a value" 2 "Usage"  --lan
expect_refused "--lan twice" 2 "more than once" --lan "$lan_cidr" --lan "$other_cidr"
expect_refused "an unknown option" 2 "unknown option" --bogus
expect_refused "a malformed network" 1 "--lan" --lan 192.0.2.0/33
expect_refused "text instead of a network" 1 "--lan" --lan not-a-network
expect_refused "an address without a prefix length" 1 "--lan" --lan 192.0.2.0
expect_refused "a host bit set" 1 "--lan" --lan 192.0.2.1/24
expect_refused "all IPv4 addresses" 1 "every address" --lan 0.0.0.0/0
expect_refused "all IPv6 addresses" 1 "every address" --lan ::/0
rc="$(SSH_CONN="$other_client_ip 50000 $vm_other_ip 22" run_script_rc "$work/refusal.log" --lan "$lan_cidr")"
expect_eq "an SSH session from outside --lan exits 1" 1 "$rc"
grep -qF "outside --lan" "$work/refusal.log" || fail "the outside-session message doesn't say so"
expect_eq "an SSH session from outside --lan changes nothing" "$snap0" "$(snapshot)"

rc=0
docker exec -u 0 -e HOME=/root "$vm" bash "$script" --lan "$lan_cidr" < /dev/null > "$work/refusal.log" 2>&1 || rc=$?
expect_eq "running as root exits 1" 1 "$rc"
grep -qF "not as root" "$work/refusal.log" || fail "the root message doesn't say so"
expect_eq "running as root changes nothing" "$snap0" "$(snapshot)"

: > "$work/empty"
printf '# a comment, no key\n\n' > "$work/comment-only"
for keyless in empty comment-only; do
  install_authorized_keys "$work/$keyless"
  rc="$(run_script_rc "$work/refusal.log" --lan "$lan_cidr")"
  expect_eq "an authorized_keys file with no key ($keyless) exits 1" 1 "$rc"
  grep -qF "holds no key line" "$work/refusal.log" || fail "the no-key message doesn't say so"
  expect_eq "an authorized_keys file with no key ($keyless) changes nothing" "$snap0" "$(snapshot)"
done
rm -f "$work/empty" "$work/comment-only"
x_root rm -f "$dev_home/.ssh/authorized_keys"
rc="$(run_script_rc "$work/refusal.log" --lan "$lan_cidr")"
expect_eq "a missing authorized_keys file exits 1" 1 "$rc"
install_authorized_keys "$work/key.pub"
# sshd's StrictModes would ignore the key when the file or its folder is writable by the group.
for loose in ".ssh/authorized_keys" ".ssh"; do
  x_dev chmod g+w "$dev_home/$loose"
  rc="$(run_script_rc "$work/refusal.log" --lan "$lan_cidr")"
  expect_eq "a group-writable ~/$loose exits 1" 1 "$rc"
  grep -qF "chmod go-w $dev_home/$loose" "$work/refusal.log" || fail "the message for a group-writable ~/$loose doesn't give the chmod command"
  expect_eq "a group-writable ~/$loose changes nothing" "$snap0" "$(snapshot)"
  x_dev chmod g-w "$dev_home/$loose"
done

# --- Runs one and two ----------------------------------------------------------------------------

# The first run comes from inside --lan, like a real SSH session, so the guard's allowing side runs.
echo "First run (installs Docker; this takes a few minutes)"
rc="$(SSH_CONN="$lan_client_ip 50000 $vm_lan_ip 22" run_script_rc "$work/run1.log" --lan "$lan_cidr")"
cat "$work/run1.log"
expect_eq "run 1 exits 0" 0 "$rc"
snap1="$(snapshot)"

rc="$(run_script_rc "$work/run2.log" --lan "$lan_cidr")"
cat "$work/run2.log"
expect_eq "run 2 exits 0" 0 "$rc"
no_change "run 2" "$work/run2.log"
snap2="$(snapshot)"
if [ "$snap1" != "$snap2" ]; then
  diff <(echo "$snap1") <(echo "$snap2") | head -20 || true
  fail "run 2 changed the packages, the repos, sshd's drop-ins, firewalld's files, the user's units or ~/.docker"
fi
pass "run 2 leaves the rpm packages, yum repos, sshd drop-ins, /etc/firewalld, the user's systemd units and ~/.docker as they were"

# --- sshd -----------------------------------------------------------------------------------------

x_root sshd -t || fail "sshd -t fails"
pass "sshd -t passes"
effective="$(x_root sshd -T)"
for expected in 'passwordauthentication no' 'kbdinteractiveauthentication no' 'permitrootlogin no' \
  'pubkeyauthentication yes' 'authenticationmethods publickey'; do
  grep -qxF "$expected" <<< "$effective" || fail "sshd -T lacks '$expected'"
done
pass "sshd -T shows the five values"
expect_eq "the drop-in is mode 600, owned by root" "600 root root" "$(x_root stat -c '%a %U %G' /etc/ssh/sshd_config.d/00-dev-vm.conf)"
expect_eq "sshd is active" active "$(x_root systemctl is-active sshd)"

for method in password keyboard-interactive; do
  out="$(client_ssh "$lan_client" -o PubkeyAuthentication=no -o "PreferredAuthentications=$method" "dev@$vm_lan_ip" true 2>&1 < /dev/null || true)"
  case "$out" in *"Permission denied (publickey)"*) ;; *) fail "a $method login didn't end with 'Permission denied (publickey)': $out" ;; esac
  pass "a $method login from the LAN is refused with 'Permission denied (publickey)'"
done
expect_eq "the key login from the LAN succeeds" ok "$(client_ssh "$lan_client" -i /root/.ssh/id_ed25519 "dev@$vm_lan_ip" echo ok < /dev/null)"

# --- firewalld ------------------------------------------------------------------------------------

expect_eq "the default zone is drop" drop "$(fw --get-default-zone)"
zone_all="$(fw --zone=dev-vm-ssh --list-all)"
field() { sed -n "s/^  $1: \{0,1\}\(.*\)\$/\1/p" <<< "$zone_all"; }
expect_eq "zone dev-vm-ssh has exactly the LAN as its source" "$lan_cidr" "$(field sources)"
expect_eq "zone dev-vm-ssh allows exactly ssh" ssh "$(field services)"
for what in interfaces ports protocols source-ports forward-ports icmp-blocks 'rich rules'; do
  expect_eq "zone dev-vm-ssh has no $what" "" "$(field "$what")"
done
expect_eq "the active zones are exactly drop and dev-vm-ssh" "dev-vm-ssh drop" \
  "$(fw --get-active-zones | grep -E '^[^[:space:]]' | awk '{print $1}' | sort | tr '\n' ' ' | sed 's/ $//')"

x_root bash -c "nohup nc -lk 0.0.0.0 $test_port > /dev/null 2>&1 &"
wait_for "the test listener" 10 x_root bash -c "ss -Hltn | grep -q ':$test_port '"
pass "a test listener is up on all addresses at port $test_port"
port_open "$lan_client" "$vm_lan_ip" 22 || fail "port 22 isn't reachable from the LAN"
pass "port 22 connects from the LAN"
if port_open "$lan_client" "$vm_lan_ip" "$test_port"; then fail "the test port is reachable from the LAN"; fi
pass "the listener on all addresses doesn't connect from the LAN"
if port_open "$other_client" "$vm_other_ip" 22; then fail "port 22 is reachable from outside the LAN"; fi
pass "port 22 doesn't connect from another network"
if port_open "$other_client" "$vm_other_ip" "$test_port"; then fail "the test port is reachable from another network"; fi
pass "the listener doesn't connect from another network"

# --- Docker ---------------------------------------------------------------------------------------

case "$(x_user docker info --format '{{.SecurityOptions}}')" in *rootless*) pass "docker info lists rootless" ;; *) fail "docker info doesn't list rootless" ;; esac
expect_eq "the Docker context is rootless" rootless "$(x_user docker context show)"
expect_eq "the user's docker.service is enabled" enabled "$(x_user systemctl --user is-enabled docker.service)"
expect_eq "linger is on" Linger=yes "$(x_root loginctl show-user dev -p Linger)"
for unit in docker.service docker.socket; do
  state="$(x_root systemctl is-enabled "$unit" 2>&1 || true)"
  expect_eq "the system $unit is disabled" disabled "$state"
  if x_root systemctl is-active --quiet "$unit"; then fail "the system $unit is active"; fi
done
x_user docker compose version > /dev/null || fail "docker compose version fails"
pass "docker compose version works"

# --- cockpit ----------------------------------------------------------------------------------------

expect_eq "cockpit.socket is disabled" disabled "$(x_root systemctl is-enabled cockpit.socket 2>&1 || true)"
if x_root systemctl is-active --quiet cockpit.socket; then fail "cockpit.socket is active"; fi
pass "cockpit.socket is not active"

# --- Another network replaces the source ------------------------------------------------------

rc="$(run_script_rc "$work/run3.log" --lan "$other_cidr")"
cat "$work/run3.log"
expect_eq "run 3 with another network exits 0" 0 "$rc"
grep -qx 'provision: firewall: changed' "$work/run3.log" || fail "run 3 didn't report the firewall as changed"
pass "run 3 reports the firewall as changed"
for step in docker-repo packages sshd cockpit docker-system linger docker-rootless docker-context; do
  grep -qx "provision: $step: unchanged" "$work/run3.log" || fail "run 3 didn't report $step as unchanged"
done
pass "run 3 reports every other step as unchanged"
zone_all="$(fw --zone=dev-vm-ssh --list-all)"
expect_eq "the zone's only source is now the other network" "$other_cidr" "$(field sources)"
port_open "$other_client" "$vm_other_ip" 22 || fail "port 22 isn't reachable from the new network"
if port_open "$lan_client" "$vm_lan_ip" 22; then fail "port 22 is still reachable from the old network"; fi
pass "port 22 connects from the new network and no longer from the old one"

rc="$(run_script_rc "$work/run4.log" --lan "$other_cidr")"
expect_eq "run 4 exits 0" 0 "$rc"
no_change "run 4" "$work/run4.log"

# An interrupted earlier run: the running firewall lost the zone's source while the permanent
# configuration is right. A rerun repairs it (and says so), and a further run is unchanged.
fw --zone=dev-vm-ssh --remove-source="$other_cidr" > /dev/null
expect_eq "the runtime zone lost its source" "" "$(fw --zone=dev-vm-ssh --list-sources)"
rc="$(run_script_rc "$work/run5.log" --lan "$other_cidr")"
expect_eq "run 5 after a runtime-only loss exits 0" 0 "$rc"
grep -qx 'provision: firewall: changed' "$work/run5.log" || fail "run 5 didn't report the firewall as changed"
expect_eq "run 5 restores the source" "$other_cidr" "$(fw --zone=dev-vm-ssh --list-sources)"
expect_eq "run 5 leaves the default zone drop" drop "$(fw --get-default-zone)"
port_open "$other_client" "$vm_other_ip" 22 || fail "port 22 isn't reachable after the repair"
pass "run 5 repairs the runtime zone and port 22 connects again"
rc="$(run_script_rc "$work/run6.log" --lan "$other_cidr")"
expect_eq "run 6 exits 0" 0 "$rc"
no_change "run 6" "$work/run6.log"

echo "check-dev-vm: $passed checks passed in ${SECONDS}s"
