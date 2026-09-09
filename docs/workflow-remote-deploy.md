# Workflow: `dapsman prod deploy`

## Purpose

Documents the design rationale, constraints, and decisions for the `prod deploy` workflow.

---

## Automatic Caddy Reload on Deploy

### Motivation

`dapsman local build` ends with a Caddy reload so the local reverse proxy immediately picks up new site files. `prod deploy` uploads caddy site files to the remote server but historically did not reload Caddy — leaving the operator to run `dapsman prod caddy restart` as a separate step. This is easy to forget and means the deploy isn't truly complete until that step is done.

### Decision

Run the prod Caddy reload automatically as the final step of `prod deploy`, mirroring `local build`. The implementation reuses `RemoteCaddyRestartPlanBuilder` + `CaddyRestartService` with the toolkit bash runner — exactly the same code path as `prod caddy restart`, just called internally after the deploy step.

The reload passes `--force`. Caddy skips a reload when the incoming config is byte-identical to the running one, which would make `prod caddy restart` a no-op in the case it is most needed: forcing a retry of TLS certificate issuance after a domain's DNS was pointed at the server, where the caddy file has not changed at all. `prod deploy` inherits `--force` by sharing the code path; it is redundant there, since a deploy has usually just uploaded a changed caddy file, but harmless.

### Step order

1. build-images (if needed)
2. deploy-remote (SCP uploads + remote docker compose via toolkit)
3. caddy-reload (SSH caddy reload via toolkit — new step)

### Dry-run behavior

The `caddy-reload` step is included in the dry-run plan output (shows the reload command) but not executed, consistent with all other steps.

### Constraints / trade-offs

- Requires `--provider` to be resolvable at plan time (same requirement as `prod caddy restart`). This was already true of `prod deploy` so no new constraint.
- If Caddy is not running on the remote (e.g. first-ever deploy), the reload will fail. This is acceptable — first deploy is expected to be a manual bootstrapping step where the operator verifies state. (Same behavior as running `prod caddy restart` manually today.)
- `prod caddy restart` remains a standalone command for cases where only the Caddy config needs refreshing without a full deploy.

---

## Image Build Behavior

### `--build` flag

- **No existing tar** — build runs automatically (first deploy, or after `dapsman local teardown`).
- **Tar exists, no flag** — skipped; existing tar reused. Correct for code-only or config-only changes.
- **`--build`** — forces rebuild. Use after Dockerfile changes.

The old `--skip-build-images` flag was removed; the lazy default makes it redundant.

### Target platform

Image tarballs carry the architecture of the machine that built them. A Windows or Intel-Mac
workstation and an x86_64 VPS happen to agree, so this went unnoticed for a long time; an Apple
Silicon workstation does not. The failure is quiet in every place you would look for it: the build
succeeds, `docker save`/`docker load` transfer the image without complaint, `prod deploy` reports
success, and the only symptom is a container restart-looping on the remote with
`exec format error: /usr/local/bin/docker-entrypoint.sh` — a message that names neither
architecture nor the image. This cost a real user most of a debugging session, arrived at only
after ruling out DNS, TLS, Caddy, and the database.

So `prod deploy` asks the host what to build for, immediately before building:

```
docker version --format '{{.Server.Os}}/{{.Server.Arch}}'   # → linux/amd64
```

**Why `docker version` and not `uname -m`.** `uname -m` reports the kernel (`x86_64`, `aarch64`)
and would need a mapping table to become a `--platform` value — one more thing to maintain as
architectures appear. `docker version` reports the daemon's own Go platform, which *is* a valid
`--platform` value verbatim, and it stays correct on a host whose kernel and userland
architectures differ (a 64-bit kernel running a 32-bit userland runs `arm` images and reports
`aarch64`). The daemon is the thing that will execute the image, so the daemon is what to ask.

**No chicken-and-egg.** The reading needs no image on either side, so it can run before the first
build has ever happened. It is a plain SSH command against a host that `prod provision` has
already put Docker on.

**It reuses the `prod system` collection path.** Deploy builds a `SystemStatusPlan` and calls
`ToolkitSystemStatusCollector`, reading `DockerPlatform` off the result. Staging a script into the
toolkit, running it over SSH and escaping bash on the way is work that collector already does; a
second path for one field would have duplicated all of it.

Two details make the reuse work:

- **Summary, not verbose.** `docker_platform` sits in the always-collected section of
  `remote-system-status.sh`, so deploy pays one SSH round trip. The verbose pass adds `docker
  stats` and a `du -sb` walk over every project directory — bounded by nothing, and growing with
  the size of the sites. A `du` over a large `wp-content` on a cold cache is not a cost a build
  should carry. The script's comment says as much, because the record's placement is now
  load-bearing for a second workflow.
- **A provider-free plan factory.** Deploy cannot call `ISystemStatusPlanBuilder.BuildPlan`: that
  resolves through `ResolveExplicit`, which deliberately refuses to guess between several
  configured providers, whereas deploy infers its provider from the selected projects and arrives
  with the host already decided. `RemoteSystemStatusPlanBuilder.BuildPlanForHost` takes the
  resolved host directly and keeps the plan's shape and constants in one place.

**Not cached.** One SSH round trip per deploy, and only when a build will actually run. A stored
value would go stale the moment a provider's host is replaced — silently, and in the direction
that breaks the deploy. There is nothing to invalidate if there is nothing to store.

**Fails closed.** If the host cannot answer, the deploy stops. Falling back to an unpinned build
would reintroduce exactly the silent mismatch this step exists to prevent, and would do it at the
moment the user has least reason to suspect it. Two failures to cover: the collector already
throws when SSH fails, and deploy adds a guard for the host that answered but reported no docker
platform, which the fault-tolerant script reports as an empty value rather than an error.

The value reaches the build script as the `DAPS_TARGET_PLATFORM` environment variable, which the
script turns into `docker build --platform`. An environment variable rather than a positional
argument, because `build-docker-images.toolkit.sh` lives in every project folder created before
this change: a script that does not read the variable keeps working exactly as it did, whereas a
new positional argument would have shifted arguments underneath it. Unset means "build native",
which is correct for a local build, where the workstation is the target.

### Verification after load

The probe fixes new builds. It cannot fix a tarball built last month by a project whose
`build-docker-images.toolkit.sh` predates `DAPS_TARGET_PLATFORM`, and those tarballs are reused by
default whenever one already exists. So the remote deploy script also compares each loaded image
against the host after `docker load`:

```
docker image inspect <ref> --format '{{.Os}}/{{.Architecture}}'
```

A mismatch fails the deploy with a message naming both platforms and the fix, instead of leaving a
restart loop behind. Two lines of bash, and it is the only part of this that protects projects
whose scripts were never updated.

### Cross-building needs QEMU

`docker build --platform` for a foreign architecture is emulated. Docker Desktop ships the
binfmt/QEMU support, so Mac and Windows workstations are fine; a bare Linux Docker install may
not have it, and `docker build` says so. The WordPress image only layers a `wp-cli.phar` download
onto the official base, so the emulation cost is negligible.

---

## Stale Caddy File Cleanup

`prod deploy` removes `.prod.caddy` files from `/srv/daps/caddy_sites/` on the remote that don't correspond to any project in `daps.yaml`. See [planning-daps-yaml.md](planning-daps-yaml.md) for the full design and current status of the cleanup narrowing.
