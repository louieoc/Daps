# Docker Assisted Portable Sovereignty (Daps)

A set of patterns, conventions, workflows and tooling for building and managing portable web stuff.

Author: Louis O'Callaghan (https://louisocallaghan.com)

Daps gives individual artists, hobbyists, and small businesses a way to own and control their web presence — running Docker-based sites locally, deploying them to any VPS hosting provider, taking backups, and migrating without lock-in. It doesn't require knowing Docker; a CLI called `dapsman` provides all the workflows.

Documentation is in the [`docs/`](docs/) folder and published at [dapster.org](https://dapster.org).

## Documentation

### Readme
- [Overview](docs/readme-overview.md) — what Daps is, technology requirements, project structure, architecture diagrams
- [Setup](docs/readme-setup.md) — installing prerequisites, setting up `dapsman`, initializing a project from a template
- [Deployment](docs/readme-deployment.md) — provisioning a host (OpenStack and generic VPS), deploying projects, toolkit access
- [Conventions](docs/readme-conventions.md) — terminology, file naming, folder layout, Docker and Caddy patterns
- [CLI Commands](docs/readme-cli-commands.md) — all `dapsman` commands with flags and behavior
- [Security](docs/readme-security.md) — design trade-offs and their implications
- [Principles](docs/readme-principles.md) — governing philosophy, about the authors

### Other docs: Planning, Workflows, etc.
Internal design documents capturing the why and how behind features are in `docs/planning-*.md`.

As features are completed and become "mature" these documents "graduate" (Claude's term, I like it) to a more specific categorization, e.g. `workflow-local-restore.md`, `template-astro.md`, etc.

The idea is for these files to serve multiple purposes: as an Azure DevOps or Jira work item type of thing, a planning context for LLMs and humans to refine each feature, and finally as mature feature documentation, mainly for humans but also providing context for future development by humans and LLMs.

These files all feed into [dapster.org](dapster.org).
