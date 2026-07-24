# DAPS WordPress Template

A template for running a self-hosted WordPress site in containers managed by [DAPS](https://codeberg.org/louieoc/Daps).

Includes Redis object caching and PhpMyAdmin (local dev only).

---

## Before you start: replace the placeholder name

This template uses `mywpsite` as a placeholder throughout. There are two ways to set up a project from this template:

### Option A: Use `dapsman init` (recommended)

From the DAPS root folder:

```bash
dapsman init --template wordpress --name yourprojectname --prod-url yourdomain.com
```

This copies the template, replaces all `mywpsite` placeholders with your project name (in file contents and filenames), removes any inherited secrets, writes your production domain into `_caddy_sites/yourprojectname.prod.caddy`, and registers the project in `daps.yaml` automatically. `--prod-url` is optional — if omitted, edit the caddy file manually before deploying.

### Option B: Manual setup

Do a find-and-replace across the whole folder:

| Replace | With |
|---|---|
| `mywpsite` | your project name (e.g. `janesdrum`) |
| `mywpsite_wp` | your database name (e.g. `janesdrum_wp`) |
| `mywpsite.localhost` | your local dev hostname (e.g. `janesdrum.localhost`) |
| `mywpsite.example.com` | your real domain name (e.g. `janesdrum.com`) |
| `mywpsite-prod` | your OpenStack server name (e.g. `janesdrum-prod`) |

Files to update (rename the files too):
- `_docker/compose_mywpsite.yaml`
- `_docker/compose_mywpsite.dev.yaml`
- `_docker/compose_mywpsite.prod.yaml`
- `_caddy_sites/mywpsite.dev.caddy`
- `_caddy_sites/mywpsite.prod.caddy`

Then add an entry to the root `daps.yaml`:
```yaml
projects:
  mywpsite:           # <-- your project name
    path: ../daps-wp  # <-- path to this folder
```

---

## Initial setup

### 1. Generate secrets

Secrets (passwords and WordPress security keys) are generated automatically by `dapsman local build` the first time you run it. They are written to `_secrets/`, which is gitignored.

To generate them manually, or to regenerate them (e.g. after a security incident), run:

```bash
./_scripts/prerequisites.sh           # skip existing secrets
./_scripts/prerequisites.sh --force   # regenerate all secrets
```

Never commit the `_secrets/` folder or share its contents.

### 2. Add your local hostname

`dapsman local build` runs `prerequisites.dev.sh` automatically, which will attempt to add the hosts entry for you:

- **Windows**: requires an elevated terminal (right-click → Run as administrator). If not elevated, a warning is printed and you can add it manually.
- **Mac/Linux**: requires sudo. If credentials aren't cached, a warning is printed with the manual command.
- **Chrome and Firefox** resolve `*.localhost` automatically without a hosts entry, so this step is only strictly needed for other tools (curl, wp-cli, etc.).

If you need to add it manually:

```
# Windows: C:\Windows\System32\drivers\etc\hosts
# Mac/Linux: /etc/hosts

127.0.0.1  mywpsite.localhost
```

### 3. Start the local site

From the DAPS root folder:

```
dapsman local build --project mywpsite
```

Or run docker compose directly from this project folder:

```
docker compose -f _docker/compose_mywpsite.yaml -f _docker/compose_mywpsite.dev.yaml up -d
```

Your site will be available at `https://mywpsite.localhost` (via Caddy) or `http://localhost:8080` (direct).
PhpMyAdmin is available at `http://localhost:8082`.

Note that it can take a few minutes for the db server to finish booting up. If you see "Error establishing a database connection" right after running the local build the first time, this could be why. If it persists beyond a few minutes, however, then something else is wrong.

### 4. Complete the WordPress setup wizard

Open your site in a browser. WordPress will walk you through choosing a title, admin username, and password.

### 5. Reset the local admin password (if needed)

After a database restore the admin password may not match what you expect (e.g. it may still be the production password, or the hash may not verify correctly in the new environment -- honestly we -- me and Claude -- don't know why but it happens). Reset it via wp-cli from inside the toolkit container:

```bash
# open a shell in the toolkit container
docker exec -it daps-toolkit-1 bash -l

# list users to find the username
docker exec mywpsite-wordpress-1 wp user list --allow-root

# reset the password
docker exec mywpsite-wordpress-1 wp user update <username> --user_pass=<newpassword> --allow-root
```

### 6. Install the Redis Object Cache plugin

In WP Admin, go to **Plugins > Add New**, search for **Redis Object Cache**, install and activate it. Then go to **Settings > Redis** and click **Enable Object Cache**.

---

## Backing up your site

From the DAPS root folder:

```bash
dapsman prod backup --project mywpsite --provider <name>
```

This runs `_scripts/backup-remote.toolkit.sh` from inside the toolkit container. Backups are saved to `_backups/from_prod/` inside the project folder. See the script for what is backed up (database dump and `wp-content/uploads` by default).

---

## Project structure

```
mywpsite/
├── _caddy_sites/                        # Caddy reverse proxy configs (imported by DAPS)
│   ├── mywpsite.dev.caddy
│   └── mywpsite.prod.caddy
├── _docker/                             # Docker Compose files
│   ├── compose_mywpsite.yaml            # Base config (shared dev + prod)
│   ├── compose_mywpsite.dev.yaml        # Dev overrides (ports, PhpMyAdmin, volume mounts)
│   ├── compose_mywpsite.prod.yaml       # Prod overrides (volume paths, Redis, site URL)
│   └── compose_daps_mywpsite.dev.yaml   # Toolkit extension (mounts project into toolkit)
├── _scripts/                            # Utility scripts
│   ├── prerequisites.sh                 # Secret generation (run automatically by dapsman)
│   ├── prerequisites.dev.sh             # Dev-only setup: secrets + hosts entry (run automatically by dapsman)
│   └── backup-remote.toolkit.sh        # Backup script run from toolkit container
├── _secrets/                            # Generated secrets — gitignored, never commit
├── _backups/                            # Local backup archive — gitignored
├── wp-content/                          # WordPress content
│   ├── themes/                          # Tracked in git — customise your theme here
│   ├── uploads/                         # Gitignored — managed on server, backed up separately
│   └── plugins/                         # Gitignored — reinstall via WP Admin or backup restore
```

---

## Services

| Service | Dev port | Description |
|---|---|---|
| wordpress | 8080 | WordPress (Apache + PHP 8.3) |
| db | 3306 | MySQL 8.0 database |
| redis | — | Redis 7 object cache |
| phpmyadmin | 8082 | Database browser (dev only) |

---

## Custom WordPress image

The template builds a custom image (`_docker/wordpress.dockerfile`) that extends the official WordPress image with wp-cli baked in. wp-cli is used in the sync and restore scripts for URL rewriting and Redis cache flushing after database imports.

The image must be built before the first remote deploy. `dapsman prod deploy` handles this via `_scripts/build-docker-images.toolkit.sh`, which builds the image inside the toolkit container and exports it as a tar to `_docker/image-exports/` for upload to the remote server.

---

## Secrets reference

10 secret files live in `_secrets/` (gitignored). All are generated by `prerequisites.sh`:

| File | Used by |
|---|---|
| `db_password.txt` | MySQL user password |
| `mysql_root_password.txt` | MySQL root password |
| `auth_key.txt` | WordPress security key |
| `secure_auth_key.txt` | WordPress security key |
| `logged_in_key.txt` | WordPress security key |
| `nonce_key.txt` | WordPress security key |
| `auth_salt.txt` | WordPress security salt |
| `secure_auth_salt.txt` | WordPress security salt |
| `logged_in_salt.txt` | WordPress security salt |
| `nonce_salt.txt` | WordPress security salt |

Secrets are bind-mounted as files into containers at `/run/secrets/` and must be `chmod 644` so `www-data` inside the WordPress container can read them.

---

## wp-content

`wp-content/` contains themes, plugins, and uploads. It is handled differently per environment:

- **Dev**: bind-mounted from `../wp-content` so theme and plugin edits are reflected immediately without rebuilding the container.
- **Prod**: lives at `/srv/projects/mywpsite/wp-content` on the VM and is bind-mounted from there. This path persists across deploys.

`themes/` is tracked in git. `plugins/` and `uploads/` are gitignored — reinstall plugins via WP Admin after init; uploads are managed via backup and restore.

---

## Sync scripts

**`sync-local-to-remote.toolkit.sh`** — pushes the local WordPress state to the remote server. Rsyncs `wp-content/`, dumps the local database, imports it on the remote, runs `wp search-replace` to rewrite local URLs (`mywpsite.localhost`) to the production domain, then flushes the Redis object cache. Cache flush is required after a DB import, otherwise WordPress reads stale cached options from Redis.

**`sync-remote-to-local.toolkit.sh`** — the reverse: pulls `wp-content/` and a DB dump from the remote server to local dev, then rewrites URLs to the `.localhost` domain. Useful for working with real production content locally.

---

## Upgrading WordPress core

WordPress core files live inside the container image, not in a bind-mounted directory. **Do not use the WordPress dashboard to upgrade WordPress core.** The dashboard upgrade modifies files inside the container's writable layer, which works until the container is recreated.

To prevent this from happening silently, the template sets `WP_AUTO_UPDATE_CORE false` in `WORDPRESS_CONFIG_EXTRA`, which disables WordPress's background auto-update feature. Core updates go through the image tag flow below. On the next `dapsman local build --build`, `dapsman prod deploy`, or `docker compose down` + `up`, the container is rebuilt from the image — and all those in-container changes are gone. If the database was migrated to the newer WP schema but the running core reverts to the old version, the mismatch can cause PHP errors, broken admin screens, or unpredictable behavior.

The correct upgrade path is to update the image tag and rebuild.

### Steps

1. **Put prod offline** if the site has active traffic (prevents writes during the upgrade):
   ```
   dapsman prod offline --project mywpsite [--provider <name>]
   ```

2. **Sync prod to local** (prod is canonical for WordPress — don't lose any recent content):
   ```
   dapsman local sync-from-prod --project mywpsite [--provider <name>]
   ```

3. **Back up prod** (good to have in case something goes wrong and you need to restore the site):
   ```
   dapsman prod backup --project mywpsite --provider <name>
   ```

4. **Update the image tag** in `_docker/wordpress.dockerfile`:
   ```dockerfile
   # before
   FROM wordpress:6.7-php8.3-apache
   # after (check Docker Hub for the actual available tag)
   FROM wordpress:7.0-php8.3-apache
   ```
   Before committing: check Docker Hub for the exact tag and verify PHP compatibility with your installed plugins.

5. **Rebuild and test locally**:
   ```
   dapsman local build --build --project mywpsite
   ```
   Then log into the local site (e.g. `mywpsite.localhost`).

   WordPress will prompt you to run its database migration on first page load — complete it. Then upgrade any plugins with pending updates via WP Admin. Test the front end and admin thoroughly.

6. **Deploy to prod**:
   ```
   dapsman prod deploy --build --project mywpsite --provider <name>
   ```
   Rebuilds the image from the new tag, exports it, uploads it, and restarts the container. WordPress will auto-migrate prod's database on first page load.

7. **Sync local to prod** (optional: see **alternative** below):
   ```
   dapsman prod sync-from-local --project mywpsite --provider <name>
   ```
   Since you ran the DB migration and upgraded plugins locally, local is now ahead of prod. This pushes the migrated database and updated `wp-content/` to the remote server.

8. **Bring prod back online**:
   ```
   dapsman prod online --project mywpsite [--provider <name>]
   ```

WordPress handles its own database schema migrations — no manual SQL needed.

> **Alternative:** If you prefer, you can skip step 7 and run the DB migration and plugin upgrades directly in prod WP Admin after step 6. Both paths work — the local-first approach keeps prod offline for less time and lets you catch problems before they reach prod.

### Plugins and themes

Plugins and themes live in `wp-content/`, which is bind-mounted. These **can** be updated via WP Admin. Recommended flow: update on prod (since prod has the live data), then sync back to local with `dapsman local sync-from-prod`.

---

## Running multiple WordPress projects

Multiple WordPress projects can run alongside each other on the same DAPS instance. The template is designed for this:

- `db` and `redis` are on an internal project network (`mywpsite_net`) and are not visible to other projects.
- Caddy routes to the WordPress container via a project-specific alias (`mywpsite`) on `daps_net`, not the generic service name `wordpress`, so there is no DNS collision.

The one thing you must change manually for a second project is the **dev host port bindings** in `_docker/compose_mywpsite.dev.yaml`, since each project uses fixed ports and only one can bind a given host port at a time:

| Service | Default port | Change to (example) |
|---|---|---|
| wordpress | 8080 | 8081 |
| db | 3306 | 3307 |
| phpmyadmin | 8082 | 8083 |

These ports are only used for direct local access (e.g. connecting a DB tool to MySQL). Traffic through `https://mywpsite.localhost` goes via Caddy and is unaffected.


