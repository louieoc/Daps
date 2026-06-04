#!/usr/bin/env bash
# post-remote-deploy.toolkit.sh
# Run automatically by dapsman after prod deploy completes.
# Uploads public/ content to the remote server.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
bash "${SCRIPT_DIR}/sync-local-to-remote.toolkit.sh"
