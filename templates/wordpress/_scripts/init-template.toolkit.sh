#!/usr/bin/env bash
set -euo pipefail

# DAPS WordPress Template — Initialisation Script
#
# Called by `dapsman init` after copying the template directory.
# Replaces the placeholder name 'mywpsite' with the chosen project name
# in all file contents, then renames any files or directories that
# contain 'mywpsite' in their name.
#
# Usage: init-template.sh <project-name>

PROJECT_NAME="${1:?Usage: init-template.sh <project-name>}"
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

echo "  project root: $PROJECT_ROOT"
echo "  project name: $PROJECT_NAME"

# --- Replace placeholder in file contents ---
echo "  replacing 'mywpsite' in file contents..."

# Find text files that contain the placeholder and replace in-place.
# grep -rl skips binary files automatically.
grep -rl 'mywpsite' "$PROJECT_ROOT" \
    --exclude-dir='.git' \
    --exclude-dir='_secrets' \
    --exclude-dir='backups' \
    --exclude-dir='wp-content' \
    2>/dev/null \
    | while IFS= read -r file; do
        sed -i "s/mywpsite/$PROJECT_NAME/g" "$file"
        echo "    updated: $file"
    done

# --- Rename files and directories containing the placeholder ---
echo "  renaming files and directories containing 'mywpsite'..."

# Use -depth so children are processed before their parents.
find "$PROJECT_ROOT" -depth -name '*mywpsite*' \
    -not -path '*/.git/*' \
    2>/dev/null \
    | while IFS= read -r path; do
        dir="$(dirname "$path")"
        base="$(basename "$path")"
        newbase="${base//mywpsite/$PROJECT_NAME}"
        if [ "$base" != "$newbase" ]; then
            mv "$path" "$dir/$newbase"
            echo "    renamed: $base -> $newbase"
        fi
    done

# --- Remove any secrets inherited from the template ---
# _secrets/ is gitignored and should never be shared between projects.
# prerequisites.sh will generate fresh secrets on first `dapsman local build`.
if [ -d "$PROJECT_ROOT/_secrets" ]; then
    rm -rf "$PROJECT_ROOT/_secrets"
    echo "  removed _secrets/ (will be regenerated on first build)"
fi

echo "  init-template done"
