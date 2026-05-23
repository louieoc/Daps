# DAPS — Claude Code Instructions

## What DAPS Is

DAPS (Docker Assisted Portable Sovereignty) is a CLI + conventions framework for non-technical creative people who want to own and control their web presence. Target persona: an artist, blogger, or musician who wants to spin up a website, back it up, and move it to a different host — without learning Docker.

DAPS comprises a CLI for executing workflows, host definitions for targeting hosting providers that support OpenStack, and one or more projects. A project could be a custom web application, a Wordpress site, or one of any number of things for which a Docker image is available.

The CLI is called **Dapsman**, written in C# (.NET 9). Keep it in C#.

It's ok to refer to it as Daps (capitalized instead of all caps). It is open source, hosted on Codeberg right now, maybe Github later.

## Environments

### Workstation / local / dev

Assume the user has a desktop or laptop computer, i.e. their workstation.

Daps is meant to be OS-agnostic but is being developed in Windows, and needs testing in Mac OS and Linux.

The workstation has Docker Desktop and git installed.

It's unclear how Daps will be distributed to users but for now assume it's cloned from a Daps public git repository.

Dapsman runs on the workstation's OS. The `local build` workflow installs Daps and projects onto the local Docker instance. This local docker environment is also referred to as "dev" as it's intended for development.

It's possible that the local environment could be used for "production" applications that aren't meant for deployment to a remote server (e.g. personal finance software), but we haven't explored that yet.


#### Toolkit Container

The toolkit container (`daps-toolkit-1`) is an Ubuntu 22.04 container with OpenStack/SSH tools. It runs on the local Docker instance and is the execution context for all remote operations. SSH keys live at `~/.ssh/` inside the container.


### Remote / prod

Regarding projects that are meant ultimately to be accessible on the public internet, they must be deployed to a remote host. The remote docker environment is referred to as "prod."

The `prod build` workflow sets up Docker on the remote host.

The `prod deploy` workflow copies projects to containers on the remote Docker.

For simplicity Daps supports only one remote environment (prod) but we should remain open to someday supporting additional remote "qa" or "stage" environments.


## Repository Layout

```
daps/
  caddy/              # Caddyfile (imported by Caddy container)
  caddy_sites/        # Runtime caddy site files (gitignored contents, populated by local build)
  docker/             # DAPS compose files (compose_daps.yaml, compose_daps.dev.yaml)
  hosting/            # Provider-specific config (openrc files, instance vars)
  secrets/ssh/        # SSH keys (gitignored)
  src/                # Dapsman C# solution
  templates/          # Project templates (e.g. templates/wordpress/)
  tests/              # xUnit tests
  daps.yaml           # Project and hosts registry
```

Each project lives in its own directory (on the workstation it's typically a sibling of daps/) and is registered in `daps.yaml`.

## Project Folder Conventions

Every project folder uses underscore-prefixed subfolders to alphabetize infrastructure files away from project source:

```
myproject/
  _caddy_sites/       # *.dev.caddy, *.prod.caddy
  _docker/            # compose files, image-exports/ (image-exports is gitignored)
  _scripts/           # automation scripts (see naming below)
  _secrets/           # secret text files (gitignored)
  wp-content/         # (WordPress template) bind-mounted into container
```

## Hosting Files Naming Convention

Currently we only support OpenStack VPS hosting. We want to support other kinds of hosting in the future.

Assume the user has to open an account with their preferred hosting provider and download an OpenRC file. They should save the file to the `daps/hosting` folder.

The user also must define the host in the `daps.yaml` file and reference the OpenRC file.

OpenStack requires users to select OS images and make other choices when creating an instance. Those choices are saved in a file `daps/hosting/openstack_example_instance_vars.sh` where "example" is the name of the host, matching the name in `daps.yaml`.


## Script Naming Convention

Scripts in `_scripts/` follow an environment suffix convention:

| Suffix | Behaviour |
|--------|-----------|
| `*.dev.sh` | Local/workstation only. Run by `dapsman local build` (e.g. `prerequisites.dev.sh`). Never uploaded to remote. |
| `*.toolkit.sh` | Run from the toolkit container only. Never uploaded to remote. Never auto-run by Dapsman. |
| `*.prod.sh` | Uploaded to remote server and run there. Never run locally. |
| `*.sh` (bare) | All environments. Run locally by `dapsman local build` AND uploaded to remote and run there. Also used for library scripts sourced by other scripts (e.g. `generate-secrets.sh`). |

Examples:
- `prerequisites.dev.sh` — generates secrets locally before local build
- `prerequisites.prod.sh` — generates secrets on remote before prod deploy; sources `generate-secrets.sh`
- `generate-secrets.sh` — shared library sourced by prerequisite scripts; uploaded to remote alongside `prerequisites.prod.sh`
- `init-template.toolkit.sh` — one-time init script run from toolkit; never uploaded or auto-run
- `backup-remote.toolkit.sh` — TODO: backup script run from toolkit

Dapsman discovers `prerequisites.sh` (all environments) and `prerequisites.dev.sh` (local only) by name for local build. It uploads all `*.sh` and `*.prod.sh` files (excluding `*.dev.sh` and `*.toolkit.sh`) during prod deploy, and runs `prerequisites.sh` and `prerequisites.prod.sh` if present.

## Compose File Naming

| Filename pattern | Used by |
|-----------------|---------|
| `compose_daps.yaml` | DAPS shared services (Caddy) |
| `compose_daps.dev.yaml` | DAPS local dev (Toolkit) |
| `compose_<name>.yaml` | Project shared (dev + prod) |
| `compose_<name>.dev.yaml` | Project local dev overrides |
| `compose_<name>.prod.yaml` | Project prod overrides |
| `compose_daps_<name>.dev.yaml` | Toolkit volume mounts for project (inside project's `_docker/`) |

Paths inside compose files resolve relative to the compose file's directory (`_docker/`). So `../_secrets/` and `../wp-content` correctly point to the project root. Use `dapsman`-generated absolute paths with `-f` flags.

## daps.yaml

Minimal project registry. Most config is convention-driven.

```yaml
openstack:
  ramnode:
    openrc: ./hosting/ramnode_openrc

projects:
  myproject:
    path: ../myproject
```

`path` is relative to `daps.yaml`. Dapsman resolves it to an absolute path at runtime.

## Dapsman Architecture (C# DDD)

**Layers:** Domain → Application → Infrastructure → CLI. Dependencies flow inward only.

- **Domain** — pure data: Plans (`LocalBuildPlan`, `RemoteDeployPlan`, `CaddyRestartPlan`, `InitPlan`, etc.)
- **Application** — services + interfaces. Services take interfaces; no Infrastructure types.
- **Infrastructure** — implementations: plan builders, runners, executors
- **CLI** (`Program.cs`) — wires everything together; no business logic

### The Plan Pattern

Every workflow follows: `CreatePlan()` → print plan → `Execute()`. `--dry-run` prints the plan and skips execution.

### IBashRunner

```csharp
interface IBashRunner {
    void RunScript(string scriptPath, string workingDirectory, string? arguments = null, bool interactive=false);
    void RunShell(string shellExpression, string workingDirectory, bool interactive=false);
}
```

| Implementation | Context |
|---------------|---------|
| `WorkstationBashRunner` | Local workstation (Git Bash on Windows). `RunShell` sets `MSYS_NO_PATHCONV=1` to prevent path mangling. The `interactive` parameter is ignored. |
| `ContainerBashRunner(containerName)` | Inside a Docker container via `docker exec -i` for non-interactive execution and `docker exec -it` for interactive with TTY. |

### Secret Files on Remote

Secret files in `_secrets/` are bind-mounted into containers. They must be `chmod 644` (not 600) so `www-data` inside the container can read them. Docker secrets daemon handles permissions automatically, but manual bind-mount doesn't.


## CLI Command Structure

```
dapsman init --template <name> --name <project-name> [--path <dest>] [--dry-run]
dapsman local build [--build] [--dry-run] [--project <name>...] [--config <path>]
dapsman local caddy restart [--dry-run] [--config <path>]
dapsman prod build [--dry-run] [--provider <name>] [--toolkit-container <name>] ...
dapsman prod deploy [--dry-run] [--project <name>...] [--provider <name>] ...
dapsman prod caddy restart [--dry-run] [--provider <name>] [--toolkit-container <name>]
```

Commands use `group action` token pairs (e.g. `local build`, `prod deploy`) except `init` which is a single token.

## Init / Template Convention

`dapsman init` copies a template directory and runs `_scripts/init-template.toolkit.sh <project-name>` inside the copy. Dapsman is template-agnostic — all placeholder replacement and file renaming logic lives in `init-template.toolkit.sh`, not in Dapsman. This keeps template-specific logic in the template.

## WordPress Template

Located at `templates/wordpress/`. Placeholder project name is `mywpsite`. The init script replaces all occurrences of `mywpsite` with the new project name.

Key details:
- 10 WordPress secrets in `_secrets/*.txt`, generated by `prerequisites.dev.sh` (locally) and `prerequisites.prod.sh` (remotely)
- `generate-secrets.sh` is a shared library sourced by both prerequisite scripts
- Redis Object Cache requires `WP_REDIS_HOST=redis` and `WP_REDIS_PORT=6379` set via `WORDPRESS_CONFIG_EXTRA` in both dev and prod compose
- `wp-content/` is bind-mounted in dev; lives at `/srv/projects/<name>/wp-content` in prod
- `.localhost` TLD for dev (not `.local` — mDNS on Windows intercepts `.local`)

## Security Observations

Notes on security trade-offs in the current design. DAPS is a local dev tool for trusted single-user environments, so some of these are acceptable for now but worth revisiting if the context changes.

- **Docker socket mount in toolkit (`/var/run/docker.sock`)** — The toolkit container has the host Docker socket mounted so it can run `docker exec` against local containers (e.g. for DB sync scripts). This gives any process inside the toolkit root-equivalent access to the host: it can start containers, mount host paths, etc. Acceptable for a trusted local dev tool; would be a serious risk in a multi-user or shared environment.
- **Secret files on remote are `chmod 644`** — Required so `www-data` inside containers can read them via bind-mount. A proper secrets manager (e.g. Docker secrets with swarm, Vault) would be better long-term, but manual bind-mount doesn't support it without this trade-off.
- **SSH keys in toolkit container** — Keys at `~/.ssh/` inside the toolkit are accessible to any process that can `docker exec` into it, which includes anyone with access to the Docker socket. Same trust boundary as above.
- **OpenRC files on disk** — OpenStack credentials (including cloud provider API tokens) are stored as plain files in `daps/hosting/`. They should remain gitignored and never committed.


## C# Formatting

- Use tabs for indentation in C# files, not spaces. Leave tab width to editor settings.

## Caddy Notes

- **`respond` directive does not set `Content-Type` automatically.** Always add `header Content-Type "text/html; charset=utf-8"` before `respond` when returning HTML, otherwise browsers display the markup as plain text. This applies to both generated offline responses (see `ConventionProjectStatusPlanBuilder.GenerateOfflineResponse`) and any hand-authored `.offline.caddy` files.

## Known Conventions in Practice

- `daps_net` is the shared external Docker network connecting Caddy to all project containers
- Remote paths mirror local: `_docker` stays `_docker`, not renamed to `docker`
- Caddy container name: `daps-caddy-1` (Docker Compose project name `daps`, service `caddy`)
- Caddy config path in container: `/etc/caddy/Caddyfile`
- Remote and toolkit project root: `/srv/projects/<name>/`
- Remote and toolkit DAPS root: `/srv/daps/`

## Docker Compose Conventions

These apply to all project compose files (templates and real projects):

- **Dev host ports must be bound to `127.0.0.1`** — e.g. `"127.0.0.1:8080:80"`, not `"8080:80"`. Prevents exposure to other machines on the local network.
- **`restart: unless-stopped`** — use this on all services, not `always` (which restarts even on deliberate `docker stop`) and not omitted (which means no restart on reboot).
- **Caddy reverse proxy uses a project-specific DNS alias, not the generic service name** — each project's web-facing service registers an alias on `daps_net` matching the project name (e.g. `mywpsite`), and the `.caddy` file uses `reverse_proxy mywpsite:80`. Using the raw service name (e.g. `wordpress`) would cause DNS collisions when multiple projects share `daps_net`.
- **Internal services (db, redis, cache) go on a project-internal network only** — they don't need to be on `daps_net` since only the web-facing service needs Caddy access. This prevents service name collisions and reduces the shared network surface. Name the internal network `<projectname>_net`.
- **Prod compose files do not expose host ports** — Caddy routes to containers via `daps_net`; host port bindings are not needed in prod and would expose services directly to the internet.
