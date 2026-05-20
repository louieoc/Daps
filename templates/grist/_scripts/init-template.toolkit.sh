#!/usr/bin/env bash
set -euo pipefail

# DAPS Grist Template — Initialisation Script
#
# Called by `dapsman init` after copying the template directory.
# Replaces the placeholder name 'mygrist' with the chosen project name
# in all file contents, then renames any files or directories that
# contain 'mygrist' in their name.
#
# Usage: init-template.sh <project-name>

PROJECT_NAME="${1:?Usage: init-template.sh <project-name>}"
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

echo "  project root: $PROJECT_ROOT"
echo "  project name: $PROJECT_NAME"

# --- Replace placeholder in file contents ---
echo "  replacing 'mygrist' in file contents..."

grep -rl 'mygrist' "$PROJECT_ROOT" \
    --exclude-dir='.git' \
    --exclude-dir='_secrets' \
    --exclude-dir='_backups' \
    --exclude-dir='persist' \
    2>/dev/null \
    | while IFS= read -r file; do
        sed -i "s/mygrist/$PROJECT_NAME/g" "$file"
        echo "    updated: $file"
    done

# --- Rename files and directories containing the placeholder ---
echo "  renaming files and directories containing 'mygrist'..."

find "$PROJECT_ROOT" -depth -name '*mygrist*' \
    -not -path '*/.git/*' \
    2>/dev/null \
    | while IFS= read -r path; do
        dir="$(dirname "$path")"
        base="$(basename "$path")"
        newbase="${base//mygrist/$PROJECT_NAME}"
        if [ "$base" != "$newbase" ]; then
            mv "$path" "$dir/$newbase"
            echo "    renamed: $base -> $newbase"
        fi
    done

# --- Remove any secrets inherited from the template ---
if [ -d "$PROJECT_ROOT/_secrets" ]; then
    rm -rf "$PROJECT_ROOT/_secrets"
    echo "  removed _secrets/ (will be regenerated on first build)"
fi

echo "  init-template done"
