---
title: Changelog
sidebar:
  order: 99
---

Changes are listed newest first. For history prior to `0.1.0`, see `git log`.

---

## 0.2.1 — 2026-08-09

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

## 0.1.0 — 2026-07-24

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
