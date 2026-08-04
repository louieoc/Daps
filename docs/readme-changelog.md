---
title: Changelog
sidebar:
  order: 99
---

Changes are listed newest first. For history prior to `0.1.0`, see `git log`.

---

## Unreleased

### Added

- `dapsman local build --rebuild` recreates the selected projects from scratch, running `docker compose down -v` before `up`. Recovers a project whose named volumes are unusable — e.g. a MySQL data directory left half-initialized by an interrupted first build, which fails with `MY-012960: Cannot create redo log files because data files are corrupt`. Implies `--build`, requires `--project`, and prompts for a typed `yes`.
- `--yes` flag to skip the `--rebuild` confirmation prompt for scripted use
- `DISALLOW_FILE_MODS true` added to WordPress template prod compose file, blocking plugin/theme installation and the file editor from WP Admin. Paired with the existing `WP_AUTO_UPDATE_CORE false`, these two constants close the main in-container file-write attack surfaces.
- `postmortem-2026-07-loccom-compromise.md` doc

### Changed
- `dapsman prod offline` now generates its offline Caddy config from every site address in the project's `*.prod.caddy` file, not just the first. Previously a file with a comma-separated address list (`example.com, www.example.com {`) produced an invalid site address with a trailing comma, and any additional site blocks (e.g. a `www` redirect or an `api` subdomain) were dropped from the offline config entirely.
- fix titles of workflow docs
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
