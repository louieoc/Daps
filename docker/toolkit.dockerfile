FROM ubuntu:22.04

ENV DEBIAN_FRONTEND=noninteractive

# Install OpenStack client and common tooling for admin tasks.
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        ca-certificates \
        curl \
        gcc \
        gnupg \
        libffi-dev \
        libssl-dev \
        nano \
        netcat-openbsd \
        openssh-client \
        rsync \
        #pass \
        python3-dev \
        python3-pip \
        python3-venv \
    && install -m 0755 -d /etc/apt/keyrings \
    && curl -fsSL https://download.docker.com/linux/ubuntu/gpg | gpg --dearmor -o /etc/apt/keyrings/docker.gpg \
    && echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] https://download.docker.com/linux/ubuntu jammy stable" > /etc/apt/sources.list.d/docker.list \
    && apt-get update \
    && apt-get install -y --no-install-recommends docker-ce-cli \
    && pip3 install --no-cache-dir python-openstackclient \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*

# Add root shell config that loads per-project bashrc files.
COPY docker/toolkit.bashrc /root/.bashrc

# Convenience link so root can quickly reach mounted daps and projects.
RUN ln -sfn /srv/daps /root/daps
RUN ln -sfn /srv/projects /root/projects
