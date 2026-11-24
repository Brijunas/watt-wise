# The throwaway machine for .github/scripts/check-setup.sh: a stock Fedora (vidar's release) with only
# what ./setup lists as its prerequisites, git and Docker (the CLI and the Compose plugin, no daemon),
# and a normal user. The passwordless sudo is for this test image only. Built with no context:
#   docker build -t wattwise-setup-check - < .github/setup-check/Dockerfile
FROM fedora:44

RUN dnf -y install git docker-cli docker-compose \
    && dnf clean all \
    && useradd --create-home --uid 1000 dev \
    && echo 'dev ALL=(ALL) NOPASSWD: ALL' > /etc/sudoers.d/dev \
    && chmod 440 /etc/sudoers.d/dev
