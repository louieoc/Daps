# Workflow: `dapsman prod provision`

## What Prod Provision Does

`dapsman prod provision` creates a remote VM on an OpenStack provider and installs Docker on it, leaving it ready for `prod deploy`. It runs entirely from the toolkit container, which has OpenStack CLI tools and SSH keys.

Steps:
1. Sources the provider's OpenRC file to authenticate with OpenStack
2. Sources the instance vars file (e.g. `hosting/ramnode_openstack_instance_vars.sh`) for image, flavor, network, etc.
3. Generates an SSH keypair in the toolkit if one doesn't exist
4. Uploads the public key to OpenStack if not already registered
5. Runs `scripts/openstack-create-instance.sh`, which creates (or reuses) the server and assigns an IP
6. The instance boots with `scripts/openstack-cloud-init.yaml` as user-data, which installs Docker and performs initial server setup

The provision workflow is intentionally minimal — it gets Docker running and nothing else. Project-specific setup happens during `prod deploy`.

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

The cloud-init changes only apply to newly provisioned instances. To add swap to an existing instance, SSH in and run:

```bash
fallocate -l 2G /swapfile
chmod 600 /swapfile
mkswap /swapfile
swapon /swapfile
echo '/swapfile none swap sw 0 0' >> /etc/fstab
echo 'vm.swappiness=10' > /etc/sysctl.d/99-daps.conf
sysctl -p /etc/sysctl.d/99-daps.conf
```
