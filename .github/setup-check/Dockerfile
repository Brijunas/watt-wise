# The throwaway machine for .github/scripts/check-setup.sh: a stock distribution image with only what
# docs/setup.md lists as prerequisites (git, the Docker CLI with the Compose plugin and no daemon, and
# mise, activated in the shell), and a normal user. The passwordless sudo is for this test image only.
# BASE and INSTALL come from the CI matrix entry: another distribution is another base image and one
# INSTALL line, the package manager's command for git, the Docker CLI with the Compose plugin, curl,
# sudo, tar and libatomic (node needs libatomic for `mise install`, before setup runs).
# ICU is left out on Fedora on purpose, so the Fedora profile's dnf step runs in CI.
# Built with no context:
#   docker build -t wattwise-setup-check - < .github/setup-check/Dockerfile
ARG BASE=fedora:latest
FROM ${BASE}

ARG INSTALL="dnf -y install git docker-cli docker-compose curl sudo tar gzip xz libatomic && dnf clean all"
# The 1Password CLI is a manual prerequisite on every OS: INSTALL_OP is the distribution's official way
# (https://developer.1password.com/docs/cli/get-started/), Fedora's being 1Password's RPM repository.
ARG INSTALL_OP="rpm --import https://downloads.1password.com/linux/keys/1password.asc && printf '[1password]\nname=1Password Stable Channel\nbaseurl=https://downloads.1password.com/linux/rpm/stable/\$basearch\nenabled=1\ngpgcheck=1\nrepo_gpgcheck=1\ngpgkey=https://downloads.1password.com/linux/keys/1password.asc\n' > /etc/yum.repos.d/1password.repo && dnf -y install 1password-cli && dnf clean all"
RUN sh -c "$INSTALL" \
    && sh -c "$INSTALL_OP" \
    && useradd --create-home --uid 1000 dev \
    && echo 'dev ALL=(ALL) NOPASSWD: ALL' > /etc/sudoers.d/dev \
    && chmod 440 /etc/sudoers.d/dev

USER dev
# mise from its official installer, activated for login and interactive shells like on a developer's machine.
RUN curl -fsSL https://mise.run | sh \
    && printf '\neval "$(~/.local/bin/mise activate bash)"\n' >> ~/.bashrc
USER root
