# Planning: Static Site Template

## Context

Daps currently supports container-heavy projects (WordPress + MySQL + Redis). Static sites have fundamentally different needs: just files and a web server. The goal is to define a first-class static site pattern in Daps and create a `static` template, then use it to create `dapster` — a simple website for Daps itself (possibly Jekyll-generated).

---

## Architecture Options Considered

### Option A: Caddy serves files directly (per-project overlay)
The `compose_daps_<project>.dev.yaml` overlay mechanism already exists and could add a volume mount to the Caddy service. **Trade-off:** adding or removing a static site requires recreating the Caddy container, briefly taking all other sites down. Deployments and content updates would also require Caddy restarts on prod.

### Option B: Shared nginx container (Daps service)
A single nginx container alongside Caddy, serving all static sites via per-site config files imported from a shared directory — mirroring how Caddy imports `*.caddy` files. Supports `nginx -s reload` (no connection drops on config change). **Trade-off:** requires adding a new shared service to `compose_daps.yaml`, a new nginx site-config convention alongside the caddy site-config convention, and Caddy → nginx → files indirection. Meaningful core Dapsman work; all static sites share a failure domain.

### Option C: Per-project nginx:alpine container (chosen)
One nginx:alpine container per project. Caddy reverse-proxies to it exactly as it does for WordPress. No Caddy restarts on project add/remove, each project is independently deployable and teardown-able. nginx:alpine is ~10 MB. Zero changes to the Caddy or shared-services layer. Consistent with all existing projects.

---

## What's Simpler vs WordPress

| | WordPress | Static |
|---|---|---|
| Containers | wordpress + db + redis | nginx:alpine only |
| Custom Docker image | Yes (wp + wp-cli) | No — pulls from registry |
| Secrets | 10+ files | None |
| Prerequisites script | Yes | No |
| Image tar/build step | Yes | No — `docker compose up` pulls nginx |
| Content deploy | Separate sync workflow | `post-remote-deploy.toolkit.sh` hook |

No `build-docker-images.toolkit.sh`, no `prerequisites.sh`, no `_secrets/` directory.

---

## Template Structure (`templates/static/`)

Placeholder name: `mystaticsite`

```
templates/static/
  _caddy_sites/
    mystaticsite.dev.caddy          # reverse_proxy mystaticsite:80
    mystaticsite.prod.caddy         # reverse_proxy mystaticsite:80 + commented www redirect
  _docker/
    compose_mystaticsite.yaml           # nginx:alpine, daps_net alias
    compose_mystaticsite.dev.yaml       # bind-mount ../public:/usr/share/nginx/html:ro + host port
    compose_mystaticsite.prod.yaml      # bind-mount /srv/projects/mystaticsite/public:/usr/share/nginx/html:ro
    compose_daps_mystaticsite.dev.yaml  # toolkit mount: ../../mystaticsite:/srv/projects/mystaticsite
  _scripts/
    init-template.toolkit.sh            # replaces mystaticsite with project name
    post-remote-deploy.toolkit.sh       # rsyncs public/ to remote after prod deploy
    sync-local-to-remote.toolkit.sh     # content-only update: rsyncs public/ without full redeploy
  public/
    index.html                          # placeholder "Hello from mystaticsite"
```

---

## Content Deployment

### Why not the existing upload manifest?
The existing `ParseProjectUploadManifest()` / `deploy-prod-uploads` mechanism is file-only (rejects directories), uses `install -m 600` (secret-file permissions, wrong for public web content), and is designed for small numbers of individual config files (e.g. `google_credentials.json` in j-shirt.com). It's the wrong tool for uploading a directory of static files.

### `post-remote-deploy.toolkit.sh` hook
Dapsman auto-discovers `_scripts/post-remote-deploy.toolkit.sh`. If present, it runs inside the toolkit container after the remote deploy step completes (containers up, caddy reloaded). The script has full access to toolkit environment — SSH keys, provider credentials, rsync/scp.

The static site template's hook calls `sync-local-to-remote.toolkit.sh` to rsync `public/` to the remote. Rsync is incremental — subsequent deploys only transfer changed files.

### `sync-local-to-remote.toolkit.sh`
`prod deploy` is a full infrastructure operation. For routine content updates (editing a page, publishing a new post), the operator runs `sync-local-to-remote.toolkit.sh` directly from the toolkit. No containers are restarted — it just rsyncs `public/` and exits. Same pattern as WordPress sync scripts.

`post-remote-deploy.toolkit.sh` sources `sync-local-to-remote.toolkit.sh` to avoid duplication.

### Dapsman code changes
- `RemoteProjectDeployPlan` (Domain) — add `string? PostDeployScriptPath`
- `ConventionRemoteDeployPlanBuilder` (Infrastructure) — detect `_scripts/post-remote-deploy.toolkit.sh` and populate `PostDeployScriptPath`
- `DapsmanRunner.RunRemoteDeploy` (CLI) — after caddy-reload, if `PostDeployScriptPath` is set, run via toolkit bash runner
- `DapsmanRunner.PrintRemoteDeployPlan` — show post-deploy hook step in dry-run output

