#!/usr/bin/env bash

# note: this file may be modified by dapsman
# Set variables that will be consumed by the script that launches a new instance/VM
# Here's how to get the values. Note that you can use the uniqueids or the names for image, flavor and network:
# OS_IMAGE_ID
# run: openstack image list
# OS_FLAVOR_ID
# run: openstack flavor list
# OS_NETWORK_ID
# run: openstack network list
# OS_KEY_NAME
# see readme under "OpenStack resources"
# OS_SERVER_NAME and OS_SECURITY_GROUP_NAME are names you pick to be created, but they also have default values as seen here

export OS_IMAGE_ID="<get value from openstack host>"
export OS_FLAVOR_ID="<get value from openstack host>"
export OS_NETWORK_ID="<get value from openstack host>"
export OS_KEY_NAME=daps-key-example
export OS_SERVER_NAME=daps-prod
export OS_SECURITY_GROUP_NAME=daps-web-sg
# export OS_SERVER_USER="root" # default user is ubuntu
# export OS_SERVER_ID=""
# export OS_SERVER_IP=""
