#!/usr/bin/env bash
set -euo pipefail

# DAPS Grist prerequisites (prod/remote).
# Generates the Grist session secret on the remote server if not yet created.
# Run automatically by `dapsman prod deploy` before docker compose.
# Safe to re-run: skips any file that already exists unless --force is passed.

FORCE=0
if [[ "${1:-}" == "--force" ]]; then FORCE=1; fi

# Set by `dapsman prod deploy` when it runs this script on the remote host.
: "${DAPS_PROJECT:?DAPS_PROJECT is required}"

SECRETS_DIR="/srv/projects/${DAPS_PROJECT}/_secrets"

# shellcheck source=generate-secrets.sh
source "$(dirname "$0")/generate-secrets.sh"
