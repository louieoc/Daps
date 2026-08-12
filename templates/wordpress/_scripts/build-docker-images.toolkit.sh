#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "${script_dir}/.." && pwd)"

export_dir="${repo_root}/_docker/image-exports"
mkdir -p "${export_dir}"

docker build --pull -f "${repo_root}/_docker/wordpress.dockerfile" -t mywpsite-wordpress "${repo_root}"
docker save -o "${export_dir}/mywpsite-wordpress.tar" mywpsite-wordpress
