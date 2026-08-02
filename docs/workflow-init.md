# Workflow: `dapsman init`

## Purpose

Documents the design rationale, constraints, and decisions for the `init` workflow.

---

## What Init Does

`dapsman init` creates a new DAPS project by:

1. **Resolving** the template directory under `daps/templates/<template>/`
2. **Copying** the template to the destination directory (defaults to a sibling of `daps/`, i.e. `../<project-name>`)
3. **Running** `_scripts/init-template.toolkit.sh <project-name>` inside the copy (if the script exists), which handles all placeholder substitution and file renaming — Dapsman is template-agnostic
4. **Registering** the new project in `daps.yaml` with a relative path

---

## Template Convention

Dapsman is template-agnostic: all placeholder replacement and file renaming lives in `init-template.toolkit.sh`, not in Dapsman. This keeps template-specific logic inside the template itself.

The script is optional — if absent, Dapsman copies the template as-is without error. This supports simple templates that need no customization.

The script receives the project name as `$1` and runs in the copied project directory.

---

## Project Name: `--name` vs `--project`

### The tension

`init` takes a project name to create a new project. Two flags are natural candidates:

- **`--name`** — semantically "correct": the thing being named doesn't yet exist as a project, so calling it `--project` is a stretch.
- **`--project`** — consistent with every other Dapsman workflow that refers to a project by name. Users who reach for `--project` by muscle memory should not be surprised.

### Decision

Accept both flags as aliases. `--name` is documented as the canonical form in usage/help text; `--project` is accepted silently as an alias. If both are provided, `--name` wins.

### Implementation

`RunInit()` resolves the name from `_parsed.ProjectName` (set by `--name`) first, then falls back to the first entry in `_parsed.ProjectFilters` (set by `--project`). No parser changes required — `--project` already populates `ProjectFilters` in all commands.

---

## `--path` Override

By default the destination is `<daps-parent>/<project-name>` (a sibling of the `daps/` root). `--path` overrides this for cases where the project should live elsewhere.

_EDS. NOTE: this is untested and probably doesn't work, given how docker compose files currently use relative paths by convention and assume the project lives in a sibling directory to daps._ TODO confirm

---

## daps.yaml Registration

After copying and running the init script, Dapsman appends the new project to `daps.yaml`:

```yaml
myproject:
  path: ../myproject
```

The path is relative to `daps.yaml`. This makes the project immediately usable with `local build`, `prod deploy`, etc. without any manual config editing.

### Duplicate name guard

`ConventionInitPlanBuilder` checks `_config.Projects` for a name collision before building the plan — before any files are copied or scripts run. If the name is already registered, init exits with a clear error message. This is a plan-time check, not an execution-time check, so nothing is left in a partially-created state.

The `DapsYamlEditor.AddProject` method also guards against duplicates as a safety net, but the plan builder is the authoritative check.

---

## Dry-Run Behavior

`--dry-run` prints the plan (template source, destination, init script command, daps.yaml entry) without copying any files, running any scripts, or modifying `daps.yaml`.

---

## `--overlay` Mode

### Problem

By default, `dapsman init` throws if the destination directory already exists. This prevents accidentally clobbering an existing project. For templates that are added on top of an existing project (e.g. Astro, where the user creates the project first, then adds Daps to it), this guard is incorrect — the directory is expected to exist.

### Design

`--overlay` inverts the guard:

- **Normal mode** (no `--overlay`): throws if destination exists.
- **Overlay mode** (`--overlay`): throws if destination does **not** exist. Copies template files into the existing directory, skipping any file that already exists. Never overwrites.

The destination check is done at plan-time in `ConventionInitPlanBuilder` (before any files are touched), keeping it consistent with the "duplicate name" and "template not found" guards.

### Init script forwarding

Dapsman passes `--overlay` as a second argument to `init-template.toolkit.sh`: `bash <script> <project-name> --overlay`. Templates that need to restrict their placeholder replacement to DAPS directories (to avoid touching the user's files) check `$2` and act accordingly.

- **`templates/static`** — in normal mode processes all project files; in overlay mode restricts to `_docker/`, `_caddy_sites/`, `_scripts/`.
- **`templates/astro`** — always restricts to DAPS dirs unconditionally (it is only ever used with `--overlay`).

### `--prod-url`

`--prod-url` works in overlay mode exactly as in normal mode. After the init script runs, `InitService.Execute()` calls `ApplyProdUrl()` which patches two locations:

1. **`_caddy_sites/*.prod.caddy`** — replaces the bare hostname placeholder `{projectName}.example.com` with the supplied prod URL (e.g. `mysite.com`)
2. **`_docker/*.prod.yaml`** — replaces the full HTTPS URL `https://{projectName}.example.com` with `https://{prodUrl}` — needed for templates like WordPress that set `WP_HOME` / `WP_SITEURL` as environment variables in the prod compose file
