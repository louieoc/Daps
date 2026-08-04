---
title: CLI Commands
sidebar:
  order: 7
---

## Dapsman Workflows

All commands support `--dry-run`, which prints the plan without executing anything. `--config <path>` overrides the default `daps.yaml` location (defaults to the current directory).

### `dapsman init`
```
dapsman init --template <name> --name <project-name>|--project <name> [--overlay] [--prod-url <domain>] [--path <destination>] [--dry-run] [--config <path>]
```
Creates a new project from a template. Copies the template directory to the destination (defaults to a sibling of the daps folder), runs `_scripts/init-template.toolkit.sh <project-name>` inside the copy if present (template-specific placeholder replacement lives there, not in dapsman), and registers the project in `daps.yaml`. If `--prod-url` is given (e.g. `--prod-url mysite.com`), the production domain is written into `_caddy_sites/*.prod.caddy` automatically, saving a manual edit step.

`--overlay` adds DAPS infrastructure files into an **existing** directory instead of creating a new one — required for templates like `astro` where the project directory pre-exists. In overlay mode the destination must already exist, and any files that already exist there are skipped (never overwritten).

### `dapsman local build`
```
dapsman local build [--build] [--rebuild [--yes]] [--dry-run] [--project <name>...] [--config <path>]
```
Brings up the local dev Docker environment. This combines, for the local environment, what are handled separately for prod as "provision" and "deploy" workflows. Steps: syncs project caddy site files to `caddy_sites/`, runs each project's prerequisite scripts (e.g. `prerequisites.sh`, `prerequisites.dev.sh`), composes DAPS services (Caddy, toolkit), then composes each project's containers. `--build` forces Docker image rebuilds. Omitting `--project` runs all configured projects (except those with `disabled: true` in `daps.yaml`).

`--rebuild` starts the selected projects over from scratch: before composing them back up it runs `docker compose down -v`, destroying their containers **and named volumes**. Use it when a project's data volume is unusable — most commonly a database whose data directory was left half-written by an interrupted first build, which shows up as a container restart loop rather than a clean error. It implies `--build`, requires `--project` (so a stray `--rebuild` can never wipe every project at once), and prompts for a typed `yes` before doing anything. `--yes` skips that prompt for scripted use.

`--rebuild` only touches the projects you name — DAPS shared services (Caddy, toolkit) and their volumes are left alone. It destroys data with no backup step, so use `dapsman local restore` afterwards if you need the contents back.

### `dapsman local caddy restart`
```
dapsman local caddy restart [--dry-run] [--config <path>]
```
Reloads Caddy's configuration on the local Docker instance (`docker exec daps-caddy-1 caddy reload`). Useful after changing site files without doing a full local build.

### `dapsman prod provision`
```
dapsman prod provision [--dry-run] [--provider <name>] [--upgrade] [--set-vars-script <path>] [--create-script <path>] [--config <path>]
```
Sets up Docker on a remote host from the toolkit container. Behavior depends on the provider's type in `daps.yaml`:
- **`openstack` providers** — creates a new instance. Creates an SSH keypair if one doesn't exist, uploads the public key to OpenStack, and runs the instance creation script (`scripts/openstack-create-instance.sh`). Run this once when setting up a new hosting environment.
- **`generic-vps` providers** — configures an existing host (e.g. a VPS from a provider with no creation API, like a bare-metal OVHCloud instance) rather than creating one. Creates an SSH keypair if one doesn't exist, copies it to the host with `ssh-copy-id` (prompts for the host's password interactively — the password is never stored by Daps), then installs Docker and sets up swap via `scripts/provision-generic-vps.sh`. Safe to re-run; skips key setup once a working key is in place. `--upgrade` additionally runs `apt-get upgrade -y` on the host.

`--set-vars-script` / `--create-script` only apply to `openstack` providers.

### `dapsman prod deploy`
```
dapsman prod deploy [--dry-run] [--project <name>...] [--provider <name>] [--build] [--set-vars-script <path>] [--config <path>]
```
Deploys projects to the remote server. Steps: builds Docker image tarballs via `build-docker-images.toolkit.sh` (if needed), uploads Caddyfile, caddy site files, compose files, image tarballs, and project scripts to the remote via SCP from the toolkit, starts containers on the remote via SSH, then reloads Caddy. Omitting `--project` deploys all configured projects.

Image build behavior:
- **No existing tar** — build runs automatically (first deploy or after `dapsman local teardown`).
- **Tar exists, no flag** — build is skipped; the existing tar is reused. Use this for deploys where the Dockerfile hasn't changed.
- **`--build`** — forces a rebuild even if a tar already exists. Use after changing the Dockerfile.

### `dapsman prod caddy restart`
```
dapsman prod caddy restart [--dry-run] [--provider <name>] [--config <path>]
```
Reloads Caddy's configuration on the remote server via SSH from the toolkit container. Useful after a caddy site file change without a full redeploy.

### `dapsman prod offline`
```
dapsman prod offline [--project <name>...] [--dry-run] [--provider <name>] [--config <path>]
```
Takes one or more projects offline by replacing the prod Caddy site file with a 503 maintenance page. If the project has a `_caddy_sites/<name>.offline.caddy`, that file is used; otherwise a generic page is generated, covering every domain the prod caddy file serves. Omitting `--project` runs against all configured projects.

### `dapsman prod online`
```
dapsman prod online [--project <name>...] [--dry-run] [--provider <name>] [--config <path>]
```
Restores one or more projects from offline by pushing the original prod Caddy site file back to the remote. Omitting `--project` runs against all configured projects.

### `dapsman prod sync-from-local`
```
dapsman prod sync-from-local --project <name> [--dry-run] [--provider <name>] [--config <path>]
```
Syncs content from the local workstation to the remote server. Runs `_scripts/sync-local-to-remote.toolkit.sh` from the toolkit container. What exactly is synced is defined by the script; typically used to push a local database or uploaded files to prod (e.g. for WordPress: rsync `wp-content`, mysqldump, URL replacement). Requires `--project`.

### `dapsman local sync-from-prod`
```
dapsman local sync-from-prod --project <name> [--dry-run] [--provider <name>] [--config <path>]
```
Pulls content from the remote server to the local workstation. Runs `_scripts/sync-remote-to-local.toolkit.sh` from the toolkit container. Typically used to pull a production database or uploaded files down for local development. Requires `--project`.

### `dapsman prod backup`
```
dapsman prod backup [--project <name>...] [--dry-run] [--provider <name>] [--config <path>]
```
Backs up remote data to the local workstation by running `_scripts/backup-remote.toolkit.sh` from the toolkit container. Projects without this script are skipped with a message. Backups are written to `_backups/from_prod/` inside the project directory. Omitting `--project` runs against all configured projects that support it.

### `dapsman local restore`
```
dapsman local restore --project <name> [--restore-point <n|timestamp>] [--prod-url <url>] [--list-restore-points] [--dry-run] [--config <path>]
```
Restores a backup to the local dev environment. Runs `_scripts/restore-local.toolkit.sh` from the toolkit container, using backup files previously pulled to `_backups/from_prod/` by `dapsman prod backup`. Projects without this script are skipped with a message. Requires `--project`.

`--list-restore-points` discovers and prints available restore points without performing a restore. `--restore-point` selects a specific restore point by 1-based index (e.g. `--restore-point 1` for the most recent) or by timestamp string (e.g. `--restore-point 20260420_202738`); defaults to the most recent complete restore point. `--prod-url` overrides the production URL for URL replacement in the database; if omitted the script derives it from the project's prod compose file. Requires confirmation before executing.

### `dapsman local teardown`
```
dapsman local teardown --project <name> [--dry-run] [--config <path>]
```
Tears down a project's local dev environment. Requires `--project`. Requires confirmation before executing. Steps:

- stops all project containers and deletes their volumes (`docker compose down -v`) — the `-v` flag is essential to avoid stale volumes causing broken rebuilds on re-init
- deletes the project's caddy site file from `daps/caddy_sites/` and reloads Caddy
- deletes the project folder from the workstation

### `dapsman prod teardown`
```
dapsman prod teardown --project <name> [--dry-run] [--provider <name>] [--config <path>]
```
Deletes a project from the remote deployment. Requires specifying a project. Requires confirmation ("are you sure?") before executing the teardown. Tearing down a project does the following:

- updates the caddy site file to return a "site has been removed" message (and restarts Caddy)
- deletes all of the site's containers using `docker compose -f ... down -v` using the project's remote compose files
- deletes the remote project folder

### `dapsman prod unprovision`
```
dapsman prod unprovision [--dry-run] [--provider <name>] [--config <path>]
```
OpenStack providers only. Deletes the OpenStack instance and SSH keypair from the remote host, the SSH key files from the toolkit, and the instance vars file from the workstation. Requires confirmation ("are you sure?") and re-confirmation ("really really sure?"). Generic-vps providers have no instance-creation API, so there is no corresponding unprovision workflow for them.
