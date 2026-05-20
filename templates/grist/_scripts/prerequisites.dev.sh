#!/usr/bin/env bash
set -euo pipefail

# DAPS Grist prerequisites (dev/local).
# Generates the Grist session secret if not yet created.
# Adds a hosts file entry for the local dev domain.
# Run automatically by `dapsman local build` before docker compose.
# Safe to re-run: skips any file that already exists unless --force is passed.

FORCE=0
if [[ "${1:-}" == "--force" ]]; then FORCE=1; fi

SECRETS_DIR="$(cd "$(dirname "$0")/.." && pwd)/_secrets"

# shellcheck source=generate-secrets.sh
source "$(dirname "$0")/generate-secrets.sh"

# --- hosts file entry ---
# Adds "127.0.0.1 mygrist.localhost" so the dev site is reachable by name.
# Note: Chrome and Firefox resolve *.localhost automatically without a hosts entry.
# This entry is needed for curl and other tools that rely on system DNS.

_add_hosts_entry() {
  local entry="127.0.0.1 mygrist.localhost"
  local os
  os="$(uname -s)"

  case "$os" in
    MINGW*|MSYS*|CYGWIN*)
      local hosts_file="/c/Windows/System32/drivers/etc/hosts"
      if grep -qF "$entry" "$hosts_file" 2>/dev/null; then
        echo "hosts: entry already present, skipping."
        return
      fi
      if net session > /dev/null 2>&1; then
        echo "$entry" >> "$hosts_file"
        echo "hosts: added '$entry'."
      else
        echo "WARNING: Cannot modify hosts file — Dapsman is not running as Administrator."
        echo "  Re-run Dapsman in an elevated terminal (right-click → Run as administrator)"
        echo "  and run 'dapsman local build' again, or add the following line manually to"
        echo "  C:\\Windows\\System32\\drivers\\etc\\hosts:"
        echo "    $entry"
        echo "  (Chrome and Firefox will work without this entry.)"
      fi
      ;;
    Linux*|Darwin*)
      local hosts_file="/etc/hosts"
      if grep -qF "$entry" "$hosts_file" 2>/dev/null; then
        echo "hosts: entry already present, skipping."
        return
      fi
      echo "hosts: adding '$entry' to $hosts_file (requires sudo)..."
      if echo "$entry" | sudo -n tee -a "$hosts_file" > /dev/null 2>&1; then
        echo "hosts: added '$entry'."
      else
        echo "WARNING: Cannot modify $hosts_file — sudo requires a password."
        echo "  Add the entry manually:"
        echo "    echo '$entry' | sudo tee -a $hosts_file"
        echo "  Or run 'sudo -v' first to cache credentials, then re-run 'dapsman local build'."
        echo "  (Chrome and Firefox will work without this entry.)"
      fi
      ;;
    *)
      echo "WARNING: Unknown OS ('$os'). Add the following line to your hosts file manually:"
      echo "  $entry"
      ;;
  esac
}

_add_hosts_entry
