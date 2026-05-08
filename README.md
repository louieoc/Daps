# Docker Assisted Portable Sovereignty (daps)

A set of patterns, conventions, workflows and tooling for building and managing portable web stuff.

Author: Louis O'Callaghan (https://louisocallaghan.com)


## Overview

You are an individual artist, hobbyist, or small business. You own one or more websites. You don't want to be locked into a specific hosting provider. You want to own your data and take it with you when you leave.

Stretch goal: you don't have or want a public website but you use products like Google Docs and don't want to depend as much on third parties (and you want to own your data). But managing FOSS (Free Open Source Software) alternatives is daunting. DAPS can make it easier.

You should be able to:
- pick some web technology you want to use (e.g Wordpress, or a custom website)
- develop it on your local computer
- run and test it on your local computer (dev)
- deploy it to a remote hosting provider (production aka prod)
- backup any data and/or files captured at the hosting provider back to your local computer
- push changes and updates to prod
- delete your data/files from the hosting provider
- deploy it again somewhere else
- try to avoid lock-in and enshitification


## Technology

The basis for this is containerization with Docker. Lots of FOSS is available as docker images and it's fairly easy to create your own.

You have a computer, say a laptop. It shouldn't matter whether you're running Windows, Mac or Linux. You have these prerequisites:
- docker desktop running locally
- git
- bash (git bash for Windows)

Daps enables 2 environments:
1. the local docker which acts as a "dev" environment
    - a "toolkit" Linux container runs here and provides a common runtime environment for tools and remote deployment workflows
    - local Caddy container for managing reverse proxy for "mysite.localhost"
2. a remote environment running docker that acts as "prod"
    - remote Caddy container for managing reverse proxy for domains
    - currently Daps targets OpenStack VPS instances, but we should be able to support other Unix hosting options


## Projects

The point of Daps is to provide the local and remote environments for your projects. A project is typically a web site or app that might have its own repository and is saved in its own folder.

Your setup might look like this:

```
repositories/           # wherever you store your code projects 
├── daps/               # clone of this repo
├── mywpsite/           # a Wordpress project
└── mycustomsite/       # a bespoke website you've made
     ├── app/           # your website's frontend
     └── api/           # your website's api
```

The `daps` folder contains daps. The `mywpsite` and `mycustomsite` folders are projects.

Each project contains a small set of folders and files that enable it to be deployed and managed with Daps. Otherwise each project doesn't know about Daps, and you develop it however you need to.

Daps includes a file `daps.yaml` in which you register the projects you want it to manage.

Daps then provides workflows for managing project deployment. The workflows are described below.


## Governing principles

The internet was meant to be decentralized, and is largely built using free and open source software and protocols. The technology for controlling your own internet stuff exists but isn't easy to use, while proprietary software services make things easy to set up but are not portable (and you become someone else's product).

FOSS > simplicity > flexibility

composition > inheritance


### Who am I

I am a musician and software developer. I've had some public websites and ran them personal servers, shared Linux hosting, shared hosting with "easy" wordpress management, Azure App Services, and like, Tumblr. Setting these up and migrating from one to another or maintaining multiple sites in different ways has been annoying. It makes it less fun and makes it harder to build new things.

Daps is an idea to abstract deployment into essential workflows within a common environment that's as nonproprietary as practical. I built it with a lot of help from first Codex and now Claude. Those are extremely proprietary and I'm aware of the irony. Ideally Daps is easy to use without an LLM (but boy does it help). I want to use Daps to build new things and have the deployment pieces "solved."


## Conventions

### Terminology
- **local** vs **remote**: terms signaling location, where local is the local workstation (i.e. docker running locally), remote is at a hosting provider
- **dev** vs **prod**: terms signaling the purpose of the deployment. AKA the "environment," though I want to call out what the environment is for vs where it lives. In practice "dev" will be synonymous with "local" and "prod" with "remote." There's no "staging" or "uat," given I'm going for simplicity, but supporting other remote environments could be a thing in the future.


### Practices
- prefer keep local and remote paths the same when possible
    - e.g. for bind mounts like daps `docker` and projects' `_docker` folders, or Caddyfile inside a caddy folder instead of in the root
- prefer not to copy files to central daps if possible
    - e.g. `caddy_sites` are copied centrally because I don't think it would work otherwise, but docker compose files don't need to be copied to a central daps `docker` folder


### daps.yaml
Configuration file for daps
- providers section
    - remote host info
- projects section
    - contains settings for each project
    - most project settings should be discoverable by convention, but just knowing the project exists is the least info needed in this file

### Docker compose filenames
- `compose_daps.yaml`
- `compose_projectname.yaml`
    - used by both dev and prod
    - appears first when executing compose
    - daps files are composed first
- `compose_daps.dev.yaml`
- `compose_projectname.dev.yaml`
    - used only by dev
- `compose_daps_projectname.yaml`
    - executed when composing daps
    - used to bind mount project folders to the toolkit
    - should only be for dev(?)
- `compose_daps.prod.yaml`
- `compose_projectname.prod.yaml`
    - used only by prod

### Docker compose file locations
- `compose_daps.yaml`
- `compose_daps.dev.yaml`
- `compose_daps.prod.yaml`
    - in the daps project folder, inside folder named `docker`
- `compose_projectname.yaml`
- `compose_projectname.dev.yaml`
- `compose_projectname.prod.yaml`
- `compose_daps_projectname.yaml`
    - in project folder, inside folder named `_docker`
    - underscore is to alphabetize it "out of the way" of the project source
    - during compose, should be read from this location (i.e. not copied elsewhere first)
    - relative paths within it need special attention to 

### Docker folders
- `docker`
    - in daps project root
- `_docker`
    - in project root
    - underscore is to alphabetize it "out of the way" of the project source
    - the underscore should be maintained in bind mounts (as in, don't bind `projectname/_docker` to `/srv/projects/projectname/docker` but to `/srv/projects/projectname/_docker`)

### Docker context
- for local deployment, docker runs on your local workstation
- for remote deployment, docker runs on the remote VM (e.g. Openstack instance)
- for building images, docker runs on the local workstation to save on compute costs, assuming you're charged for those on the remote VM


### A note on docker paths
- Write project compose files assuming paths are relative to the compose file directory (`_docker`).
- Avoid relying on current shell working directory.
- dapsman uses `-f <absolute path>` when generating a docker compose command.
- `compose_daps_projectname.yaml` files need paths relative to where `compose_daps.yaml` lives
    - e.g. `../../projectname/_docker:/srv/projects/projectname/_docker`
    - longer-term dapsman should generate absolute paths from daps.yaml project paths

### Project scripts
- Project scripts for use with daps appear in a folder `_scripts` (the underscore alphabetizes them away from the main project files)
- `build-docker-images.toolkit.sh` daps will look for this file for instructions for building container images for the project.


### Caddy files
- `Caddyfile`
    - located in `caddy` folder for dev, VM's `/srv/daps/caddy/` folder for prod
    - both locations are bind-mounted identically in `compose_daps.yaml`
        - why bind mount it? We might want to change it while deployed, e.g. to set it http only for testing, or to set up maintenance redirects or something
    - no setup inside, only imports files (`*.caddy`) from `caddy_sites` folder
- `project.dev.caddy`
    - file to be copied into `caddy_sites` folder for dev
- `project.prod.caddy`
    - file to be copied into `caddy_sites` folder for prod

### Caddy folders
- `caddy`
    - located in daps project root
    - contains `Caddyfile`
- `caddy_sites` (dev)
    - located in daps root
    - should be empty for daps in git (or .gitignored) -- will receive files copied from projects
    - for prod a folder should be created with project files copied into it
    - should be bind-mounted from caddy container -- to `daps/caddy_sites` in dev, to VM `caddy_sites` in prod
- `caddy_sites` (prod)
    - located in VM (openstack) `/srv/daps/caddy_sites`
    - bind mounted (readonly) from caddy `/etc/caddy/sites`
- `_caddy_sites`
    - located in project root
    - underscore is to alphabetize it "out of the way" of the project source
    - contains caddy config to be imported by `Caddyfile`


### Hosting folder and files
- `hosting`
    - located in daps project root
    - contains files specific to a particular hosting provider, so as to separate out "custom" configuration/settings from "generic" scripts that act on them
    - supporting OpenStack only to start
    - can include files for multiple hosting providers
- openrc file
    - the openrc filename is not restricted by convention, since typically you download this file from the OpenStack provider
    - if the name given at download is too generic, lean toward `<providername>_openrc` (e.g. `ramnode_openrc` where RamNode is a hosting company)
- `openstack_providername_instance_vars.sh`
    - variables describing OpenStack "flavor," "image" and "network" IDs which are specific to the hosting provider
    - this file is read/write, meaning we should allow Dapsman to write to it if/when we can support an interactive setup workflow. One can also just update it directly


### Backup folders and scripts
- `_backups`
    - located in project root
    - underscore is to alphabetize it "out of the way" of the project source
    - created automatically when a backup is run; should be gitignored (may contain database dumps, uploaded files, or other sensitive data)
    - backups from prod are placed in `_backups/from_prod/`
    - the toolkit container accesses this folder via the project's bind mount (same path as other `_` folders)
- `_scripts/backup-remote.toolkit.sh`
    - optional; daps skips the backup workflow for projects that don't have this script
    - runs inside the toolkit container (never uploaded to the remote)
    - receives env vars: `DAPS_PROJECT`, `DAPS_REMOTE_HOST`, `DAPS_REMOTE_USER`, `DAPS_SSH_KEY`
    - receives args: `--env prod --to <destination-path-in-toolkit>`


### OpenStack resources
- keys
    - we'll need a security key to access the OpenStack instance/VM. Naming convention is `daps-key-providername` (e.g. `daps-key-dreamcompute`) for both the OpenStack key and the ssh key that goes with it (saved in toolkit's `/root/.ssh`)
- instance name
    - to start we only support one instance for any OpenStack host, but might want to add support for more later
    - default name should be `daps-environment` where `prod` is the assumed environment for remote daps deployments. Again, might want to support others later
    - should be able to override instance name in the daps.yaml file


#### Remote VM
On the remote openstack instance or whichever VM we're running docker on, we will also have conventions.

- folder locations
    - `/root/.ssh` - we copy the private key here? not sure actually
    - `/srv/daps/`
        - `caddy/Caddyfile` - copy the Caddyfile.prod here
        - `docker/` - copy daps shared and prod compose files
    - `/srv/projects/`
        - `projectname/_docker/` - copy project daps-linked, shared and prod compose files
        - `projectname/_docker/image-exports` - copy built docker image tars here


## Security Considerations

DAPS is designed for trusted single-user environments (your personal workstation and a VPS you control). Some of its design choices involve trade-offs that are acceptable in that context but worth understanding.

### Docker socket mount in toolkit

The toolkit container mounts the host Docker socket (`/var/run/docker.sock`). This gives any process inside the toolkit root-equivalent access to the host — it can start containers, mount host paths, and so on. This is intentional: toolkit scripts need to `docker exec` into other local containers (e.g. for database sync). It would be a serious risk in a multi-user or shared environment.

### SSH keys in toolkit

SSH keys live at `~/.ssh/` inside the toolkit container, bind-mounted from `secrets/ssh/` on the workstation. Any process that can `docker exec` into the toolkit has access to these keys. The same trust boundary applies as above: acceptable for a single-user local setup.

### Secret files on remote are `chmod 644`

Secret files in `_secrets/` are bind-mounted into containers on the remote server. They must be readable by `www-data` inside the container, which requires `chmod 644`. This means any process running in the container can read them. A proper secrets manager (e.g. Docker Swarm secrets, Vault) would be better long-term, but is outside the current scope.

### OpenRC and instance vars files

OpenStack credentials (`hosting/*_openrc`) and instance variable files (`hosting/openstack_*_instance_vars.sh`) are stored as plain files on disk. They are gitignored and should never be committed. Keep the `hosting/` folder out of any backup or sync that might expose it.

### Dev ports are localhost-only

Dev compose files bind exposed ports to `127.0.0.1` so they are not reachable from other machines on the network. Do not change these to `0.0.0.0` on a shared or corporate network.

---

## Dapsman Workflows

All commands support `--dry-run`, which prints the plan without executing anything. `--config <path>` overrides the default `daps.yaml` location (defaults to the current directory).

### `dapsman init`
```
dapsman init --template <name> --name <project-name> [--path <destination>] [--dry-run] [--config <path>]
```
Creates a new project from a template. Copies the template directory to the destination (defaults to a sibling of the daps folder), runs `_scripts/init-template.toolkit.sh <project-name>` inside the copy if present (template-specific placeholder replacement lives there, not in dapsman), and registers the project in `daps.yaml`.

### `dapsman local build`
```
dapsman local build [--build] [--dry-run] [--project <name>...] [--config <path>]
```
Brings up the local dev Docker environment. Steps: syncs project caddy site files to `caddy_sites/`, runs each project's prerequisite scripts (e.g. `prerequisites.sh`, `prerequisites.dev.sh`), composes DAPS services (Caddy, toolkit), then composes each project's containers. `--build` forces Docker image rebuilds. Omitting `--project` runs all configured projects.

### `dapsman local caddy restart`
```
dapsman local caddy restart [--dry-run] [--config <path>]
```
Reloads Caddy's configuration on the local Docker instance (`docker exec daps-caddy-1 caddy reload`). Useful after changing site files without doing a full local build.

### `dapsman prod build`
```
dapsman prod build [--dry-run] [--provider <name>] [--set-vars-script <path>] [--create-script <path>] [--config <path>]
```
Provisions a new remote OpenStack instance from the toolkit container. Creates an SSH keypair if one doesn't exist, uploads the public key to OpenStack, and runs the instance creation script (`scripts/openstack-create-instance.sh`). Run this once when setting up a new hosting environment.

### `dapsman prod deploy`
```
dapsman prod deploy [--dry-run] [--project <name>...] [--provider <name>] [--set-vars-script <path>] [--skip-build-images] [--config <path>]
```
Deploys projects to the remote server. Steps: builds Docker image tarballs on the workstation (unless `--skip-build-images`), uploads Caddyfile, caddy site files, compose files, image tarballs, and project scripts to the remote via SCP from the toolkit, then starts containers on the remote via SSH. Omitting `--project` deploys all configured projects.

### `dapsman prod caddy restart`
```
dapsman prod caddy restart [--dry-run] [--provider <name>] [--config <path>]
```
Reloads Caddy's configuration on the remote server via SSH from the toolkit container. Useful after a caddy site file change without a full redeploy.

### `dapsman prod offline`
```
dapsman prod offline [--project <name>...] [--dry-run] [--provider <name>] [--config <path>]
```
Takes one or more projects offline by replacing the prod Caddy site file with a 503 maintenance page. If the project has a `_caddy_sites/<name>.offline.caddy`, that file is used; otherwise a generic page is generated from the domain in the prod caddy file. Omitting `--project` runs against all configured projects.

### `dapsman prod online`
```
dapsman prod online [--project <name>...] [--dry-run] [--provider <name>] [--config <path>]
```
Restores one or more projects from offline by pushing the original prod Caddy site file back to the remote. Omitting `--project` runs against all configured projects.

### `dapsman prod sync-from-local`
```
dapsman prod sync-from-local --project <name> [--dry-run] [--provider <name>] [--config <path>]
```
Syncs content from the local workstation to the remote server. Runs `_scripts/sync-local-to-remote.toolkit.sh` from the toolkit container. What exactly is synced is defined by the script; typically used to push a local database or uploaded files to prod (e.g. for WordPress: rsync `wp-content`, mysqldump, URL replacement). Requires `--project`.

### `dapsman local sync-from-prod`
```
dapsman local sync-from-prod --project <name> [--dry-run] [--provider <name>] [--config <path>]
```
Pulls content from the remote server to the local workstation. Runs `_scripts/sync-remote-to-local.toolkit.sh` from the toolkit container. Typically used to pull a production database or uploaded files down for local development. Requires `--project`.

### `dapsman prod backup`
```
dapsman prod backup [--project <name>...] [--dry-run] [--provider <name>] [--config <path>]
```
Backs up remote data to the local workstation by running `_scripts/backup-remote.toolkit.sh` from the toolkit container. Projects without this script are skipped with a message. Backups are written to `_backups/from_prod/` inside the project directory. Omitting `--project` runs against all configured projects that support it.
