#!/usr/bin/env bash
# Provisions a Fedora Server VM for the dev-VM role (tethys: Development, and Testing later): Docker CE
# in rootless mode for your own user, key-only SSH, and a firewalld setup that accepts only SSH from
# one LAN and drops everything else. It is safe to rerun: every step prints
# `provision: <step>: changed|unchanged`, and a rerun with the same arguments prints no `changed`.
# What it does and why is in deploy/docs/environments.md. It holds no address, host name or network
# detail: the LAN is its required argument, which you read from your private runbook.
#
# Copy it to the VM before any clone (it installs git itself) and run it as your own user, who needs
# sudo and a key in ~/.ssh/authorized_keys:
#   scp deploy/provision/dev-vm.sh <vm>:
#   ssh -t <vm> 'bash ~/dev-vm.sh --lan <LAN-CIDR>'
set -euo pipefail
# Tracing (`bash -x`, SHELLOPTS) is switched off for the whole script, like ./setup.
{ set +x; } 2> /dev/null

usage() {
  cat << 'EOF'
Usage: dev-vm.sh --lan <CIDR>

  --lan <CIDR>   The one network SSH is accepted from, written as its network address with a prefix
                 length. Required; 0.0.0.0/0 and ::/0 are refused.
  -h, --help     Show this help.
EOF
}

step() { printf 'provision: %s: %s\n' "$1" "$2"; }
die() {
  echo "provision: $*" >&2
  exit 1
}

lan_arg=""
lan_given=false
while [ "$#" -gt 0 ]; do
  case "$1" in
    --lan)
      [ "$#" -ge 2 ] || {
        usage >&2
        exit 2
      }
      [ "$lan_given" = false ] || {
        echo "provision: --lan is given more than once." >&2
        exit 2
      }
      lan_arg="$2"
      lan_given=true
      shift 2
      ;;
    -h | --help)
      usage
      exit 0
      ;;
    *)
      echo "provision: unknown option: $1" >&2
      usage >&2
      exit 2
      ;;
  esac
done
if [ "$lan_given" = false ]; then
  echo "provision: --lan is required." >&2
  usage >&2
  exit 2
fi

work=""
cleanup() {
  if [ -n "$work" ]; then rm -rf "$work"; fi
}
trap cleanup EXIT
work="$(mktemp -d)"

# --- 1. Guards: everything that can lock you out is checked before anything changes ---------------

# shellcheck disable=SC1091
os_id="$( (. /etc/os-release 2> /dev/null && printf '%s' "${ID:-}") || true)"
[ "$os_id" = fedora ] || die "this script supports Fedora only."
[ "$(id -u)" -ne 0 ] || die "run as your own user, not as root. The script uses sudo for the root steps."
[ -n "${HOME:-}" ] || die "HOME is not set."
command -v sudo > /dev/null 2>&1 || die "sudo is required."
command -v python3 > /dev/null 2>&1 || die "python3 is required (it validates --lan); Fedora Server has it."

# The network must be a network address with a prefix length, in its canonical form, and not all
# addresses. Exit codes: 3 = prefix length 0, 4 = not the canonical form (printed on stdout).
# shellcheck disable=SC2016
validate_lan='
import ipaddress, sys
text = sys.argv[1]
try:
    net = ipaddress.ip_network(text, strict=True)
except ValueError:
    sys.exit(1)
if net.prefixlen == 0:
    sys.exit(3)
if "/" not in text or str(net) != text:
    print(net)
    sys.exit(4)
print(net)
'
rc=0
lan="$(python3 -I -c "$validate_lan" "$lan_arg")" || rc=$?
case "$rc" in
  0) ;;
  3) die "--lan covers every address; SSH would be open to everyone. Give your LAN's network." ;;
  4) die "--lan must be written as the network address with a prefix length: $lan" ;;
  *) die "--lan is not a network address with a prefix length (a host bit is set, or it isn't an address)." ;;
esac

# An SSH session that comes from outside the network would lose its next connection to the firewall.
if [ -n "${SSH_CONNECTION:-}" ]; then
  client="${SSH_CONNECTION%% *}"
  # shellcheck disable=SC2016
  python3 -I -c '
import ipaddress, sys
address = ipaddress.ip_address(sys.argv[1])
if address.version == 6 and address.ipv4_mapped is not None:
    address = address.ipv4_mapped
sys.exit(0 if address in ipaddress.ip_network(sys.argv[2]) else 1)
' "$client" "$lan" || die "this SSH session comes from outside --lan (or its address can't be read): the firewall would cut off your next connection. Run the script from a machine on that network."
fi

# Key-only SSH locks you out unless a key is in place. A key line is a key type and its base64 body
# after any options; comments and blank lines don't count.
authorized_keys="$HOME/.ssh/authorized_keys"
if ! grep -vE '^[[:space:]]*(#|$)' "$authorized_keys" 2> /dev/null \
  | grep -qE '(^|[[:space:]])(ssh-(ed25519|rsa)|ecdsa-sha2-nistp(256|384|521)|sk-ssh-ed25519@openssh\.com|sk-ecdsa-sha2-nistp256@openssh\.com)[[:space:]]+[A-Za-z0-9+/=]{20,}'; then
  die "$authorized_keys holds no key line. Add your public key first: SSH would accept no login once passwords are off."
fi

user="$(id -un)"
uid="$(id -u)"
# Rootless Docker maps container users to the user's subordinate ids (Fedora's useradd adds them).
for subid_file in /etc/subuid /etc/subgid; do
  grep -qE "^($user|$uid):" "$subid_file" 2> /dev/null || die "$subid_file has no entry for $user; rootless Docker needs one."
done

# One sudo prompt, up front, before anything changes.
sudo -v || die "sudo failed."

# sshd's StrictModes ignores the key when the home folder, ~/.ssh or the file is writable by group or
# others or belongs to someone else. Every refusal comes first; only then are wrong SELinux labels on
# ~/.ssh, which stop sshd as well, fixed quietly (this is not a change the script reports).
for path in "$HOME" "$HOME/.ssh" "$authorized_keys"; do
  [ "$(stat -c %u "$path")" = "$(id -u)" ] || die "$path doesn't belong to you; sshd would ignore the key in it."
  [ $((8#$(stat -c %a "$path") & 8#022)) -eq 0 ] || die "$path is writable by group or others; sshd would ignore the key in it. Fix it: chmod go-w $path"
done
if command -v restorecon > /dev/null 2>&1; then sudo restorecon -R "$HOME/.ssh"; fi
step guards unchanged

# --- helpers ------------------------------------------------------------------------------------

# Installs a root-owned file with a mode from a source, when its content, mode or owner differ.
# Returns 0 when it wrote the file and 1 when it was already right. The target's old content, if
# any, goes to "$work/<name>.old" so a caller can restore it.
install_file() { # mode, source, target
  local mode="$1" source="$2" target="$3" old
  old="$work/$(basename "$target").old"
  rm -f "$old"
  if sudo test -e "$target"; then
    if sudo cmp -s "$source" "$target" && [ "$(sudo stat -c '%a %U %G' "$target")" = "$mode root root" ]; then
      return 1
    fi
    # The copy is the user's own file in "$work", so the redirect should not run as root.
    # shellcheck disable=SC2024
    sudo cat "$target" > "$old"
  fi
  sudo install -m "$mode" -o root -g root "$source" "$target"
  return 0
}

unit_exists() { systemctl cat "$1" > /dev/null 2>&1; }

# --- 2. Docker's package repo and the packages --------------------------------------------------

repo_file=/etc/yum.repos.d/docker-ce.repo
# shellcheck disable=SC2016
printf '%s\n' \
  '[docker-ce-stable]' \
  'name=Docker CE Stable - $basearch' \
  'baseurl=https://download.docker.com/linux/fedora/$releasever/$basearch/stable' \
  'enabled=1' \
  'gpgcheck=1' \
  'gpgkey=https://download.docker.com/linux/fedora/gpg' > "$work/docker-ce.repo"
if install_file 644 "$work/docker-ce.repo" "$repo_file"; then step docker-repo changed; else step docker-repo unchanged; fi

# git is for the clone that comes after this script; passt and fuse-overlayfs are what rootless Docker
# needs for its network and its storage.
packages=(git firewalld openssh-server passt fuse-overlayfs
  docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin docker-ce-rootless-extras)
missing=()
for package in "${packages[@]}"; do
  rpm -q --quiet "$package" || missing+=("$package")
done
if [ "${#missing[@]}" -gt 0 ]; then
  sudo dnf -y install "${missing[@]}"
  step packages changed
else
  step packages unchanged
fi

# --- 3. Key-only SSH ----------------------------------------------------------------------------

# 00- sorts ahead of 01-permitrootlogin.conf, 50-redhat.conf and 50-cloud-init.conf, and in sshd the
# first value wins. Forwarding stays on (Remote-SSH and the port forwards need it), and no AllowUsers.
sshd_dropin=/etc/ssh/sshd_config.d/00-dev-vm.conf
printf '%s\n' \
  '# Managed by deploy/provision/dev-vm.sh. Key-only SSH.' \
  'PasswordAuthentication no' \
  'KbdInteractiveAuthentication no' \
  'PermitRootLogin no' \
  'PubkeyAuthentication yes' \
  'AuthenticationMethods publickey' > "$work/00-dev-vm.conf"
ssh_changed=false
[ -d /etc/ssh/sshd_config.d ] || sudo install -d -m 755 -o root -g root /etc/ssh/sshd_config.d
# sshd -t needs host keys, which a VM whose sshd never ran may not have yet.
sudo ssh-keygen -A > /dev/null
if install_file 600 "$work/00-dev-vm.conf" "$sshd_dropin"; then ssh_changed=true; fi
# A mislabelled file is corrected whether or not it was just written; that is not a reported change.
if command -v restorecon > /dev/null 2>&1; then sudo restorecon -F "$sshd_dropin"; fi
if ! sudo sshd -t; then
  # Put the previous drop-in back, or remove the new one, so the running sshd's file is sound.
  if [ "$ssh_changed" = true ]; then
    if [ -e "$work/00-dev-vm.conf.old" ]; then
      sudo install -m 600 -o root -g root "$work/00-dev-vm.conf.old" "$sshd_dropin"
    else
      sudo rm -f "$sshd_dropin"
    fi
    die "sshd -t rejects the configuration; the drop-in was put back as it was."
  fi
  die "sshd -t rejects the configuration."
fi
if ! systemctl is-enabled --quiet sshd.service || ! systemctl is-active --quiet sshd.service; then
  sudo systemctl enable --now sshd.service
  ssh_changed=true
fi
# Every run, so a daemon that kept the old configuration after an interrupted reload gets the new one.
sudo systemctl reload sshd.service
sshd_effective="$(sudo sshd -T)"
for expected in 'passwordauthentication no' 'kbdinteractiveauthentication no' 'permitrootlogin no' \
  'pubkeyauthentication yes' 'authenticationmethods publickey'; do
  grep -qxF "$expected" <<< "$sshd_effective" || die "sshd's effective configuration lacks '$expected'; another drop-in or Match block wins."
done
if [ "$ssh_changed" = true ]; then step sshd changed; else step sshd unchanged; fi

# --- 4. Cockpit ---------------------------------------------------------------------------------

# It listens on every interface; the firewall blocks it already, and the VM is reached over SSH only.
if unit_exists cockpit.socket; then
  if systemctl is-enabled --quiet cockpit.socket || systemctl is-active --quiet cockpit.socket; then
    sudo systemctl disable --now cockpit.socket
    step cockpit changed
  else
    step cockpit unchanged
  fi
  if systemctl is-enabled --quiet cockpit.socket || systemctl is-active --quiet cockpit.socket; then
    die "cockpit.socket is still enabled or active."
  fi
else
  step cockpit unchanged
fi

# --- 5. Rootless Docker -------------------------------------------------------------------------

# The system daemon is off: rootful Docker writes its own firewall rules ahead of firewalld's zones.
system_docker_changed=false
for unit in docker.service docker.socket; do
  if unit_exists "$unit" && { systemctl is-enabled --quiet "$unit" || systemctl is-active --quiet "$unit"; }; then
    sudo systemctl disable --now "$unit"
    system_docker_changed=true
  fi
done
if [ "$system_docker_changed" = true ]; then step docker-system changed; else step docker-system unchanged; fi

# Linger keeps the user's systemd manager, and so the stack, running after the SSH session ends.
if [ -e "/var/lib/systemd/linger/$user" ]; then
  step linger unchanged
else
  sudo loginctl enable-linger "$user"
  step linger changed
fi

# `systemctl --user` needs the user's manager and its bus. An SSH login has both; this covers a
# session without them.
export XDG_RUNTIME_DIR="${XDG_RUNTIME_DIR:-/run/user/$uid}"
export DBUS_SESSION_BUS_ADDRESS="${DBUS_SESSION_BUS_ADDRESS:-unix:path=$XDG_RUNTIME_DIR/bus}"
# A DOCKER_HOST or DOCKER_CONTEXT from the environment would hide the context this script sets.
unset DOCKER_HOST DOCKER_CONTEXT
waited=0
manager_state=""
until [ "$manager_state" = running ] || [ "$manager_state" = degraded ]; do
  waited=$((waited + 1))
  [ "$waited" -le 60 ] || die "the user's systemd manager didn't start (is linger on?)."
  sleep 1
  manager_state="$(systemctl --user is-system-running 2> /dev/null || true)"
done

rootless_changed=false
if [ ! -e "$HOME/.config/systemd/user/docker.service" ] || ! docker context inspect rootless > /dev/null 2>&1; then
  dockerd-rootless-setuptool.sh install
  rootless_changed=true
fi
if ! systemctl --user is-enabled --quiet docker.service || ! systemctl --user is-active --quiet docker.service; then
  systemctl --user enable --now docker.service
  rootless_changed=true
fi
if [ "$rootless_changed" = true ]; then step docker-rootless changed; else step docker-rootless unchanged; fi

# A context, not DOCKER_HOST: a ~/.bashrc export doesn't reach VS Code's remote `docker exec` or
# `ssh <vm> 'docker ...'`, and the context lives in ~/.docker.
docker context inspect rootless > /dev/null 2>&1 || die "the 'rootless' Docker context doesn't exist; dockerd-rootless-setuptool.sh creates it."
if [ "$(docker context show)" != rootless ]; then
  docker context use rootless > /dev/null
  step docker-context changed
else
  step docker-context unchanged
fi

waited=0
until docker info > /dev/null 2>&1; do
  waited=$((waited + 1))
  [ "$waited" -le 60 ] || die "the rootless Docker daemon doesn't answer."
  sleep 1
done
docker info --format '{{.SecurityOptions}}' | grep -q rootless || die "docker info doesn't list rootless: the daemon isn't the rootless one."
docker compose version > /dev/null || die "docker compose version fails."

# --- 6. Firewall --------------------------------------------------------------------------------

# The end state, whatever the start: the default zone drops everything, and one zone whose only
# source is the LAN and whose only service is ssh accepts SSH. Source zones win over interface zones,
# so LAN packets reach the SSH zone. Last, so a failure above leaves the VM reachable.
fw() { sudo firewall-cmd "$@"; }
zone=dev-vm-ssh
firewall_changed=false
permanent_changed=false

if ! systemctl is-enabled --quiet firewalld.service || ! systemctl is-active --quiet firewalld.service; then
  sudo systemctl enable --now firewalld.service
  firewall_changed=true
fi
waited=0
until fw --state > /dev/null 2>&1; do
  waited=$((waited + 1))
  [ "$waited" -le 30 ] || die "firewalld doesn't answer."
  sleep 1
done

# The zone is written to the permanent configuration first, then loaded, then made the one that
# matters; the established SSH session survives the switch (connection tracking).
if ! fw --permanent --get-zones | tr ' ' '\n' | grep -qxF "$zone"; then
  fw --permanent --new-zone="$zone" > /dev/null
  permanent_changed=true
fi
out="$(fw --permanent --zone="$zone" --list-services)"
read -ra services <<< "$out"
for service in "${services[@]}"; do
  if [ "$service" != ssh ]; then
    fw --permanent --zone="$zone" --remove-service="$service" > /dev/null
    permanent_changed=true
  fi
done
if ! fw --permanent --zone="$zone" --query-service=ssh > /dev/null; then
  fw --permanent --zone="$zone" --add-service=ssh > /dev/null
  permanent_changed=true
fi
out="$(fw --permanent --zone="$zone" --list-sources)"
read -ra sources <<< "$out"
for source in "${sources[@]}"; do
  if [ "$source" != "$lan" ]; then
    fw --permanent --zone="$zone" --remove-source="$source" > /dev/null
    permanent_changed=true
  fi
done
if ! fw --permanent --zone="$zone" --query-source="$lan" > /dev/null; then
  fw --permanent --zone="$zone" --add-source="$lan" > /dev/null
  permanent_changed=true
fi
# A connection NetworkManager pins to a zone other than drop is cleared, so it follows the default
# zone (drop), also after a reboot. A machine without NetworkManager skips this.
if command -v nmcli > /dev/null 2>&1 && systemctl is-active --quiet NetworkManager.service; then
  out="$(sudo nmcli -t -f UUID connection show)"
  while IFS= read -r connection; do
    [ -n "$connection" ] || continue
    pinned="$(sudo nmcli -g connection.zone connection show "$connection")"
    if [ -n "$pinned" ] && [ "$pinned" != drop ]; then
      sudo nmcli connection modify "$connection" connection.zone ""
      permanent_changed=true
    fi
  done <<< "$out"
fi
# Any other interface pinned to another zone moves to drop.
out="$(fw --permanent --get-zones)"
read -ra permanent_zones <<< "$out"
for other_zone in "${permanent_zones[@]}"; do
  if [ "$other_zone" = drop ] || [ "$other_zone" = "$zone" ]; then continue; fi
  out="$(fw --permanent --zone="$other_zone" --list-interfaces)"
  read -ra interfaces <<< "$out"
  for interface in "${interfaces[@]}"; do
    fw --permanent --zone=drop --change-interface="$interface" > /dev/null
    permanent_changed=true
  done
done
# The running firewall must hold the zone before the default zone switches. An earlier run that
# stopped after writing the permanent configuration left it unloaded; a reload repairs that.
if [ "$permanent_changed" = false ] \
  && { ! fw --zone="$zone" --query-source="$lan" > /dev/null 2>&1 || ! fw --zone="$zone" --query-service=ssh > /dev/null 2>&1; }; then
  permanent_changed=true
fi
if [ "$permanent_changed" = true ]; then
  fw --reload > /dev/null
  firewall_changed=true
fi
if ! fw --zone="$zone" --query-source="$lan" > /dev/null 2>&1 || ! fw --zone="$zone" --query-service=ssh > /dev/null 2>&1; then
  die "the running firewall lacks zone $zone with --lan as its source and ssh after a reload; the default zone stays as it is."
fi

if [ "$(fw --get-default-zone)" != drop ]; then
  fw --set-default-zone=drop > /dev/null
  firewall_changed=true
fi
out="$(fw --get-zones)"
read -ra runtime_zones <<< "$out"
for other_zone in "${runtime_zones[@]}"; do
  if [ "$other_zone" = drop ] || [ "$other_zone" = "$zone" ]; then continue; fi
  out="$(fw --zone="$other_zone" --list-interfaces)"
  read -ra interfaces <<< "$out"
  for interface in "${interfaces[@]}"; do
    fw --zone=drop --change-interface="$interface" > /dev/null
    firewall_changed=true
  done
done

# End state: stop when it isn't the declared one.
[ "$(fw --get-default-zone)" = drop ] || die "the default zone is not drop."
[ "$(fw --zone="$zone" --list-services)" = ssh ] || die "zone $zone allows more than ssh, or not ssh."
[ "$(fw --zone="$zone" --list-sources)" = "$lan" ] || die "zone $zone's sources are not exactly --lan."
for what in ports protocols source-ports forward-ports icmp-blocks rich-rules interfaces; do
  [ -z "$(fw --zone="$zone" "--list-$what")" ] || die "zone $zone has $what; it must have none."
done
if fw --zone="$zone" --query-masquerade > /dev/null; then die "zone $zone masquerades."; fi
expected_zones="$(printf '%s\n' drop "$zone" | sort)"
active_zones="$(fw --get-active-zones | grep -E '^[^[:space:]]' | awk '{print $1}' | sort)"
[ "$active_zones" = "$expected_zones" ] || die "the active zones are not exactly drop and $zone: $(tr '\n' ' ' <<< "$active_zones")"
if [ "$firewall_changed" = true ]; then step firewall changed; else step firewall unchanged; fi

echo "provision: done. Clone the repo over HTTPS and run ./setup; docs/setup.md has the steps."
