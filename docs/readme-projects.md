---
title: Projects
sidebar:
  order: 3
---

## Projects

Daps isn't anything without projects. What's a project? A website, a web app or API. At a more basic level it's something you can run in a container, but mainly Daps is about websites.

Daps is meant to "overlay" on a project, providing tooling so the project can run in the Daps environment. In every other way a project should be independent of Daps.

For example, if it's an Astro site, you would develop it as you would any other Astro site, using Node, Astro's CLI, repository management, internal web server, etc. to get it working. Daps adds some folders (prefixed with underscore) to the project root folder, and an entry in its own daps.yaml file, to become a Daps project. Then you can use Daps to "build" your project into your local Docker Desktop, and from there, deploy to remote hosts.

Some Daps templates, which are [covered more in the next section](readme-templates.md), can also be used to initialize an entirely new project. The Wordpress template works like this.

### Initializing a project from a template

In this example I'll create the Wordpress site at `wp.dapster.org`.
Assume that the Setup steps have been completed and Daps is cloned at `C:\repos\daps`.

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

For production, see [Deployment](readme-deployment.md).


### Initializing a project without a template

TODO
