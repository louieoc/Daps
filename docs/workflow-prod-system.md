---
title: Workflow — prod system
---

# `dapsman prod system`

## Why

Daps could provision, deploy to, and back up a remote host, but there was no way to ask "how is
the server doing?" without SSH-ing in and reading `free`/`df`/`top`. For the target persona — a
non-technical creative — that is a wall. The first symptom of a full disk or an exhausted 1 GB VM
is a site that stops working, with no obvious cause and nothing to look at.

The workflow is read-only. It writes nothing to the remote host, which is why the status script
travels over stdin rather than being copied to `/tmp` the way `configure-host.sh` is.

## Shape

```
dapsman prod system [--verbose] [--dry-run] [--provider <name>] [--config <path>]
```

Provider-scoped, not project-scoped. It resolves through `IHostingProviderResolver.ResolveExplicit`,
so with several providers configured an omitted `--provider` is an error rather than a default —
answering the question about a different server than the user meant is worse than refusing to
guess. Same rule as `prod provision` and `prod unprovision`.

Default output is the VM summary plus the reboot flag. `--verbose` adds uptime, per-container CPU
and memory, per-project disk usage, and total Docker disk consumption — the drill-down for when
the summary shows a problem and the next question is which project caused it.

## Design decisions

### Bash collects, C# interprets

`scripts/remote-system-status.sh` does no arithmetic, no unit conversion, no percentages, and
generates no structured format. It reads `/proc` and coreutils output and prints it. Everything
else — kB→bytes, load÷cores, `190MiB` → bytes, "12 days, 4 hours" — happens in
`SystemStatusParser` and `DapsmanPlanPrinter`.

This was a deliberate reversal of the first design, which had the script emit JSON the way
`list-restore-points.toolkit.sh` does. Hand-rolling JSON in bash means hand-rolling JSON escaping,
and bash has no floats without `bc`, which is not guaranteed installed. Both problems disappear
when the script's only job is to report raw values.

The wire format is `<key><TAB><value>[<TAB><value>…]`, one record per line, repeated keys forming
a list (which is why it is not a key/value map). Tabs are safe here: Docker container names are
restricted to `[a-zA-Z0-9][a-zA-Z0-9_.-]*`, and project directory names come from `dapsman init`.

Parsing is forward-compatible on purpose — unknown record keys are ignored, malformed records
dropped, and a missing record leaves its field null so the printer prints "unavailable" rather
than the command failing over one unreadable value. All numeric parsing is
`CultureInfo.InvariantCulture`; the remote emits `0.31`, which a comma-decimal workstation locale
would otherwise read as thirty-one.

Because the fault tolerance is real, the script needs nothing installed on the host: only
coreutils and `/proc`. No `jq`, no `bc`.

### Two levels of quoting on the way out

The toolkit runs a generated wrapper that pipes the payload to the remote:

```bash
ssh -q "${ssh_opts[@]}" "user@host" "bash -s -- 'sudo docker' '1' '/srv/projects'" < payload.sh
```

Both halves matter, and both were bugs first:

- **The payload goes over stdin, not as an ssh argument.** The status script is full of `$`,
  quotes, and `awk`. Embedded, it would have to survive C# interpolation, the
  `docker exec bash -lc "…"` wrapper, and the remote shell's expansion. Redirecting from a file
  removes the last two entirely.
- **The remote arguments are single-quoted inside the command string.** `ssh` does not pass an
  argument vector; it joins everything after the host into one string and hands it to the remote
  login shell, which splits it again. Unquoted, `"sudo docker"` arrives as two arguments and
  shifts every later argument along by one. That failure is silent — `$2` becomes `docker`
  instead of the verbose flag, so the whole verbose section is skipped and the command still
  exits 0. It only showed up on the generic-vps provider, because the RamNode path uses the
  single-word `docker`.

The corollary of the stdin approach: **no command in the status script may read stdin**, since
stdin is the remainder of the script. This is why `sudo` is invoked as `sudo -n` — a password
prompt would try to read the password from the script's own text.

### Two architecture readings, not one

The summary reports `arch` from `uname -m` and `docker_platform` from
`docker version --format '{{.Server.Os}}/{{.Server.Arch}}'`. They are collected separately and
printed together (`x86_64 (docker linux/amd64)`) rather than one being derived from the other: the
two can legitimately disagree on a host whose kernel and userland architectures differ, and the
Docker value is the authoritative one, because it is what the daemon will actually execute.

`docker_platform` is the one summary reading that needs Docker on the host, which looks like a
breach of the section rule above. It is not, because the rule is about not failing: the call is
guarded with `2>/dev/null || true`, so a host without Docker reports an empty value and the
summary still comes back complete. What the rule forbids is a summary that breaks on a missing
tool, and this cannot.

It earns its place in the summary because a second workflow consumes it. `prod deploy` needs the
host's platform before it builds an image for it, and it gets that by calling this workflow's
collector with `Verbose = false` — see [workflow-remote-deploy.md](workflow-remote-deploy.md),
"Target platform". Had this record stayed in the verbose block, every deploy would have paid for
`docker stats` and a `du -sb` walk of `/srv/projects` to read one string. The placement is
therefore load-bearing, and the script says so where the record is defined.

For the human reading a report, the line answers a narrower question: when a container will not
start and the error is `exec format error`, this is where you confirm what the host actually is.

### MemAvailable, not MemFree

RAM used is `MemTotal - MemAvailable`. `MemFree` excludes the page cache, which Linux fills with
reclaimable data on any healthy long-running host, so reporting it would show a well-behaved
server as almost out of memory. For this persona a false alarm is worse than no reading at all.
Verified against `free -m` on a live host: `free`'s own "used" column reads lower still
(it also discounts reclaimable slab), so the `MemAvailable`-derived figure is the conservative
of the two.

### Decimal units in output

Sizes print as decimal GB/MB, not GiB/MiB. A user comparing this against their hosting plan is
reading "40 GB" off a pricing page, which is 40 billion bytes.

### The reboot line names no command

There is no Dapsman workflow that reboots a host or applies updates on demand.
`scripts/configure-host.sh`, run once by `prod provision`, enables unattended-upgrades with
`Automatic-Reboot "true"` at `04:00` (Dapsman never passes the script's optional reboot-time
argument, so the default always applies). `prod provision --upgrade` runs `apt-get upgrade -y`
for generic-vps only, as part of provisioning, and never reboots.

So a pending reboot is reported as an explanation: the host reboots itself at 04:00, and if the
flag persists past that, reboot the VM from the hosting provider's control panel. The wording
lives in one place, `DapsmanPlanPrinter.FormatRebootRequired`. If a `prod reboot` workflow is
ever added, that string is the only thing that needs to change.

## Files

| File | Role |
|---|---|
| `scripts/remote-system-status.sh` | The remote payload. Named `remote-` to leave room for a local counterpart later. |
| `src/Dapsman.Domain/SystemStatusPlan.cs` | The plan |
| `src/Dapsman.Domain/SystemStatus.cs` | The reading; all sizes in bytes, all scalars nullable |
| `src/Dapsman.Application/SystemStatusService.cs` | `CreatePlan` / `Execute` |
| `src/Dapsman.Infrastructure/RemoteSystemStatusPlanBuilder.cs` | `ResolveExplicit`, sudo prefix |
| `src/Dapsman.Infrastructure/ToolkitSystemStatusCollector.cs` | Staging, ssh wrapper, capture |
| `src/Dapsman.Infrastructure/SystemStatusParser.cs` | All interpretation. Separate file so it is testable against a fixture string with no runner, container, or host. |
| `src/Dapsman.Cli/DapsmanPlanPrinter.cs` | `PrintSystemStatusPlan`, `PrintSystemStatusReport`, formatting |

## Possible follow-ups

- `dapsman local system`, reporting the same for the workstation's Docker. The remote script is
  named for this.
- A `prod reboot` workflow, which would change the reboot-required wording.
- Thresholds — flagging a disk over 90% rather than leaving the user to read the percentage.
