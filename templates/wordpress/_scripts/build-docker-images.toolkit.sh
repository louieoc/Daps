#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "${script_dir}/.." && pwd)"

# Set by `dapsman prod deploy`. The tag must match the `image:` key in the project's compose
# files, which dapsman writes as <project>-wordpress at init.
: "${DAPS_PROJECT:?DAPS_PROJECT is required}"
image_name="${DAPS_PROJECT}-wordpress"

export_dir="${repo_root}/_docker/image-exports"
mkdir -p "${export_dir}"

# DAPS_TARGET_PLATFORM is set by `dapsman prod deploy` to the platform the remote host's Docker
# daemon actually runs (e.g. linux/amd64), read from the host just before this script runs. Without
# it the image inherits the workstation's architecture, which is invisible until the container
# reaches the server and dies with "exec format error" -- so pin it whenever it is known.
#
# It is unset for a plain local build, where the workstation is the target and native is correct.
# Cross-building needs QEMU/binfmt on the workstation; Docker Desktop ships it, a bare Linux
# Docker install may not, and docker build says so if it is missing.
platform_args=()
if [ -n "${DAPS_TARGET_PLATFORM:-}" ]; then
	platform_args=(--platform "${DAPS_TARGET_PLATFORM}")
	echo "Building for platform ${DAPS_TARGET_PLATFORM}"
fi

docker build "${platform_args[@]}" --pull -f "${repo_root}/_docker/wordpress.dockerfile" -t "${image_name}" "${repo_root}"
docker save -o "${export_dir}/${image_name}.tar" "${image_name}"
