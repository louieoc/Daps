#!/usr/bin/env bash
# sync-remote-to-local.toolkit.sh
# Syncs remote (prod) WordPress files and database to the local dev environment.
# Run from the toolkit container via: dapsman local sync-from-prod
#
# WARNING: This overwrites the local database and wp-content. Make sure
# you have a backup of local dev work you want to keep before running.
#
# Required env vars (set by dapsman):
#   DAPS_PROJECT      - project name (e.g. mywpsite)
#   DAPS_REMOTE_HOST  - remote server IP or hostname
#   DAPS_REMOTE_USER  - SSH user on the remote server
#   DAPS_SSH_KEY      - absolute path to SSH private key inside toolkit (e.g. /root/.ssh/daps-key-ramnode)

set -euo pipefail

: "${DAPS_PROJECT:?DAPS_PROJECT is required}"
: "${DAPS_REMOTE_HOST:?DAPS_REMOTE_HOST is required}"
: "${DAPS_REMOTE_USER:?DAPS_REMOTE_USER is required}"
: "${DAPS_SSH_KEY:?DAPS_SSH_KEY is required}"

PROJECT_ROOT="/srv/projects/${DAPS_PROJECT}"
SSH_OPTS=(-o StrictHostKeyChecking=accept-new -o BatchMode=yes -i "${DAPS_SSH_KEY}")

# --- Resolve URLs from caddy files ---
dev_caddy="${PROJECT_ROOT}/_caddy_sites/${DAPS_PROJECT}.dev.caddy"
prod_caddy="${PROJECT_ROOT}/_caddy_sites/${DAPS_PROJECT}.prod.caddy"

if [[ ! -f "$dev_caddy" ]]; then
  echo "Dev caddy file not found: $dev_caddy" >&2; exit 1
fi
if [[ ! -f "$prod_caddy" ]]; then
  echo "Prod caddy file not found: $prod_caddy" >&2; exit 1
fi

dev_domain=$(grep -v '^\s*#' "$dev_caddy" | grep -v '^\s*$' | head -1 | awk '{print $1}')
prod_domain=$(grep -v '^\s*#' "$prod_caddy" | grep -v '^\s*$' | head -1 | awk '{print $1}')
dev_url="http://${dev_domain}"
prod_url="https://${prod_domain}"

echo "Syncing ${DAPS_PROJECT}: remote (${prod_url}) -> local (${dev_url})"
echo "Remote: ${DAPS_REMOTE_USER}@${DAPS_REMOTE_HOST}"
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
remote_db_container="${DAPS_PROJECT}-db-1"
tmp_dump="/tmp/${DAPS_PROJECT}-sync-$(date +%Y%m%d%H%M%S).sql"

# --- Step 1: Sync wp-content files from remote to local ---
echo "Step 1/3: syncing wp-content..."
rsync -az --delete \
  --exclude='cache/' \
  --exclude='upgrade/' \
  -e "ssh ${SSH_OPTS[*]}" \
  "${DAPS_REMOTE_USER}@${DAPS_REMOTE_HOST}:/srv/projects/${DAPS_PROJECT}/wp-content/" \
  "${PROJECT_ROOT}/wp-content/"
echo "- wp-content sync done"

# --- Step 2: Dump remote DB, transfer via SSH, import into local DB container ---
echo "Step 2/3: syncing database..."

ssh "${SSH_OPTS[@]}" "${DAPS_REMOTE_USER}@${DAPS_REMOTE_HOST}" \
  "remote_pw=\$(cat /srv/projects/${DAPS_PROJECT}/_secrets/mysql_root_password.txt) && \
   docker exec ${remote_db_container} mysqldump \
     -uroot -p\"\${remote_pw}\" \
     --default-character-set=utf8mb4 \
     --add-drop-table \
     ${db_name}" > "${tmp_dump}"

docker exec -i "${local_db_container}" mysql \
  --binary-mode=1 -uroot -p"${root_pw}" "${db_name}" < "${tmp_dump}"

rm -f "${tmp_dump}"
echo "- database sync done"

# --- Step 3: Replace URLs in local DB ---
echo "Step 3/3: replacing URLs (${prod_url} -> ${dev_url})..."
docker exec "${local_wp_container}" wp search-replace "${prod_url}" "${dev_url}" \
  --allow-root --path=/var/www/html
docker exec "${local_wp_container}" wp cache flush \
  --allow-root --path=/var/www/html
echo "- URL replacement done"

echo ""
echo "Sync complete."
