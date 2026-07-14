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

1. run deploy: `dapsman prod deploy --project <project name>` (or omit the project name to deploy everything)

For some projects, that's it. For the Wordpress template, if you customized your local instance, run `dapsman prod sync-from-local --project dapster-wp`


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
