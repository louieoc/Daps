# Workflow: `dapsman prod teardown`

## Context

`dapsman prod deploy` puts a project onto a remote host: its containers -- the web-facing one on
`daps_net`, internal services like db and redis on the project's own `<name>_net` — a caddy site
file at `/srv/daps/caddy_sites/<name>.prod.caddy`, and project files under
`/srv/projects/<name>`. `prod teardown` is the workflow that reverses it. It is the remote
counterpart to `dapsman local teardown`, and unlike that one it does **not** touch the
workstation: the local project folder and the `daps.yaml` entry are left alone.

The workflow was reshaped in 0.3.2. It used to resolve the project through
`IProjectResolver.Resolve`, which throws `Project '<name>' not found in daps.yaml.` — so a
project absent from `daps.yaml` could not be torn down at all, and its containers, volumes and
files stayed on the host with no Dapsman route to remove them.

`local teardown` is one way into that state: it deletes the project folder **and** the
`daps.yaml` entry (`LocalTeardownService.cs:20`) while touching nothing remote, so running it on
a project that had been deployed left the prod copy live and orphaned. Removing the entry by
hand, or working from a `daps.yaml` that never listed the project -- a second workstation, a
fresh clone of the daps repo -- are other ways to achieve this condition.

Note what is *not* an instance of this. Migrating a site between providers does not strand
anything: the project keeps its `daps.yaml` entry and only `provider:` changes, so the entry
still resolves and `--provider` names the old host directly. That case worked before this change
and still does.

---

## Command

```
dapsman prod teardown --project <name> [--dry-run] [--provider <name>] [--config <path>]
```

- `--project <name>` — required (destructive; exactly one project)
- `--provider <name>` — which host to tear down from; see *Targeting* below
- `--dry-run` — print plan and skip execution
- `--config <path>` — override daps.yaml path

Requires a typed `yes` before executing. The prompt names the provider and host, not just the
project, because the whole risk in this workflow is acting on the wrong host.

---

## Targeting

The target is **(provider, project name)**. Everything the teardown needs on the remote side
follows from those two values by convention:

| | |
|---|---|
| project folder | `/srv/projects/<name>` |
| caddy site file | `/srv/daps/caddy_sites/<name>.prod.caddy` |
| compose files | `/srv/projects/<name>/_docker/compose_<name>.yaml` (+ `.prod.yaml` if present) |

A `daps.yaml` entry is therefore an *enhancement*, not a precondition — it is what lets the
workflow render the "Site Removed" page (see below). `RemoteTeardownPlanBuilder` uses
`IProjectResolver.TryResolve`, which returns null when the project is absent from `daps.yaml`
**or** its local folder is gone, rather than throwing.

`TryResolve` deliberately does not catch exceptions to make that decision. A project that is
registered and whose folder exists but is malformed — no `compose_daps_<name>.dev.yaml`, say —
still throws. That is a broken project rather than an absent one, and routing silently around
it would hide a real problem.

### Provider resolution

```
project in daps.yaml      ->  Resolve(--provider, project.Provider)
project not in daps.yaml  ->  ResolveExplicit(--provider)
```

For a registered project, `--provider` overrides the project's configured `provider:`. This allows for tearing down a remote project after it's been migrated to a new host, where `daps.yaml` now references the new host but files and containers still exist on the old host.

For an unregistered project there is no configured provider to fall back on, so
`ResolveExplicit` is used — the same method behind `prod provision`, `prod unprovision`,
`prod system` and `prod caddy restart`. It returns the sole active provider when only one is
configured, and otherwise requires `--provider`:

```
--provider is required when multiple providers are configured.
```

The alternative — `Resolve(null, null)`, which falls back to *the first non-disabled provider in
`daps.yaml` file order* — is what the other project-scoped workflows do, and it is wrong here.
For a workflow that ends in `rm -rf` on a host, silently guessing which host is the worst
available failure mode.

### Project name validation

`ConfigUtils.RequireSafeProjectName` enforces `^[A-Za-z0-9._-]+$` (and rejects `.` and `..`)
before anything resolves. A name read from `daps.yaml` is trusted input; a free-form
`--project` value that is interpolated into `rm -rf /srv/projects/<name>` and
`docker compose -f ...` over SSH is not.

---

## Steps

1. **Caddy** — one of two forms, then a reload either way. The reload only runs if
   `daps-caddy-1` is running; a stopped Caddy reads the changed site file when it next starts.
   A reload that fails on a running Caddy means a broken config, so it still stops the teardown.
   - **replace** — the project's local `_caddy_sites/<name>.prod.caddy` was readable, so the
     domain is known. `scp` a generated 410 "Site Removed" page over the remote site file.
   - **delete** — no local file to read a domain from (unregistered project, or no prod caddy
     file). `rm -f` the remote site file, so the retired host stops claiming the domain and
     stops renewing a certificate for it.

   `RemoteTeardownPlan.RemovedCaddyContent` is the discriminator: non-null means replace, null
   means delete. The plan output states which will run. `rm -f` is a no-op when the file is
   already absent, so the delete branch is safe unconditionally — this also covers a case that
   previously skipped caddy altogether and left the old host serving.

2. **Containers and volumes** — `docker compose -f <base> [-f <prod>] down -v` over SSH. `-v`
   removes named volumes; this is the step that makes teardown irreversible. It also removes the
   project's own `<name>_net`, which compose created. `daps_net` is declared `external: true`, so
   compose leaves it alone and the other projects sharing it are unaffected.

3. **Project folder** — `rm -rf /srv/projects/<name>`, with `sudo` for a non-root remote user.
   Containers write into the folder as their own users (e.g. `www-data`), so the remote user
   cannot always delete it unaided. The name has already passed `RequireSafeProjectName`.

On a non-root host every removal (`rm -f` of the site file, `rm -rf` of the folder) and every
`docker` command runs under `sudo`. `RemoteTeardownPlan.IsRoot` carries that, matching
`RemoteDeployPlan.IsRoot`.

---

## Notes and trade-offs

- **The 410 page is only reachable while DNS still points at the host.** In a migration it is
  invisible, since DNS has already moved. That is fine — it exists for the "I am shutting this
  site down" case, where the domain still resolves to the host being torn down.
- **Remote teardown does not remove the `daps.yaml` entry.** `local teardown` is the workflow that
  deletes local state (the project folder, backups and the registry entry). Keeping `prod
  teardown` purely remote means the two can be run in either order, and that re-deploying after
  a teardown needs no re-registration.
- **`--provider` is not extended to unregistered projects elsewhere.** `prod backup`,
  `prod offline`/`online` still require the project to have a `daps.yaml` entry (providers always 
  need a `daps.yaml` entry). Teardown is the one where a stranded project has no other escape 
  route; the others have a workable answer (re-add the entry) that does not involve destroying 
  anything.
- **The `_providerName` / `options.ProviderName` duplication is untouched.** `DapsmanRunner`
  passes the CLI provider through both the builder constructor and `TeardownOptions`, so
  `_providerName ?? options.ProviderName` always collapses to one value. There is a standing
  TODO at `RemoteBackupPlanBuilder.cs:37`; it is a pattern shared by deploy, backup, offline and
  sync-from-local, and fixing it in teardown alone would make the codebase less consistent.
