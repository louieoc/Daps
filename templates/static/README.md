# Daps Static Site Template

A template for serving static HTML files via nginx, managed by [Daps](https://codeberg.org/louieoc/Daps).

No database, no build step, no secrets — just a folder of files and a web server.

---

## Before you start: replace the placeholder name

This template uses `mystaticsite` as a placeholder throughout. There are two ways to set up a project from this template:

### Option A: Use `dapsman init` (recommended)

From the Daps root folder:

```bash
dapsman init --template static --name yourprojectname --prod-url yourdomain.com
```

This copies the template, replaces all `mystaticsite` placeholders with your project name (in file contents and filenames), writes your production domain into `_caddy_sites/yourprojectname.prod.caddy`, and registers the project in `daps.yaml` automatically. `--prod-url` is optional — if omitted, edit the caddy file manually before deploying.

### Option B: Manual setup

Do a find-and-replace across the whole folder:

| Replace | With |
|---|---|
| `mystaticsite` | your project name (e.g. `janesdrum`) |
| `mystaticsite.localhost` | your local dev hostname (e.g. `janesdrum.localhost`) |
| `mystaticsite.example.com` | your real domain name (e.g. `janesdrum.com`) |
| `mystaticsite-prod` | your OpenStack server name (e.g. `janesdrum-prod`) |

Files to update (rename the files too):
- `_docker/compose_mystaticsite.yaml`
- `_docker/compose_mystaticsite.dev.yaml`
- `_docker/compose_mystaticsite.prod.yaml`
- `_docker/compose_daps_mystaticsite.dev.yaml`
- `_caddy_sites/mystaticsite.dev.caddy`
- `_caddy_sites/mystaticsite.prod.caddy`

Then add an entry to the root `daps.yaml`:
```yaml
projects:
  mystaticsite:           # <-- your project name
    path: ../mystaticsite # <-- path to this folder
```

---

## Initial setup

### 1. Add your local hostname

Chrome and Firefox resolve `*.localhost` automatically without a hosts entry. For other tools (curl, etc.) add it manually:

```
# Windows: C:\Windows\System32\drivers\etc\hosts
# Mac/Linux: /etc/hosts

127.0.0.1  mystaticsite.localhost
```

### 2. Add your site content

Replace the placeholder `public/index.html` with your own files. The entire `public/` directory is served by nginx — put your HTML, CSS, JS, and images there.

### 3. Start the local site

From the Daps root folder:

```
dapsman local build --project mystaticsite
```

Or run docker compose directly from this project folder:

```
docker compose -f _docker/compose_mystaticsite.yaml -f _docker/compose_mystaticsite.dev.yaml up -d
```

Your site will be available at `https://mystaticsite.localhost` (via Caddy) or `http://localhost:8080` (direct).

---

## Deploying to production

### First deploy

Before deploying, confirm `_caddy_sites/mystaticsite.prod.caddy` names your real domain, not the `mystaticsite.example.com` placeholder, and read "DNS and HTTPS" in `daps/docs/readme-deployment.md` — whether you point DNS at the host before or after this step depends on whether the domain is already serving a live site.

```bash
dapsman prod deploy --project mystaticsite --provider <name>
```

This brings up the nginx container on the remote server and then rsyncs `public/` to the remote — no separate content upload step needed.

### Updating content

For routine content updates (editing a page, adding images) you don't need a full redeploy:

```bash
dapsman prod sync-from-local --project mystaticsite --provider <name>
```

This rsyncs `public/` to the remote server incrementally — only changed files are transferred.

Alternatively, you can run the sync script directly from the toolkit container:

```bash
docker exec -it daps-toolkit-1 bash -l
/srv/projects/mystaticsite/_scripts/sync-local-to-remote.toolkit.sh
```

---

## Project structure

```
mystaticsite/
├── _caddy_sites/                           # Caddy reverse proxy configs (imported by Daps)
│   ├── mystaticsite.dev.caddy
│   └── mystaticsite.prod.caddy
├── _docker/                                # Docker Compose files
│   ├── compose_mystaticsite.yaml           # Base config (shared dev + prod)
│   ├── compose_mystaticsite.dev.yaml       # Dev overrides (port binding, volume mount)
│   ├── compose_mystaticsite.prod.yaml      # Prod overrides (volume path)
│   └── compose_daps_mystaticsite.dev.yaml  # Toolkit extension (mounts project into toolkit)
├── _scripts/                               # Utility scripts
│   ├── post-remote-deploy.toolkit.sh       # Auto-run after prod deploy: rsyncs public/
│   └── sync-local-to-remote.toolkit.sh     # Content-only sync: run manually from toolkit
└── public/                                 # Your site files — put everything here
    └── index.html
```

---

## Services

| Service | Dev port | Description |
|---|---|---|
| nginx | 8080 | nginx:1.31-alpine static file server |

---

## Upgrading nginx

The nginx version is pinned in `_docker/compose_mystaticsite.yaml`. To upgrade:

1. Update the image tag:
   ```yaml
   image: nginx:1.31-alpine  →  nginx:1.32-alpine
   ```
2. Test locally:
   ```
   dapsman local build --project mystaticsite
   ```
3. Deploy:
   ```
   dapsman prod deploy --project mystaticsite --provider <name>
   ```

No backup needed — your site files live in `public/`, not in the container, so they are unaffected by the image change.

---

## Running multiple static sites

Multiple static sites can run alongside each other and alongside other project types (WordPress, etc.) on the same Daps instance:

- Caddy routes to each site's nginx container via a project-specific alias on `daps_net`, so there is no DNS collision.
- Each project's nginx container is independent — starting, stopping, or tearing down one has no effect on the others.

The one thing you must change manually for a second static project is the **dev host port** in `_docker/compose_mystaticsite.dev.yaml`, since each project uses a fixed port and only one project can bind a given host port at a time:

| Service | Default port | Change to (example) |
|---|---|---|
| nginx | 8080 | 8081 |

This port is only used for direct local access (`http://localhost:8080`). Traffic through `https://mystaticsite.localhost` goes via Caddy and is unaffected.

