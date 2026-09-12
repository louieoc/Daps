#!/usr/bin/env bash
# backup-local.toolkit.sh
# Creates a timestamped snapshot of the LOCAL (dev) Grist data directory as
# <project>_<env>_<timestamp>.tar.gz
#
# Run from the toolkit container via: dapsman local backup
#
# Unlike backup-remote.toolkit.sh — which rsyncs the remote persist/ into a single mirror —
# this keeps every snapshot, so you can take one before an upgrade and roll back to it.
#
# Grist keeps its SQLite documents open while running, and a copy taken mid-write can be
# corrupt. The Grist container is therefore stopped for the duration of the archive and
# restarted afterwards (including on failure).
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
local_container="${DAPS_PROJECT}-grist-1"
archive_file="${DAPS_PROJECT}_${ENV}_${timestamp}.tar.gz"

if [[ ! -d "${PROJECT_ROOT}/persist" ]]; then
  echo "Grist data directory not found: ${PROJECT_ROOT}/persist" >&2
  echo "Run 'dapsman local build --project ${DAPS_PROJECT}' first." >&2
  exit 1
fi

mkdir -p "${BACKUP_DIR}"

echo "Backing up ${DAPS_PROJECT} (${ENV}) from the local Docker instance"
echo "Destination: ${BACKUP_DIR}"
echo ""

# --- Stop Grist so the SQLite files are not written to mid-archive ---
was_running=false
if docker ps --format '{{.Names}}' | grep -qx "${local_container}"; then
  was_running=true
fi

# The archive is written to a .partial file and renamed only once tar has succeeded. tar
# creates its output file before it writes anything, so an interrupted run would otherwise
# leave a truncated .tar.gz sitting in _backups/from_local looking like a usable backup.
partial=""

on_exit() {
  if [[ -n "${partial}" ]]; then
    rm -f "${partial}"
  fi
  if [[ "${was_running}" == "true" ]]; then
    echo "Restarting ${local_container}..."
    docker start "${local_container}" >/dev/null
  fi
}
trap on_exit EXIT

if [[ "${was_running}" == "true" ]]; then
  echo "Stopping ${local_container} for a consistent snapshot..."
  docker stop "${local_container}" >/dev/null
else
  echo "Note: ${local_container} is not running; archiving persist/ as-is."
fi

echo "Archiving persist/..."
partial="${BACKUP_DIR}/${archive_file}.partial"
tar -czf "${partial}" -C "${PROJECT_ROOT}" persist
mv "${partial}" "${BACKUP_DIR}/${archive_file}"
partial=""
echo "- ${archive_file}"

echo ""
echo "Backup complete: ${BACKUP_DIR}"
