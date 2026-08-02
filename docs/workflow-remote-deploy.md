# Workflow: `dapsman prod deploy`

## Purpose

Documents the design rationale, constraints, and decisions for the `prod deploy` workflow.

---

## Automatic Caddy Reload on Deploy

### Motivation

`dapsman local build` ends with a Caddy reload so the local reverse proxy immediately picks up new site files. `prod deploy` uploads caddy site files to the remote server but historically did not reload Caddy — leaving the operator to run `dapsman prod caddy restart` as a separate step. This is easy to forget and means the deploy isn't truly complete until that step is done.

### Decision

Run the prod Caddy reload automatically as the final step of `prod deploy`, mirroring `local build`. The implementation reuses `ConventionRemoteCaddyRestartPlanBuilder` + `CaddyRestartService` with the toolkit bash runner — exactly the same code path as `prod caddy restart`, just called internally after the deploy step.

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

---

## Stale Caddy File Cleanup

`prod deploy` removes `.prod.caddy` files from `/srv/daps/caddy_sites/` on the remote that don't correspond to any project in `daps.yaml`. See [planning-daps-yaml.md](planning-daps-yaml.md) for the full design and current status of the cleanup narrowing.
