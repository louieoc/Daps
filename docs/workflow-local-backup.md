# Workflow: `dapsman local backup`

## Context

`dapsman prod backup` pulls remote data to `_backups/from_prod/`, and `dapsman local restore`
applies those files to the local dev instance. Neither one can snapshot the **local** instance.

You want that snapshot when the local instance is the work in progress: before a major WordPress
or plugin upgrade, before a change you might want to undo, or simply so the workstation isn't the
only copy of a site you're still building. `dapsman local backup` writes those snapshots, and
`local restore` reads them back, so the loop closes rather than being write-only.

---

## Command

```
dapsman local backup [--project <name>...] [--dry-run] [--config <path>]
```

No `--provider` — nothing remote is touched. Omitting `--project` runs against every configured
project that supports it, printing `- skipped: <reason>` for the rest.

The project must be running. The database is dumped from the running container, so
`dapsman local build` has to have happened first.

---

## Design: convention-driven

Identical in shape to `prod backup`. Support is detected by file existence on the workstation:

| File | Role |
|------|------|
| `_scripts/backup-local.toolkit.sh` | Present ⇒ the project supports `local backup`. Absent ⇒ `PlanResult.NotSupported`. |

The script runs **in the toolkit container**, not on the workstation. The toolkit has the Docker
socket mounted and the project bind-mounted at `/srv/projects/<name>`, so it reads secrets and
files directly and reaches the local containers with `docker exec`. `restore-local.toolkit.sh`
already relies on the same capability.

### Script contract

- Env vars: `DAPS_PROJECT` (only — no host, user, or key; nothing remote is involved)
- Args: `--env local --to "<toolkit path to _backups/from_local>"`

Output filenames use `local` as the environment token, keeping the existing
`<project>_<env>_<timestamp>` convention. That is what lets restore-point discovery work unchanged
across both sources.

---

## Implementation

| Layer | Type |
|-------|------|
| Domain | `LocalBackupPlan` — `ScriptArguments`, `EnvVars` |
| Domain | `BackupSources` — `from_prod` / `from_local` constants, and the discovery search order |
| Application | `ILocalBackupPlanBuilder`, `LocalBackupService` |
| Infrastructure | `LocalBackupPlanBuilder` |
| CLI | `CliArguments.IsLocalBackup`, `DapsmanRunner.RunLocalBackup`, `DapsmanPlanPrinter.PrintLocalBackup` |

`LocalBackupService` takes a resolved `DapsProject` (as `RestoreService` does) rather than a
yaml path (as `RemoteBackupService` does) — there is no provider to resolve. Execution goes
straight through `IBashRunner`; no separate executor, matching `RemoteBackupService`.

`LocalBackupPlanBuilder` returns `NotSupported` when the script is missing, and again when
`project.ToolkitPath` is null (the compose file exists but binds nothing whose target matches the
project name), which is the check `LocalRestorePlanBuilder` already makes.

### Restore points from both sources

`RestorePointsDiscoverer` previously hardcoded `from_prod`. It now runs
`list-restore-points.toolkit.sh` once per existing directory in `BackupSources.All`, tags each
`RestorePoint` with its `SourceDirectory`, merges the results, sorts newest-first and renumbers
`Index` across the merged list — so `--restore-point <n>` counts across both sources.
`LocalRestorePlanBuilder` derives `BackupsToolkitPath` from the selected point's `SourceDirectory`.

The list script itself is unchanged: it only ever sees one directory, via `DAPS_BACKUPS_PATH`.

`RestorePoint` became a `record` so discovery can renumber `Index` when merging, and carries
`ReplacesUrls` — false for a `local` backup — so the plan printer describes the restore without
re-deriving the rule. That rule is stated twice by necessity: once there, and once as the
`DAPS_RESTORE_ENV` guard inside `restore-local.toolkit.sh`, which is what actually skips the step.
Change one and change the other, or the dry-run will describe a restore that doesn't happen.

---

## Template scripts

**`templates/wordpress/_scripts/backup-local.toolkit.sh`** — the remote script with the SSH hops
removed: `mysqldump` via `docker exec` into `<project>_local_<ts>.sql`, then `tar -czf` of
`wp-content` into `<project>_local_wp-content_<ts>.tar.gz`. Fails with a clear message if the db
container isn't running or the root password file is missing.

**`templates/grist/_scripts/backup-local.toolkit.sh`** — a timestamped `tar -czf` of `persist/`,
deliberately **not** the rsync mirror `prod backup` uses for Grist: a mirror only ever holds one
copy, and keeping successive snapshots is the point of this workflow. Grist keeps its SQLite
documents open, so the container is stopped for the archive and restarted afterwards via a
`trap ... EXIT` that fires on failure too.

### Partial artifacts

Every artifact is written to a `.partial` file and renamed only once the command producing it has
succeeded, with an `EXIT` trap removing the in-flight file if the script aborts. The shell creates
a redirect's output file *before* `mysqldump` or `tar` runs, so without this a dump that failed
part-way left a truncated file in `_backups/from_local/` — which restore point discovery would
then offer as a usable backup. `list-restore-points.toolkit.sh` anchors its match on `\.sql$`, so a
`.partial` is invisible to it even in the window before the trap fires. `backup-remote.toolkit.sh`
does the same for the same reason, with the `scp` of the wp-content archive landing on the
`.partial` path and renamed only after the remote temp file has been cleaned up.

**Not implemented** for `astro` or `static`. Those projects are assumed to be tracked in git, which 
already gives them history; the absent script is the "unsupported" signal.

### Restoring a local backup

`restore-local.toolkit.sh` (WordPress) skips the prod→dev URL replacement when
`DAPS_RESTORE_ENV` is `local` — the URLs in a local backup are already dev URLs, and requiring a
`.prod.caddy` file to restore a local snapshot would be wrong. The cache flush still runs
unconditionally: Redis caches the options table, so a freshly imported database serves stale
values until it is flushed.

---

## Follow-up work

Grist has `backup-local.toolkit.sh` but no `restore-local.toolkit.sh` or
`list-restore-points.toolkit.sh`, so `dapsman local restore` remains unsupported for Grist. Adding
those two scripts to the Grist template would make its snapshots restorable; the C# side already
handles any project that provides them.

---

## Verification

```
dapsman local backup --dry-run
dapsman local backup --project <wordpress-project> --dry-run
dapsman local backup --project <astro-project> --dry-run     # expect "skipped"
dapsman local backup --project <wordpress-project>
dapsman local restore --project <wordpress-project> --list-restore-points
dapsman local restore --project <wordpress-project> --restore-point 1
```
