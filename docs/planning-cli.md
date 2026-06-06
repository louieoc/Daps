# Planning: CLI Architecture

## Overview

The CLI layer consists of three files in `src/Dapsman.Cli/`:

| File | Responsibility |
|---|---|
| `Program.cs` | Entry point — parses args, constructs `DapsmanRunner`, calls `RunAsync` |
| `CliArguments.cs` | Argument parsing — single `Parse(string[] args)` static method, returns immutable `CliArguments` record |
| `DapsmanRunner.cs` | Workflow orchestration — one `RunXxx()` method per workflow; wires services, creates plans, executes |
| `DapsmanPlanPrinter.cs` | Plan summary output — one `PrintXxx(plan)` static method per workflow |

---

## `DapsmanRunner` Responsibilities

`DapsmanRunner` owns:
- Service construction (wiring `IBashRunner`, plan builders, executors, resolvers)
- Plan creation (calling `service.CreatePlan(...)`)
- Calling `DapsmanPlanPrinter.PrintXxx(plan)` to display the plan
- Confirming destructive operations with the user (`Console.ReadLine`)
- Executing the plan (calling `service.Execute(plan)` etc.) and printing step-progress output
- Dry-run gating (`if (!options.DryRun)`)

`DapsmanRunner` does not:
- Format plan summaries (that's `DapsmanPlanPrinter`)
- Parse arguments (that's `CliArguments`)
- Contain business logic (that's Application/Infrastructure)

---

## `DapsmanPlanPrinter` Responsibilities

`DapsmanPlanPrinter` is a static class containing one `public static void PrintXxx(plan, ...)` method per workflow. It owns:
- All `Console.WriteLine` calls that form the plan summary shown before execution and in `--dry-run` mode
- Knowing what aspects of each plan are worth showing to the user

`DapsmanPlanPrinter` does not:
- Execute anything
- Gate on `--dry-run` (that's the caller's job)
- Print step-execution progress lines (e.g. `"Step: copy-template"` inside `if (!dryRun)` blocks — those stay in `DapsmanRunner`)

### Why a separate class rather than on the service?

Presentation format (verbosity, style, line breaks) belongs in the CLI layer. Services in the Application/Infrastructure layers should not know about how their plans are displayed. If Daps ever had a second consumer (library, GUI), the services would carry CLI formatting baggage unnecessarily.

Keeping `DapsmanPlanPrinter` in the CLI project also means it can freely reference CLI-only types (e.g. `WorkstationDockerComposeExecutor.BuildDockerComposeCommand`) without creating upward dependencies.

---

## `CliArguments` Conventions

- `--project` populates `ProjectFilters` (a list, supports multiple values for multi-project commands)
- `--name` populates `ProjectName` (used only by `init`)
- `--overlay` is a boolean flag (no value), used only by `init`
- `--prod-url` is shared between `init` (sets prod caddy domain) and `local restore` (overrides URL for search-replace)
- All flags are parsed in a single `while (tokens.Count > 0)` switch — order does not matter
- Unknown flags throw `ArgumentException` immediately

---

## Adding a New Workflow

1. Add a `RunXxx()` method to `DapsmanRunner` following the existing pattern (build service, create plan, print plan, execute if not dry-run)
2. Add a `PrintXxx(plan)` method to `DapsmanPlanPrinter`
3. Add the command token(s) to `CliArguments.Parse` and a dispatch branch in `DapsmanRunner.RunAsync`
4. Add the usage line to `PrintUsage()`
5. Add the command to the README `### Dapsman Workflows` section
