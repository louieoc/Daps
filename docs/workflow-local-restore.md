# Workflow: `dapsman local restore`

## Context

`dapsman prod backup` pulls backup files to `_backups/from_prod/` on the workstation. Until now there's no workflow to apply those backups to the local instance. `dapsman local restore` closes that loop — it applies a backup to the local dev instance so the user can then redeploy it to a new host via `dapsman prod deploy`.

This is the counterpart to `local sync-from-prod`, but the data source is local backup files rather than a live remote server.

---

## Command

```
dapsman local restore --project <name> [--dry-run] [--list-restore-points] [--restore-point <n|timestamp>] [--prod-url <url>] [--config <path>]
```

- `--list-restore-points` — discover and print available restore points; no restore performed
- `--restore-point <n|timestamp>` — restore from restore point. Can be:
  - Integer (1-based index, most recent = 1): `--restore-point 1`
  - Timestamp string: `--restore-point 20260420_202738`
  - Default (if omitted): 1 (most recent)
- `--prod-url <url>` — override the prod URL for URL replacement; default: read `WP_HOME` from `_docker/compose_<project>.prod.yaml`

---

## Restore Point Discovery

Restore point discovery is **project-specific** — each project or template implementing the restore workflow must provide `_scripts/list-restore-points.toolkit.sh` to discover and validate restore points according to its backup structure.

For example:
- WordPress template: scans for paired `<project>_<env>_<timestamp>.sql` + `<project>_<env>_wp-content_<timestamp>.tar.gz` files
- Grist template: scans for `<project>_<env>_<timestamp>.tar.gz` (single file per restore point) -- (future TODO)
- Future projects may have different structures

The script outputs JSON or a parseable format with:
- Index (for `--restore-point 1` style)
- Timestamp (for `--restore-point 20260420_202738` style)
- Env, completeness status, file sizes, etc.

Dapsman parses the output to present the numbered list to the user. If the script is absent: `PlanResult.NotSupported`.

List output (most recent first):
```
1. prod: 2026-04-20 20:27:38
2. prod: 2026-02-05 13:07:12  (incomplete)
```

---

## Design: Convention-Driven (project provides the scripts)

Follows the same pattern as `prod backup`:

- Dapsman looks for `_scripts/list-restore-points.toolkit.sh` and `_scripts/restore-local.toolkit.sh` in the project
- If either is absent: `PlanResult.NotSupported("Project does not have _scripts/list-restore-points.toolkit.sh")`
- If present: execute list via toolkit, build plan, execute restore via toolkit

Both scripts are **project-specific**:
- WordPress template provides both
- Other projects and templates can add their own if they want to support this workflow (Grist has a different backup structure)
- Each template's list script outputs restore points in a format Dapsman can parse (JSON or structured text)

---

## C# Changes

### New domain types

**`src/Dapsman.Domain/RestorePoint.cs`**
```csharp
public sealed class RestorePoint {
    public required string Env { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    public required string SqlFilePath { get; init; }     // workstation absolute path
    public required string? WpContentTarPath { get; init; }
    public bool IsComplete => WpContentTarPath is not null;
}
```

**`src/Dapsman.Domain/RestorePlan.cs`**
```csharp
public sealed class RestorePlan {
    public required string ProjectName { get; init; }
    public required string ToolkitContainerName { get; init; }
    public required RestorePoint RestorePoint { get; init; }
    public required string SqlBackupToolkitPath { get; init; }
    public required string? WpContentBackupToolkitPath { get; init; }
    public required string RestoreScriptToolkitPath { get; init; }
    public required string? ProdUrl { get; init; }  // null = script derives from compose
    public required string DevDomain { get; init; } // e.g. "dapster-wp.localhost"
}
```

### New application types

**`src/Dapsman.Application/RestoreOptions.cs`**
```csharp
public sealed class RestoreOptions {
    public bool DryRun { get; init; }
    public bool ListRestorePoints { get; init; }
    public string? RestorePoint { get; init; }  // "1" or "20260420_202738"
    public string? ProdUrl { get; init; }
}
```

**`src/Dapsman.Application/Interfaces.cs`** — add:
```csharp
public interface IRestorePlanBuilder {
    IReadOnlyList<RestorePoint> DiscoverRestorePoints(DapsProject project, bool dryRun);
    PlanResult<RestorePlan> BuildPlan(DapsProject project, IReadOnlyList<RestorePoint> discovered, RestoreOptions options);
}
public interface IRestoreExecutor {
    void Execute(RestorePlan plan);
}
```

**`src/Dapsman.Application/RestoreService.cs`**
```csharp
public sealed class RestoreService {
    // DiscoverRestorePoints(project, dryRun) — calls toolkit script, parses output
    // BuildPlan(project, discovered, options) — resolves restore point by index or timestamp
    // Execute(plan) — delegates to executor
}
```

### New infrastructure types

**`src/Dapsman.Infrastructure/ConventionLocalRestorePlanBuilder.cs`**
- Calls `_scripts/list-restore-points.toolkit.sh` via toolkit runner with env vars: `DAPS_PROJECT`, `DAPS_BACKUPS_PATH`
- Parses output to build list of `RestorePoint` objects
- Resolves restore point by index (1-based) or timestamp string; errors if not found or incomplete
- Converts workstation paths to toolkit paths using `/srv/projects/<project>/` prefix
- DevDomain derived as `<project>.localhost`
- Reads prod URL from `_docker/compose_<project>.prod.yaml` if `RestoreOptions.ProdUrl` is null

**`src/Dapsman.Infrastructure/ToolkitLocalRestoreExecutor.cs`**
- Stages a bash script to `.dapsman/restore/<id>/restore.sh`
- Calls `_toolkitBashRunner.RunScript(...)` passing env vars:
  - `DAPS_PROJECT`
  - `DAPS_SQL_BACKUP` — toolkit path to `.sql`
  - `DAPS_WP_CONTENT_BACKUP` — toolkit path to `.tar.gz`
  - `DAPS_PROD_URL` — explicit override or empty string
  - `DAPS_DEV_DOMAIN` — e.g. `dapster-wp.localhost`

### CLI changes

**`src/Dapsman.Cli/CliArguments.cs`**
- Add `IsLocalRestore`, `ListRestorePoints` (bool), `RestorePoint` (int?, nullable)
- Add `--prod-url` string flag
- Parse `local restore` action

**`src/Dapsman.Cli/DapsmanRunner.cs`** — add `RunLocalRestore()`:
- If `--list-restore-points`: discover points via service, print numbered list, return 0
- Otherwise: build plan (respecting `NotSupported`), print plan with resolved restore point timestamp
- If not dry-run: prompt "Are you sure you want to restore from backup? This will overwrite local wp-content and database. Type 'yes' to confirm:"
  - If yes: execute
  - If no: return 1
- Dry-run output:
  ```
  Step: validate
  - restore point: prod 2026-04-20 20:27:38 (index 1)
  - sql: _backups/from_prod/dapster-wp_prod_20260420_202738.sql
  - wp-content: _backups/from_prod/dapster-wp_prod_wp-content_20260420_202738.tar.gz
  - prod url: https://wp.dapster.org (or override: https://example.com)
  
  Step: restore
  - import SQL into local database
  - extract wp-content archive
  - wp search-replace <prod-url> https://dapster-wp.localhost
  - wp cache flush
  ```

---

## WordPress Template Scripts

**`templates/wordpress/_scripts/list-restore-points.toolkit.sh`**

Receives env vars: `DAPS_PROJECT`, `DAPS_BACKUPS_PATH` (e.g., `/srv/projects/dapster-wp/_backups/from_prod`)

Scans the backups path for files matching:
- `<project>_<env>_<timestamp>.sql`
- `<project>_<env>_wp-content_<timestamp>.tar.gz`

Groups by `(env, timestamp)`, marks incomplete restore points (missing `.tar.gz`).

Outputs JSON array (or structured text) with:
- Index (1-based)
- Env
- Timestamp (string, `YYYYMMDD_HHMMSS`)
- SQL file path
- wp-content tar path (null if incomplete)
- Size info (optional)

Sorted most-recent-first.

Example JSON:
```json
[
  {
    "index": 1,
    "env": "prod",
    "timestamp": "20260420_202738",
    "sqlPath": "/srv/projects/dapster-wp/_backups/from_prod/dapster-wp_prod_20260420_202738.sql",
    "wpContentPath": "/srv/projects/dapster-wp/_backups/from_prod/dapster-wp_prod_wp-content_20260420_202738.tar.gz",
    "incomplete": false
  },
  {
    "index": 2,
    "env": "prod",
    "timestamp": "20260205_130712",
    "sqlPath": "/srv/projects/dapster-wp/_backups/from_prod/dapster-wp_prod_20260205_130712.sql",
    "wpContentPath": null,
    "incomplete": true
  }
]
```

**`templates/wordpress/_scripts/restore-local.toolkit.sh`**

Receives env vars: `DAPS_PROJECT`, `DAPS_SQL_BACKUP`, `DAPS_WP_CONTENT_BACKUP`, `DAPS_PROD_URL` (may be empty), `DAPS_DEV_DOMAIN`

Steps:
1. **Resolve prod URL** — if `DAPS_PROD_URL` is empty, read `WP_HOME` from `_docker/compose_${DAPS_PROJECT}.prod.yaml`
2. **Import SQL** — `docker exec -i ${DAPS_PROJECT}-db-1 mysql -u root -p<root_pw> wordpress < $DAPS_SQL_BACKUP` (reads `_secrets/mysql_root_password.txt` for password)
3. **Extract wp-content** — `tar -xzf $DAPS_WP_CONTENT_BACKUP -C /srv/projects/${DAPS_PROJECT}/` (produces `wp-content/`)
4. **URL replacement** — `docker exec ${DAPS_PROJECT}-wordpress-1 wp search-replace "$PROD_URL" "https://${DAPS_DEV_DOMAIN}" --allow-root`
5. **Cache flush** — `docker exec ${DAPS_PROJECT}-wordpress-1 wp cache flush --allow-root`

**Future optimization**: Extract steps 4–5 (URL replacement + cache flush) into a shared library script used by both `restore-local.toolkit.sh` and `sync-remote-to-local.toolkit.sh`.

---

## Key Design Notes

- **List and restore are project-specific** — each project or template provides its own discovery and restore scripts to handle its backup structure (WordPress: SQL + tar.gz; Grist: potentially different)
- **Both integer and timestamp restore points supported** — `--restore-point 1` or `--restore-point 20260420_202738` both work
- **Restore is destructive** — local wp-content and DB are overwritten; confirmation prompt in live run
- **Incomplete restore points are flagged** — shown in list with `(incomplete)` tag and rejected with a clear error if selected
- **Shared library opportunity** — URL replacement + cache flush logic can be extracted and reused by both restore and sync-from-prod (future optimization)

---

## Verification

```
dapsman local restore --project dapster-wp --list-restore-points
dapsman local restore --project dapster-wp --dry-run
dapsman local restore --project dapster-wp --dry-run --restore-point 2
dapsman local restore --project dapster-wp
```
