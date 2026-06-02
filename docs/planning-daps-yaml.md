# Planning: `daps.yaml`

## Hosting Provider Registry Design

TODO

## Project Registry Design

### Purpose

`daps.yaml` is the project registry: it tells Dapsman what projects exist and where they live. This doc captures design decisions and constraints around how Dapsman interprets and acts on the registry, particularly around the boundary between "not in the registry" and "temporarily inactive."

---

### Stale Caddy File Cleanup

#### Current behaviour

`dapsman prod deploy` includes a stale cleanup step that deletes `.prod.caddy` files from `/srv/daps/caddy_sites/` on the remote server if they are not present in any configured project's `_caddy_sites/` directory. The intent was garbage collection: if a project is fully removed from `daps.yaml`, its caddy file eventually disappears from prod.

#### The problem

The cleanup is based on which projects appear in `_config.Projects` at deploy time. Any project absent from that list — whether truly removed, commented out, or filtered by `--project` — has its remote caddy file deleted as a side effect of deploying something else. This caused `wp.dapster.org` to go offline when `dapsman prod deploy --project loccom` ran while `dapster-wp` was commented out of `daps.yaml`.

Side effects of this kind are dangerous: an action on project A silently takes down project B. That's the job of `prod teardown` and `prod offline`, not `prod deploy`.

#### Decision

Narrow the cleanup: **only delete a `.prod.caddy` file if no project in `daps.yaml` (in any form — active, disabled, or otherwise) has a name matching that file.**

The rule becomes: *if the project is known to Dapsman (present in `daps.yaml`), its remote caddy file is never touched by another project's deploy.* Actual removal from the remote is always an explicit act via `prod teardown` or `prod offline`.

This means a stale file only gets cleaned up when the corresponding entry is fully removed from `daps.yaml` — which is the only safe signal that the operator genuinely wants it gone.

#### Implementation note

`CaddyResolver.CollectExpectedProdCaddyFileNames` must include `<project>.prod.caddy` for every project in `daps.yaml`, not just those whose local `_caddy_sites/*.prod.caddy` file currently exists. The local file's presence controls whether it gets *uploaded*; the project's presence in `daps.yaml` controls whether an existing remote file gets *protected*.

**Status: not yet implemented.** The immediate bug (dapster-wp going offline) was resolved by restoring `dapster-wp` to `daps.yaml` and redeploying.

---

### Disabled Projects (future)

#### Motivation

Operators sometimes want to keep a project in `daps.yaml` without including it in batched workflows (`local build`, `prod deploy` without `--project`, etc.). The natural syntax would be:

```yaml
projects:
  myproject:
    path: ../myproject
    disabled: true
```

#### Constraints

- A disabled project **must not** be acted on by batch workflows.
- A disabled project **must not** have its remote caddy file deleted as a side effect of another project's deploy (see stale cleanup above).
- Explicit single-project commands (`--project myproject`) should still work on a disabled project, or at minimum warn clearly.
- `prod teardown` and `prod offline` remain the correct explicit path for taking a project down.

#### Status: not yet designed or implemented.
