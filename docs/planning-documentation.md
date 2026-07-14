---
title: Documentation System
sidebar:
  order: 10
---

## Context

Daps documentation lives in `daps/docs/` as the source of truth and is published at [dapster.org](https://dapster.org) via a separate Astro Starlight site (`dapster/`). The sync script (`dapster/_scripts/sync-docs.mjs`) copies and transforms files from `daps/docs/` into dapster's content directory before each build.

The goals:
- Authors write in `daps/docs/` without needing to think about Astro or Starlight
- Dapster always reflects the current state of `daps/docs/` — no manual copy step
- Synced files are gitignored in dapster; `daps` is the single source of truth

---

## Sync script behaviour

`sync-docs.mjs` runs automatically via npm `prebuild` and `predev` hooks in dapster. On each run:

1. **Wipes** each target directory (`src/content/docs/readme/`, `src/content/docs/workflow/`, etc.) before copying. This prevents orphaned files when source docs are renamed or deleted.
2. **Routes** source files by filename prefix into the appropriate target directory.
3. **Transforms** each file:
   - Injects frontmatter (`title`, `sidebar.order`) if not already present
   - Rewrites relative image references to absolute `/diagrams/` paths
   - Rewrites cross-doc links (bare filename or `.md`) to absolute Starlight URLs
4. **Copies PNGs** from `daps/docs/` to `dapster/public/diagrams/`

### File routing

| Filename prefix | Target directory | Starlight section |
|---|---|---|
| `readme-*` | `src/content/docs/readme/` | Readme |
| `workflow-*` | `src/content/docs/workflow/` | Workflows |
| `planning-*` | `src/content/docs/planning/` | Planning |
| `configuration-*` | `src/content/docs/configuration/` | Configuration |

### `no index` marker

Adding `<!-- no index -->` (or any text containing `no index`) as the **first line** of a source file causes the sync to skip it. Useful for drafts or docs under active revision that shouldn't appear on dapster yet.

### Cross-doc link rewriting

Source docs use relative links between files (e.g. `[CLI Commands](readme-cli-commands)`). The sync script rewrites these to absolute Starlight URLs (e.g. `/readme/readme-cli-commands/`) by matching the link target against the known prefix map. External URLs, root-relative links, and in-page anchors are left untouched.

### Image path rewriting

Source docs reference diagram images with bare filenames (e.g. `![diagram](Daps architecture-Local.drawio-800.png)`). The sync script rewrites these to `/diagrams/<filename>` to match where the PNGs are served from in dapster.

---

## Document naming conventions

Files in `daps/docs/` follow a prefix convention that determines where they appear in dapster's sidebar:

- `readme-*` — user-facing documentation: overview, setup, deployment, conventions, etc.
- `workflow-*` — stable documentation for specific Dapsman workflows
- `planning-*` — in-progress design docs (work-item spec + implementation notes)
- `configuration-*` — reserved for future configuration reference docs

---

## The graduation system

Planning docs start as `planning-<feature>.md`. Once a feature is stable and the doc has been reviewed, it is renamed to `workflow-<feature>.md` (or `configuration-`, etc.). This signals that the doc has shifted from "how we built this" to "how this works."

The graduation process:
1. Review the planning doc and trim scaffolding (implementation task lists, verification steps) that is no longer useful as reference
2. Rename the file with the appropriate prefix
3. The new prefix routes it to a different sidebar section on dapster automatically

Planning docs that have not yet graduated remain under the Planning section and are visible on dapster — this is intentional, as they provide context for in-progress work.

---

## Template READMEs

Each project template (`templates/<name>/README.md`) serves as the primary documentation for that template. The sync script does not copy these files — they live in the Daps repository and are visible on Codeberg.

The `readme-templates.md` file in `daps/docs/` provides a high-level overview of available templates and links to the Daps repository. Template-specific `docs/template-*.md` files are not used; any such files should be marked `no index` or removed.

---

## Decisions made

**Why wipe-and-recopy instead of incremental sync?**
Incremental sync leaves orphaned files when source docs are renamed or deleted. The doc set is small enough that a full recopy on each build is negligible.

**Why not sync template READMEs?**
Template READMEs contain setup instructions aimed at users of that template — they are better read in context (next to the template files on Codeberg) than as standalone pages on dapster. Syncing them would also create a maintenance burden when the README structure differs from the rest of the docs.

**Why absolute Starlight URLs instead of Codeberg links for cross-doc links?**
Cross-doc links reference files that exist in dapster. Starlight root-relative URLs (`/readme/readme-cli-commands/`) are stable and work correctly in the built site. Codeberg links would require hardcoding a repository URL and would break if the repo moved.
