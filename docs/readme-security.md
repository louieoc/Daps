---
title: Security
sidebar:
  order: 8
---

## Security Considerations

DAPS is designed for trusted single-user environments (your personal workstation and a VPS you control). Some of its design choices involve trade-offs that are acceptable in that context but worth understanding.

### Docker socket mount in toolkit

The toolkit container mounts the host Docker socket (`/var/run/docker.sock`). This gives any process inside the toolkit root-equivalent access to the host — it can start containers, mount host paths, and so on. This is intentional: toolkit scripts need to `docker exec` into other local containers (e.g. for database sync). It would be a serious risk in a multi-user or shared environment.

### SSH keys in toolkit

SSH keys live at `~/.ssh/` inside the toolkit container, bind-mounted from `secrets/ssh/` on the workstation. Any process that can `docker exec` into the toolkit has access to these keys. The same trust boundary applies as above: acceptable for a single-user local setup.

### Secret files on remote are `chmod 644`

Secret files in `_secrets/` are bind-mounted into containers on the remote server. They must be readable by `www-data` inside the container, which requires `chmod 644`. This means any process running in the container can read them. A proper secrets manager (e.g. Docker Swarm secrets, Vault) would be better long-term, but is outside the current scope.

### OpenRC and instance vars files

OpenStack credentials (`hosting/*_openrc`) and instance variable files (`hosting/openstack_*_instance_vars.sh`) are stored as plain files on disk. They are gitignored and should never be committed. Keep the `hosting/` folder out of any backup or sync that might expose it.

### Dev ports are localhost-only

Dev compose files bind exposed ports to `127.0.0.1` so they are not reachable from other machines on the network. Do not change these to `0.0.0.0` on a shared or corporate network.

---

## Host Security Updates

`dapsman prod provision` configures your remote host to install security updates on its own and to reboot when one requires it. You do not need to log in and patch the server.

### What it sets up

Ubuntu already installs security updates unattended. What it does *not* do by default is reboot afterwards. So a new kernel gets installed and the machine keeps running the old, vulnerable one indefinitely. Nothing warns you; the update looks applied because it is, it just isn't in use.

Provisioning closes that gap by writing `/etc/apt/apt.conf.d/52daps-unattended-upgrades` on the host:

```
Unattended-Upgrade::Automatic-Reboot "true";
Unattended-Upgrade::Automatic-Reboot-WithUsers "true";
Unattended-Upgrade::Automatic-Reboot-Time "04:00";
```

It also installs `unattended-upgrades` if the host's image didn't include it, and makes sure the `apt-daily` timers are enabled.

**Your sites come back automatically after a reboot.** Every Daps compose file uses `restart: unless-stopped`, so containers restart with the host. A project you deliberately took down with `dapsman prod offline` stays down.

### The 04:00 reboot

The host reboots at 04:00 **server time**, and only on days when an update actually requires it — typically a kernel update, which is a handful of times a year. Server time is usually UTC, so check what that is in your timezone if a few minutes of downtime at that hour would be disruptive.

To change the time or turn the reboot off, edit that file on the host directly:

```bash
sudo nano /etc/apt/apt.conf.d/52daps-unattended-upgrades
```

Note that re-running `dapsman prod provision` rewrites the file and restores the Daps default.

### Applying this to an older host

The provisioning step is idempotent, so a host set up by an earlier version of Daps just needs a re-run:

```
dapsman prod provision --provider <name>
```

It will not create a second VM — an OpenStack provider reuses the existing instance. It only reapplies host configuration.

### Checking that it is working

On the host:

```bash
systemctl list-timers 'apt-daily*' --all
journalctl -u apt-daily.service -u apt-daily-upgrade.service --since -7d
cat /var/run/reboot-required 2>/dev/null && echo "REBOOT PENDING"
```

Do **not** use `systemctl status unattended-upgrades` to check this. Despite the name, that unit does not install updates — it is a shutdown helper that blocks poweroff while an upgrade is in progress. It reports `active (running)` continuously whether or not patching works, so it tells you nothing.

### What this does not cover

This patches the host operating system only. The software inside your containers — WordPress, MySQL, Redis, Caddy — is updated by changing image tags and redeploying. See [Versioning](planning-versioning.md) under "Upgrading project dependencies".
