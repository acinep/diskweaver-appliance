# diskweaver-appliance

Reproducible provisioning for a personal NAS: vanilla Ubuntu Server LTS +
Cockpit, with [DiskWeaver](https://github.com/acinep/DiskWeaver) for mixed-size RAID
pooling, `cockpit-zfs` for optional ZFS datasets on top, 45Drives'
`cockpit-file-sharing` for Samba/NFS/S3 management, and
[Garage](https://garagehq.deuxfleurs.fr/) as the S3-compatible object
store.

This is deliberately *not* a respun ISO or a fork of anyone else's
appliance. Install stock Ubuntu Server normally (the installer already
handles your user/password/SSH), then run one script to layer everything
else on top. If this repo disappeared, the result is still just a normal
Ubuntu box with standard packages on it — nothing here is a special
format or a fork to maintain.

```bash
curl -sSL https://raw.githubusercontent.com/acinep/diskweaver-appliance/master/provision/provision.sh | sudo bash
```

## What it lays down

| Component | Role |
|---|---|
| Ubuntu Server 26.04 LTS | Base OS |
| Cockpit | Web admin UI |
| [DiskWeaver](../DiskWeaver) | Mixed-size RAID pooling (mdadm/LVM), daemon + Cockpit plugin |
| [`cockpit-zfs`](https://github.com/45Drives/cockpit-zfs) | Optional: ZFS datasets/snapshots on top of a DiskWeaver-managed volume (or, later, a native zpool -- see the repo's own notes on that tradeoff) |
| [`cockpit-file-sharing`](https://github.com/45Drives/cockpit-file-sharing) | Samba/NFS/iSCSI/S3 management UI (detects Garage as an S3 backend) |
| [Garage](https://garagehq.deuxfleurs.fr/) | S3-compatible object storage |

## Layout

- `provision/provision.sh` — idempotent post-install script: installs
  Cockpit + `mdadm`/`lvm2`, the latest DiskWeaver `.deb` release,
  `cockpit-file-sharing` (downloaded directly, bypassing 45Drives' apt
  repo -- see the script's own comment), `zfsutils-linux` + `cockpit-zfs`
  (downloaded from this repo's own Releases, built by CI -- see below),
  and Garage. Self-contained — fetches `garage.toml.tmpl` by URL, so it
  works piped straight from `curl` with no local checkout needed.
- `provision/garage.toml.tmpl` — single-node Garage config template.
  `provision.sh` substitutes a freshly generated `rpc_secret`
  (`openssl rand -hex 32`) into it on first run; the tracked file itself
  never holds a real secret.
- `.github/workflows/build-cockpit-zfs.yml` — `45Drives/cockpit-zfs` ships
  no prebuilt package anywhere reachable outside their (codename-gated)
  apt repo. Pushing a `cockpit-zfs-vX.Y.Z` tag here builds that exact
  upstream tag in CI and publishes the result as a Release asset in this
  repo, which `provision.sh` then just downloads.

## Usage

1. Install stock [Ubuntu Server 26.04 LTS](https://ubuntu.com/download/server)
   normally — set your own user, password, and SSH keys through the
   regular installer, same as any other box.
2. Run the provisioning script:
   ```bash
   curl -sSL https://raw.githubusercontent.com/acinep/diskweaver-appliance/master/provision/provision.sh | sudo bash
   ```
3. On completion, Cockpit, DiskWeaver, `cockpit-file-sharing`, and Garage
   are installed and running.
4. Log into Cockpit, add your user to the `diskweaver` group (see
   [DiskWeaver's deployment doc](../DiskWeaver/docs/deployment.md)), and
   pool your disks from there.

## Status

Early scaffold — untested end-to-end. Treat `provision/provision.sh` as a
starting point to iterate against a real VM/box, not a finished installer.
