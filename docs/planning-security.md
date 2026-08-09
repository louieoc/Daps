# Planning: Security

Planning document for security enhancements to Daps. Items here are future work — not yet implemented.

---

## Container network isolation

**Status: open decision, deferred 2026-08-06.** Three options are documented below. Daps is already
open source, so there is no future release date to work back from — the window that matters is
**before Daps has users beyond its author**. Migrating other people's live projects to a different
networking model would be considerably harder than changing it now, which is why the decision is
being examined at this stage rather than left to drift.

### Current state

All web-facing containers and Caddy share `daps_net`. Caddy routes to project containers by DNS
alias (e.g. `reverse_proxy loccom:80`). A compromised container can open TCP connections to any
other container on `daps_net` by name — this is how a compromised WordPress project could probe a
co-hosted .NET app.

Note what is *already* isolated: each project's internal services (db, redis, cache) sit on a
project-private `<project>_net` and never join `daps_net` (see
`templates/wordpress/_docker/compose_mywpsite.yaml`). A compromised container therefore cannot
reach another project's database, cache, or any other non-web service. The exposure is limited to
web-facing containers reaching each other's HTTP origin port.

### Actual risk assessment

This is defense-in-depth, not a primary control, and the residual risk is narrower than it first
appears.

The origin port a compromised container can reach on a peer project is the same port the entire
public internet already reaches through Caddy on :443. An attacker with code execution inside any
container can simply make an outbound request to `https://the-other-site.com`. What lateral
movement over `daps_net` adds on top of that is:

- **Bypassing Caddy-layer controls** — rate limits, IP allowlists, basic auth, or anything else
  enforced in a `.caddy` file rather than by the application.
- **Reaching services deliberately not published** — a container on `daps_net` with no
  corresponding Caddy site file is reachable internally but not from the internet.

Both are real. Neither is large for the current threat model.

It is also worth stating plainly: **none of the options below would have prevented or limited the
July 2026 loccom compromise.** That incident was unauthenticated RCE inside a single container,
with damage bounded by that container's writable `wp-content` bind mount and its readable
`/run/secrets/`. No pivot to `daps_net` peers was attempted or evidenced. The higher-leverage
follow-ups from that postmortem are secrets handling and availability monitoring, not network
segmentation.

---

### Option A — per-project Caddy networks

Replace the single shared `daps_net` with one Caddy-facing network per project. Each project gets
`<project>_web`, joined only by Caddy and that project's web-facing service. Caddy joins N
networks, one per registered project.

This yields the same isolation property as host-port routing — container A cannot open a connection
to container B — at substantially lower cost.

Project base compose:

```yaml
networks:
  mywpsite_net:
  mywpsite_web:
    external: true

services:
  wordpress:
    networks:
      mywpsite_net:
      mywpsite_web:
        aliases:
          - mywpsite
```

`compose_daps.yaml` gains one entry per project on both the top-level `networks:` map and the Caddy
service.

**Advantages:**
- DNS aliases survive — every `.caddy` file is unchanged (`reverse_proxy mywpsite:80`)
- No port allocation, no registry, no new class of deploy-time failure
- No host network mode; behaves identically on Docker Desktop and Linux, so dev still mirrors prod
- Compatible with a future Swarm overlay, so the horizontal-scaling path stays open

**Costs:**
- Caddy's network list becomes generated rather than static — `local build` / `prod deploy` would
  write a compose fragment (e.g. `compose_daps.networks.yaml`) from the `daps.yaml` project list.
  This is the same class of work as the existing `caddy_sites/` generation.
- **Adding or removing a project changes Caddy's own compose definition, which means recreating the
  Caddy container — a few seconds of downtime for *every* site on the host, every time a project is
  added.** Today Caddy only needs a config reload when a project is added, which is zero-downtime.
  This is a real regression in operational behavior and the strongest argument against Option A.
  A partial mitigation exists — `docker network connect daps-caddy-1 <project>_web` attaches a
  network to the running container without recreating it, and Caddy resolves reverse-proxy
  upstreams per request so it would pick the new alias up without a restart — but that leaves
  imperative state outside the compose file, so the running container and `compose_daps.yaml` drift
  apart until the next full recompose. Worth prototyping before committing to this option.
- Existing projects need a compose edit (one network rename) plus a Caddy restart
- Practical ceiling of roughly 30 bridge networks per host under Docker's default
  `default-address-pools`; widenable in `daemon.json` if ever needed. Far beyond the target
  persona's scale.

---

### Option B — host-port routing

The original proposal. Run Caddy in Docker host network mode. Project containers expose a port
bound to `127.0.0.1` on the host rather than joining `daps_net`. Caddy routes by `127.0.0.1:PORT`
instead of container alias. Containers not in host network mode cannot reach the host's loopback
interface, so a compromised project container cannot connect to another project's port even on the
same VM.

`compose_daps.yaml` — Caddy switches to host network mode:

```yaml
services:
  caddy:
    network_mode: host
```

Project prod compose — exposes a host-local port, drops `daps_net`:

```yaml
services:
  wordpress:
    ports:
      - "127.0.0.1:8080:80"
    networks:
      - loccom_net   # internal only; no daps_net
```

Project prod Caddy site file:

```
louisocallaghan.com {
    reverse_proxy 127.0.0.1:8080
}
```

**Costs:**
- **Ports are per-host state, which cuts against Daps's core portability promise.** Today a
  project's compose and `.caddy` files are host-agnostic — move `loccom` from ramnode to ovhcloud
  and nothing changes. With host ports, each project needs an assigned, recorded, collision-free
  port; consolidating two projects that were on different hosts produces a collision at deploy
  time. "Move it to a different host" is the value proposition.
- **Port allocation needs a registry the alias approach gets for free.** Project names are already
  unique in `daps.yaml`. Ports would need a new field and an allocator. Evidence this is a genuine
  burden: every template hardcodes `127.0.0.1:8080` in its dev compose, so two local projects
  already collide today.
- **`network_mode: host` is a Linux-only concept in any meaningful sense.** Docker Desktop's
  variant is a limited shim, not equivalent. Prod would use host networking while dev kept a bridge
  network — two networking models to build, document, and debug, in a tool whose premise is that
  dev mirrors prod.
- **Single-host only.** See "Scaling and networking" below.
- Existing projects need a migration: new compose file, new Caddy site file, port assignment

---

### Option C — accept the risk

Keep `daps_net` as-is. Document the trade-off and treat availability monitoring — still an open
follow-up in the loccom postmortem — as the higher-value use of the same effort. The July 2026
compromise ran undetected for roughly two days and was noticed only because an attacker bug caused
a white screen; monitoring would have caught it sooner, and would have caught it regardless of
network topology.

---

### Scaling and networking

The networking choice constrains the scaling path, so the analysis belongs here.

How a Daps site scales, in order of leverage:

1. **CDN in front (e.g. Cloudflare).** Full-page edge caching is how WordPress actually serves very
   high traffic. It is a DNS-level change with zero architectural cost and is available today
   without any change to Daps.
2. **Vertical — a larger VM.** With the Redis object cache already in place, a single modest VPS
   goes a long way.
3. **Horizontal — multiple hosts.** Requires an overlay network (Swarm) or Kubernetes, plus shared
   `wp-content/uploads` (NFS or an S3-offload plugin) and a MySQL primary/replica split. The
   data-synchronisation problem is the hard part, and it is independent of the networking choice.

The conclusion relevant to this decision: **DNS-alias routing survives a move to a multi-host
overlay network unchanged.** A Swarm overlay named `daps_net` (or `<project>_web` under Option A)
keeps `reverse_proxy mywpsite:80` working across hosts. `reverse_proxy 127.0.0.1:8080` is
inherently single-host and cannot. Option B forecloses the cheapest horizontal path Daps has.

---

### Decision

**2026-08-06 — deferred.** No change for now. The risk is real but modest and sits behind an
already-isolated data tier. Option B as originally proposed is not the right shape: it trades
portability, dev/prod parity, and the multi-host path for a narrow security gain. Option A is
cheaper and keeps those properties, but its per-project Caddy recreation is a real cost that would
need prototyping first. Neither is clearly worth doing today.

**Revisit when either of these becomes true:**
- Daps gains users beyond its author — after that, changing the networking model means migrating
  other people's live sites
- A project needs to run a service on the shared network that is deliberately not published through
  Caddy, which is the case where the current design's exposure actually bites

**Context:** Identified during the July 2026 loccom compromise postmortem. The compromised
WordPress container had network-level access to j-shirt.com containers via `daps_net`. See
`docs/postmortem-2026-07-loccom-compromise.md`.

---

## Automatic host security updates

**Status: mostly working by default; one real gap (automatic reboot).**

### Current state — verified on the ramnode prod VM, 2026-08-08

`unattended-upgrades` is **not** a paid feature and **not** missing. It ships in Ubuntu's `main`
repository, and Ubuntu Server / cloud images install and enable it by default. Although neither
`scripts/openstack-cloud-init.yaml` nor `scripts/provision-generic-vps.sh` mentions it, the
provisioned VMs have had it running daily since first boot.

Verified state on `daps-prod` (ramnode):

- `apt-daily.timer` and `apt-daily-upgrade.timer` — both scheduled and firing on time
- `journalctl -u apt-daily.service -u apt-daily-upgrade.service` — seven days of runs, every one
  `Deactivated successfully`, no failures
- `/etc/apt/apt.conf.d/20auto-upgrades` — `Update-Package-Lists "1"`, `Unattended-Upgrade "1"`
- Allowed origins — `jammy`, `jammy-security`, and the two ESM pockets. The plain `jammy` pocket is
  frozen at release, and `jammy-updates` is commented out, so this is **already effectively
  security-only**. No change needed there.
- The log shows kernel updates being applied — a run on 2026-08-08 auto-removed the superseded
  `linux-*-5.15.0-181` packages.

**Do not use `systemctl status unattended-upgrades` as a health check.** Despite the name, that unit
does not run upgrades — it is the shutdown helper ("Unattended Upgrades Shutdown") that blocks
poweroff while an upgrade is mid-flight. It reports `active (running)` continuously from boot to
shutdown whether or not patching works, and its journal is near-empty by design. Checking it is what
initially made this host look healthy. The units that actually do the work are `apt-daily.service`
(download) and `apt-daily-upgrade.service` (install), driven by their respective timers.

### The actual gap: `Unattended-Upgrade::Automatic-Reboot`

The setting is absent from `50unattended-upgrades`, so it defaults to **false**. New kernels are
installed and then never booted into. The host keeps running the old, vulnerable kernel
indefinitely — observed directly: the VM was running 5.15.0-186 while carrying newer installed
kernels.

This is the one thing worth fixing. It is also a good illustration of why "updates are being
applied" is not the same as "the machine is patched".

**Note on the login banner that prompted this investigation** (76 updates, 8 security): most of that
was expected behaviour, not failure. The ~68 non-security packages come from the `-updates` pocket,
which is deliberately not auto-applied. The 8 security ones were most likely held back by Ubuntu's
phased-update rollout, which `unattended-upgrades` respects by design. The banner overstates the
problem.

**Note on "truncated" log entries — expected, not a fault.** `unattended-upgrades.log` contains
entries that print the startup banner and then stop with no result line. These are not failed runs.
Each one timestamp-matches an `apt-daily.service` run, which invokes
`unattended-upgrade --download-only` — it writes the same banner, pre-fetches packages, and exits
without an install result because it never installs anything. Only `apt-daily-upgrade.service`
produces a result line:

| Log entry | systemd unit |
|---|---|
| 2026-08-06 16:13, 2026-08-07 06:18, 2026-08-08 07:09 (banner only) | `apt-daily` (download-only) |
| 2026-08-08 06:04 → "No packages found that can be upgraded unattended" | `apt-daily-upgrade` |

Scattered run times are `RandomizedDelaySec` on the timers, not irregular scheduling. Worth
recording because a banner-only entry looks like a killed process and invites a wrong diagnosis.

### Why this matters for Daps specifically

The target persona will never SSH into a VM to reboot it after a kernel update. Any patching
strategy that depends on the user remembering to do it will not happen. For this tool, unattended
patching *including the reboot* is not a convenience — it is the only patching that will actually
complete.

### Proposed change

Set the reboot policy during provisioning, in both the OpenStack cloud-init and the generic-VPS
script so the two provider paths stay equivalent:

```
Unattended-Upgrade::Automatic-Reboot "true";
Unattended-Upgrade::Automatic-Reboot-Time "04:00";
```

Containers use `restart: unless-stopped`, so they return after a reboot — and anything deliberately
stopped by `prod offline` correctly stays stopped.

Explicitly **not** proposed:

- **Installing the package** — already present and enabled by default.
- **Narrowing the origins** — already effectively security-only.
- **Auto-upgrading Docker.** `docker-ce` comes from Docker's own repository origin and is therefore
  outside the allowed origins already. Leave it that way: an unattended daemon restart bounces every
  container on the host, which is worse than a slightly stale Docker.

Because the defaults are already mostly right, the honest framing is that Daps should *assert* the
configuration it depends on rather than assume the distro default will hold across providers and
images — a generic-VPS provider could well ship an image with it disabled.

### Scope limits

This covers the host only. Container images — WordPress, MySQL, Redis, Caddy — are patched by
rebuilding and redeploying, which is a separate concern covered in
[planning-versioning.md](planning-versioning.md) under "Upgrading project dependencies".

### Ubuntu Pro / ESM

Free for personal use on up to five machines, and would extend coverage to `universe` packages and
past the standard-support window. Nearly everything Daps installs is in `main` and covered by
standard support, so this is low priority — the "Enable ESM Apps" login banner overstates the gap
for this workload.

### Implementation items

- [ ] Write `Automatic-Reboot` + `Automatic-Reboot-Time` into a Daps-owned drop-in
      (`/etc/apt/apt.conf.d/52daps-unattended-upgrades`, so it wins over `50unattended-upgrades`
      without editing a distro-managed file) from `scripts/openstack-cloud-init.yaml`
- [ ] Add the same to `scripts/provision-generic-vps.sh` so both provider paths match, and have it
      install and enable `unattended-upgrades` if absent — a generic VPS image may not include it
- [ ] Document the patching and reboot behaviour, and how to disable auto-reboot, in
      `docs/readme-deployment.md` — an unexpected 04:00 reboot should not be a surprise
- [ ] Apply the drop-in manually to the existing ramnode and ovhcloud VMs
- [ ] Audit the ovhcloud VM the same way — it was provisioned by a different path
      (`provision-generic-vps.sh`) and has not been checked

**To audit patching health on a Daps host:**

```bash
systemctl list-timers 'apt-daily*' --all
journalctl -u apt-daily.service -u apt-daily-upgrade.service --since -7d --no-pager
grep -v '^\s*//' /etc/apt/apt.conf.d/50unattended-upgrades | grep -v '^\s*$'
cat /var/run/reboot-required 2>/dev/null && echo "REBOOT PENDING"
```

Not `systemctl status unattended-upgrades` — see the warning above.
