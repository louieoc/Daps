# Daps Astro Template

A template for deploying an [Astro](https://astro.build/) site via nginx, managed by [Daps](https://codeberg.org/louieoc/Daps).

Astro handles local development and building. Daps handles containerized local preview and remote deployment. Run `npm run build` to produce `dist/`, then Daps serves and syncs that folder.

---

## Before you start

This template is added **on top of an existing Astro project** using `--overlay`, rather than initialized on its own. The Astro project directory must already exist before running `dapsman init`. It's expected to be a sister folder to `daps/`.

This template uses `myastrosite` as a placeholder for the project name (also the project folder name) throughout. Only the DAPS infrastructure files (`_docker/`, `_caddy_sites/`, `_scripts/`) are touched — your Astro source files are left untouched.

### Initialize with `dapsman init`

From the Daps root folder:

```bash
dapsman init --template astro --name yourprojectname --overlay --prod-url yourdomain.com
```

This adds `_docker/`, `_caddy_sites/`, and `_scripts/` to your existing Astro project directory, replaces all `myastrosite` placeholders with your project name, writes your production domain into `_caddy_sites/yourprojectname.prod.caddy`, and registers the project in `daps.yaml`. `--prod-url` is optional — if omitted, edit the caddy file manually before deploying.

---

## Local development

Use Astro's built-in dev server for local development:

```bash
npm run dev
```

This gives you hot-reload and Astro's native tooling. No Docker needed for active development.

### Local preview with Daps (optional)

To preview the production build locally through Caddy (the same setup used in prod):

```bash
npm run build
dapsman local build --project yourprojectname
```

Your site will be available at `https://yourprojectname.localhost` (via Caddy) or `http://localhost:8080` (direct). A placeholder `dist/index.html` is included so nginx starts successfully before your first build — run `npm run build` to replace it with your actual site.

---

## Deploying to production

### First deploy

Before deploying, confirm `_caddy_sites/yourprojectname.prod.caddy` names your real domain, not the `example.com` placeholder, and read "DNS and HTTPS" in `daps/docs/readme-deployment.md` — whether you point DNS at the host before or after this step depends on whether the domain is already serving a live site.

```bash
npm run build
dapsman prod deploy --project yourprojectname --provider <name>
```

This brings up the nginx container on the remote server and then rsyncs `dist/` to the remote.

### Updating content

For routine content updates, build locally then sync:

```bash
npm run build
dapsman prod sync-from-local --project yourprojectname --provider <name>
```

This rsyncs `dist/` to the remote server incrementally — only changed files are transferred.

Alternatively, you can run the sync script directly from the toolkit container:

```bash
docker exec -it daps-toolkit-1 bash -l
/srv/projects/yourprojectname/_scripts/sync-local-to-remote.toolkit.sh
```

---

## Project structure

After `dapsman init`, your Astro project will have these additional DAPS files:

```
yourprojectname/
├── _caddy_sites/                             # Caddy reverse proxy configs (imported by Daps)
│   ├── yourprojectname.dev.caddy
│   └── yourprojectname.prod.caddy
├── _docker/                                  # Docker Compose files
│   ├── compose_yourprojectname.yaml          # Base config (shared dev + prod)
│   ├── compose_yourprojectname.dev.yaml      # Dev overrides (port binding, volume mount)
│   ├── compose_yourprojectname.prod.yaml     # Prod overrides (volume path)
│   └── compose_daps_yourprojectname.dev.yaml # Toolkit extension (mounts project into toolkit)
├── _scripts/                                 # Utility scripts
│   ├── post-remote-deploy.toolkit.sh        # Auto-run after prod deploy: rsyncs dist/
│   └── sync-local-to-remote.toolkit.sh      # Content-only sync: run manually from toolkit
├── dist/                                     # Astro build output — gitignored, served by nginx
└── src/                                      # Your Astro source files
    └── ...
```

---

## Services

| Service | Dev port | Description |
|---|---|---|
| nginx | 8080 | nginx:1.31-alpine static file server |

---

## Differences from the `static` template

| | `static` | `astro` |
|---|---|---|
| Init mode | normal — creates a new directory | overlay — adds Daps files to an existing Astro project directory |
| Served directory | `public/` | `dist/` |
| Build step | none (files go directly in `public/`) | `npm run build` produces `dist/` |
| Init script scope | whole project directory | only `_docker/`, `_caddy_sites/`, `_scripts/` — Astro source untouched |
| Placeholder content | `public/index.html` | `dist/index.html` (replaced on first build) |

Use `static` when you have hand-authored HTML. Use `astro` when you have an existing Astro project and want to build, preview, and deploy it via Daps.

---

## Upgrading nginx

The nginx version is pinned in `_docker/compose_myastrosite.yaml`. To upgrade:

1. Update the image tag:
   ```yaml
   image: nginx:1.31-alpine  →  nginx:1.32-alpine
   ```
2. Test locally:
   ```
   npm run build
   dapsman local build --project yourprojectname
   ```
3. Deploy:
   ```
   dapsman prod deploy --project yourprojectname --provider <name>
   ```

No backup needed — your built files live in `dist/`, not in the container, so they are unaffected by the image change.

**Upgrading Astro itself** (npm packages) is separate from Daps — run `npm update` or edit `package.json` in your project directory as you normally would. Daps only cares about the `dist/` output.

---

## Running multiple Astro sites

Multiple Astro projects can run alongside each other and alongside other project types on the same Daps instance:

- Caddy routes to each site's nginx container via a project-specific alias on `daps_net`, so there is no DNS collision.
- Each project's nginx container is independent — starting, stopping, or tearing down one has no effect on the others.

The one thing you must change manually for a second Astro project is the **dev host port** in `_docker/compose_yourprojectname.dev.yaml`:

| Service | Default port | Change to (example) |
|---|---|---|
| nginx | 8080 | 8081 |

This port is only used for direct local access (`http://localhost:8080`). Traffic through `https://yourprojectname.localhost` goes via Caddy and is unaffected.
