#!/usr/bin/env bash
# backup-local.toolkit.sh
# Creates a timestamped backup of the LOCAL (dev) WordPress site:
#   - A full MySQL database dump (utf8mb4) as <project>_<env>_<timestamp>.sql
#   - A compressed archive of wp-content as <project>_<env>_wp-content_<timestamp>.tar.gz
#
# Run from the toolkit container via: dapsman local backup
#
# The local project is bind-mounted into the toolkit at /srv/projects/<project>, and the
# toolkit has the Docker socket, so this reads secrets and files directly and talks to the
# local containers with docker exec. No SSH is involved.
#
# The project must be running (dapsman local build) — the database is dumped from the
# running container.
#
# Required env vars (set by dapsman):
#   DAPS_PROJECT      - project name (e.g. myproject)
#
# Optional args (set by dapsman, override defaults here if running manually):
#   --env <env>       - environment name, used in output filenames (default: local)
#   --to <path>       - destination directory inside toolkit (default: /srv/projects/<project>/_backups/from_local)

set -euo pipefail

: "${DAPS_PROJECT:?DAPS_PROJECT is required}"

ENV="local"
BACKUP_DIR="/srv/projects/${DAPS_PROJECT}/_backups/from_local"

while [[ $# -gt 0 ]]; do
  case "$1" in
    --env) ENV="$2"; shift 2 ;;
    --to)  BACKUP_DIR="$2"; shift 2 ;;
    *) echo "Unknown argument: $1" >&2; exit 1 ;;
  esac
done

PROJECT_ROOT="/srv/projects/${DAPS_PROJECT}"
timestamp=$(date +"%Y%m%d_%H%M%S")
db_name="${DAPS_PROJECT}_wp"
local_db_container="${DAPS_PROJECT}-db-1"
db_file="${DAPS_PROJECT}_${ENV}_${timestamp}.sql"
wpcontent_file="${DAPS_PROJECT}_${ENV}_wp-content_${timestamp}.tar.gz"

# --- Validate the local environment ---
if ! docker ps --format '{{.Names}}' | grep -qx "${local_db_container}"; then
  echo "Local database container '${local_db_container}' is not running." >&2
  echo "Run 'dapsman local build --project ${DAPS_PROJECT}' first." >&2
  exit 1
fi

root_pw_file="${PROJECT_ROOT}/_secrets/mysql_root_password.txt"
if [[ ! -f "$root_pw_file" ]]; then
  echo "Local root password file not found: $root_pw_file" >&2; exit 1
fi
root_pw=$(cat "$root_pw_file")

if [[ ! -d "${PROJECT_ROOT}/wp-content" ]]; then
  echo "wp-content not found: ${PROJECT_ROOT}/wp-content" >&2; exit 1
fi

mkdir -p "${BACKUP_DIR}"

# Each artifact is written to a .partial file and renamed only once the command producing it
# has succeeded. The shell creates the output file before mysqldump or tar ever runs, so a
# failure part-way leaves a truncated file behind -- and _backups/from_local is scanned for
# restore points, so a truncated .sql would be offered as one. list-restore-points.toolkit.sh
# matches *.sql exactly, so a .partial is invisible to it, and the trap clears it on the way out.
partial=""
cleanup_partial() {
  if [[ -n "${partial}" ]]; then
    rm -f "${partial}"
  fi
}
trap cleanup_partial EXIT

echo "Backing up ${DAPS_PROJECT} (${ENV}) from the local Docker instance"
echo "Destination: ${BACKUP_DIR}"
echo ""

# --- Step 1: DB dump ---
echo "Step 1/2: dumping database..."
partial="${BACKUP_DIR}/${db_file}.partial"
docker exec "${local_db_container}" mysqldump \
  -uroot -p"${root_pw}" \
  --default-character-set=utf8mb4 \
  --add-drop-table \
  "${db_name}" > "${partial}"
mv "${partial}" "${BACKUP_DIR}/${db_file}"
partial=""
echo "- ${db_file}"

# --- Step 2: wp-content archive ---
echo "Step 2/2: archiving wp-content..."
partial="${BACKUP_DIR}/${wpcontent_file}.partial"
tar -czf "${partial}" -C "${PROJECT_ROOT}" wp-content
mv "${partial}" "${BACKUP_DIR}/${wpcontent_file}"
partial=""
echo "- ${wpcontent_file}"

echo ""
echo "Backup complete: ${BACKUP_DIR}"
