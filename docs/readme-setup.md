---
title: Setup
sidebar:
  order: 2
---

## Setup

1. Install [Docker Desktop](https://www.docker.com/products/docker-desktop/)
1. Install [Git](https://git-scm.com/install/)
1. Install [.NET 9 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/9.0)
1. Clone daps (currently on [Codeberg](https://codeberg.org/louieoc/Daps))
1. Copy `daps.yaml.example` to `daps.yaml` — this is your personal config file and is gitignored
1. Set up the `dapsman` command (instructions below)
1. Set up a project (instructions below)


### Setting up the `dapsman` command

Add `<repo>/bin` to your PATH once after cloning. The `bin/` folder contains launcher scripts for each platform that delegate to `dotnet run`, so you always run from source without a separate build step.

**Windows** — add `C:\path\to\daps\bin` to your user PATH via System Properties, or for the current session:
```powershell
$env:PATH += ";C:\path\to\daps\bin"
```

**Mac/Linux** — make the script executable, then add to PATH:
```bash
chmod +x /path/to/daps/bin/dapsman
export PATH="/path/to/daps/bin:$PATH"  # add to ~/.zshrc or ~/.bashrc to persist
```

After that, `dapsman` works from any directory.


### Initializing a project from a template

Daps has 2 templates currently, Wordpress (`daps/templates/wordpress`) and Grist (`daps/templates/grist`) -- note both have readmes as well, worth looking at.

In this example I'll create the Wordpress site at `wp.dapster.org`.
Assume that the setup steps above have been completed and Daps is cloned at `C:\repos\daps`.

1. run init: `dapsman init --template wordpress --name dapster-wp`
    - This creates a folder at `C:\repos\dapster-wp`
1. edit caddy site files
    - Open `C\repos\dapster-wp\_caddy_sites\dapster-wp.prod.caddy` and `dapster-wp.offline.caddy`
    - replace text of `dapster-wp.example.com` with the actual domain, e.g. `wp.dapster.org`
1. edit docker files
    - Open `C\repos\dapster-wp\_docker\compose_dapster-wp.prod.yaml`
    - set `WP_HOME` and `WP_SITEURL` URLs to `https://wp.dapster.org`
1. run local build: `dapsman local build`
    - dapsman will try to update your local `hosts` file to add this entry, but if you're not running as administrator you will need to do it manually: `127.0.0.1 dapster-wp.localhost`
    - you may need to run `dapsman local caddy restart` depending on whether you're adding a project to an existing setup
1. visit `https://dapster-wp.localhost` and set up your local Wordpress installation (e.g. themes, plugins, posts, etc.)

For production, see [Deployment](readme-deployment).


### Initializing a project without a template

TODO
