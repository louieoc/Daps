#!/usr/bin/env bash
# init-template.toolkit.sh
# Renames all occurrences of the placeholder project name to the actual project name.
# Called by dapsman init with the new project name as $1.
# In overlay mode ($2 = --overlay) only DAPS infrastructure dirs are processed,
# so existing project files are never touched.

set -euo pipefail

if [[ -z "${1:-}" ]]; then
  echo "Usage: $0 <project-name> [--overlay]" >&2
  exit 1
fi

PROJECT_NAME="$1"
OVERLAY="${2:-}"
PLACEHOLDER="mystaticsite"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(dirname "$SCRIPT_DIR")"

echo "Initializing static site project: ${PROJECT_NAME}"

if [[ "$OVERLAY" == "--overlay" ]]; then
  # Restrict to DAPS infrastructure directories only
  SEARCH_DIRS=("$PROJECT_DIR/_docker" "$PROJECT_DIR/_caddy_sites" "$PROJECT_DIR/_scripts")
else
  SEARCH_DIRS=("$PROJECT_DIR")
fi

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

# Replace placeholder in file contents
for search_dir in "${SEARCH_DIRS[@]}"; do
  [[ -d "$search_dir" ]] || continue
  find "$search_dir" -type f | while read -r file; do
    if grep -qF "$PLACEHOLDER" "$file" 2>/dev/null; then
      replace_in_file "s/${PLACEHOLDER}/${PROJECT_NAME}/g" "$file"
    fi
  done
done

# Rename files containing the placeholder
for search_dir in "${SEARCH_DIRS[@]}"; do
  [[ -d "$search_dir" ]] || continue
  find "$search_dir" -depth -name "*${PLACEHOLDER}*" | while read -r file; do
    dir="$(dirname "$file")"
    base="$(basename "$file")"
    newbase="${base//${PLACEHOLDER}/${PROJECT_NAME}}"
    mv "$file" "${dir}/${newbase}"
  done
done

echo "Done. Project '${PROJECT_NAME}' initialized."
