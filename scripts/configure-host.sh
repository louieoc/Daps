#!/usr/bin/env bash
set -euo pipefail

# Applies Daps host policy that must hold on every prod host, regardless of how
# the host came to exist (an OpenStack instance Daps created, or a pre-existing
# generic VPS). Both provisioning paths run this over SSH.
#
# Idempotent by design: re-running against an already-configured host rewrites
# the drop-in and exits. This is what lets `dapsman prod provision` bring a host
# provisioned by an older version of Daps up to current policy.
#
# Usage: configure-host.sh [reboot-time]   (default 04:00, 24h HH:MM)

reboot_time="${1:-04:00}"
dropin=/etc/apt/apt.conf.d/52daps-unattended-upgrades

run_root() {
  if [[ "$(id -u)" -eq 0 ]]; then
    bash -lc "$1"
  else
    sudo bash -lc "$1"
  fi
}

# A freshly created instance may still be running cloud-init, which holds the
# apt lock. Wait it out rather than racing it.
if command -v cloud-init >/dev/null 2>&1; then
  echo "Waiting for cloud-init to finish..."
  run_root "cloud-init status --wait >/dev/null 2>&1 || true"
fi

# Ubuntu Server and cloud images ship unattended-upgrades already installed and
# enabled, but a generic VPS image may not.
if ! dpkg -s unattended-upgrades >/dev/null 2>&1; then
  echo "Installing unattended-upgrades..."
  run_root "apt-get update"
  run_root "DEBIAN_FRONTEND=noninteractive apt-get install -y unattended-upgrades"
fi

# Assert the periodic schedule is on. Distro default, but don't assume it holds
# across every provider image.
printf '%s\n' \
  'APT::Periodic::Update-Package-Lists "1";' \
  'APT::Periodic::Unattended-Upgrade "1";' \
  | run_root "tee /etc/apt/apt.conf.d/20auto-upgrades >/dev/null"

# The gap this closes: Ubuntu's default allowed-origins already cover the
# security pocket, so security updates DO get installed unattended -- but
# Automatic-Reboot is unset, which means false. New kernels are installed and
# then never booted into, so the host keeps running the vulnerable one
# indefinitely. See docs/planning-security.md.
#
# This lands in a Daps-owned drop-in rather than an edit to the distro-managed
# 50unattended-upgrades: it sorts after (52 > 50) so it wins, it survives
# package upgrades of unattended-upgrades, and the override stays visible.
printf '%s\n' \
  '// Managed by Daps (dapsman prod provision). Edits will be overwritten.' \
  '//' \
  '// Reboot when an unattended upgrade requires it, so that kernel updates' \
  '// actually take effect. Containers use restart: unless-stopped, so they' \
  '// come back after the reboot -- and anything deliberately stopped by' \
  '// `dapsman prod offline` correctly stays stopped.' \
  'Unattended-Upgrade::Automatic-Reboot "true";' \
  'Unattended-Upgrade::Automatic-Reboot-WithUsers "true";' \
  "Unattended-Upgrade::Automatic-Reboot-Time \"${reboot_time}\";" \
  | run_root "tee ${dropin} >/dev/null"

run_root "chmod 644 ${dropin}"

run_root "systemctl enable --now apt-daily.timer apt-daily-upgrade.timer >/dev/null 2>&1 || true"

# Verify apt actually parses what we wrote, rather than trusting the write.
if run_root "apt-config dump" | grep -q 'Unattended-Upgrade::Automatic-Reboot "true"'; then
  echo "Automatic reboot enabled at ${reboot_time} on $(hostname)."
else
  echo "ERROR: ${dropin} was written but apt did not pick up Automatic-Reboot." >&2
  exit 1
fi
