#!/usr/bin/env bash
# list-restore-points.toolkit.sh
# Discovers available WordPress restore points and outputs a JSON array to stdout.
# Each restore point consists of a paired SQL dump + wp-content archive.
# Outputs [] if no restore points exist.
#
# Run from toolkit via: dapsman local restore --list-restore-points
#
# Required env vars (set by dapsman):
#   DAPS_PROJECT      - project name (e.g. mywpsite)
#   DAPS_BACKUPS_PATH - absolute path to _backups/from_prod/ inside the toolkit
#
# Output format (most-recent-first, 1-based index):
#   [{"index":1,"env":"prod","timestamp":"20260420_202738","complete":true},...]

set -euo pipefail

: "${DAPS_PROJECT:?DAPS_PROJECT is required}"
: "${DAPS_BACKUPS_PATH:?DAPS_BACKUPS_PATH is required}"

if [[ ! -d "$DAPS_BACKUPS_PATH" ]]; then
  echo "[]"
  exit 0
fi

output="["
sep=""
index=1

while IFS= read -r filepath; do
  filename=$(basename "$filepath")
  if [[ "$filename" =~ ^${DAPS_PROJECT}_([a-z0-9]+)_([0-9]{8}_[0-9]{6})\.sql$ ]]; then
    env="${BASH_REMATCH[1]}"
    timestamp="${BASH_REMATCH[2]}"
    tar_file="${DAPS_BACKUPS_PATH}/${DAPS_PROJECT}_${env}_wp-content_${timestamp}.tar.gz"
    complete="false"
    [[ -f "$tar_file" ]] && complete="true"
    output+="${sep}{\"index\":${index},\"env\":\"${env}\",\"timestamp\":\"${timestamp}\",\"complete\":${complete}}"
    sep=","
    index=$((index + 1))
  fi
done < <(find "$DAPS_BACKUPS_PATH" -maxdepth 1 -name "${DAPS_PROJECT}_*.sql" 2>/dev/null | sort -r)

output+="]"
echo "$output"
