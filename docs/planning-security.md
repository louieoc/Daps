# Planning: Security

Planning document for security enhancements to Daps. Items here are future work — not yet implemented.

---

## Container network isolation (host-port routing)

**Current state:** All web-facing containers and Caddy share `daps_net`. Caddy routes to project containers by DNS alias (e.g., `reverse_proxy loccom:80`). A compromised container can open TCP connections to any other container on `daps_net` by name — this is how a compromised WordPress project could probe a co-hosted .NET app.

**Proposed change:** Run Caddy in Docker host network mode. Project containers expose a port bound to `127.0.0.1` on the host rather than joining `daps_net`. Caddy routes by `127.0.0.1:PORT` instead of container alias.

Containers not in host network mode cannot reach the host's loopback interface, so a compromised project container cannot connect to another project's port even on the same VM.

**What changes:**

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

**Trade-offs:**
- Each project needs a unique host port assigned — a new convention to manage
- `daps_net` is no longer needed (simplification)
- Caddy in host network mode is a bigger change to the DAPS shared services layer
- Existing projects need a migration (new compose file, new caddy site file, port assignment)

**Context:** Identified during the July 2026 loccom compromise postmortem. The compromised WordPress container had network-level access to j-shirt.com containers via `daps_net`. See `docs/postmortem-2026-07-loccom-compromise.md`.
