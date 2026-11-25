# Environments

Where each environment runs and how a machine for it is set up. The whole-system summary is "Environments" and "Hosting and operations" in [technical.md](../../docs/technical.md); how the VMs are kept safe is "Deployment" in [security.md](../../docs/security.md#deployment). This file holds the details that belong to `deploy/`, starting with Development on tethys.

| Environment | Where                 | Set up by                                                                    |
| ----------- | --------------------- | ---------------------------------------------------------------------------- |
| Development | vidar, the laptop     | [setup.md](../../docs/setup.md) and `./setup`                                |
| Development | tethys, the dev VM    | [Development on tethys](#development-on-tethys): `dev-vm.sh`, then `./setup` |
| Testing     | tethys, next to above | E5C: the deploy agent; its VM provisioning will extend `dev-vm.sh`'s role    |
| Staging     | phoebe, the prod VM   | E5D                                                                          |
| Production  | phoebe, next to above | E5D                                                                          |

The repo holds no address, host name or network detail of any machine. The developer's private runbook has them, and `<LAN-CIDR>` below stands for the network written there.

## Development on tethys

tethys is a Fedora Server VM on saturn, the Proxmox server. The developer creates the VM, installs Fedora Server and puts a public key in the user's `~/.ssh/authorized_keys`. Everything after that is `deploy/provision/dev-vm.sh` and the steps in [setup.md](../../docs/setup.md#dev-vm-tethys). Nothing on tethys is reached except over SSH: the IDE connects with Remote-SSH, and the apps' ports reach vidar through SSH forwards.

### What the script does

`dev-vm.sh` is one self-contained bash file: the developer copies it to the VM before any clone, and it installs git itself. It runs as the developer's own user, not as root, and uses `sudo` for the root steps, because `systemctl --user` needs the user's session. It takes one required argument, `--lan <LAN-CIDR>`: the one network SSH is accepted from.

Every step prints `provision: <step>: changed|unchanged`, compares before it writes, and a rerun with the same argument prints no `changed`.

1. **Guards**, all before anything changes. It stops when: the machine isn't Fedora; it runs as root; `--lan` is missing, isn't a network address with a prefix length in its canonical form (checked with Python's `ipaddress`, strictly) or is `0.0.0.0/0` or `::/0`; `$SSH_CONNECTION` is set and the client is outside `--lan`; `~/.ssh/authorized_keys` holds no key line, or it, `~/.ssh` or the home folder is writable by group or others or isn't yours (sshd's `StrictModes` would ignore the key); or the user has no `/etc/subuid` and `/etc/subgid` entry. Each guard is a way to lock yourself out, or to start rootless Docker without what it needs. Before closing the session that ran the script, open a second SSH session with your key: that proves the key login works.
2. **`docker-repo` and `packages`.** Docker's signed RPM repo (`download.docker.com/linux/fedora`, `gpgcheck=1`), written from fixed content with `install -m 644` (no `dnf config-manager`, which needs `dnf5-plugins`). Then git, firewalld, openssh-server, `slirp4netns`, `fuse-overlayfs` and Docker CE: `docker-ce`, `docker-ce-cli`, `containerd.io`, `docker-buildx-plugin`, `docker-compose-plugin` and `docker-ce-rootless-extras`.
3. **`sshd`.** One drop-in, `/etc/ssh/sshd_config.d/00-dev-vm.conf` (mode 600, root; `restorecon` when SELinux tools exist). It sets `PasswordAuthentication no`, `KbdInteractiveAuthentication no`, `PermitRootLogin no`, `PubkeyAuthentication yes` and `AuthenticationMethods publickey`. The `00-` prefix matters: in sshd the first value wins, and it must sort ahead of `01-permitrootlogin.conf` (the Server installer can write one), `50-redhat.conf` and `50-cloud-init.conf`. It sets no `AllowTcpForwarding`, `AllowAgentForwarding` or `AllowUsers`: Remote-SSH and the forwards need forwarding, and each extra rule is one more way to lock the developer out. The script runs `sshd -t` before it reloads sshd (and puts the old drop-in back if it fails), then reads the effective values with `sshd -T` and stops when one differs.
4. **`cockpit`.** `cockpit.socket` is disabled when it exists: it listens on every interface, and nobody uses it.
5. **`docker-system`, `linger`, `docker-rootless`, `docker-context`.** The system `docker.service` and `docker.socket` are disabled. `loginctl enable-linger` keeps the user's systemd manager, and so the stack, running after the SSH session ends. `dockerd-rootless-setuptool.sh install` runs only when the user unit is missing, then `systemctl --user enable --now docker`, then `docker context use rootless`. Before the daemon first starts, a user drop-in `docker.service.d/10-dev-vm-network.conf` pins the network and port drivers ([why](#why-rootless-docker)); when it changes, the daemon restarts, and the script stops unless the running rootlesskit has `--net=slirp4netns` and `--port-driver=builtin`. A context, not `DOCKER_HOST`: a `~/.bashrc` export doesn't reach VS Code's remote `docker exec` or `ssh tethys 'docker …'`. At the end it checks that `docker info` lists `rootless` and `docker compose version` works.
6. **`firewall`**, last, so a failure above leaves the VM reachable. See below.

### Why rootless Docker

Same as vidar, and the reason is the stack's bind mount ([development-stack.md](development-stack.md#the-tree-and-what-the-containers-write)): on rootless Docker a container's uids map to subuids that own none of the developer's files, so no container can write the checkout or `.git`. Rootful Docker isn't supported for this stack. The other reason is the firewall: rootful Docker writes its own nftables rules ahead of firewalld's zones, so "only SSH from the LAN" would rest on the loopback binds alone. A rootless port is bound by the user's own rootlesskit process, which the firewall filters like any other.

The drivers are pinned to `slirp4netns` and `builtin` because the default can't be trusted for this. Without slirp4netns installed, Docker picks `pasta`, whose default port driver (`implicit`) forwards every port bound in the container namespace on all host addresses and ignores the host IP of a published port: `-p 127.0.0.1:5432:5432` would listen on every address, and the firewall would be the only guard. The `builtin` port driver binds the host IP it is given, so "every published port binds to loopback" holds (the check publishes one and reads `ss -Htln`). Docker access isn't root-equivalent here, so there is no `docker` group, and the token file is as safe as the developer's SSH key ([security.md](../../docs/security.md#deployment)). This covers Development only; Testing's deploy agent (E5C) decides its own mode.

### The firewall

The end state, declared and checked, whatever the start:

- the default zone is `drop`;
- a zone `dev-vm-ssh` whose only source is `--lan` and whose only service is `ssh` (a rerun with another network replaces the source);
- any interface pinned to another zone moves to `drop`;
- `firewall-cmd --get-active-zones` shows only those two zones, or the script stops.

A source zone takes precedence over an interface zone, so packets from the LAN reach `dev-vm-ssh` and everything else is dropped. That also removes the Server install's `cockpit` and `dhcpv6-client` services; firewalld's base rules still accept IPv6 router and neighbour advertisements, so SLAAC keeps working. The new zone is written and loaded before the default zone switches, and the SSH session that runs the script survives because connection tracking keeps established connections. The script reloads firewalld when the permanent configuration changed or when the running firewall lacks the zone (an interrupted earlier run), and stops before the default zone switches if the zone still isn't loaded. A connection that NetworkManager pins to another zone has its zone cleared, so it follows `drop` after a reboot too. IPv4 DHCP and IPv6 SLAAC keep working, but replies of stateful DHCPv6 are dropped, so tethys needs IPv4 or SLAAC addressing. Besides the firewall, every port the stack publishes binds to loopback, which `./setup` enforces for all nine services ([development-stack.md](development-stack.md#published-ports)).

### Reaching the apps

The apps listen on tethys's loopback. vidar reaches them through SSH forwards, to its own `http://localhost`: the refresh cookie is `Secure` and the service worker needs a secure context, and `http://localhost` is one. No Cloudflare route leads to Development.

The vault is shared, so vidar's and tethys's ports are the same. That means vidar's own stack must be stopped first: `docker compose -f deploy/docker-compose.development.yml stop api jobs frontend admin mailpit lgtm`. Read the ports from tethys's rendered file, not from here:

```bash
ssh tethys "grep -E '^(API|FRONTEND|ADMIN|MAILPIT|GRAFANA)_PORT=' ~/watt-wise/deploy/.env"
```

Then add a `LocalForward` line for each to vidar's `~/.ssh/config`:

```
Host tethys
    HostName <from the private runbook>
    User <your user>
    ExitOnForwardFailure yes
    LocalForward <API_PORT> 127.0.0.1:<API_PORT>
    LocalForward <FRONTEND_PORT> 127.0.0.1:<FRONTEND_PORT>
    LocalForward <ADMIN_PORT> 127.0.0.1:<ADMIN_PORT>
    LocalForward <MAILPIT_PORT> 127.0.0.1:<MAILPIT_PORT>
    LocalForward <GRAFANA_PORT> 127.0.0.1:<GRAFANA_PORT>
```

`ssh -N tethys` opens the forwards. `ExitOnForwardFailure` makes a port that vidar already holds fail loudly. Open an app at its `url` from `deploy/.env` (the Api's CORS allows exactly that origin). Registering sends the confirmation email to Mailpit on tethys, so the Mailpit forward is how you read it ([mailpit.md](mailpit.md)).

**Installing the PWA.** The dev server registers no service worker, so Chrome offers no install against it. An installable PWA means the frontend's runtime image on the same port: stop the `frontend` service on tethys, build the image and run it with the command in [images.md](images.md#running-an-app-image-against-development), install from vidar's Chrome through the forward, and start the service again.

### VS Code

Remote-SSH to tethys and open `~/watt-wise`; the extensions install on the remote. Debugging over it is in [debugging.md](debugging.md#remote-tethys).

### The check

`.github/scripts/check-dev-vm.sh` runs the script on a throwaway Fedora container (`.github/dev-vm-check/Dockerfile`: systemd as PID 1, sshd, firewalld set to the Server zone, cockpit, and no git or Docker) joined to two Docker networks of documentation addresses (`192.0.2.0/24` as `--lan`, `198.51.100.0/24` as another network). It tests the committed HEAD, and CI's `dev-vm` job runs it. It asserts:

- each refusal (no `--lan`, a malformed network, a host bit set, `0.0.0.0/0`, `::/0`, root, a session from outside `--lan`, no key line) exits non-zero and changes nothing;
- the first run exits 0, a second prints no `changed`, and the packages, repos, sshd drop-ins, `/etc/firewalld`, the user's units and `~/.docker` are unchanged;
- `sshd -t` passes, `sshd -T` shows the five values, a password and a keyboard-interactive login from the LAN end in `Permission denied (publickey)` and the key login works;
- the default zone is `drop`, `dev-vm-ssh` has exactly the source and `ssh`, only the two zones are active, port 22 connects from the LAN, a listener on every address at a test port does not, and port 22 does not connect from the other network;
- `docker info` as the user lists `rootless`, the context is `rootless`, the user unit is enabled, linger is on, the system `docker.service` and `docker.socket` are disabled and `docker compose version` works;
- `cockpit.socket` is disabled;
- a third run with the other network replaces the source (`changed`, and the networks swap roles), and a fourth is unchanged.

The Dockerfile gives the test user a subordinate id range that fits inside the 65536 ids a rootless Docker host hands the container, and makes `newuidmap` and `newgidmap` setuid because their file capabilities don't survive a pull on rootless Docker; both belong to the test machine, not the script.

What a container can't prove, and the developer checks on tethys: SELinux labels on a real Server install, real LAN filtering on the VM's network card, linger surviving a logout and a reboot, the guards over a real SSH session, NetworkManager's zone pinning (the container has none; check `firewall-cmd --get-active-zones` after a reboot), an IPv6 `--lan` and IPv4-mapped clients, and the stack's own listeners (`ss -Htln` shows only SSH off loopback).

### Troubleshooting

| Problem                                        | Fix                                                                                                                                                                                                                                                                                                                                                                                                                                                                             |
| ---------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| The script stops at a guard                    | The message names it. A session from outside `--lan` needs a machine on that network; a missing key needs `ssh-copy-id` before the script.                                                                                                                                                                                                                                                                                                                                      |
| Locked out after the script                    | Use the hypervisor's console for the VM, remove `/etc/ssh/sshd_config.d/00-dev-vm.conf` or fix the key, and run the script again.                                                                                                                                                                                                                                                                                                                                               |
| Locked out by the firewall (a wrong `--lan`)   | On the hypervisor's console run `sudo firewall-cmd --set-default-zone=FedoraServer`, then run the script again with the right `--lan`.                                                                                                                                                                                                                                                                                                                                          |
| A stack port on `0.0.0.0` in `ss -ltn`         | The daemon runs pasta with the implicit port driver ([why](#why-rootless-docker)). Check `~/.config/systemd/user/docker.service.d/10-dev-vm-network.conf` and `slirp4netns`, then run the script again or `systemctl --user restart docker`. A run that changes the drop-in on a machine with a running stack restarts the daemon, which stops the stack's containers (start them again with `up`); a rerun restarts nothing. On another machine, add the same drop-in by hand. |
| `Cannot connect to the Docker daemon` over SSH | `docker context show` must print `rootless`; if not, run the script again. Linger must be on (`loginctl show-user $USER -p Linger`).                                                                                                                                                                                                                                                                                                                                            |
| A forward fails with "address already in use"  | vidar's own stack, or another forward, holds the port: stop the stack's services first.                                                                                                                                                                                                                                                                                                                                                                                         |
| The first `up` on tethys is slow               | Same as vidar: it builds the images and initializes the database ([development-stack.md](development-stack.md#first-start)).                                                                                                                                                                                                                                                                                                                                                    |
