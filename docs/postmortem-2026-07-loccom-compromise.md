<!-- no index -->
# Postmortem: loccom WordPress Compromise — July 2026

## Summary

The `loccom` project (louisocallaghan.com) was compromised via an unauthenticated remote code execution exploit starting July 30, 2026. Three unauthorized administrator accounts were created and multiple backdoor plugins were installed. The site served a blank white page to visitors for approximately two days before the compromise was discovered during unrelated troubleshooting.

---

## Timeline

| Time (UTC) | Event |
|---|---|
| 2026-07-31 01:32 | First unauthorized admin account created: `cunotiet` (louisocallaghanew@outlook.com) |
| 2026-07-31 03:54 | Second unauthorized admin account: `adminbockup` (adminbockup@wordpress.org) |
| 2026-07-31 08:37 | Third unauthorized admin account: `admin_n91894hh9g` |
| 2026-07-31 (unknown) | Attacker installed `fileorganizer` plugin and planted `wp-compat/wp-compat.php` + must-use plugins in `wp-content/mu-plugins/` |
| 2026-07-31 (unknown) | `wp-compat.php` PHP fatal error begins causing blank white screen for all visitors |
| 2026-08-01 | Site owner notices blank white screen while troubleshooting an unrelated issue |
| 2026-08-01 | Investigation: all containers healthy, HTTP 200 from server, DNS correct — issue narrowed to WordPress itself |
| 2026-08-01 | `wp user list` reveals 3 unauthorized admin accounts and the malicious `wp-compat` plugin warning |
| 2026-08-01 | Unauthorized users 7, 8, 9 deleted via wp-cli; `wp-compat` deactivated; admin password reset |
| 2026-08-01 | Site remains stopped on prod pending hardening and clean redeploy |
| 2026-08-02 | Full postmortem written; hardening applied; recovery plan confirmed |

---

## Findings

### Unauthorized administrator accounts

Three accounts were created by the attacker with full administrator privileges:

| ID | Username | Email | Registered |
|---|---|---|---|
| 7 | cunotiet | louisocallaghanew@outlook.com | 2026-07-31 01:32 |
| 8 | adminbockup | adminbockup@wordpress.org | 2026-07-31 03:54 |
| 9 | admin_n91894hh9g | admin_n91894hh9g_8pj05h@yahoo.com | 2026-07-31 08:37 |

All three were deleted via wp-cli during the incident.

### Malicious plugins installed

| Plugin | Type | Status |
|---|---|---|
| `wp-compat/wp-compat.php` | Regular plugin | Caused PHP fatal error → blank white screen; removed |
| `site-compat-layer` | Must-use plugin (mu-plugins/) | Persistent backdoor; not removable via WP Admin |
| `sso-loader` | Must-use plugin (mu-plugins/) | Persistent backdoor |
| `wp-compat-layer` | Must-use plugin (mu-plugins/) | Persistent backdoor |
| `fileorganizer` | Regular plugin | Attacker-installed file manager; used to plant other files |

Must-use plugins in `wp-content/mu-plugins/` load automatically on every request and cannot be deactivated through WP Admin or standard wp-cli commands. These represent persistent, deep backdoor access.

### Pre-existing suspicious accounts (unrelated to this incident)

The user list also revealed two accounts created in 2011 — likely from a previous compromise of this long-running site:

| ID | Username | Email | Registered |
|---|---|---|---|
| 5 | user | support@wordpress.com | 2011-06-27 |
| 6 | user | support@wordpress.com | 2011-06-27 |

These are not default WordPress accounts. They were retained for investigation but should be deleted.

---

## Root Cause

### The exploit: CVE-2026-60137 ("wp2shell")

A two-bug chained exploit affecting WordPress 6.8 and above:

1. **SQL injection (CVE-2026-60137, CVSS 9.1)** — present in WordPress 6.8+. Gives an unauthenticated attacker access to internal WordPress API pathways.
2. **Batch endpoint route confusion** — present in WordPress 6.9+. Allows untrusted input through the internal API, completing the RCE chain.

Combined: unauthenticated remote code execution on any default, unmodified WordPress 6.9+ installation. No credentials, no special configuration, no plugin required.

### Why the site was vulnerable

The `loccom` compose files did not include `WP_AUTO_UPDATE_CORE false`. WordPress's built-in background auto-update feature silently upgraded the core files from 6.7 to 6.8 (and likely 6.9) inside the running container's writable layer. The Docker image tag remained `loccom-wordpress` built from `wordpress:6.7-php8.3-apache`, but the code executing on every request was the auto-updated version — which was vulnerable to wp2shell.

This is the exact scenario documented in the WordPress template README under "Upgrading WordPress core": in-container modifications via auto-update or the WP dashboard are lost on container rebuild, but they are dangerous precisely because they work right up until that point.

### The attack sequence

1. wp2shell RCE exploited the SQL injection + batch endpoint chain to execute arbitrary PHP code — no login required
2. Attacker used the execution context to install the `fileorganizer` plugin (a legitimate WP file manager), giving a persistent file-browsing and upload interface
3. FileOrganizer was used to plant backdoor files directly into `wp-content/`:
   - `plugins/wp-compat/wp-compat.php` — a malicious plugin that inadvertently caused a PHP fatal error on line 164, breaking the site
   - `mu-plugins/site-compat-layer`, `mu-plugins/sso-loader`, `mu-plugins/wp-compat-layer` — persistent backdoors that load automatically regardless of plugin activation state
4. Three administrator accounts were created for persistent access via the WP Admin UI

The PHP fatal in `wp-compat.php` was not intentional — it caused the white screen of death that ultimately drew attention to the compromise.

---

## Blast Radius

### Within the WordPress container (confirmed compromised)
- Full read/write access to `wp-content/` — which is bind-mounted to the host VM at `/srv/projects/loccom/wp-content/`, so all attacker-written files persist on the host
- Docker secrets at `/run/secrets/` (10 WordPress security keys + MySQL password) were readable by any process running inside the container
- MySQL database fully accessible: all posts, comments, user data, email addresses, and any contact form submissions could have been read or modified

### Via loccom_net (internal project network)
- Full access to the MySQL container and Redis (no authentication on Redis)
- Could read, modify, or drop the WordPress database

### Via daps_net (shared network — potential lateral movement)
- The loccom WordPress container is on `daps_net`, the shared network used by all projects on this server
- Other containers reachable: `daps-caddy-1`, `j-shirt-site-1`, `j-shirt-api-1`
- The j-shirt.com project containers were potentially probed; however, j-shirt is a .NET Blazor app with a different attack surface than WordPress, and no compromise evidence was found

### Host VM
- The WordPress container does NOT have the Docker socket mounted, so the attacker could not escape to the host via Docker
- Bind mounts are limited to `wp-content/` — arbitrary host path writes were not possible from the container
- The host is likely not fully compromised, but should be treated with caution

### What the attackers likely intended
- SEO spam: hosting keyword-stuffed pages to manipulate search rankings for other sites
- Server resources: using the container for spam relay or compute (crypto mining)
- Persistence and pivot: admin accounts + mu-plugins provide stable long-term access, and the shared server also hosts j-shirt.com

---

## Remediation

### Immediate actions taken

1. Deleted unauthorized admin accounts (IDs 7, 8, 9) via wp-cli
2. Deactivated `wp-compat` plugin (was already partially removed; wp-cli confirmed deletion)
3. Reset admin password via wp-cli
4. Stopped the loccom WordPress container on prod

### Recovery plan

**Option A (recommended): prod teardown + redeploy from local**

Given the depth of the compromise (mu-plugins, attacker file-write access to all of `wp-content/`), the safest path is a complete teardown of the prod project and a clean redeploy from the local state:

```
dapsman prod teardown --project loccom --provider ramnode
dapsman prod deploy --build --project loccom --provider ramnode
dapsman prod sync-from-local --project loccom --provider ramnode
```

**Option B: in-place cleanup**

If data loss is unacceptable, at minimum:

```bash
# On the prod server:
rm -rf /srv/projects/loccom/wp-content/mu-plugins/
rm -rf /srv/projects/loccom/wp-content/plugins/fileorganizer/
# Scan themes for common backdoor patterns:
grep -r "eval(base64_decode" /srv/projects/loccom/wp-content/themes/
grep -r "system(" /srv/projects/loccom/wp-content/themes/
```

Then redeploy the hardened compose config.

### Hardening applied

The following `wp-config.php` constants were added to both `compose_loccom.dev.yaml` and `compose_loccom.prod.yaml` via `WORDPRESS_CONFIG_EXTRA`:

```php
define( 'WP_AUTO_UPDATE_CORE', false );  // Prevents silent core upgrades inside the container
define( 'DISALLOW_FILE_MODS', true );    // Blocks plugin/theme install and file editor from WP Admin
```

These two constants together close the two main attack surface areas:
- `WP_AUTO_UPDATE_CORE false` prevents WordPress from self-updating to a vulnerable version inside the container's writable layer
- `DISALLOW_FILE_MODS true` blocks attackers from installing plugins or writing files via the WP Admin interface, even if they obtain admin credentials

The same hardening has been applied to the WordPress template for all future projects.

---

## Lessons Learned

1. **`WP_AUTO_UPDATE_CORE false` is not optional.** The loccom project predates the template's addition of this constant. All existing projects must be audited to ensure it is set.

2. **`DISALLOW_FILE_MODS true` should be standard in prod.** This would have blocked the attacker from installing FileOrganizer even after achieving RCE. It is now part of the WordPress template.

3. **Shared network means shared risk.** `daps_net` connects all project containers. A compromise of one project is a potential pivot to others. Consider whether all projects strictly need to be on the same network, or whether Caddy could route via host ports for greater isolation.

4. **Old accounts accumulate.** The 2011 accounts (users 5 and 6) suggest this site was hacked before, years ago, and those accounts were never noticed or cleaned up. Regular user audits are worthwhile on long-running sites.

5. **The white screen was the alert.** The blank page was caused by an attacker bug, not a monitoring system. There is no alerting set up for site availability.

---

## Follow-up items

- [x] Delete legacy suspicious accounts (users 5 and 6) after recovery
- [ ] <strike>Check prod content for attacker-added posts/pages via wp-cli</strike>
- [x] Rotate all loccom secrets (MySQL password, WordPress security keys) — the attacker could read these from `/run/secrets/`
- [ ] Investigate wp.dapster.org TLS cert renewal failures visible in Caddy logs (separate issue, unrelated to this incident)
- [x] Fix `dapsman prod offline` bug: multiple Caddy site entries are collapsed to one with a trailing comma, producing invalid Caddyfile syntax
- [ ] Consider site availability monitoring for all prod projects
