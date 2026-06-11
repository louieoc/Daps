# Planning: HostPortManager — Dev Port Collision Detection and Auto-Assignment

## Context

Multiple project templates default to the same host port (`8080`), so users who run more than one project locally will have silent port collisions. This adds `HostPortManager` to:
1. Detect and warn about existing port conflicts during `local build`
2. Auto-assign non-colliding ports when `dapsman init` copies a template

No YAML parsing library is needed — the port binding format (`"127.0.0.1:<host>:<container>"`) is consistent across all dev compose files and extractable with a simple regex.

---

## New Types

### Application layer — `src/Dapsman.Application/`

**`HostPortBinding.cs`**
```csharp
public sealed record HostPortBinding(
    string ProjectName,
    string FilePath,
    int HostPort,
    int ContainerPort);
```

**`HostPortConflict.cs`**
```csharp
public sealed record HostPortConflict(
    int Port,
    IReadOnlyList<HostPortBinding> Bindings);
```

**In `Interfaces.cs`** — add:
```csharp
public interface IHostPortManager
{
    IReadOnlyList<HostPortBinding> GetBindings(string projectName, IEnumerable<string> devComposeFilePaths);
    IReadOnlyList<HostPortConflict> FindConflicts(IReadOnlyList<HostPortBinding> bindings);
    int FindNextAvailable(int preferredPort, IReadOnlyCollection<int> reservedPorts);
}
```

### Domain layer — `src/Dapsman.Domain/`

**`PortAssignment.cs`** — lives in Domain (not Application) so `InitPlan` can reference it without a circular dependency:
```csharp
public sealed record PortAssignment(int OriginalPort, int AssignedPort, string Reason);
```

### Infrastructure layer — `src/Dapsman.Infrastructure/HostPortManager.cs`

- `GetBindings()`: reads each file, applies regex `127\.0\.0\.1:(\d+):(\d+)` to find host:container port pairs, returns `HostPortBinding` list tagged with project name and file path
- `FindConflicts()`: groups bindings by host port, returns groups with count > 1
- `FindNextAvailable()`: increments from `preferredPort` until finding a port not in `reservedPorts` and not in a built-in skip list (ports < 1024, and common service ports: 3306, 5432, 6379, 27017, 5672, 8161, 5900)

**Note:** Reserved ports only gate `FindNextAvailable` — projects may still explicitly bind reserved ports (e.g. 3306 for a local MySQL service). `GetBindings` and `FindConflicts` treat all ports equally.

---

## `local build` — Collision Warnings

### `ConventionComposePlanBuilder`
Takes `IHostPortManager` as a constructor parameter. Resolves **all configured projects** (not just the `--project` filter) to collect `LocalDevComposeFiles` for port conflict detection — so a filtered `local build` still reports collisions involving projects that weren't selected. Calls `GetBindings()` for each project, then `FindConflicts()`. Appends each conflict as a warning:
```
warning: Port 8080 is used by multiple projects: dapster-wp (compose_dapster-wp.dev.yaml), loccom (compose_loccom.dev.yaml)
```

`LocalDevComposeFiles` is a property on `ProjectDockerDefinition` that returns only the `.dev.yaml` project compose files (excluding the shared base and daps extension files). This replaces an earlier inline `.Where(f => f.EndsWith(".dev.yaml", ...))` filter.

Warnings flow into `LocalBuildPlan.Warnings`, already printed by `DapsmanPlanPrinter.PrintLocalBuild`.

### Wiring in `DapsmanRunner`
`new HostPortManager()` passed into `ConventionComposePlanBuilder` in `RunLocalBuild()`.

---

## `dapsman init` — Auto Port Assignment

### `InitPlan` (Domain)
```csharp
public IReadOnlyList<PortAssignment> DevPortAssignments { get; init; } = [];
```

### `ConventionInitPlanBuilder`
Takes `IHostPortManager`, `IProjectResolver`, and `IDockerResolver` as constructor parameters. At plan time:
1. Reads template's `_docker/*.dev.yaml` to extract desired host ports
2. Collects all existing configured projects' bindings via `LocalDevComposeFiles` (dev-only files, from `ProjectDockerDefinition`)
3. For each template port: if it conflicts, calls `FindNextAvailable()` passing all already-reserved + already-assigned ports
4. Populates `DevPortAssignments` on the plan

**Preferred-port-with-fallback**: templates define natural defaults (e.g. `8080` for WordPress); init only changes a port if there's a collision.

### `InitService.Execute()`
After `CopyTemplateDirectory()`, applies port assignments: for each `PortAssignment` where `AssignedPort != OriginalPort`, scans `_docker/*.dev.yaml` in the destination and replaces `127.0.0.1:{OriginalPort}:` → `127.0.0.1:{AssignedPort}:`. Follows the same pattern as `ApplyProdUrl()`.

### `DapsmanPlanPrinter.PrintInit()`
```
- dev port assignments:
  8080 → 8083 (conflict with dapster-wp)
  8082 (available)
  3306 (available)
```

### Wiring in `DapsmanRunner`
`new HostPortManager()` passed to `ConventionInitPlanBuilder` in `RunInit()`.

---

## Files Changed

| File | Change |
|---|---|
| `src/Dapsman.Application/Interfaces.cs` | Add `IHostPortManager` |
| `src/Dapsman.Application/HostPortBinding.cs` | New record |
| `src/Dapsman.Application/HostPortConflict.cs` | New record |
| `src/Dapsman.Domain/PortAssignment.cs` | New record (Domain, not Application) |
| `src/Dapsman.Domain/InitPlan.cs` | Add `DevPortAssignments` |
| `src/Dapsman.Infrastructure/HostPortManager.cs` | New class |
| `src/Dapsman.Domain/ProjectDockerDefinition.cs` | Add `LocalDevComposeFiles` property (dev-only compose files, excludes shared base and daps extension) |
| `src/Dapsman.Infrastructure/DockerResolver.cs` | Populate `LocalDevComposeFiles` by filtering `localComposeFiles` to `.dev.yaml` entries |
| `src/Dapsman.Infrastructure/ConventionComposePlanBuilder.cs` | Add `IHostPortManager`; scan all projects (not just selected) for conflict detection using `LocalDevComposeFiles`; always include all projects' toolkit mount files |
| `src/Dapsman.Infrastructure/ConventionInitPlanBuilder.cs` | Add `IHostPortManager` + resolvers; use `LocalDevComposeFiles` for existing project bindings |
| `src/Dapsman.Application/InitService.cs` | `ApplyDevPorts()` in `Execute()` |
| `src/Dapsman.Cli/DapsmanPlanPrinter.cs` | Show port assignments in `PrintInit()` |
| `src/Dapsman.Cli/DapsmanRunner.cs` | Wire `HostPortManager` into `RunInit()` and `RunLocalBuild()` |
| `tests/Dapsman.Infrastructure.Tests/HostPortManagerTests.cs` | New test class |
| `tests/Dapsman.Infrastructure.Tests/DockerResolver_IntegrationTests.cs` | New integration tests confirming `LocalProjectComposeFiles` includes shared file and `LocalDevComposeFiles` is dev-only |
| `tests/Dapsman.Infrastructure.Tests/Fakes.cs` | Add `FakeHostPortManager` |
| `tests/Dapsman.Infrastructure.Tests/ConventionComposePlanBuilderTests.cs` | Pass `FakeHostPortManager` |
| `tests/Dapsman.Infrastructure.Tests/ConventionInitPlanBuilderTests.cs` | Pass resolvers + `FakeHostPortManager` |

---

## Unit Tests — `HostPortManagerTests`

- `GetBindings_ExtractsPortsFromDevComposeFile`
- `GetBindings_MultipleServicesInOneFile` — WordPress dev compose has 3 port bindings
- `GetBindings_ReservedPortAllowed` — port 3306 in a compose file is returned normally
- `GetBindings_IgnoresNonLocalBindings` — `"8080:80"` (no IP) is not matched
- `GetBindings_MissingFileReturnsEmpty`
- `FindConflicts_NoConflicts_ReturnsEmpty`
- `FindConflicts_DetectsCollision`
- `FindNextAvailable_ReturnsPreferredIfFree`
- `FindNextAvailable_SkipsReservedPorts`
- `FindNextAvailable_SkipsWellKnownPorts` — ports < 1024 always skipped
- `FindNextAvailable_SkipsCommonServicePorts` — e.g. 3306 skipped by built-in list
