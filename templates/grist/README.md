# DAPS Grist Template

A template for running a self-hosted [Grist](https://www.getgrist.com/) instance in containers managed by [DAPS](https://github.com/your-org/daps).

Grist is an open-source spreadsheet/database hybrid. Self-hosting gives you a private data workspace with a built-in UI and a REST API for reading and writing data from other applications.

---

## Before you start: replace the placeholder name

This template uses `mygrist` as a placeholder throughout. There are two ways to set up a project from this template:

### Option A: Use `dapsman init` (recommended)

From the DAPS root folder:

```bash
dapsman init --template grist --name yourprojectname
```

This copies the template, replaces all `mygrist` placeholders with your project name (in file contents and filenames), removes any inherited secrets, and registers the project in `daps.yaml` automatically.

### Option B: Manual setup

Do a find-and-replace across the whole folder:

| Replace | With |
|---|---|
| `mygrist` | your project name (e.g. `mypersonaldata`) |
| `mygrist.localhost` | your local dev hostname (e.g. `mypersonaldata.localhost`) |
| `mygrist.example.com` | your real domain name (e.g. `data.example.com`) |

Files to update (rename the files too):
- `_docker/compose_mygrist.yaml`
- `_docker/compose_mygrist.dev.yaml`
- `_docker/compose_mygrist.prod.yaml`
- `_docker/compose_daps_mygrist.dev.yaml`
- `_caddy_sites/mygrist.dev.caddy`
- `_caddy_sites/mygrist.prod.caddy`

Then add an entry to the root `daps.yaml`:
```yaml
projects:
  mygrist:           # <-- your project name
    path: ../mygrist # <-- path to this folder
```

---

## Initial setup

### 1. Set your email address

Open `_docker/compose_mygrist.yaml` and replace `admin@example.com` with your email address. Grist uses this as the default account — it can't be changed after the first run without resetting your data.

### 2. Generate secrets (optional)

The Grist session secret is generated automatically by `dapsman local build` the first time you run it. It is written to `_secrets/.env`, which is gitignored.

To generate it manually, or to regenerate it (e.g. after a security incident), run:

```bash
./_scripts/prerequisites.dev.sh           # skip existing secret
./_scripts/prerequisites.dev.sh --force   # regenerate
```

Never commit the `_secrets/` folder or share its contents.

### 3. Add your local hostname

`dapsman local build` runs `prerequisites.dev.sh` automatically, which will attempt to add the hosts entry for you:

- **Windows**: requires an elevated terminal (right-click → Run as administrator). If not elevated, a warning is printed and you can add it manually.
- **Mac/Linux**: requires sudo. If credentials aren't cached, a warning is printed with the manual command.
- **Chrome and Firefox** resolve `*.localhost` automatically without a hosts entry, so this step is only strictly needed for other tools.

If you need to add it manually:

```
# Windows: C:\Windows\System32\drivers\etc\hosts
# Mac/Linux: /etc/hosts

127.0.0.1  mygrist.localhost
```

### 4. Start the local site

From the DAPS root folder:

```
dapsman local build --project mygrist
```

Your Grist instance will be available at `https://mygrist.localhost` (via Caddy) or `http://localhost:8484` (direct).

### 5. Set up your Grist account

Open your site in a browser. Grist will prompt you to create an account. Use the email address you intend to set as `GRIST_DEFAULT_EMAIL` in the prod compose file.

---

## Production setup

### 1. Set your email

In `_docker/compose_mygrist.prod.yaml`, replace `admin@example.com` with your email address. Grist uses this to associate documents with an owner.

### 2. Set up a password for the Caddy basic auth

The prod Caddy site file (`_caddy_sites/mygrist.prod.caddy`) uses HTTP basic auth to protect your Grist instance. You need to replace the placeholder hash with one generated for your chosen password:

```bash
docker run --rm caddy:latest caddy hash-password --plaintext 'yourpassword'
```

Copy the output hash into `_caddy_sites/mygrist.prod.caddy`, replacing `$2a$14$REPLACEME_...`.

You can also change the username from `admin` to anything you like.

### 3. Point DNS at your host

Confirm `_caddy_sites/mygrist.prod.caddy` names your real domain, not the `mygrist.example.com` placeholder, then read "DNS and HTTPS" in `daps/docs/readme-deployment.md` — whether you create the A record before or after deploying depends on whether the domain is already serving a live site.

### 4. Deploy

```bash
dapsman prod deploy --project mygrist --provider <name>
```

---

## Backing up your site

```bash
dapsman prod backup --project mygrist --provider <name>
```

This rsyncs the remote `/persist` directory (where all Grist documents live as SQLite files) to `_backups/from_prod/persist/` inside the project folder.

---

## Project structure

```
mygrist/
├── _caddy_sites/                        # Caddy reverse proxy configs (imported by DAPS)
│   ├── mygrist.dev.caddy
│   └── mygrist.prod.caddy
├── _docker/                             # Docker Compose files
│   ├── compose_mygrist.yaml            # Base config (shared dev + prod)
│   ├── compose_mygrist.dev.yaml        # Dev overrides (port, bind mount)
│   ├── compose_mygrist.prod.yaml       # Prod overrides (persist path, email)
│   └── compose_daps_mygrist.dev.yaml   # Toolkit extension (mounts project into toolkit)
├── _scripts/                            # Utility scripts
│   ├── generate-secrets.sh             # Shared secret generation library
│   ├── prerequisites.dev.sh            # Dev-only setup: secret + hosts entry (run automatically by dapsman)
│   ├── prerequisites.prod.sh           # Prod setup: secret on remote (run automatically by dapsman)
│   ├── init-template.toolkit.sh        # One-time init script run by dapsman init
│   └── backup-remote.toolkit.sh        # Backup script run from toolkit container
├── _secrets/                            # Generated secrets — gitignored, never commit
│   └── .env                            # Contains GRIST_SESSION_SECRET
├── _backups/                            # Local backup archive — gitignored
└── persist/                             # Grist data directory — gitignored, dev only
```

---

## Services

| Service | Dev port | Description |
|---|---|---|
| grist | 8484 | Grist Core (Node.js) |

---

## Upgrading Grist

The Grist version is pinned in `_docker/compose_mygrist.yaml`. Grist documents are stored as SQLite files in the `/persist` directory, which is bind-mounted from the host — they are not inside the container and are safe across image upgrades.

1. **Back up first** (Grist may migrate document schemas on startup):
   ```
   dapsman prod backup --project mygrist --provider <name>
   ```

2. **Update the image tag** in `_docker/compose_mygrist.yaml`:
   ```yaml
   image: gristlabs/grist:1.7  →  gristlabs/grist:1.8
   ```

3. **Test locally**:
   ```
   dapsman local build --project mygrist
   ```
   Open `https://mygrist.localhost` and confirm your documents open correctly.

4. **Deploy**:
   ```
   dapsman prod deploy --project mygrist --provider <name>
   ```
   Grist applies any necessary document migrations on first startup.

---

## Using Grist as a data backend

Grist exposes a REST API at `https://mygrist.example.com/api/`. You can read and write table data, making Grist useful as a lightweight backend where you manage data in the Grist UI and consume it from other applications.

See the [Grist API documentation](https://support.getgrist.com/api/) for details. You'll need an API key, which you can generate in Grist under **Profile → API key**.
