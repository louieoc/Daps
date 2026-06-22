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
