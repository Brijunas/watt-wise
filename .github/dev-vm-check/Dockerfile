# The throwaway machine for .github/scripts/check-dev-vm.sh: a stock Fedora with systemd as PID 1,
# sshd and firewalld (set to the Server install's default zone), cockpit-ws like a Server install,
# a normal user `dev` and no git and no Docker, which deploy/provision/dev-vm.sh installs itself.
# The passwordless sudo is for this test image only.
# `dev`'s subordinate id range fits inside the 65536 ids a rootless Docker host gives this
# container, so the nested rootless Docker starts even there. newuidmap and newgidmap are made
# setuid because their file capabilities don't survive an image pull on rootless Docker. Both
# belong to this machine, not to the script. The harness adds the user's authorized_keys at run
# time. Built with no context:
#   docker build -t wattwise-dev-vm-check - < .github/dev-vm-check/Dockerfile
FROM fedora:44

RUN dnf -y install systemd sudo openssh-server openssh-clients firewalld cockpit-ws iproute \
        procps-ng nmap-ncat \
    && dnf clean all \
    && useradd --create-home --uid 1000 dev \
    && printf 'dev:10000:55000\n' > /etc/subuid \
    && printf 'dev:10000:55000\n' > /etc/subgid \
    && echo 'dev ALL=(ALL) NOPASSWD: ALL' > /etc/sudoers.d/dev \
    && chmod 440 /etc/sudoers.d/dev \
    && sed -i 's/^DefaultZone=.*/DefaultZone=FedoraServer/' /etc/firewalld/firewalld.conf \
    && chmod u+s /usr/bin/newuidmap /usr/bin/newgidmap \
    && systemctl enable sshd firewalld cockpit.socket \
    && systemctl mask systemd-remount-fs.service systemd-resolved.service getty.target console-getty.service

STOPSIGNAL SIGRTMIN+3
CMD ["/sbin/init"]
