---
title: Conventions
sidebar:
  order: 6
---

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

### Docker folders
- `docker`
    - in daps project root
- `_docker`
    - in project root
    - underscore is to alphabetize it "out of the way" of the project source
    - the underscore should be maintained in bind mounts (as in, don't bind `projectname/_docker` to `/srv/projects/projectname/docker` but to `/srv/projects/projectname/_docker`)

### Docker context
- for local deployment, docker runs on your local workstation
- for remote deployment, docker runs on the remote VM (e.g. an OpenStack instance or a generic VPS)
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
- **Template scripts must not contain the project placeholder.** as of v0.3.1, `dapsman init` copies `_scripts` verbatim and never runs the placeholder transform over it, so a project's scripts stay byte-identical to the template's and can be updated by copying the template's forward. Scripts that need the project name read `DAPS_PROJECT` from the environment, guarded with `: "${DAPS_PROJECT:?DAPS_PROJECT is required}"`.
- Dapsman sets `DAPS_PROJECT` wherever it runs a project script: prerequisites (local and remote), `build-docker-images.toolkit.sh`, backup, restore, sync, and post-deploy. Values other than the project name follow the same pattern — new inputs are added as `DAPS_*` env vars rather than positional arguments, so scripts written before the variable existed keep working unchanged.


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
    - contains files specific to a particular hosting provider, to separate provider-specific configuration from generic scripts
    - can include files for multiple hosting providers
    - for OpenStack providers: OpenRC file and instance vars file
    - for generic-vps providers: no files needed (hostname and user live directly in `daps.yaml`)
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
    - backups from prod are placed in `_backups/from_prod/`; snapshots of the local instance are placed in `_backups/from_local/`
    - one subfolder per environment a backup can come from; `dapsman local restore` searches all of them
    - the toolkit container accesses this folder via the project's bind mount (same path as other `_` folders)
- `_scripts/backup-remote.toolkit.sh`
    - optional; daps skips `prod backup` for projects that don't have this script
    - runs inside the toolkit container (never uploaded to the remote)
    - receives env vars: `DAPS_PROJECT`, `DAPS_REMOTE_HOST`, `DAPS_REMOTE_USER`, `DAPS_SSH_KEY`
    - receives args: `--env prod --to <destination-path-in-toolkit>`
- `_scripts/backup-local.toolkit.sh`
    - optional; daps skips `local backup` for projects that don't have this script
    - runs inside the toolkit container, against the local Docker instance — no SSH, so no host/user/key
    - receives env vars: `DAPS_PROJECT`
    - receives args: `--env local --to <destination-path-in-toolkit>`
    - output filenames use `local` as the environment token, e.g. `<project>_local_<timestamp>.sql`, so the same restore-point discovery works for both sources
    - not provided by the `astro` or `static` templates: those are assumed to be tracked in git, which already gives them history


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
    - `/srv/daps/`
        - `caddy/Caddyfile` - copy the Caddyfile.prod here
        - `docker/` - copy daps shared and prod compose files
    - `/srv/projects/`
        - `projectname/_docker/` - copy project daps-linked, shared and prod compose files
        - `projectname/_docker/image-exports` - copy built docker image tars here
