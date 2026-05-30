#!/usr/bin/env bash
# restore-local.toolkit.sh
# Restores a WordPress backup to the local dev environment.
# Imports a database dump, overwrites wp-content, replaces prod URLs with dev URLs,
# and flushes the cache.
#
# Run from the toolkit container via: dapsman local restore
#
# WARNING: This overwrites the local database and wp-content. Any local dev changes
# not captured in a backup will be lost.
#
# Required env vars (set by dapsman):
#   DAPS_PROJECT           - project name (e.g. mywpsite)
#   DAPS_RESTORE_ENV       - environment the backup came from (e.g. prod)
#   DAPS_RESTORE_TIMESTAMP - timestamp string from backup filenames (e.g. 20260420_202738)
#   DAPS_BACKUPS_PATH      - absolute path to _backups/from_prod/ inside the toolkit
#
# Optional env vars:
#   DAPS_PROD_URL          - prod URL override (e.g. https://example.com); if empty,
#                            derived from _caddy_sites/<project>.prod.caddy

# TODO: extract shared URL-replacement + cache-flush logic into a library script
# (lib-wp-local.sh) shared with sync-remote-to-local.toolkit.sh

set -euo pipefail

: "${DAPS_PROJECT:?DAPS_PROJECT is required}"
: "${DAPS_RESTORE_ENV:?DAPS_RESTORE_ENV is required}"
: "${DAPS_RESTORE_TIMESTAMP:?DAPS_RESTORE_TIMESTAMP is required}"
: "${DAPS_BACKUPS_PATH:?DAPS_BACKUPS_PATH is required}"

PROJECT_ROOT="/srv/projects/${DAPS_PROJECT}"
SQL_FILE="${DAPS_BACKUPS_PATH}/${DAPS_PROJECT}_${DAPS_RESTORE_ENV}_${DAPS_RESTORE_TIMESTAMP}.sql"
TAR_FILE="${DAPS_BACKUPS_PATH}/${DAPS_PROJECT}_${DAPS_RESTORE_ENV}_wp-content_${DAPS_RESTORE_TIMESTAMP}.tar.gz"

# --- Validate backup files exist ---
if [[ ! -f "$SQL_FILE" ]]; then
  echo "SQL backup file not found: $SQL_FILE" >&2; exit 1
fi
if [[ ! -f "$TAR_FILE" ]]; then
  echo "wp-content archive not found: $TAR_FILE" >&2; exit 1
fi

# --- Resolve URLs ---
dev_caddy="${PROJECT_ROOT}/_caddy_sites/${DAPS_PROJECT}.dev.caddy"
prod_caddy="${PROJECT_ROOT}/_caddy_sites/${DAPS_PROJECT}.prod.caddy"

if [[ ! -f "$dev_caddy" ]]; then
  echo "Dev caddy file not found: $dev_caddy" >&2; exit 1
fi

dev_domain=$(grep -v '^\s*#' "$dev_caddy" | grep -v '^\s*$' | head -1 | awk '{print $1}')
dev_url="http://${dev_domain}"

if [[ -n "${DAPS_PROD_URL:-}" ]]; then
  prod_url="$DAPS_PROD_URL"
else
  if [[ ! -f "$prod_caddy" ]]; then
    echo "Prod caddy file not found: $prod_caddy (use --prod-url to specify prod URL manually)" >&2; exit 1
  fi
  prod_domain=$(grep -v '^\s*#' "$prod_caddy" | grep -v '^\s*$' | head -1 | awk '{print $1}')
  prod_url="https://${prod_domain}"
fi

echo "Restoring ${DAPS_PROJECT} from backup: ${DAPS_RESTORE_ENV} ${DAPS_RESTORE_TIMESTAMP}"
echo "Prod URL: ${prod_url} -> Dev URL: ${dev_url}"
echo ""

# --- Read local secrets ---
root_pw_file="${PROJECT_ROOT}/_secrets/mysql_root_password.txt"
if [[ ! -f "$root_pw_file" ]]; then
  echo "Local root password file not found: $root_pw_file" >&2; exit 1
fi
root_pw=$(cat "$root_pw_file")
db_name="${DAPS_PROJECT}_wp"
local_db_container="${DAPS_PROJECT}-db-1"
local_wp_container="${DAPS_PROJECT}-wordpress-1"

# --- Step 1: Import SQL into local database ---
echo "Step 1/3: importing database..."
docker exec -i "${local_db_container}" mysql \
  --binary-mode=1 -uroot -p"${root_pw}" "${db_name}" < "${SQL_FILE}"
echo "- database import done"

# --- Step 2: Extract wp-content archive ---
echo "Step 2/3: extracting wp-content..."
tar -xzf "${TAR_FILE}" -C "${PROJECT_ROOT}"
echo "- wp-content restore done"

# --- Step 3: Replace URLs and flush cache ---
echo "Step 3/3: replacing URLs (${prod_url} -> ${dev_url})..."
docker exec "${local_wp_container}" wp search-replace "${prod_url}" "${dev_url}" \
  --allow-root --path=/var/www/html
docker exec "${local_wp_container}" wp cache flush \
  --allow-root --path=/var/www/html
echo "- URL replacement done"

echo ""
echo "Restore complete."
