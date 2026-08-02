# Planning: `dapsman local build`

Design notes for the `dapsman local build` workflow. See `readme-cli-commands.md` for user-facing documentation.

## Volume handling

Three levels of destruction are available, deliberately kept distinct:

| Flag | Effect | Data loss |
|------|--------|-----------|
| (none) | `docker compose up -d` | none |
| `--build` | adds `--build --renew-anon-volumes` | anonymous volumes only |
| `--rebuild` | `docker compose down -v` first, then `up -d --build --renew-anon-volumes` | **named volumes too — databases included** |

`--renew-anon-volumes` was added to `--build` so that image upgrades (e.g. a new WordPress version) take effect immediately instead of being shadowed by a stale anonymous volume. It deliberately does *not* touch named volumes, because those hold the project's actual data.

### Why `--rebuild` exists

`--build` cannot recover a project whose *named* volume is corrupt. The motivating case: MySQL's first-run initialization is interrupted (Ctrl-C on a slow build, Docker Desktop restarting, the VM being killed). The data directory is left with `#innodb_redo/` full of `*_tmp` files and an empty `mysql/` system-tables directory. On the next start the entrypoint sees a non-empty `/var/lib/mysql`, skips initialization, and InnoDB aborts with:

```
[ERROR] [MY-012960] [InnoDB] Cannot create redo log files because data files
are corrupt or the database was not shut down cleanly after creating the data files.
```

The container then restart-loops. Nothing in the build workflow could clear that volume, so the only fix was a hand-written `docker compose down -v` with the right `-f` flags — exactly the kind of Docker knowledge Daps exists to hide from its users.

### Constraints

- **`--project` is required.** Without it, `local build` selects every configured project, and a stray `--rebuild` would wipe every database on the workstation. Making the filter mandatory removes that failure mode entirely.
- **Typed `yes` confirmation**, matching `local teardown`'s prompt. `--yes` skips it for scripted use.
- **The confirmation happens before any step runs**, not immediately before the teardown, so an aborted rebuild leaves nothing half-applied.
- **DAPS shared services are out of scope.** The teardown runs against each project's compose files only. Tearing down the Caddy/toolkit stack would destroy `daps_caddy_data` and take every other project offline.

### Deliberately not included

- **No backup before teardown.** `--rebuild` is for volumes that are already unusable, where a backup would capture corrupt data and add a slow step to the common case. `local restore` remains the path for recovering contents.
- **No container-only variant** (`down` without `-v`). It would not have solved the motivating problem, and `--build` already covers image staleness.

## Related

- `local teardown` also removes the project folder, backups, and the `daps.yaml` entry. `--rebuild` is the narrower operation: same volumes destroyed, but the project stays registered and on disk.
