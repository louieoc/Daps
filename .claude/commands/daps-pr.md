---
description: Pre-merge PR review — checks version bump, changelog, README, daps-dryrun coverage, code quality, and runs a dry-run health check
---

Run a pre-merge review of the current branch against main. Follow every step below and produce a consolidated report at the end.

---

## 1. Understand what changed

Run these commands and note the results — you will need them throughout:

```
git log main..HEAD --oneline
git diff main...HEAD --name-only
git diff main...HEAD -- src/
git diff main...HEAD -- docs/ README.md .claude/
```

---

## 2. Version and changelog check

**a. Check whether the version was bumped:**

```
git show main:src/Dapsman.Cli/Dapsman.Cli.csproj | grep "<Version>"
```

Compare to the current value in `src/Dapsman.Cli/Dapsman.Cli.csproj`.

- If the version is bumped: confirm it follows semver (MAJOR.MINOR.PATCH). A user-facing change warrants at minimum a PATCH bump.
- If the version is NOT bumped: flag it. Any PR that adds, changes, or fixes user-visible behavior should bump the version.

**b. Check that the changelog is updated:**

Read `docs/readme-changelog.md`. Confirm there is an entry for the new version. Flag if missing.

---

## 3. README check

Read `README.md`'s "Dapsman Workflows" section.

If any CLI command was added, removed, renamed, or had its flags changed in this PR, confirm the README reflects the current state. Flag any discrepancy.

---

## 4. daps-dryrun coverage check

The goal: every command registered in `CliArguments.cs` must have a corresponding line in `.claude/commands/daps-dryrun.md`.

**a. Extract registered commands from `src/Dapsman.Cli/CliArguments.cs`:**

Read the file and list every `group`/`action` pair and every top-level command (like `init`) that is handled by the argument parser.

**b. Extract covered commands from `.claude/commands/daps-dryrun.md`:**

Read the file and list every `dotnet run --project src/Dapsman.Cli --` invocation.

**c. Compare:**

- Any command in `CliArguments.cs` that has no corresponding line in `daps-dryrun.md` is a gap.
- For each gap, write the exact line that should be added to `daps-dryrun.md` (and the corresponding table row), and ask whether to apply it.

If there are no gaps, report PASS.

---

## 5. Code review

Review the changed C# files (from step 1) for the following. Report any findings with file and approximate line number.

**DDD layering:**
- No Infrastructure types referenced in Application or Domain layers
- No business logic in CLI (`Program.cs`, `DapsmanRunner.cs`)
- Services take interfaces, not concrete implementations

**Plan pattern:**
- New workflows follow CreatePlan → print plan → Execute
- `--dry-run` skips execution but still prints the plan

**IBashRunner usage:**
- `RunScript` used for `.sh` files, `RunShell` for inline expressions
- `ContainerBashRunner` used for toolkit operations, `WorkstationBashRunner` for local
- Interactive prompts use `interactive: true`

**General:**
- No floating image tags (`:latest`, `nginx:alpine`) introduced in templates
- Dev host ports bound to `127.0.0.1`
- Secret files expected to be `chmod 644` on remote, not 600

---

## 6. Dry-run health check

Follow the instructions in `.claude/commands/daps-dryrun.md` exactly. Run all workflows and capture pass/fail.

---

## 7. Report

Print a single consolidated report with these sections:

**Version & changelog**
PASS or NEEDS ATTENTION — state the old and new version, confirm changelog entry present or flag it missing.

**README**
PASS or NEEDS ATTENTION — list any workflow entries that need updating.

**daps-dryrun coverage**
PASS or GAPS FOUND — list any uncovered commands and the exact lines to add.

**Code review**
List any findings (file, line, description), or "No issues found."

**Dry-run results**

| Workflow | Result |
|---|---|
| (results from step 6) | |

For any FAILs, include the error beneath the table.
