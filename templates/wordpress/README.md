# DAPS WordPress Template

A template for running a self-hosted WordPress site in containers managed by [DAPS](https://codeberg.org/louieoc/Daps).

Includes Redis object caching and PhpMyAdmin (local dev only).

---

## Before you start: replace the placeholder name

This template uses `mywpsite` as a placeholder throughout. There are two ways to set up a project from this template:

### Option A: Use `dapsman init` (recommended)

From the DAPS root folder:

```bash
dapsman init --template wordpress --name yourprojectname
```

This copies the template, replaces all `mywpsite` placeholders with your project name (in file contents and filenames), removes any inherited secrets, and registers the project in `daps.yaml` automatically.

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


