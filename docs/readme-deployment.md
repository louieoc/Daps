---
title: Deployment
sidebar:
  order: 5
---

## Deploying to production

Deploying to prod is accomplished with 2 workflows: provisioning a host, and deploying one or more projects to it.

### Provisioning a host

When you're ready to go live to the public with your projects, you need to set up remote hosting. Daps supports two provider types configured in `daps.yaml`.

#### OpenStack providers (DreamCompute, RamNode, etc.)

OpenStack providers have an API — Daps creates the VM for you.

1. Open an account with an OpenStack hosting provider. I've run Daps against [DreamCompute](https://www.dreamhost.com/cloud/computing/) and [RamNode](https://ramnode.com/products/cloud-vps) so far.
1. Download the OpenRC file from your provider into `daps/hosting/`
1. Add a provider entry in `daps.yaml` (type `openstack`, with `openrc:` pointing to the file)
1. Pick the OpenStack instance size and OS image; create an instance vars file at `hosting/openstack_<name>_instance_vars.sh`
1. Run `dapsman prod provision --provider <name>`

#### Generic VPS providers (OVHCloud bare-metal VPS, Hostinger, etc.)

Generic VPS providers have no API. You subscribe to a plan, receive a hostname and username, and the host already exists when you provision with Daps.

1. Subscribe to a VPS plan and note the hostname and username (possibly this involves a welcome email)
1. Add a provider entry in `daps.yaml` (type `generic-vps`, with `hostname:` and `user:`)
1. Run `dapsman prod provision --provider <name>` — this will prompt once for the host's password to copy an SSH key, then install Docker and set up swap

After either path, you may verify the remote host by SSHing to it from the toolkit. See [Toolkit and SSH to remote host](#toolkit-and-ssh-to-remote-host) below.


### Deploying projects

1. Confirm the project's `_caddy_sites/<name>.prod.caddy` names your real domain, not a template's `example.com` placeholder
1. Point DNS at your host — see [DNS and HTTPS](#dns-and-https) below for whether to do this before or after the next step
1. run deploy: `dapsman prod deploy --project <project name>` (or omit the project name to deploy everything)

For some projects, that's it. For the Wordpress template, if you customized your local instance, run `dapsman prod sync-from-local --project dapster-wp`

### DNS and HTTPS

Daps uses Caddy as a reverse proxy: it receives incoming traffic and routes each domain to the container that serves it. Caddy also requests an HTTPS certificate from [Let's Encrypt](https://letsencrypt.org/) for every domain in its config, as soon as it starts. Let's Encrypt will only issue that certificate if the domain already points at your server — which leaves you a choice about when to update DNS.

Either way, DNS changes are not instant. Every record has a TTL telling other servers how long to cache it, and the old value stays in circulation until that expires. If your domain already has an A record, lower its TTL to 300 seconds a day or two before you change it, so the switch takes minutes rather than hours.

**New site** — create an A record for your domain, pointing at the host's IP address, before deploying. Caddy gets its certificate on startup and HTTPS works as soon as the site comes up. Nothing to revisit later.

**Moving a live site** — deploy to the new host first, then flip DNS, so visitors keep hitting the old server until the new one is ready. The trade-off is that Caddy's first certificate request fails, since DNS still points at the old host, and it then retries on a backoff that grows longer with each failure. Once DNS has propagated, run `dapsman prod caddy restart` to make Caddy retry immediately instead of waiting that backoff out.

Editing your workstation's hosts file does not let you preview the new server over HTTPS beforehand. Let's Encrypt validates from its own servers using public DNS, so a local override cannot produce a certificate, and Caddy will redirect you to HTTPS and fail the handshake. To verify a site before flipping DNS, add a temporary subdomain (e.g. `new.yourdomain.com`) with its own A record and site block, then remove it after the cutover.


## Toolkit and SSH to remote host

After you run the `dapsman local build` workflow, you should have a toolkit container.

The toolkit provides a common Linux environment including any tools needed for managing remote deployments, such as the OpenStack CLI, OpenSSH and rsync. If you create a project that needs special dependencies for deployment you can add them to `daps/docker/toolkit.dockerfile` and run `dapsman local build --build`

You can log into the toolkit by running this:

`docker exec -it daps-toolkit-1 bash -l`

After you've created a remote environment by running `dapsman prod provision`, you can log into the toolkit and then into the remote VM:

`ssh -i .ssh/daps-key-<providername> <root or ubuntu>@<ip address>`

e.g.

`ssh -i .ssh/daps-key-ramnode root@111.222.333.444`

At this point you'll be 3 terminal levels deep: once for your workstation, second for the toolkit then third for the remote VM -- wow
