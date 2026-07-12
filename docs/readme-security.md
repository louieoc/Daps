---
title: Security
sidebar:
  order: 6
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
