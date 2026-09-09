---
title: Changelog
sidebar:
  order: 99
---

Changes are listed newest first. For history prior to `0.1.0`, see `git log`.

---

## 0.3.0 - 2026-08-19

### Added

- `dapsman --version` to show the current version of the dapsman CLI and by extension the version of Daps itself.
- `dapsman prod system [--verbose]` — reports how the remote server is doing: architecture, CPU cores and load, memory, disk, and whether a reboot is pending. Read-only; it collects values over SSH from the toolkit and writes nothing to the host. `--verbose` adds uptime, per-container CPU and memory, per-project disk usage, and total Docker disk consumption.

  Architecture is reported as `uname -m` alongside the Docker daemon's own platform: `x86_64 (docker linux/amd64)`. The two are collected separately because they can legitimately disagree, and the Docker value is the authoritative one — it is what will actually execute an image.

  Memory "used" comes from `MemAvailable` rather than `MemFree`, so page cache is not counted against the user — `MemFree` would show a perfectly healthy long-running server as almost out of memory.

  A pending reboot is reported with an explanation rather than a command, because there is no Dapsman workflow that reboots a host: `prod provision` configures unattended-upgrades to reboot automatically at 04:00, so the flag normally clears overnight, and the manual route is the hosting provider's control panel.

  The remote half is `scripts/remote-system-status.sh`, which only collects — it emits tab-separated records and does no arithmetic, unit conversion, or percentages. Dapsman interprets those in C#. It uses only coreutils and `/proc`, so nothing needs installing on the host, and each optional section is individually fault-tolerant: a host without Docker, or with nothing deployed yet, loses that section rather than failing the command.
- `dapsman local backup [--project <name>...]` — snapshots the local dev instance to `_backups/from_local/`, for taking a checkpoint before an upgrade or a risky change to a site still in development. Runs `_scripts/backup-local.toolkit.sh` from the toolkit container; projects without that script are skipped with a message, the same convention `prod backup` uses. Implemented in the `wordpress` and `grist` templates. Not implemented for `astro` or `static`, which are tracked in git and already have history.

  Unlike Grist's `prod backup` — an rsync mirror that only ever holds one copy — the Grist local backup writes a timestamped archive per run, and stops the Grist container while it archives so the open SQLite files can't be caught mid-write.

### Changed

- WordPress template base image upgraded from `wordpress:7.0-php8.3-apache` to `wordpress:7.1-php8.3-apache`
- `dapsman local restore` now finds restore points in `_backups/from_local/` as well as `_backups/from_prod/`. Both directories are scanned, merged, and renumbered most-recent-first, so `--restore-point <n>` counts across both; the existing environment label on each entry says which one it came from. Restoring a `local` backup skips the prod→dev URL replacement (the URLs are already dev URLs) but still flushes the cache, which is required after any database import.
- `DapsmanRunner` class now lazy-loads all dependencies, e.g. `IToolkitResolver`, `ICaddyResolver`, `IBashRunner`, `DapsConfig`, etc. That way workflows that are supported before certain dependencies exist, e.g. `dapsman local build`, which installs the toolkit container on first run, needs to be able to run before the toolkit container exists.
- `dapsman prod deploy` now builds project images for the remote host's architecture instead of the workstation's, passing it to `build-docker-images.toolkit.sh` as `DAPS_TARGET_PLATFORM`. Previously the image inherited the workstation's architecture -- harmless from Windows or an Intel Mac to an x86_64 VPS, but from an Apple Silicon Mac the deploy reported success and the container restart-looped on the remote with `exec format error`. Scripts that don't read the variable keep working unchanged. See [Deploying to production](readme-deployment.md) and [workflow-remote-deploy.md](workflow-remote-deploy.md).
- `dapsman prod deploy` now verifies each image against the host after `docker load` and fails with both platforms named if they differ, catching tarballs built before `DAPS_TARGET_PLATFORM` existed.
- Refactor: removed the single-argument IHostingProviderResolver.Resolve.
- Refactor: workstation docker compose commands are built once and included as properties in LocalBuildPlan, to ensure that the dry run describes exactly what the real run will do, and to stop repeated calls to the command builder.
- Refactor: `dapsman init` no longer supports `_scripts/init-template.toolkit.sh` scripts, obviating the fixes from v0.2.2. The logic for placeholder substitution and line ending consistency is now in dapsman itself. A `template.yaml` file now contains template-specific configuration info, currently only supporting the list of folders to exclude from transformation. The `template.yaml` and any `_secrets` folder that might exist are excluded from being copied to the destination.
- Refactor: the readme cli commands page now lists cli commands in alphabetical order
- Refactor: the toolkit's `.bashrc` no longer sources per-project `*.bashrc` files. The feature came from an early idea that projects would need shell-level environment variables, which was never needed, and its glob (`/srv/projects/*/scripts/`) predated the `_scripts/` convention — so it had silently matched nothing for some time.

### Fixed

- `.gitattributes` now pins `*.bashrc` to LF endings. `docker/toolkit.bashrc` is copied into the toolkit image at build time, and with `core.autocrlf=true` a Windows checkout converted it to CRLF, so every command running through the toolkit printed carriage-return and syntax errors first. Note that fixing the file's endings does not fix a running container — the copy inside it is baked into the image, so it takes `dapsman local build --build`.
- The `wordpress` template's `.gitignore` ignored `backups/`, but the folder is `_backups/` — database dumps and content archives were not actually being ignored.
- Backup scripts now write each artifact to a `.partial` file and rename it only once the command producing it has succeeded, in `backup-local.toolkit.sh` (`wordpress`, `grist`) and `backup-remote.toolkit.sh` (`wordpress`). The shell creates a redirect's output file before `mysqldump` or `tar` ever runs, and `scp` writes as it goes, so a dump that failed part-way left a truncated file sitting in `_backups/` looking like a usable backup — and `dapsman local restore` would offer it as a restore point. An interrupted run now leaves the backup directory as it found it.

---

## 0.2.2 - 2026-08-12

### Fixed

- `dapsman init` now works on macOS. Every template's `init-template.toolkit.sh` used `sed -i "s/…/…/g" "$file"` for placeholder replacement, which is GNU-only: BSD sed (macOS) requires `-i` to take a backup suffix, so it consumed the expression as the suffix and then parsed the *filename* as the sed script, failing with `command a expects \ followed by text`. Replacement now goes through a `replace_in_file` helper that works identically under GNU and BSD sed. Affects the `wordpress`, `grist`, `astro`, and `static` templates.

  The helper writes a sibling temp file (seeded with `cp -p`, so permissions carry over) and `mv`s it into place. The rename matters: the init script's own source contains the placeholder, so the replacement loop rewrites the file bash is still reading. Renaming leaves the running shell's file descriptor on the original inode; overwriting in place shifts every later byte offset and bash resumes mid-token, producing errors like `ories: command not found`. GNU `sed -i` had been getting this right by accident — it renames too.

  Despite the `.toolkit.sh` suffix, `init-template.toolkit.sh` runs on the workstation (`InitService` is wired with `_workstationBashRunner`), so it must stay portable across Git Bash, macOS, and Linux — noted in `CLAUDE.md`.
- `dapsman local build --build --project mywpsite` and `dapsman prod deploy --project mywpsite` workflows for Wordpress are updated to pull new images when the floating `7.0-php8.3-apache` image receives a new patch update, e.g. 7.0.2 to 7.0.3.

---

## 0.2.1 - 2026-08-09

### Added

- `dapsman prod provision` now applies Daps host policy over SSH via the new `scripts/configure-host.sh`, for both `openstack` and `generic-vps` providers. The script enables an automatic reboot at 04:00 when an unattended security upgrade requires one, by writing a Daps-owned drop-in at `/etc/apt/apt.conf.d/52daps-unattended-upgrades`. Ubuntu already installs security updates unattended, but ships with `Unattended-Upgrade::Automatic-Reboot` unset — so new kernels were installed and never booted into, leaving hosts running a vulnerable kernel indefinitely. The script also installs `unattended-upgrades` if the host image lacks it, and asserts the `apt-daily` timers are enabled.

  The script is idempotent, so running the upgraded `prod provision` against a host provisioned by an earlier version of Daps brings that host up to current policy. For `openstack` providers this adds a post-create SSH step, which waits for the instance to accept connections; the step is also why re-provisioning an existing instance now reaches the host rather than stopping after the create script.

  Containers use `restart: unless-stopped` and return after the reboot. Anything deliberately stopped by `dapsman prod offline` correctly stays stopped.

### Fixed

- Shell scripts uploaded to a remote host are now staged with LF line endings, by both `dapsman prod provision` (`provision-generic-vps.sh`, `configure-host.sh`) and `dapsman prod deploy` (a project's `_scripts/*.sh`). On a Windows checkout, `core.autocrlf` can leave CRLF endings in the working tree, and a CRLF shell script fails as soon as it runs on Linux with `set: pipefail: invalid option name`. Git Bash tolerates CRLF, so this only ever surfaced on the remote. Normalization is done by the new, explicitly named `ConfigUtils.CopyFileAndReplaceLineEndingsForLinux`; other staged files keep a verbatim copy, since image tarballs and manifest uploads may be binary and compose/caddy files tolerate CRLF.

### Changed

- `dapsman prod provision` now runs through a `ToolkitRemoteProvisionExecutor` that stages its scripts under `.dapsman/provision` and runs a generated `provision.sh`, rather than passing one long `&&` chain to `bash -lc`. This matches the executor + staging pattern used by `prod deploy`, and is what makes the line-ending normalization above possible. The staging directory is removed after the run.

---

## 0.2.0 - 2026-08-05

### Added

- `dapsman local build --rebuild` recreates the selected projects from scratch, running `docker compose down -v` before `up`. Recovers a project whose named volumes are unusable — e.g. a MySQL data directory left half-initialized by an interrupted first build, which fails with `MY-012960: Cannot create redo log files because data files are corrupt`. Implies `--build`, requires `--project`, and prompts for a typed `yes`.
- `--yes` flag to skip the `--rebuild` confirmation prompt for scripted use
- `DISALLOW_FILE_MODS true` added to WordPress template prod compose file, blocking plugin/theme installation and the file editor from WP Admin. Paired with the existing `WP_AUTO_UPDATE_CORE false`, these two constants close the main in-container file-write attack surfaces.
- `postmortem-2026-07-loccom-compromise.md` doc

### Fixed

- First-run chicken-and-egg: `dapsman init` and `dapsman local build` failed on a workstation where Daps had never been built, because every command resolved the toolkit container at startup and errored with "No running toolkit container found. Start local Daps first (dapsman local build)". The toolkit is now resolved only by the workflows that use it, and the caddy container name is resolved by convention when no container is running. Workflows that genuinely need the toolkit (all `prod` workflows, `local restore`, `local sync-from-prod`) still report a clear error, and `--dry-run` no longer needs any container to exist.
- `dapsman local build` now creates the shared `daps_net` Docker network if it is missing, in a new `docker-network` step. Compose files declare it as external, so a fresh workstation previously failed with "network daps_net declared as external, but could not be found".
- `dapsman init` no longer requires Docker to be running — it copies a template and runs the template's init script, so it now checks only for bash.
- The missing-`daps.yaml` error now says to copy `daps.yaml.example`.
- `dapsman prod offline` now generates its offline Caddy config from every site address in the project's `*.prod.caddy` file, not just the first. Previously a file with a comma-separated address list (`example.com, www.example.com {`) produced an invalid site address with a trailing comma, and any additional site blocks (e.g. a `www` redirect or an `api` subdomain) were dropped from the offline config entirely.
- `dapsman prod offline` and `dapsman prod online` now fall back to the project's own `provider:` from `daps.yaml` when no `--provider` is given. They were resolving to the first provider in the file instead, so a project hosted elsewhere was taken offline against the wrong server — the command reported success while the real site stayed up. Every other per-project prod workflow already did this; these two were the exception.
- `dapsman prod unprovision` now requires `--provider` when more than one provider is configured, instead of silently defaulting to the first one in `daps.yaml`. It destroys a VM and its keypair and has no project to infer a provider from, so guessing was the wrong default — `dapsman prod provision` already worked this way.
- fix titles of workflow docs

### Changed
- `dapsman local caddy restart` and `dapsman prod caddy restart` now pass `--force` to `caddy reload`. Caddy skips a reload when the incoming config is byte-identical to the running one, which made both commands a no-op in the case they are most needed: forcing Caddy to retry TLS certificate issuance after a domain's DNS was pointed at the server. The caddy file is unchanged in that scenario, so the reload was silently skipped and the site stayed uncertified until Caddy's own retry backoff elapsed.
- misc documentation updates

---

## 0.1.0 - 2026-07-24

### Added

- Version tracking: `<Version>0.1.0</Version>` added to `Dapsman.Cli.csproj`
- Upgrade instructions added to all template READMEs (WordPress, Grist, Astro, Static)
- `WP_AUTO_UPDATE_CORE false` set in WordPress template compose files to prevent silent background core upgrades
- `/daps-dryrun` and `/daps-pr` Claude Code slash commands for health checking and pre-merge review

### Changed

- `dapsman local build --build` now passes `--renew-anon-volumes` to `docker compose up`, ensuring that image upgrades (e.g. WordPress) take effect immediately rather than being shadowed by stale anonymous volumes
- `dapsman prod deploy` now passes `--renew-anon-volumes` to `docker compose up` on the remote server for the same reason
- WordPress template base image upgraded from `wordpress:6.7-php8.3-apache` to `wordpress:7.0-php8.3-apache`
- All template images pinned to specific versions:
  - `phpmyadmin:latest` → `phpmyadmin:5.2`
  - `nginx:alpine` → `nginx:1.31-alpine`
  - `gristlabs/grist:latest` → `gristlabs/grist:1.7`
