# Environments

Where each environment runs. The whole-system summary is "Environments" and "Hosting and operations" in [technical.md](../../docs/technical.md); how the deployed machines are kept safe is "Deployment" in [security.md](../../docs/security.md#deployment). This file holds the details that belong to `deploy/`, starting with Development on a remote machine.

| Environment | Where                                    | Set up by                                                             |
| ----------- | ---------------------------------------- | --------------------------------------------------------------------- |
| Development | A local machine, any OS                  | [setup.md](../../docs/setup.md): `mise run setup`, then `mise run up` |
| Development | A remote machine or VM, reached over SSH | The same, on that machine; [A remote machine](#a-remote-machine)      |
| Testing     | tethys, the dev VM                       | E5C: the deploy agent                                                 |
| Staging     | phoebe, the prod VM                      | E5D                                                                   |
| Production  | phoebe, next to Staging                  | E5D                                                                   |

The repo holds no address, host name or network detail of any machine. Which machine plays which role is the developer's own record (the private runbook, and for agents `~/.config/watt-wise/machines.md` when it exists). Testing, Staging and Production name their hosts because the deploy targets are fixed; Development doesn't.

## A remote machine

A Development stack can run on a machine other than the one you type on: a VM, a home server. Nothing on it is reached except over SSH: the IDE connects with Remote-SSH (or any remote-capable editor), and the apps' ports reach the local machine through SSH forwards. Setting it up is the same as for a local machine ([setup.md](../../docs/setup.md)): clone over HTTPS (never put a private key on it), `mise run setup`, `mise run up`. Setup asks once for the service-account token ([setup.md](../../docs/setup.md#service-account-for-1password)); over SSH use a terminal (`ssh -t`) so it can prompt.

**Hardening is the machine owner's job, and this repo doesn't script it.** What a remote machine should have, so the stack's loopback binds aren't the only guard: key-only SSH (no passwords, no root login; in sshd the first value of a setting wins, so a drop-in that sorts before the distribution's own files), a firewall that drops everything but SSH from the home network, and no unused listener (Cockpit on Fedora Server, for one). Setup doesn't change any of it; it enforces only that every port the stack publishes binds to loopback ([development-stack.md](development-stack.md#published-ports)). Check the listeners with `ss -Htln`: only SSH may listen off loopback.

### Why rootless Docker

Rootless Docker is the recommended mode on Linux, and the Fedora profile of setup checks it (the user's service, linger, `/etc/subuid`, the `rootless` context) and reports the gaps without changing them; rootful Docker is left alone. The reason is the stack's bind mount ([development-stack.md](development-stack.md#the-tree-and-what-the-containers-write)): on rootless Docker a container's uids map to subuids that own none of the developer's files, so no container can write the checkout or `.git`. On a remote machine it also matters for the firewall: rootful Docker writes its own nftables rules ahead of firewalld's zones, so "only SSH from the home network" would rest on the loopback binds alone. A rootless port is bound by the user's own rootlesskit process, which the firewall filters like any other. Docker access isn't root-equivalent in this mode, so there is no `docker` group, and the token file is as safe as the developer's SSH key ([security.md](../../docs/security.md#deployment)).

Pin rootless Docker's network and port drivers to `slirp4netns` and `builtin` when you set it up on a machine that must bind to loopback, because the default can't be trusted for this: without slirp4netns installed Docker picks `pasta`, whose default port driver (`implicit`) forwards every port bound in the container namespace on all host addresses and ignores the host IP of a published port, so `-p 127.0.0.1:5432:5432` would listen on every address. The `builtin` port driver binds the host IP it is given. Setup doesn't check the drivers; `ss -Htln` shows the result. A user systemd drop-in on `docker.service` sets them (`DOCKERD_ROOTLESS_ROOTLESSKIT_NET=slirp4netns`, `DOCKERD_ROOTLESS_ROOTLESSKIT_PORT_DRIVER=builtin`, per Docker's [rootless guide](https://docs.docker.com/engine/security/rootless/)). This covers Development only; Testing's deploy agent (E5C) decides its own mode.

### Reaching the apps

The apps listen on the remote machine's loopback. The local machine reaches them through SSH forwards, to its own `http://localhost`: the refresh cookie is `Secure` and the service worker needs a secure context, and `http://localhost` is one. No Cloudflare route leads to Development.

The vault is shared, so the local and the remote stack publish the same ports. That means the local stack must be stopped first: `docker compose -f deploy/docker-compose.development.yml stop api jobs frontend admin mailpit lgtm`. Read the ports from the remote machine's rendered file, not from here:

```bash
ssh <host> "grep -E '^(API|FRONTEND|ADMIN|MAILPIT|GRAFANA)_PORT=' ~/watt-wise/deploy/.env"
```

Then add a `LocalForward` line for each to the local `~/.ssh/config`:

```
Host <host>
    HostName <from the private runbook>
    User <your user>
    ExitOnForwardFailure yes
    LocalForward <API_PORT> 127.0.0.1:<API_PORT>
    LocalForward <FRONTEND_PORT> 127.0.0.1:<FRONTEND_PORT>
    LocalForward <ADMIN_PORT> 127.0.0.1:<ADMIN_PORT>
    LocalForward <MAILPIT_PORT> 127.0.0.1:<MAILPIT_PORT>
    LocalForward <GRAFANA_PORT> 127.0.0.1:<GRAFANA_PORT>
```

`ssh -N <host>` opens the forwards. `ExitOnForwardFailure` makes a port that the local machine already holds fail loudly. Open an app at its `url` from `deploy/.env` (the Api's CORS allows exactly that origin). Registering sends the confirmation email to Mailpit on the remote machine, so the Mailpit forward is how you read it ([mailpit.md](mailpit.md)).

**Installing the PWA.** The dev server registers no service worker, so Chrome offers no install against it. An installable PWA means the frontend's runtime image on the same port: stop the `frontend` service on the remote machine, build the image and run it with the command in [images.md](images.md#running-an-app-image-against-development), install from the local Chrome through the forward, and start the service again.

### The IDE

With VS Code, Remote-SSH to the remote machine and open its checkout; the extensions install on the remote. Debugging over it is in [debugging.md](debugging.md#remote-machine). Another editor connects its own way; nothing here needs VS Code.

### Troubleshooting

| Problem                                        | Fix                                                                                                                                                            |
| ---------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| A stack port on `0.0.0.0` in `ss -ltn`         | The daemon runs pasta with the implicit port driver ([why](#why-rootless-docker)). Set the drivers and run `systemctl --user restart docker`, then `up` again. |
| `Cannot connect to the Docker daemon` over SSH | `docker context show` must print `rootless`, and linger must be on (`loginctl show-user $USER -p Linger`): the Fedora profile of setup lists the fix.          |
| A forward fails with "address already in use"  | The local stack, or another forward, holds the port: stop the stack's services first.                                                                          |
| The first `up` on the remote machine is slow   | Same as locally: it builds the images and initializes the database ([development-stack.md](development-stack.md#first-start)).                                 |
