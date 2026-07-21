#!/usr/bin/env bash
# Builds an installable appliance-manager_<version>_amd64.deb: the daemon (self-contained AOT
# binary), its systemd unit, and the Cockpit plugin -- mirrors DiskWeaver's own
# scripts/build-deb.sh exactly (same staging-dir/DEBIAN-control approach), just for this sibling
# project instead.
#
# Requires the Cockpit plugin already built (`npm run build` in src/ApplianceManager.Cockpit) and
# dpkg-deb available (any Debian/Ubuntu system, or `apt-get install dpkg-dev` elsewhere).
#
# Usage:
#   appliance-manager/build-deb.sh [version]
#
# Output: appliance-manager/dist/appliance-manager_<version>_amd64.deb
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SRC_DIR="$REPO_ROOT/src"
VERSION="${1:-0.1.0}"
OUT_DIR="$REPO_ROOT/dist"
STAGE_DIR="$(mktemp -d)"
trap 'rm -rf "$STAGE_DIR"' EXIT

COCKPIT_DIST="$SRC_DIR/ApplianceManager.Cockpit/dist"
if [ ! -d "$COCKPIT_DIST" ] || [ -z "$(ls -A "$COCKPIT_DIST" 2>/dev/null)" ]; then
    echo "build-deb: $COCKPIT_DIST is missing/empty -- run 'npm run build' in src/ApplianceManager.Cockpit first" >&2
    exit 1
fi

echo "==> Publishing ApplianceManager.Daemon (AOT, linux-x64)"
DAEMON_DIR="$STAGE_DIR/usr/lib/appliance-manager"
mkdir -p "$DAEMON_DIR"
dotnet publish "$SRC_DIR/ApplianceManager.Daemon" \
    -c Release -r linux-x64 --self-contained \
    -o "$DAEMON_DIR"

echo "==> Staging systemd unit"
mkdir -p "$STAGE_DIR/lib/systemd/system"
cp "$REPO_ROOT/packaging/appliance-managerd.service" "$STAGE_DIR/lib/systemd/system/appliance-managerd.service"

echo "==> Staging Cockpit plugin"
COCKPIT_PKG_DIR="$STAGE_DIR/usr/share/cockpit/appliance-manager"
mkdir -p "$COCKPIT_PKG_DIR/dist"
cp "$SRC_DIR/ApplianceManager.Cockpit/index.html" "$COCKPIT_PKG_DIR/index.html"
cp "$SRC_DIR/ApplianceManager.Cockpit/manifest.json" "$COCKPIT_PKG_DIR/manifest.json"
cp "$COCKPIT_DIST"/* "$COCKPIT_PKG_DIR/dist/"

echo "==> Writing DEBIAN control files"
mkdir -p "$STAGE_DIR/DEBIAN"
sed "s/@VERSION@/$VERSION/" "$REPO_ROOT/packaging/deb/control" > "$STAGE_DIR/DEBIAN/control"
install -m 0755 "$REPO_ROOT/packaging/deb/postinst" "$STAGE_DIR/DEBIAN/postinst"
install -m 0755 "$REPO_ROOT/packaging/deb/prerm" "$STAGE_DIR/DEBIAN/prerm"

echo "==> Building package"
mkdir -p "$OUT_DIR"
DEB_PATH="$OUT_DIR/appliance-manager_${VERSION}_amd64.deb"
dpkg-deb --build --root-owner-group "$STAGE_DIR" "$DEB_PATH"

echo "==> Built $DEB_PATH"
