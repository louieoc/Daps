# Planning: Per-Project Provider Assignment

## Context

Daps supports multiple providers in `daps.yaml` but currently has no way to associate a project with a specific provider — the `--provider` CLI flag is the only way to target a non-default provider. This makes it impossible to have a `daps.yaml` with two projects on different hosts without always specifying `--provider` on every command.

The fix is to add an optional `provider` key to each project entry in `daps.yaml`. Resolution rules:
1. CLI `--provider` flag → overrides everything (existing behaviour)
2. Project's configured `provider` → use it; error if not in providers list or disabled
3. Neither → first non-disabled provider in `providers:` list

Providers also get a `disabled:` property (mirroring projects), so a provider can be commented out without removing its config.

---

## daps.yaml Shape (after change)

```yaml
providers:
  ramnode: openstack
    openrc: ./hosting/ramnode_openrc
  dreamcompute: openstack
    openrc: ./hosting/dreamcompute_openrc
    disabled: true   # same semantics as disabled: true on a project

projects:
  jshirt:
    path: ../j-shirt.com
    provider: dreamcompute
  dapster-wp:
    path: ../dapster-wp
    # no provider → uses first non-disabled provider (ramnode)
```

---

## Provider Resolution

### Project-scoped operations (deploy, backup, sync, teardown)
Priority chain: CLI `--provider` > project's `provider` > first non-disabled provider.

`IHostingProviderResolver` gets a second overload:
```csharp
HostingProvider Resolve(string? cliProviderName, string? projectProviderName);
```

### Host-level operations (provision, prod caddy restart)
These are not project-scoped. When only one non-disabled provider exists, use it without `--provider`. When multiple exist, require `--provider` explicitly — silently picking the first would be confusing.

`IHostingProviderResolver` gets:
```csharp
HostingProvider ResolveExplicit(string? providerName);
```
`ResolveExplicit` returns the single provider when exactly one exists, or throws with the available list when multiple exist and none is specified.

### Disabled provider handling
Both `Resolve` overloads and `ResolveExplicit` skip disabled providers in the "first provider" fallback. If a named provider (from CLI or project config) is disabled, throw:
```
Provider 'dreamcompute' is disabled in daps.yaml.
```

---

## Multi-Project Deploy

`ConventionRemoteDeployPlanBuilder` resolves all selected projects, collects their distinct non-null `Provider` values, then:
- If CLI `--provider` given → use it
- If all projects agree on a provider (or none have one) → use that (or fallback)
- If projects disagree and no CLI override → throw:
  ```
  Projects target different providers. Specify --provider to disambiguate.
  ```

---

## Future: Per-Environment Provider Assignment

Once multi-environment support exists (stage, prod, etc.), a project could target different providers per environment. Proposed YAML shape (avoids duplicate-key problem):

```yaml
projects:
  j-shirt.com:
    providers:
      stage: ramnode
      prod: dreamcompute
```

The singular `provider:` key introduced in this feature would remain valid as shorthand for the default environment. `ProjectDefinition.Provider` is the right foundation — a `Providers` dictionary could be added alongside it later without breaking existing config.

---

## Files Changed

| File | Change |
|---|---|
| `src/Dapsman.Domain/ProjectDefinition.cs` | Add `Provider` property |
| `src/Dapsman.Domain/ProviderDefinition.cs` | Add `Disabled` property |
| `src/Dapsman.Infrastructure/DapsYamlConfigLoader.cs` | Parse `provider` on projects; parse `disabled` on providers |
| `src/Dapsman.Application/Interfaces.cs` | Add `Resolve(cli, project)` and `ResolveExplicit` to `IHostingProviderResolver` |
| `src/Dapsman.Infrastructure/HostingProviderResolver.cs` | Implement new overloads; skip disabled in fallback; disabled-provider error |
| `src/Dapsman.Infrastructure/ConventionRemoteDeployPlanBuilder.cs` | Multi-project provider inference + disagreement error |
| `src/Dapsman.Infrastructure/ConventionBackupPlanBuilder.cs` | Use `Resolve(cli, project)` |
| `src/Dapsman.Infrastructure/ConventionLocalSyncFromRemotePlanBuilder.cs` | Use `Resolve(cli, project)` |
| `src/Dapsman.Infrastructure/ConventionRemoteSyncFromLocalPlanBuilder.cs` | Use `Resolve(cli, project)` |
| `src/Dapsman.Infrastructure/ConventionTeardownPlanBuilder.cs` | Use `Resolve(cli, project)` |
| `src/Dapsman.Infrastructure/OpenStackRemoteProvisionPlanBuilder.cs` | Use `ResolveExplicit` |
| `src/Dapsman.Infrastructure/ConventionRemoteCaddyRestartPlanBuilder.cs` | Use `ResolveExplicit` |
| `tests/Dapsman.Infrastructure.Tests/DapsYamlConfigLoaderTests.cs` | Tests for new YAML fields |
| `tests/Dapsman.Infrastructure.Tests/HostingProviderResolverTests.cs` | New test file |


# Planning: Generic VPS Provider (non-OpenStack hosts)

## Background

Looking at OVHCloud (and similarly Hostinger), there are two VPS options:
- **OVH Public Cloud** is OpenStack-based, but starts at 7GB RAM with hourly billing — overkill for Daps's target instance sizes (and still more expensive than other OpenStack hosts Daps already supports, like DreamCompute and RamNode).
- **OVH "bare metal" VPS** has no API at all: you pick a plan, OVH emails you a hostname, username, and a link to set a temporary password, and you SSH in directly. A 4GB instance is about $6/mo with no commitment. This isn't unique to OVH — Hostinger and similar budget VPS providers work the same way.

There's no instance-creation API to drive for these, so the OpenStack provider model doesn't fit. What's needed instead is a way to point Daps at a host that already exists.

## Context

Daps supports multiple `openstack` providers in `daps.yaml`, each of which Daps can create a VM for via `dapsman prod provision`. This adds a second provider type, `generic-vps`, representing a pre-existing Ubuntu box reached by hostname/SSH — `prod provision` configures Docker on it instead of first creating a VM, and `prod deploy`/backup/sync/etc. work unchanged afterward since they only depend on the resolved `RemoteHost`/`RemoteUser`/`KeyName`, not on how the provider type works internally.

## daps.yaml Shape

```yaml
providers:
  ovhcloud1: generic-vps
    hostname: vps-12345678.vps.ovh.us
    user: ubuntu
```

`hostname` and `user` are plain config, not secrets — same sensitivity as a hostname, no generated instance-vars file needed (unlike OpenStack, where the IP isn't known until the instance is created).

## Provisioning Flow (`dapsman prod provision --provider <name>`)

Runs interactively from the toolkit container (`docker exec -it`), same as OpenStack provisioning:
1. Generates an SSH keypair at `~/.ssh/daps-key-<name>` in the toolkit, if missing.
2. Tests whether the key already authenticates against the host. If not, runs `ssh-copy-id`, which prompts for the host's password directly in the terminal — the password is typed by the user and never touches Dapsman, `daps.yaml`, or any log. This step is skipped entirely on re-runs once a working key exists.
3. Copies `scripts/provision-generic-vps.sh` to the host and runs it over SSH: installs Docker, sets up a 2GB swapfile + `vm.swappiness=10` (mirroring `openstack-cloud-init.yaml`'s setup — duplicated rather than shared, since cloud-init's user-data can't reference files outside itself), and adds the SSH user to the `docker` group. `--upgrade` additionally runs `apt-get upgrade -y`.

If OVH's forced first-login password change is still pending, an interactive `ssh -t` connection attempt before `ssh-copy-id` lets that prompt resolve the same way — untested against a fresh box so far, since the only host tested against had already had its password changed manually before Daps touched it.

SSH key setup and any forced password change are folded into `prod provision` itself rather than a separate workflow — they only happen on the first run per host.

## Provider Resolution

`HostingProviderResolver.ResolveFromDefinition` branches by provider type. OpenStack resolution is unchanged (reads `openstack_<name>_instance_vars.sh` for IP/user/key). Generic-vps resolution does no file I/O at all — `RemoteHost`/`RemoteUser` come straight from `daps.yaml`, `KeyName` defaults to `daps-key-<name>`.

## Dispatch

`dapsman prod provision` previously always constructed `OpenStackRemoteProvisionPlanBuilder` directly. A `CompositeRemoteProvisionPlanBuilder` now resolves the provider once and delegates to `OpenStackRemoteProvisionPlanBuilder` or `GenericVpsRemoteProvisionPlanBuilder` based on the resolved `ConfigDefinition`'s runtime type. Each concrete builder still resolves the provider again internally — a small, cheap redundancy not worth changing `IRemoteProvisionPlanBuilder`'s shape to avoid.

`prod unprovision` is OpenStack-only and unaffected — generic-vps hosts have no destroy API, so "unprovision" doesn't apply to them.

## RemoteProvisionPlan Shape

`RemoteProvisionPlan` previously had OpenStack-specific required fields (`OpenRcScriptHostPath`, `SetVarsScriptHostPath`, `CreateScriptHostPath` and their container-path counterparts). These are now a generic `ProviderDetails: IReadOnlyList<KeyValuePair<string,string>>` that each builder populates with whatever's relevant to show in `--dry-run` (OpenStack: openrc/set-vars/create script paths; generic-vps: hostname/user). `DapsmanPlanPrinter.PrintRemoteProvision` loops over this list instead of hardcoding OpenStack's three fields.

## Files Changed

| File | Change |
|---|---|
| `src/Dapsman.Domain/GenericVpsProviderDefinition.cs` | New — `Hostname`, `User` |
| `src/Dapsman.Domain/GenericVpsProviderOptions.cs` | New — empty marker |
| `src/Dapsman.Domain/RemoteProvisionPlan.cs` | Replaced OpenStack-specific fields with `ProviderDetails` |
| `src/Dapsman.Application/RemoteProvisionOptions.cs` | Added `Upgrade` |
| `src/Dapsman.Infrastructure/DapsYamlConfigLoader.cs` | Parses `generic-vps` provider type, `hostname`/`user` keys |
| `src/Dapsman.Infrastructure/HostingProviderResolver.cs` | `ResolveFromDefinition` branches by provider type |
| `src/Dapsman.Infrastructure/GenericVpsRemoteProvisionPlanBuilder.cs` | New |
| `src/Dapsman.Infrastructure/CompositeRemoteProvisionPlanBuilder.cs` | New — dispatch by provider type |
| `src/Dapsman.Infrastructure/OpenStackRemoteProvisionPlanBuilder.cs` | Populates `ProviderDetails` |
| `scripts/provision-generic-vps.sh` | New — runs on the remote host via SSH |
| `src/Dapsman.Cli/DapsmanRunner.cs` | Uses `CompositeRemoteProvisionPlanBuilder`; plumbs `--upgrade` |
| `src/Dapsman.Cli/CliArguments.cs` | Parses `--upgrade` |
| `src/Dapsman.Cli/DapsmanPlanPrinter.cs` | Loops over `ProviderDetails` |
| `README.md` | Updated `prod provision` entry |

