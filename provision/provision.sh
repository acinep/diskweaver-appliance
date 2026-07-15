#!/bin/bash
# Idempotent post-install provisioning for a DiskWeaver NAS appliance.
# Run as root (autoinstall's late-commands do this inside the target
# chroot; run manually the same way if provisioning an already-installed
# box). Safe to re-run -- every step checks current state first.
set -euo pipefail

DISKWEAVER_REPO="acinep/DiskWeaver"
GARAGE_VERSION="REPLACE_ME"               # e.g. v1.0.1 -- see https://garagehq.deuxfleurs.fr/download/

log() { echo "[provision] $*"; }

# --- 45Drives apt repo (cockpit-file-sharing) ------------------------------
if ! grep -rq "repo.45drives.com" /etc/apt/sources.list.d/ 2>/dev/null; then
    log "adding 45Drives apt repo"
    curl -sSL https://repo.45drives.com/setup | bash
fi

apt-get update

# --- Cockpit + file/S3 sharing UI ------------------------------------------
log "installing cockpit + cockpit-file-sharing"
apt-get install -y cockpit cockpit-file-sharing mdadm lvm2 parted

systemctl enable --now cockpit.socket

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

mkdir -p /etc/garage /var/lib/garage/meta /var/lib/garage/data
if [ ! -f /etc/garage.toml ]; then
    log "writing garage config from template (edit rpc_secret before first boot!)"
    cp "$(dirname "$0")/garage.toml.tmpl" /etc/garage.toml
fi

if [ ! -f /etc/systemd/system/garage.service ]; then
    log "installing garage systemd unit"
    cat > /etc/systemd/system/garage.service <<'EOF'
[Unit]
Description=Garage S3-compatible object store
After=network.target

[Service]
ExecStart=/usr/local/bin/garage server -c /etc/garage.toml
Restart=on-failure
User=root

[Install]
WantedBy=multi-user.target
EOF
    systemctl daemon-reload
fi

systemctl enable garage.service
# Not starting garage yet -- /etc/garage.toml still has a placeholder
# rpc_secret (see the template's own comment) that needs a real value
# before this is safe to bring up.

log "done. Remaining manual steps:"
log "  - generate a real rpc_secret and edit /etc/garage.toml, then: systemctl start garage"
log "  - sudo usermod -aG diskweaver <your-user>, then log out/in to Cockpit"
