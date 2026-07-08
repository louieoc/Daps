#!/usr/bin/env bash
set -euo pipefail

# Runs on the remote host (via ssh) to prepare a pre-existing VPS for Daps.
# Mirrors the docker/swap setup in openstack-cloud-init.yaml, which can't be
# shared directly since cloud-init only runs from a static user-data file at
# VM boot with no access to this repo.
#
# Usage: provision-generic-vps.sh <remote-user> [--upgrade]

remote_user="${1:?Usage: provision-generic-vps.sh <remote-user> [--upgrade]}"
upgrade=false
if [[ "${2:-}" == "--upgrade" ]]; then
  upgrade=true
fi

run_root() {
  if [[ "$(id -u)" -eq 0 ]]; then
    bash -lc "$1"
  else
    sudo bash -lc "$1"
  fi
}

run_root "apt-get update"

if [[ "$upgrade" == true ]]; then
  run_root "apt-get upgrade -y"
fi

if ! command -v docker >/dev/null 2>&1; then
  run_root "apt-get install -y ca-certificates curl gnupg"
  run_root "install -m 0755 -d /etc/apt/keyrings"
  run_root "curl -fsSL https://download.docker.com/linux/ubuntu/gpg | gpg --dearmor -o /etc/apt/keyrings/docker.gpg"
  run_root "chmod a+r /etc/apt/keyrings/docker.gpg"
  run_root "echo \"deb [arch=\$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] https://download.docker.com/linux/ubuntu \$(. /etc/os-release && echo \$VERSION_CODENAME) stable\" > /etc/apt/sources.list.d/docker.list"
  run_root "apt-get update"
  run_root "apt-get install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin"
fi

run_root "systemctl enable --now docker"
run_root "id -u ${remote_user} >/dev/null 2>&1 && usermod -aG docker ${remote_user} || true"

run_root "if [[ ! -f /swapfile ]]; then fallocate -l 2G /swapfile && chmod 600 /swapfile && mkswap /swapfile && swapon /swapfile && echo '/swapfile none swap sw 0 0' >> /etc/fstab; fi"
run_root "echo 'vm.swappiness=10' > /etc/sysctl.d/99-daps.conf && sysctl -p /etc/sysctl.d/99-daps.conf"

echo "Docker and swap setup complete on $(hostname)."
