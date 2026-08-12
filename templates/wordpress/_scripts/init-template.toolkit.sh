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

# Portable in-place edit. `sed -i` is not portable: GNU sed takes no argument,
# BSD sed (macOS) requires a backup suffix, so `sed -i "s/.../.../g" file` on a
# Mac treats the expression as the suffix and the filename as the script.
#
# The temp file must sit beside the target and be *renamed* over it, never
# copied over it. This script is itself rewritten by the loop below, and only a
# rename leaves the running shell's open file descriptor on the original inode;
# overwriting in place shifts every later byte offset and bash resumes reading
# mid-word. `cp -p` first so the replacement inherits the original permissions.
replace_in_file() {
    local pattern="$1" file="$2" tmp="$2.dapstmp.$$"
    cp -p "$file" "$tmp"
    sed "$pattern" "$file" > "$tmp"
    mv -f "$tmp" "$file"
}

# Find text files that contain the placeholder and replace in-place.
# grep -rl skips binary files automatically.
grep -rl 'mywpsite' "$PROJECT_ROOT" \
    --exclude-dir='.git' \
    --exclude-dir='_secrets' \
    --exclude-dir='backups' \
    --exclude-dir='wp-content' \
    2>/dev/null \
    | while IFS= read -r file; do
        # grep streams its results, so it may list a transient .dapstmp file.
        [ -f "$file" ] || continue
        replace_in_file "s/mywpsite/$PROJECT_NAME/g" "$file"
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
