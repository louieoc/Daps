#!/usr/bin/env bash
# init-template.toolkit.sh
# Renames all occurrences of the placeholder project name to the actual project name.
# Called by dapsman init with the new project name as $1.

set -euo pipefail

if [[ -z "${1:-}" ]]; then
  echo "Usage: $0 <project-name>" >&2
  exit 1
fi

PROJECT_NAME="$1"
PLACEHOLDER="mystaticsite"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(dirname "$SCRIPT_DIR")"

echo "Initializing static site project: ${PROJECT_NAME}"

# Replace placeholder in file contents
find "$PROJECT_DIR" -type f | while read -r file; do
  if grep -qF "$PLACEHOLDER" "$file" 2>/dev/null; then
    sed -i "s/${PLACEHOLDER}/${PROJECT_NAME}/g" "$file"
  fi
done

# Rename files containing the placeholder
find "$PROJECT_DIR" -depth -name "*${PLACEHOLDER}*" | while read -r file; do
  dir="$(dirname "$file")"
  base="$(basename "$file")"
  newbase="${base//${PLACEHOLDER}/${PROJECT_NAME}}"
  mv "$file" "${dir}/${newbase}"
done

echo "Done. Project '${PROJECT_NAME}' initialized."
