#!/usr/bin/env bash
# backup-remote.toolkit.sh
# Backs up the remote Grist data directory (persist/) to the local workstation.
# Grist stores all documents as SQLite files in /persist, so a plain rsync is sufficient.
#
# Run from the toolkit container via: dapsman prod backup
#
# Required env vars (set by dapsman):
#   DAPS_PROJECT      - project name (e.g. myproject)
#   DAPS_REMOTE_HOST  - remote server IP or hostname
#   DAPS_REMOTE_USER  - SSH user on the remote server
#   DAPS_SSH_KEY      - absolute path to SSH private key inside toolkit (e.g. /root/.ssh/daps-key-ramnode)
#
# Optional args (set by dapsman, override defaults here if running manually):
#   --env <env>       - environment name, used in log output (default: prod)
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
DEST="${BACKUP_DIR}/persist"

mkdir -p "${DEST}"

echo "Backing up ${DAPS_PROJECT} (${ENV}) from ${DAPS_REMOTE_USER}@${DAPS_REMOTE_HOST}"
echo "Destination: ${DEST}"
echo ""

echo "Syncing /persist..."
rsync -avz --delete \
    -e "ssh ${SSH_OPTS[*]}" \
    "${DAPS_REMOTE_USER}@${DAPS_REMOTE_HOST}:/srv/projects/${DAPS_PROJECT}/persist/" \
    "${DEST}/"

echo ""
echo "Backup complete: ${DEST}"
