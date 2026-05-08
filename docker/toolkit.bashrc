#!/usr/bin/env bash

echo 'DAPS environment loaded'

# Load project-specific bashrc files mounted under /srv/projects/*/scripts.
if [[ -d /srv/projects ]]; then
  shopt -s nullglob
  for script_rc in /srv/projects/*/scripts/*.bashrc; do
    if [[ -r "${script_rc}" && "${script_rc}" != "/root/.bashrc" ]]; then
      # Guard against accidental self-sourcing if a project bind-mount includes root's bashrc.
      # shellcheck source=/dev/null
      source "${script_rc}"
    fi
  done
  shopt -u nullglob
fi

cd /root

