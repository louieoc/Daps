---
description: Build Dapsman and run all workflows in --dry-run mode, reporting which plans succeed or fail
---

Run a dry-run health check of all Dapsman workflows. Follow these steps exactly:

**1. Build**

Run: `dotnet build src/Dapsman.Cli`

If the build fails, stop and report the build error. Do not proceed.

One exception: MSBuild sometimes fails on a stale file in `obj/`, reporting `MSB3492: Could not read existing file "obj\...\*.AssemblyInfoInputs.cache"`. That is a local build-artifact problem, not a code problem. Delete the named cache file and build again. Report it only if a clean build still fails.

**2. Check coverage**

Read `docs/readme-cli-commands.md` — it is the source of truth for which workflows exist. Compare its workflow list against the invocations below. If a workflow is documented there but has no invocation here, report it as **UNCOVERED** in the summary table rather than silently skipping it.

One exception: **`local teardown` is intentionally excluded** — report it as **EXCLUDED**, not UNCOVERED. It deletes the project's source folder, not just its containers and volumes, so an invocation that lost its `--dry-run` through a bad edit would destroy a project directory. Absence is a cheaper safeguard than correctness here. Do not add it.

The list below is deliberately concrete rather than generated: each invocation needs real arguments (`--provider ramnode`, `--project dapster-wp`) that only make sense against this workstation's `daps.yaml`.

It covers one flag as well as the workflows: `local build --rebuild`, which prints an extra `rebuild-teardown` step that nothing else exercises. Unlike `local teardown` it is safe to list, because losing its `--dry-run` would not destroy anything unattended — a real `--rebuild` prompts for a typed `yes`, and with stdin detached that read returns nothing and the run cancels.

**3. Run each workflow in dry-run mode**

Run each command below using the Bash tool. Capture whether each exits 0 (pass) or non-zero (fail). Do not stop on failure — run all of them.

If you batch them into one shell loop, redirect each invocation's stdin from `/dev/null` (e.g. `eval "$cmd" </dev/null`). Without it, `dotnet run` inherits the loop's stdin and consumes the remaining commands from the list, so everything after the first silently never runs and the table comes back with one row.

```
dotnet run --project src/Dapsman.Cli -- local build --dry-run
dotnet run --project src/Dapsman.Cli -- local build --rebuild --project dapster-wp --dry-run
dotnet run --project src/Dapsman.Cli -- init --template wordpress --name testproject --dry-run
dotnet run --project src/Dapsman.Cli -- local caddy restart --dry-run
dotnet run --project src/Dapsman.Cli -- prod caddy restart --dry-run --provider ramnode
dotnet run --project src/Dapsman.Cli -- prod provision --dry-run --provider ramnode
dotnet run --project src/Dapsman.Cli -- prod deploy --dry-run --provider ramnode
dotnet run --project src/Dapsman.Cli -- prod offline --dry-run --provider ramnode
dotnet run --project src/Dapsman.Cli -- prod online --dry-run --provider ramnode
dotnet run --project src/Dapsman.Cli -- prod backup --project dapster-wp --dry-run --provider ramnode
dotnet run --project src/Dapsman.Cli -- prod sync-from-local --project dapster-wp --dry-run --provider ramnode
dotnet run --project src/Dapsman.Cli -- local sync-from-prod --project dapster-wp --dry-run --provider ramnode
dotnet run --project src/Dapsman.Cli -- local restore --project dapster-wp --list-restore-points
dotnet run --project src/Dapsman.Cli -- prod teardown --project dapster-wp --dry-run --provider ramnode
dotnet run --project src/Dapsman.Cli -- prod unprovision --dry-run --provider ramnode
```

**4. Report results**

Print a summary table, adding a row marked UNCOVERED for any workflow found in step 2 that has no invocation above:

| Workflow | Result |
|---|---|
| local build | PASS / FAIL |
| local build --rebuild | PASS / FAIL |
| init | PASS / FAIL |
| local caddy restart | PASS / FAIL |
| prod caddy restart | PASS / FAIL |
| prod provision | PASS / FAIL |
| prod deploy | PASS / FAIL |
| prod offline | PASS / FAIL |
| prod online | PASS / FAIL |
| prod backup | PASS / FAIL |
| prod sync-from-local | PASS / FAIL |
| local sync-from-prod | PASS / FAIL |
| local restore (list) | PASS / FAIL |
| prod teardown | PASS / FAIL |
| prod unprovision | PASS / FAIL |

For any FAILs, include the error message beneath the table.
