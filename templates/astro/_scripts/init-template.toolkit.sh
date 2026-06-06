#!/usr/bin/env bash
# init-template.toolkit.sh
# Renames all occurrences of the placeholder project name to the actual project name.
# Called by dapsman init with the new project name as $1.
# Only processes DAPS infrastructure directories — the Astro project files are left untouched.

set -euo pipefail

if [[ -z "${1:-}" ]]; then
  echo "Usage: $0 <project-name>" >&2
  exit 1
fi

PROJECT_NAME="$1"
PLACEHOLDER="myastrosite"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(dirname "$SCRIPT_DIR")"

echo "Initializing Astro project: ${PROJECT_NAME}"

DAPS_DIRS=("$PROJECT_DIR/_docker" "$PROJECT_DIR/_caddy_sites" "$PROJECT_DIR/_scripts")

# Replace placeholder in file contents
for dir in "${DAPS_DIRS[@]}"; do
  [[ -d "$dir" ]] || continue
  find "$dir" -type f | while read -r file; do
    if grep -qF "$PLACEHOLDER" "$file" 2>/dev/null; then
      sed -i "s/${PLACEHOLDER}/${PROJECT_NAME}/g" "$file"
    fi
  done
done

# Rename files containing the placeholder
for dir in "${DAPS_DIRS[@]}"; do
  [[ -d "$dir" ]] || continue
  find "$dir" -depth -name "*${PLACEHOLDER}*" | while read -r file; do
    base="$(basename "$file")"
    newbase="${base//${PLACEHOLDER}/${PROJECT_NAME}}"
    mv "$file" "$(dirname "$file")/${newbase}"
  done
done

echo "Done. Project '${PROJECT_NAME}' initialized."
