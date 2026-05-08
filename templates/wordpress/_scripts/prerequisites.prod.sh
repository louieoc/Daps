#!/usr/bin/env bash
set -euo pipefail

# DAPS WordPress prerequisites (prod/remote).
# Generates WordPress secret keys, salts, and database passwords on the remote server.
# Run automatically by `dapsman prod deploy` before docker compose.
# Safe to re-run: skips any file that already exists unless --force is passed.

FORCE=0
if [[ "${1:-}" == "--force" ]]; then FORCE=1; fi

SECRETS_DIR="/srv/projects/mywpsite/_secrets"

# shellcheck source=generate-secrets.sh
source "$(dirname "$0")/generate-secrets.sh"
