# Workflow: `dapsman local teardown`

## Context

`dapsman local build` brings up the local dev Docker environment for a project: it composes containers, syncs caddy site files, and runs prerequisites. Until now there is no workflow to cleanly reverse this. The immediate need that surfaced this: if a `dapsman init` produces a broken project (e.g. the `_docker/_secrets` bug), re-running init leaves stale Docker volumes behind. When the fixed project is built, MySQL finds an existing data volume and ignores the new secret files — leading to "access denied" errors with no obvious cause.

`dapsman local teardown` closes this loop. It is the local counterpart to `dapsman prod teardown`.

---

## Command

```
dapsman local teardown --project <name> [--dry-run] [--config <path>]
```

- `--project <name>` — required (destructive operation; exactly one project)
- `--dry-run` — print plan and skip execution
- `--config <path>` — override daps.yaml path

Requires a confirmation prompt ("Type 'yes' to confirm") before executing, like `prod teardown`.

---

## Steps

1. **Stop containers and delete volumes**
   - Run `docker compose -f {base} [-f {dev}] down -v` on the workstation
   - `-v` removes named volumes (e.g. `loccom_loccom-mysql-data`) so they cannot cause stale-state issues on the next build
   - Use the same compose files as `local build`: base (`compose_{project}.yaml`) + dev overlay (`compose_{project}.dev.yaml`) if present; resolved via `ProjectDockerDefinition`
   - Tolerates compose failure with `|| true` (containers may already be stopped)

2. **Delete caddy site files**
   - Delete each `*.dev.caddy` file that was copied into `{dapsRoot}/caddy_sites/` for this project
   - The set of files to delete comes from `ProjectCaddyDefinition.WorkstationLocalSiteFilePaths`, mapped to their destination filenames in `{dapsRoot}/caddy_sites/`

3. **Reload Caddy**
   - Run `docker exec daps-caddy-1 caddy reload --config /etc/caddy/Caddyfile` on the workstation
   - Removes the project's site from the running Caddy config
   - Tolerates failure if Caddy is not running

4. **Delete project folder**
   - Delete the project directory (`project.Definition.Path`) and all its contents
   - This is what makes the confirmation prompt important

---

## C# Changes

### New domain types

**`src/Dapsman.Domain/LocalTeardownPlan.cs`**
```csharp
public sealed class LocalTeardownPlan
{
    public required string ProjectName { get; init; }
    public required string ProjectPath { get; init; }
    public required IReadOnlyList<string> ComposeFiles { get; init; }        // base + dev overlay
    public required IReadOnlyList<string> CaddySiteFilesToDelete { get; init; } // abs paths in dapsRoot/caddy_sites/
    public required string CaddyContainerName { get; init; }
    public required string CaddyConfigPath { get; init; }
}
```

**`src/Dapsman.Application/LocalTeardownOptions.cs`**
```csharp
public sealed class LocalTeardownOptions
{
    public string? ProjectName { get; init; }
}
```

### Application layer

**`src/Dapsman.Application/Interfaces.cs`** — add:
```csharp
public interface ILocalTeardownPlanBuilder
{
    LocalTeardownPlan BuildPlan(DapsConfig config, LocalTeardownOptions options);
}

public interface ILocalTeardownExecutor
{
    void Execute(LocalTeardownPlan plan);
}
```

**`src/Dapsman.Application/LocalTeardownService.cs`** (new, follows TeardownService pattern)
```csharp
public sealed class LocalTeardownService(
    ILocalTeardownPlanBuilder planBuilder,
    ILocalTeardownExecutor executor)
{
    public LocalTeardownPlan CreatePlan(DapsConfig config, LocalTeardownOptions options);
    public void Execute(LocalTeardownPlan plan);
}
```

### Infrastructure

**`src/Dapsman.Infrastructure/ConventionLocalTeardownPlanBuilder.cs`** (new)
- Resolves project via `IProjectResolver`
- Resolves compose files via `IDockerResolver.ResolveForProject(project)` — uses `ProjectDockerDefinition.WorkstationBaseComposeFilePath` and `WorkstationDevComposeFilePath` (same files local build uses)
- Resolves caddy site files to delete via `ICaddyResolver.ResolveForProject(project)` — maps `WorkstationLocalSiteFilePaths` basenames to `{dapsRoot}/caddy_sites/`
- Resolves caddy container name + config path via `ICaddyResolver.Resolve()`

**`src/Dapsman.Infrastructure/WorkstationLocalTeardownExecutor.cs`** (new)
- Injects `IBashRunner _workstationBashRunner` and `ICaddyRestarter _caddyRestarter` (or inline the reload)
- Step 1: builds compose file flags string from plan; calls `_workstationBashRunner.RunShell($"docker compose {flags} down -v || true", workingDirectory)`
- Step 2: deletes each file in `plan.CaddySiteFilesToDelete` via `File.Delete` (no-op if missing)
- Step 3: reloads Caddy — `_workstationBashRunner.RunShell($"docker exec {plan.CaddyContainerName} caddy reload --config {plan.CaddyConfigPath}", workingDirectory)` — wrapped in try/catch to tolerate Caddy not running
- Step 4: deletes project folder via `Directory.Delete(plan.ProjectPath, recursive: true)`

### CLI

**`src/Dapsman.Cli/CliArguments.cs`**
- Add `IsLocalTeardown` bool
- Parse `local teardown` action (alongside `local build`, `local restore`, etc.)

**`src/Dapsman.Cli/DapsmanRunner.cs`** — add `RunLocalTeardown()`:
- Validates `--project` is specified
- Creates plan + service
- Prints plan:
  ```
  Step: stop containers
  - docker compose down -v ({compose files})

  Step: delete caddy site files
  - {dapsRoot}/caddy_sites/{file}.dev.caddy
  
  Step: reload Caddy
  - docker exec daps-caddy-1 caddy reload
  
  Step: delete project folder
  - {projectPath}
  ```
- Confirmation prompt: "This will delete all containers, volumes, caddy site files, and the project folder. Type 'yes' to confirm:"
- Executes if confirmed; returns 1 if declined

---

## Key Design Notes

- **`-v` is essential** — this is the whole motivation for the workflow; without it, stale volumes persist and cause broken builds on re-init
- **Workstation runner, not toolkit** — teardown acts on local Docker directly, no need for toolkit container; mirrors how `local build` uses `WorkstationBashRunner`
- **Reuses existing resolvers** — `IDockerResolver`, `ICaddyResolver`, `IProjectResolver` already resolve the paths that `local build` uses; the plan builder simply reads the same data in reverse
- **Caddy reload tolerance** — if Caddy container isn't running (e.g. user tore down daps stack), the caddy reload step should not fail the whole teardown
- **No daps.yaml cleanup** — prod teardown does not remove the project from daps.yaml; local teardown follows the same convention; the user can clean that up manually or we add it later

---

## Verification

```
dapsman local teardown --project loccom --dry-run
dapsman local teardown --project loccom
```

After execution:
- `docker ps -a | grep loccom` — no containers
- `docker volume ls | grep loccom` — no volumes
- `ls {dapsRoot}/caddy_sites/` — no `loccom*.dev.caddy` file
- `https://loccom.localhost` — no longer routes (Caddy returns 404 or connection refused)
- `{projectPath}` — directory no longer exists
