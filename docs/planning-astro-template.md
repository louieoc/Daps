# Planning: Astro Template

## Context

The `static` template serves pre-built HTML files from a `public/` directory via nginx:alpine. Astro is a static site framework that produces a `dist/` folder after `npm run build`. The Astro project already exists as a directory on the workstation before Daps is involved — Daps is added on top of it to handle containerized local preview and remote deployment.

This means two things are needed:

1. **`templates/astro/`** — a close copy of the static template with `dist/` in place of `public/`, and an init script that only touches the DAPS-specific subdirectories (not the whole project tree).
2. **`dapsman init --overlay`** — a new flag that allows `dapsman init` to add DAPS infrastructure files into an existing directory, rather than requiring the destination to not exist.

---

## `--overlay` Mode

### Problem

`InitService.CopyTemplateDirectory` currently throws if the destination already exists:

```csharp
if (Directory.Exists(destinationPath))
    throw new InvalidOperationException($"Destination already exists: {destinationPath}");
```

For Astro (and future framework templates), the project directory pre-exists. We need a way to say "add the DAPS files to this existing directory without touching what's already there."

### Design

Add `--overlay` flag to `dapsman init`. In overlay mode:

- **Plan-time** (`ConventionInitPlanBuilder`): the destination **must** exist — throw early if it doesn't (symmetric with the normal mode guard against the destination existing).
- **Execution-time** (`InitService.CopyTemplateDirectory`): copy each template file into the destination, **skipping any file that already exists**. Never overwrite. Create directories as needed.

This is safe: the template only contains DAPS infrastructure files (`_docker/`, `_caddy_sites/`, `_scripts/`) which will never collide with Astro's own files (`src/`, `dist/`, `package.json`, etc.).

### Data model changes

- `InitOptions.cs` — add `bool Overlay`
- `InitPlan.cs` — add `bool Overlay`
- `ConventionInitPlanBuilder` — pass `options.Overlay` through; when `Overlay=true`, check that `destinationPath` exists (throw if not); when `Overlay=false`, keep existing behavior (throw if it does exist)
- `InitService.CopyTemplateDirectory` — add `bool overlay` parameter; in overlay mode, use skip-existing semantics instead of throw-if-exists
- `CliArguments.cs` — add `--overlay` flag → `bool Overlay` property
- `DapsmanRunner.RunInit` — wire `_parsed.Overlay` → `InitOptions`; show overlay mode in dry-run output and step output
- Usage line: `dapsman init --template <name> --name <project-name>|--project <name> [--overlay] [--prod-url <domain>] ...`

---

## `templates/astro/` Template

### Placeholder name: `myastrosite`

### File structure

```
templates/astro/
  _caddy_sites/
    myastrosite.dev.caddy      # reverse_proxy myastrosite:80
    myastrosite.prod.caddy     # reverse_proxy myastrosite:80 + commented www redirect
  _docker/
    compose_myastrosite.yaml           # nginx:alpine, daps_net alias, name: myastrosite
    compose_myastrosite.dev.yaml       # bind-mount ../dist:/usr/share/nginx/html:ro + port 8080
    compose_myastrosite.prod.yaml      # bind-mount /srv/projects/myastrosite/dist:/...
    compose_daps_myastrosite.dev.yaml  # toolkit mount: ../../myastrosite:/srv/projects/myastrosite
  _scripts/
    init-template.toolkit.sh           # replaces myastrosite; only processes DAPS dirs
    post-remote-deploy.toolkit.sh      # calls sync-local-to-remote
    sync-local-to-remote.toolkit.sh    # rsyncs dist/ to remote
  README.md                    # won't overwrite existing astro project readme
```

Includes a placeholder `dist/index.html` ("run npm run build to build your site") so that `dapsman local build` succeeds and nginx has something to serve before the first Astro build. Astro always clears `dist/` on build, so the placeholder is replaced automatically — and since `dist/` is gitignored in Astro projects, it never appears in the user's git history.

### Key differences from `static` template

| | `static` | `astro` |
|---|---|---|
| Served directory | `public/` | `dist/` |
| Init mode | normal (creates new dir) | overlay (adds to existing Astro project dir) |
| Placeholder content | `public/index.html` | none |
| Init script scope | whole project dir | only `_docker/`, `_caddy_sites/`, `_scripts/` |

### Passing `--overlay` to the init script

Dapsman forwards `--overlay` as a second argument to `init-template.toolkit.sh`: `bash <script> <project-name> [--overlay]`. This keeps Dapsman template-agnostic — each template's script decides what to do with the flag.

**Static template** — update `init-template.toolkit.sh` to check `$2`:
- Without `--overlay` (normal mode): process all files in the project directory (current behavior).
- With `--overlay`: restrict `sed` and file rename to `_docker/`, `_caddy_sites/`, `_scripts/` only, so existing user files are never touched.

**Astro template** — `init-template.toolkit.sh` always only processes DAPS directories. Since Dapsman will throw at plan-time if `--overlay` is absent and the destination already exists, the astro template is effectively always invoked with `--overlay` — restricting unconditionally is correct and simpler.

```bash
# astro init-template.toolkit.sh (always restricted to DAPS dirs)
for dir in _docker _caddy_sites _scripts; do
    find "$PROJECT_DIR/$dir" -type f | while read -r file; do
        sed -i "s/myastrosite/$PROJECT_NAME/g" "$file"
    done
done
# rename files containing placeholder in those dirs only
```

### `sync-local-to-remote.toolkit.sh` (astro variant)

Identical to the static version except `public/` → `dist/`:

```bash
rsync -avz --delete \
  -e "ssh ${SSH_OPTS[*]}" \
  "${PROJECT_ROOT}/dist/" \
  "${DAPS_REMOTE_USER}@${DAPS_REMOTE_HOST}:/srv/projects/${DAPS_PROJECT}/dist/"
```

---

## Usage

```bash
# Initialize Daps on top of an existing Astro project at ../myastrosite
dapsman init --template astro --name myastrosite --overlay --prod-url mysite.com
```

Dry-run output will show `overlay mode: adding DAPS files to existing directory`.

---

## Files to Change

| File | Change |
|---|---|
| `src/Dapsman.Application/InitOptions.cs` | Add `bool Overlay` |
| `src/Dapsman.Domain/InitPlan.cs` | Add `bool Overlay` |
| `src/Dapsman.Infrastructure/ConventionInitPlanBuilder.cs` | Overlay constraints; pass through to plan |
| `src/Dapsman.Application/InitService.cs` | Update `CopyTemplateDirectory` for overlay semantics; forward `--overlay` as second arg to init script |
| `src/Dapsman.Cli/CliArguments.cs` | Add `--overlay` flag |
| `src/Dapsman.Cli/DapsmanRunner.cs` | Wire `--overlay`, update dry-run/step output and usage line |
| `templates/static/_scripts/init-template.toolkit.sh` | Add `--overlay` branch: restrict to DAPS dirs when `$2 = --overlay` |
| `templates/astro/` (7 files) | New template |
| `docs/planning-init.md` | Add overlay mode section |
| `README.md` | Update init command docs |

---

## Verification

1. `dapsman init --template astro --name testastro --overlay --dry-run` — should fail with "overlay mode requires an existing directory" since `../testastro` doesn't exist
2. Create `../testastro` (empty); re-run with `--overlay --dry-run` — should succeed and show overlay mode in plan
3. Run the same command **without** `--overlay` — should fail with "destination already exists" since the directory now exists
4. `dapsman init --template astro --name testastro --overlay` — should copy `_docker/`, `_caddy_sites/`, `_scripts/` into `../testastro` and register in daps.yaml
5. `dapsman local build --project testastro` — nginx container starts (though `dist/` is empty until `npm run build` is run)
6. `dapsman init --template static --name testsite` (no `--overlay`, dest doesn't exist) — should work exactly as before, confirming no regression
