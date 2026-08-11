# Workflow: `dapsman prod provision`

## What Prod Provision Does

`dapsman prod provision` prepares a remote host for `prod deploy`. It runs entirely from the toolkit container, which has OpenStack CLI tools and SSH keys.

What it does depends on the provider's type in `daps.yaml`, because the two kinds of host differ in who creates the VM.

### `openstack` providers — Daps creates the VM

1. Sources the provider's OpenRC file to authenticate with OpenStack
2. Sources the instance vars file (e.g. `hosting/openstack_ramnode_instance_vars.sh`) for image, flavor, network, etc.
3. Generates an SSH keypair in the toolkit if one doesn't exist
4. Uploads the public key to OpenStack if not already registered
5. Runs `scripts/openstack-create-instance.sh`, which creates (or reuses) the server and assigns an IP
6. The instance boots with `scripts/openstack-cloud-init.yaml` as user-data, which installs Docker and performs initial server setup
7. Re-sources the instance vars file, waits for the host to accept SSH, then uploads and runs `scripts/configure-host.sh` on it

Step 7 re-sources the vars file because `openstack-create-instance.sh` writes `OS_SERVER_IP` back to it — on a first provision that value did not exist when the file was sourced in step 2.

### `generic-vps` providers — the host already exists

1. Generates an SSH keypair in the toolkit if one doesn't exist
2. Copies the public key to the host with `ssh-copy-id`, prompting for the host's password. The password is never stored by Daps. This step is skipped once a working key is in place.
3. Uploads and runs `scripts/provision-generic-vps.sh`, which installs Docker and sets up swap — mirroring what cloud-init does on the OpenStack path
4. Uploads and runs `scripts/configure-host.sh`

`--upgrade` additionally runs `apt-get upgrade -y` on the host during step 3.

Beyond host security updates, the provision workflow is intentionally minimal — it gets Docker running and nothing else. Project-specific setup happens during `prod deploy`.

---

## Host configuration (`scripts/configure-host.sh`)

Both provider types run this script over SSH at the end of provisioning. It applies the host policy Daps depends on, independently of how the host came to exist.

It ensures `unattended-upgrades` is installed and its `apt-daily` timers are enabled, then writes `/etc/apt/apt.conf.d/52daps-unattended-upgrades` to enable an automatic reboot at 04:00 when a security upgrade requires one. Ubuntu installs security updates unattended by default but never reboots, so kernel updates were being installed and never taking effect. See [Security](readme-security.md) for what this means day to day and how to change the reboot time.

The drop-in is a separate file rather than an edit to the distro's `50unattended-upgrades`: it sorts after it (52 > 50) so it wins, it survives package upgrades, and the override stays visible to anyone inspecting the host.

### Idempotency

The script is written to be safe to re-run, and this is load-bearing rather than defensive. It is the mechanism for bringing a host provisioned by an older version of Daps up to current policy — re-run `dapsman prod provision --provider <name>` and it reapplies configuration without creating a second VM.

This is also why the setting is not in `openstack-cloud-init.yaml`. Cloud-init runs only on an instance's first boot, and `openstack-create-instance.sh` reuses an existing server when one matches, so a cloud-init-only change would never reach an already-provisioned host.

### Line endings

Provisioning stages its scripts under `.dapsman/provision` and rewrites them with LF endings before uploading, via `ConfigUtils.CopyFileAndReplaceLineEndingsForLinux`. Daps is often developed on Windows, where `core.autocrlf` can leave CRLF endings in the working tree, and a CRLF shell script fails as soon as it runs on Linux:

```
/tmp/provision-generic-vps.sh: line 1: set: pipefail: invalid option name
```

Git Bash tolerates CRLF, so this only ever surfaces on the remote host. The staging directory is removed after the run.

---

## cloud-init (`scripts/openstack-cloud-init.yaml`)

Cloud-init runs once on first boot. It currently:
- Installs Docker CE and Docker Compose plugin
- Enables Docker at boot
- Adds the `ubuntu` user to the docker group (when present)
- Creates a 2GB swapfile at `/swapfile` (if not already present) and adds it to `/etc/fstab`
- Sets `vm.swappiness=10` via `/etc/sysctl.d/99-daps.conf`

### Why swap

Small VPS instances (1-4GB RAM) running WordPress + MySQL + Redis have no memory headroom during active use (theme editing, plugin updates, etc.). Without swap, the Linux OOM killer terminates the largest process — typically `mysqld` — causing a database unavailability error to site visitors.

2GB swap is a fixed value (not proportional to RAM) because swap on a cloud VPS is slow — it's a safety net to absorb memory spikes, not a performance resource. 2GB is sufficient for Daps's target instance sizes. For larger instances (8GB+) swap is rarely needed anyway.

`vm.swappiness=10` tells the kernel to strongly prefer keeping data in RAM and only use swap as a last resort, which is the right behavior for a server workload.

### Idempotency

The swapfile step is guarded with `if [[ ! -f /swapfile ]]` so it's a no-op if the file already exists. In practice cloud-init only runs once per instance so this guard is defensive rather than necessary.

---

## Existing Instances

Cloud-init changes only apply to newly provisioned instances, since cloud-init runs once on first boot. Anything that must reach an existing host belongs in `configure-host.sh` instead, which runs over SSH on every provision.

To add swap to an existing instance, SSH in and run:

```bash
fallocate -l 2G /swapfile
chmod 600 /swapfile
mkswap /swapfile
swapon /swapfile
echo '/swapfile none swap sw 0 0' >> /etc/fstab
echo 'vm.swappiness=10' > /etc/sysctl.d/99-daps.conf
sysctl -p /etc/sysctl.d/99-daps.conf
```
