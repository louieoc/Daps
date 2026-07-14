---
title: Templates
sidebar:
  order: 4
---


## Templates

Daps ships with several project templates. Run `dapsman init --template <name> --name <project-name>` to start a new project from one. Each template's `README.md` (in `templates/<name>/`) covers setup, services, scripts, and any template-specific workflows.

### Overlay vs. Init

The Wordpress, Grist and static templates assume you'll start your Wordpress or Grist projects from these templates, not that you're coming in with an existing Wordpress or Grist project. You can adapt the templates to existing projects but that's not how they're meant to be used.

The Astro template, on the other hand, is an "overlay" template, meaning you're meant to create the Astro project first and then run the `dapsman init` workflow to apply Daps files on top of the existing project. This is because Astro comes with its own initialization and development ecosystem that Daps doesn't need to mess with.

### Templates list

| Template | Description |
|---|---|
| `wordpress` | Self-hosted WordPress with MySQL, Redis object cache, and PhpMyAdmin (dev only) |
| `astro` | Overlay template for an existing Astro project — adds Daps infrastructure files without touching project source |
| `static` | Serves a pre-built `public/` directory via nginx |
| `grist` | Self-hosted [Grist](https://www.getgrist.com/) spreadsheet/database |

For how `dapsman init` works under the hood, see [Init](/workflow/workflow-init/).


