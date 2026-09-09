#!/usr/bin/env bash
# backup-remote.toolkit.sh
# Creates a timestamped backup of the remote WordPress site:
#   - A full MySQL database dump (utf8mb4) as <project>_<timestamp>.sql
#   - A compressed archive of wp-content as <project>_wp-content_<timestamp>.tar.gz
#
# Run from the toolkit container via: dapsman prod backup
#
# Required env vars (set by dapsman):
#   DAPS_PROJECT      - project name (e.g. mywpsite)
#   DAPS_REMOTE_HOST  - remote server IP or hostname
#   DAPS_REMOTE_USER  - SSH user on the remote server
#   DAPS_SSH_KEY      - absolute path to SSH private key inside toolkit (e.g. /root/.ssh/daps-key-ramnode)
#
# Optional args (set by dapsman, override defaults here if running manually):
#   --env <env>       - environment name, used in output filenames (default: prod)
#   --to <path>       - destination directory inside toolkit (default: /srv/projects/<project>/_backups/from_prod)

set -euo pipefail

: "${DAPS_PROJECT:?DAPS_PROJECT is required}"
: "${DAPS_REMOTE_HOST:?DAPS_REMOTE_HOST is required}"
: "${DAPS_REMOTE_USER:?DAPS_REMOTE_USER is required}"
: "${DAPS_SSH_KEY:?DAPS_SSH_KEY is required}"

ENV="prod"
BACKUP_DIR="/srv/projects/${DAPS_PROJECT}/_backups/from_prod"

while [[ $# -gt 0 ]]; do
  case "$1" in
    --env) ENV="$2"; shift 2 ;;
    --to)  BACKUP_DIR="$2"; shift 2 ;;
    *) echo "Unknown argument: $1" >&2; exit 1 ;;
  esac
done

SSH_OPTS=(-o StrictHostKeyChecking=accept-new -o BatchMode=yes -i "${DAPS_SSH_KEY}")
timestamp=$(date +"%Y%m%d_%H%M%S")
db_name="${DAPS_PROJECT}_wp"
remote_db_container="${DAPS_PROJECT}-db-1"
db_file="${DAPS_PROJECT}_${ENV}_${timestamp}.sql"
wpcontent_file="${DAPS_PROJECT}_${ENV}_wp-content_${timestamp}.tar.gz"
remote_tmp_wpcontent="/tmp/${wpcontent_file}"

mkdir -p "${BACKUP_DIR}"

# Each artifact is written to a .partial file and renamed only once the command producing it
# has succeeded. The shell creates the local output file before the remote mysqldump ever runs,
# and scp writes as it goes, so a failure part-way leaves a truncated file behind -- which
# _backups/from_prod would then offer as a restore point. list-restore-points.toolkit.sh matches
# *.sql exactly, so a .partial is invisible to it, and the trap clears it on the way out.
partial=""
cleanup_partial() {
  if [[ -n "${partial}" ]]; then
    rm -f "${partial}"
  fi
}
trap cleanup_partial EXIT

echo "Backing up ${DAPS_PROJECT} (${ENV}) from ${DAPS_REMOTE_USER}@${DAPS_REMOTE_HOST}"
echo "Destination: ${BACKUP_DIR}"
echo ""

# --- Step 1: DB dump streamed through SSH ---
echo "Step 1/2: dumping database..."
partial="${BACKUP_DIR}/${db_file}.partial"
ssh "${SSH_OPTS[@]}" "${DAPS_REMOTE_USER}@${DAPS_REMOTE_HOST}" \
  "remote_pw=\$(cat /srv/projects/${DAPS_PROJECT}/_secrets/mysql_root_password.txt) && \
   docker exec ${remote_db_container} mysqldump \
     -uroot -p\"\${remote_pw}\" \
     --default-character-set=utf8mb4 \
     --add-drop-table \
     ${db_name}" > "${partial}"
mv "${partial}" "${BACKUP_DIR}/${db_file}"
partial=""
echo "- ${db_file}"

# --- Step 2: wp-content tar created on remote, SCP'd down, cleaned up ---
echo "Step 2/2: archiving wp-content..."
partial="${BACKUP_DIR}/${wpcontent_file}.partial"
ssh "${SSH_OPTS[@]}" "${DAPS_REMOTE_USER}@${DAPS_REMOTE_HOST}" \
  "tar -czf '${remote_tmp_wpcontent}' -C /srv/projects/${DAPS_PROJECT} wp-content"
scp "${SSH_OPTS[@]}" \
  "${DAPS_REMOTE_USER}@${DAPS_REMOTE_HOST}:${remote_tmp_wpcontent}" \
  "${partial}"
ssh "${SSH_OPTS[@]}" "${DAPS_REMOTE_USER}@${DAPS_REMOTE_HOST}" \
  "rm -f '${remote_tmp_wpcontent}'"
mv "${partial}" "${BACKUP_DIR}/${wpcontent_file}"
partial=""
echo "- ${wpcontent_file}"

echo ""
echo "Backup complete: ${BACKUP_DIR}"
