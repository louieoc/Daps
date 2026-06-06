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

# Replace placeholder in file contents
for search_dir in "${SEARCH_DIRS[@]}"; do
  [[ -d "$search_dir" ]] || continue
  find "$search_dir" -type f | while read -r file; do
    if grep -qF "$PLACEHOLDER" "$file" 2>/dev/null; then
      sed -i "s/${PLACEHOLDER}/${PROJECT_NAME}/g" "$file"
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
