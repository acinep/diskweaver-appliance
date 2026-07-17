#!/bin/bash
# Post-install provisioning for a DiskWeaver NAS appliance. Run as root on
# a normal, manually-installed Ubuntu Server box (the stock installer
# already handled your user/password/SSH -- nothing here touches that):
#
#   curl -sSL https://raw.githubusercontent.com/acinep/diskweaver-appliance/master/provision/provision.sh | sudo bash
#
# Idempotent -- every step checks current state first, safe to re-run.
set -euo pipefail

# apt-get -y only auto-answers the "proceed?" confirmation -- it doesn't
# suppress debconf's own interactive prompts (msmtp's postinst pops an
# AppArmor-profile debconf dialog, for one), which otherwise block this
# script indefinitely waiting on a screen nobody's watching.
export DEBIAN_FRONTEND=noninteractive

DISKWEAVER_REPO="acinep/DiskWeaver"
APPLIANCE_REPO="acinep/diskweaver-appliance"
APPLIANCE_RAW="https://raw.githubusercontent.com/${APPLIANCE_REPO}/master/provision"
GARAGE_VERSION="v2.3.0"
CFS_VERSION="4.6.1"
CFS_REPO="45Drives/cockpit-file-sharing"
ZFS_PLUGIN_VERSION="v1.2.33"

log() { echo "[provision] $*"; }

apt-get update

# --- Cockpit ------------------------------------------------------------
log "installing cockpit"
apt-get install -y cockpit mdadm lvm2 parted

systemctl enable --now cockpit.socket

# --- cockpit-file-sharing ------------------------------------------------
# 45Drives' apt repo gates on a hardcoded codename whitelist (jammy/focal/
# bookworm/trixie) that doesn't include this OS -- but the package itself
# is `Architecture: all` with plain, unpinned deps (cockpit-bridge,
# nfs-kernel-server, samba-common-bin, python3-botocore, etc.), all in
# Ubuntu's own repos. So skip their repo and install the .deb directly;
# the "jammy" build tag in the filename is just their versioning scheme,
# not an OS dependency.
if ! dpkg -s cockpit-file-sharing >/dev/null 2>&1; then
    log "installing cockpit-file-sharing ${CFS_VERSION}"
    curl -sSL -o /tmp/cockpit-file-sharing.deb \
        "https://github.com/${CFS_REPO}/releases/download/v${CFS_VERSION}/cockpit-file-sharing_${CFS_VERSION}-1jammy_all.deb"
    apt-get install -y /tmp/cockpit-file-sharing.deb
    rm -f /tmp/cockpit-file-sharing.deb
else
    log "cockpit-file-sharing already installed, skipping"
fi

# --- ZFS + cockpit-zfs ----------------------------------------------------
# cockpit-zfs ships no prebuilt package anywhere reachable from here (see
# .github/workflows/build-cockpit-zfs.yml for why), so this pulls a
# tarball we built ourselves in CI from a pinned upstream tag -- no
# Node/Yarn/make touches this box, just zfsutils-linux and the plugin's
# plain runtime deps.
log "installing zfsutils-linux + cockpit-zfs runtime deps"
apt-get install -y zfsutils-linux python3-dateutil sqlite3 jq msmtp
# python3-libzfs is optional -- cockpit-zfs falls back to plain `zpool`/
# `zfs` CLI calls for core functionality (pools, vdevs, scrub/resilver
# status) when it's missing, so a missing package here shouldn't abort
# provisioning, just lose some of the plugin's richer/faster paths.
if ! apt-get install -y python3-libzfs; then
    log "python3-libzfs unavailable, cockpit-zfs will use its CLI fallback"
fi

if [ ! -d /usr/share/cockpit/zfs ]; then
    log "installing cockpit-zfs ${ZFS_PLUGIN_VERSION}"
    curl -sSL -o /tmp/cockpit-zfs.tar.gz \
        "https://github.com/${APPLIANCE_REPO}/releases/download/cockpit-zfs-${ZFS_PLUGIN_VERSION}/cockpit-zfs-${ZFS_PLUGIN_VERSION}.tar.gz"
    tar -C / -xzf /tmp/cockpit-zfs.tar.gz
    rm -f /tmp/cockpit-zfs.tar.gz
else
    log "cockpit-zfs already installed, skipping"
fi

# --- DiskWeaver --------------------------------------------------------------
if ! dpkg -s diskweaver >/dev/null 2>&1; then
    log "installing latest DiskWeaver release"
    DEB_URL=$(curl -sSL "https://api.github.com/repos/${DISKWEAVER_REPO}/releases/latest" \
        | grep -o 'https://[^"]*\.deb' | head -n1)
    if [ -z "$DEB_URL" ]; then
        log "ERROR: no .deb asset found on latest ${DISKWEAVER_REPO} release"
        exit 1
    fi
    curl -sSL -o /tmp/diskweaver.deb "$DEB_URL"
    apt-get install -y /tmp/diskweaver.deb
    rm -f /tmp/diskweaver.deb
else
    log "diskweaver already installed, skipping"
fi

# --- Garage (S3-compatible object store) ------------------------------------
if [ ! -x /usr/local/bin/garage ]; then
    log "installing garage ${GARAGE_VERSION}"
    curl -sSL -o /usr/local/bin/garage \
        "https://garagehq.deuxfleurs.fr/_releases/${GARAGE_VERSION}/x86_64-unknown-linux-musl/garage"
    chmod +x /usr/local/bin/garage
else
    log "garage already installed, skipping"
fi

mkdir -p /var/lib/garage/meta /var/lib/garage/data
groupadd -f garage
if [ ! -f /etc/garage.toml ]; then
    log "writing garage config with a freshly generated rpc_secret"
    curl -sSL "${APPLIANCE_RAW}/garage.toml.tmpl" \
        | sed "s/REPLACE_ME_GENERATE_WITH_openssl_rand_hex_32/$(openssl rand -hex 32)/" \
        > /etc/garage.toml
else
    log "/etc/garage.toml already exists, leaving it alone"
fi
# cockpit-file-sharing's Garage adapter (garageCliAdapter.ts) never passes
# { superuser: "try" } to Cockpit the way its own iSCSI tab does -- it
# always runs `garage status`/`bucket list` as your unprivileged Cockpit
# login user, no matter the Administrative Access toggle. World-readable
# would work around that too, but this file holds rpc_secret, so group-
# readable + group membership instead (same pattern as DiskWeaver's own
# `diskweaver` group -- see docs/deployment.md).
chgrp garage /etc/garage.toml
chmod 640 /etc/garage.toml

if [ ! -f /etc/systemd/system/garage.service ]; then
    log "installing garage systemd unit"
    cat > /etc/systemd/system/garage.service <<'EOF'
[Unit]
Description=Garage S3-compatible object store
After=network.target

[Service]
ExecStart=/usr/local/bin/garage -c /etc/garage.toml server
Restart=on-failure
User=root

[Install]
WantedBy=multi-user.target
EOF
    systemctl daemon-reload
fi

systemctl enable --now garage.service

log "done. Remaining manual steps:"
log "  - sudo usermod -aG diskweaver <your-user>, then log out/in to Cockpit"
log "  - sudo usermod -aG garage <your-user>, then log out/in to Cockpit (needed for the S3 tab)"
