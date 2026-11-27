# Debugging

How debugging works against the Development stack ([development-stack.md](development-stack.md)). No IDE is required: the committed `.vscode/` is an optional convenience for VS Code, and [Other editors](#other-editors) says what the others need. Which config to start and how: "Debugging" in [development.md](../../docs/development.md#debugging). The configs are in `.vscode/launch.json`; `.vscode/extensions.json` recommends the C# extension (`ms-dotnettools.csharp`) and Command Variable (`rioj7.command-variable`).

## .NET

`Api (container)` and `Jobs (container)` are `coreclr` attach configs. They attach to the host that `dotnet watch` already runs in the `api` or `jobs` container; they never start one.

### vsdbg

The debugger is Microsoft's `vsdbg`, which the C# extension drives over a pipe. The `dev` targets of the Api and Jobs images hold it at `/vsdbg` ([images.md](images.md)), root-owned and read-only for the container's user.

- **Pin:** `VSDBG_VERSION`, `VSDBG_BASE_URL`, `VSDBG_SHA256_AMD64` and `VSDBG_SHA256_ARM64` in a `vsdbg` stage of `backend/src/WattWise.Api/Dockerfile` and `backend/src/WattWise.Jobs/Dockerfile`. The stage sits ahead of `restore`, so a `.csproj` change doesn't download it again. The base URL is the per-release one that `getvsdbg.sh` uses for `-v latest`; the build downloads `vsdbg-linux-x64.tar.gz` or `vsdbg-linux-arm64.tar.gz` from it by `TARGETARCH`, and checks the archive's sha256 and the extracted `version.txt` (its first field, exactly).
- **No `getvsdbg.sh` at build:** the script downloads whatever is current and takes an exact version only from a short retained list, so the build wouldn't be reproducible or checked.
- **amd64 and arm64:** an empty `TARGETARCH` (the classic builder) is amd64; any other value fails the stage. The arm64 archive is untested: the development machine and CI are x64.
- **Dev targets only:** `build` and `runtime` never hold it. The dev images are never published, because vsdbg's license allows it only with Visual Studio or VS Code: that limits `vsdbg` to those two, so the other editors use another debugger ([Other editors](#other-editors)).
- The Cli has no image: it runs on the host, where an IDE attaches to it like to any host process.

#### Bumping vsdbg

Run `curl -fsSL https://aka.ms/getvsdbgsh` and read the `latest` version and its meta URL in the script. Download both archives (`vsdbg-linux-x64.tar.gz` and `vsdbg-linux-arm64.tar.gz`) from that URL's folder, compute each `sha256sum`, and set the four `ARG` values in both Dockerfiles. Build both `dev` targets: the version check in the stage fails when `version.txt` differs.

### The pipe transport

`pipeTransport` runs `docker exec -i <container> /vsdbg/vsdbg`, with the fixed names `wattwise-development-api-1` and `wattwise-development-jobs-1` (project `wattwise-development`). `docker exec` runs as the container's user (1654, like the host), so vsdbg and the host share a user, which the runtime's debugger pipe needs.

### The process

The configs pick the process with `processName` (`WattWise.Api`, `WattWise.Jobs`), not `dotnet watch`: `dotnet watch` runs the host as a child process, and the debugger must attach to the host, not the watcher.

### Source paths

The PDBs carry the container's paths (`/src/backend/...`), so `sourceFileMap` maps `/src` to `${workspaceFolder}`. That holds only when VS Code has open the worktree that last ran `up`: the stack bind-mounts that worktree ([one stack per machine](development-stack.md#one-stack-per-machine)). `justMyCode` is on, so framework frames stay out of the way.

### A restart ends the session

When `dotnet watch` restarts the host (a rude edit, a `.csproj` rebuild), the process the debugger was attached to ends and so does the session. Attach again.

## Browser

`Frontend (Chrome)`, `Admin (Chrome)` and the two Edge twins are js-debug `launch` configs: they open a browser on the app's dev server in the `frontend` or `admin` container and debug the app's TypeScript there.

### Profile

js-debug starts the browser with its own profile, not yours, so you sign in again and each app's refresh cookie lives in that profile. The dev server registers no service worker (`devOptions` is off in `vite-plugin-pwa`), so nothing caches old code between runs.

### The URL

`url` is `${input:frontendUrl}` or `${input:adminUrl}`: the `FRONTEND_URL` or `ADMIN_URL` line of `deploy/.env`, read by the Command Variable extension. The port comes from 1Password through `mise run setup`, so it can't be written into `launch.json`, and `launch.json` can't read a file by itself. The extension reads the value after the first `=`, quotes included, and a `transform` strips them. The whole origin is used, because the Api's CORS allows exactly that origin. `${workspaceFolder}` in `fileName` needs no `variableSubstArgs`: `file.content` expands variables in `fileName` itself. A missing key opens `http://deploy-env-missing.invalid` (the browser shows a name-not-resolved error); a missing file makes VS Code report a Command Variable error. Either way, run `mise run setup`.

Without the extension, the input fails. Install the recommended one, or replace `${input:…}` in your local copy with the URL and don't commit it.

### Source paths

Vite in the container serves app modules at `<origin>/src/...`, relative to the app folder, so `webRoot` is `${workspaceFolder}/frontend` (`admin`). Workspace packages come at `<origin>/@fs/src/packages/<pkg>/src/...` (`/src` is the repo root in the container), which js-debug doesn't resolve by itself, so `pathMapping` maps `/@fs/src/` to `${workspaceFolder}/`. The config also lists `/` for the app folder, which js-debug adds from `webRoot` anyway; js-debug tries the longest key first, so `/@fs/src/` wins for package files. Each module's inline source map lists only the file name, so sources resolve against the module's URL and no `sourceMapPathOverrides` is needed.

### Edge

The Edge configs are `msedge` twins with the same settings. They are untested.

## Other editors

The stack's side is editor-neutral: the containers run `dotnet watch` and the Vite servers on the published ports. What differs is the debugger.

- **JetBrains Rider** attaches with its own debugger (Attach to Process in the container, or a remote debugger over its own tooling), not `vsdbg`. Not set up or tested here.
- **vim and others** use `netcoredbg` (Samsung's open-source debugger) through a DAP client, placed in the container by the developer. Not set up or tested here.
- **The browser side** needs no editor: Chrome's or Edge's own DevTools debug the TypeScript through the source maps Vite serves.

The `.vscode` configs stay the only maintained ones.

## Remote machine

To debug a stack that runs on a remote machine ([environments.md](environments.md#a-remote-machine)), VS Code opens the checkout there over Remote-SSH, so the extensions install on the remote and every config above works as written.

- The .NET configs' `docker exec` runs on the remote machine, where the containers are.
- The browser configs open the browser on the local machine (js-debug's companion extension) at the `FRONTEND_URL` or `ADMIN_URL` of the remote's `deploy/.env`, which the SSH forward makes a local `http://localhost` port. Start the forwards first.
- Set `remote.autoForwardPorts` to `false`: VS Code would add a second forward of the same ports on another local port, and the Api's CORS allows only the `url` origin.

## Troubleshooting

| Symptom                                              | Cause and fix                                                                                                   |
| ---------------------------------------------------- | --------------------------------------------------------------------------------------------------------------- |
| The attach fails, the container isn't found          | The stack isn't up: run the `up -d --wait` command in [development.md](../../docs/development.md).              |
| "Unable to find process"                             | The host is still building or has restarted: wait for it to listen (`logs -f api`), then attach again.          |
| Breakpoints are hollow and never hit                 | VS Code is open on another worktree than the stack's: open the one that last ran `up`.                          |
| Name-not-resolved error, or a Command Variable error | `deploy/.env` lacks `FRONTEND_URL` or `ADMIN_URL` (the first) or is missing (the second): run `mise run setup`. |
| Breakpoints in a package file stay unbound           | Check the `/@fs/src/` `pathMapping`, or VS Code is open on another worktree than the stack's.                   |
