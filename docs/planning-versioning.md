---
title: Versioning and Upgrades
sidebar:
  order: 11
---

## Context

Daps has no versioning yet: no version number in the CLI, no changelog, no formal update mechanism. This doc establishes the strategy across three areas: versioning Daps itself, how users receive Daps updates, and how users upgrade project dependencies (WordPress, Grist).

---

## Daps versioning

**Semantic versioning: `MAJOR.MINOR.PATCH`**

| Bump | When |
|---|---|
| MAJOR | Breaking changes: compose conventions changed, daps.yaml format changed, CLI flags removed, template structure requires migration |
| MINOR | New workflows, new templates, new flags, image tag bumps in templates — backward compatible |
| PATCH | Bug fixes, docs, typo fixes, non-behavioral dependency bumps |

Starting version: `0.1.0` (pre-release; no stability guarantees).

**Single source of truth:** `<Version>0.1.0</Version>` in `src/Dapsman.Cli/Dapsman.Cli.csproj`. The CLI is the right home — it's the deployed artifact the user runs. If domain/application libraries were ever published as NuGet packages independently, they'd carry their own versions; for now they're internals.

Git tag `v0.1.0` on Codeberg mirrors the csproj version.

**`dapsman --version`:** Prints the version (from `Assembly.GetName().Version`) and exits. (Not yet implemented — tracked as an implementation item below.)

**Changelog:** `docs/readme-changelog.md` — one entry per release, newest first. Each entry: version header, release date, bullet points grouped as Added / Changed / Fixed / Breaking. Synced to dapster.org automatically.

---

## Receiving Daps updates

### Current mechanism: git pull

```bash
# 1. Check what changed
# (review docs/readme-changelog.md or the Codeberg release notes)

# 2. Pull updates
git pull

# 3. Apply infrastructure changes
dapsman local build
```

### Handling a breaking (MAJOR) update

A MAJOR version bump means something about your project configuration needs to change. The changelog's migration guide describes exactly what. General process:

1. Read the migration guide in the changelog
2. Update your project's compose files, daps.yaml, or scripts as instructed
3. `dapsman local build`

### Pinning to a stable version

For users who want stability over getting the latest: checkout a specific release tag and stay there until you're ready to upgrade.

```bash
git checkout v0.2.0
```

To upgrade to a later version:

```bash
git fetch
git checkout v0.3.0
# follow any migration steps in the changelog
dapsman local build
```

### Branching for local customization

Users who modify Daps itself (e.g. to add a private template) should maintain a personal branch based on a release tag:

```bash
git checkout -b my-daps v0.2.0
# make local changes
```

When a new Daps release comes out:

```bash
git fetch
git merge v0.3.0   # or rebase
# resolve conflicts — usually minimal, most changes are in src/
```

### Breaking change protocol (developer)

When shipping a MAJOR version bump:
1. Write a migration guide section in the changelog entry
2. Add a `MIGRATION.md` or section in `readme-setup.md` describing what users need to change
3. Consider keeping old behavior behind a flag for one MINOR version before removing it

---

## Distribution options

The git-clone approach requires git and some CLI familiarity. For a broader audience, packaged distribution is worth exploring once Daps is stable:

**Self-contained binary releases** — `dotnet publish -r win-x64 --self-contained true` produces a single exe with the .NET runtime bundled. Attach platform-specific binaries to Codeberg Releases:

- Windows: `dapsman-win-x64.exe` (zipped)
- macOS: `dapsman-osx-arm64`, `dapsman-osx-x64`
- Linux: `dapsman-linux-x64`

No .NET installation required. Binary is ~70 MB self-contained. Users update manually by downloading a new release.

**Recommendation for now:** Self-contained releases is the lowest-effort first step — no runtime dependency, works across platforms, Codeberg already supports release attachments.

**Other options for later:**
- `.NET global tool` via NuGet (`dotnet tool install -g dapsman`) — clean UX for users who have .NET installed
- macOS: Homebrew tap (`brew install louieoc/daps/dapsman`)
- Windows: MSI installer (WiX or similar)
- Linux: apt/snap/AppImage

---

## Upgrading project dependencies

### Understanding what lives where

Whether an upgrade goes through the Docker image or through a bind-mounted directory determines how it's done.

| What | Where | How to upgrade |
|---|---|---|
| WordPress core | Inside the container image | Update the image tag in the dockerfile, rebuild |
| WordPress plugins & themes | `wp-content/` (bind-mounted) | WP Admin dashboard |
| MySQL | Container image | Update tag in compose file, redeploy |
| Redis | Container image | Update tag in compose file, redeploy |

See [templates/wordpress/README.md](https://codeberg.org/louieoc/Daps/src/branch/main/templates/wordpress) for the step-by-step WordPress core upgrade flow.

### Is a template image tag bump a MAJOR or MINOR Daps update?

MINOR. Template updates only affect *new* projects initialized after the update — existing projects have their own copy of the dockerfile and are not touched. Updating an existing project's dependency is always the user's explicit action. No Daps migration needed.

### Pinning floating tags

Several templates currently use floating tags (`:latest`, `nginx:alpine`) which make upgrades implicit and non-reproducible. Best practice: pin to a specific version and update intentionally.

| Template | Service | Current | Better |
|---|---|---|---|
| wordpress | phpmyadmin | `phpmyadmin:latest` | `phpmyadmin:5.2` |
| astro / static | nginx | `nginx:alpine` | `nginx:1.27-alpine` |
| grist | grist | `gristlabs/grist:latest` | `gristlabs/grist:1.4` |

(Actual versions to confirm at time of pinning.)

### Future: upgrade workflow

A `dapsman upgrade --project <name>` command could compare a project's image tags against the current template version and suggest updates. Worth adding once Daps has multiple real users.

---

## Open source release practices

- **Codeberg Releases**: create a release per git tag with the changelog entry as release notes; attach self-contained binaries per platform
- **Pre-release labeling**: `0.x.y` signals no stability guarantees; cut `1.0.0` once core workflows are stable and tested across multiple real projects
- **CONTRIBUTING.md**: add contributor guidelines before soliciting external PRs

---

## Implementation items

- [ ] Add `<Version>0.1.0</Version>` to `src/Dapsman.Cli/Dapsman.Cli.csproj`
- [ ] Add `dapsman --version` command (print `Assembly.GetName().Version`)
- [ ] Create `docs/readme-changelog.md` with an initial `0.1.0` entry
- [ ] Pin `phpmyadmin`, `nginx`, `gristlabs/grist` to specific versions in templates
- [ ] Add update instructions and version-pinning guidance to `docs/readme-setup.md`
- [ ] Set up Codeberg Releases with self-contained binaries on first release
- [ ] (Future) `dapsman upgrade --project <name>` workflow
