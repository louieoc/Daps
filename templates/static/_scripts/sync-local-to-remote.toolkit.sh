#!/usr/bin/env bash
# sync-local-to-remote.toolkit.sh
# Rsyncs the local public/ folder to the remote server.
# Run directly from the toolkit for content-only updates (no full redeploy needed).
#
# Required env vars (set by dapsman):
#   DAPS_PROJECT      - project name
#   DAPS_REMOTE_HOST  - remote server IP or hostname
#   DAPS_REMOTE_USER  - SSH user on the remote server
#   DAPS_SSH_KEY      - path to SSH private key inside toolkit

set -euo pipefail

: "${DAPS_PROJECT:?DAPS_PROJECT is required}"
: "${DAPS_REMOTE_HOST:?DAPS_REMOTE_HOST is required}"
: "${DAPS_REMOTE_USER:?DAPS_REMOTE_USER is required}"
: "${DAPS_SSH_KEY:?DAPS_SSH_KEY is required}"

PROJECT_ROOT="/srv/projects/${DAPS_PROJECT}"
SSH_OPTS=(-o StrictHostKeyChecking=accept-new -o BatchMode=yes -i "${DAPS_SSH_KEY}")

echo "Syncing ${DAPS_PROJECT} public/ -> remote /srv/projects/${DAPS_PROJECT}/public/"
echo "Remote: ${DAPS_REMOTE_USER}@${DAPS_REMOTE_HOST}"
echo ""

rsync -avz --delete \
  -e "ssh ${SSH_OPTS[*]}" \
  "${PROJECT_ROOT}/public/" \
  "${DAPS_REMOTE_USER}@${DAPS_REMOTE_HOST}:/srv/projects/${DAPS_PROJECT}/public/"

echo ""
echo "Sync complete."
