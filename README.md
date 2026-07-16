# diskweaver-appliance

Reproducible provisioning for a personal NAS: vanilla Ubuntu Server LTS +
Cockpit, with [DiskWeaver](https://github.com/acinep/DiskWeaver) for mixed-size RAID
pooling, 45Drives' `cockpit-file-sharing` for Samba/NFS/S3 management, and
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
| [`cockpit-file-sharing`](https://github.com/45Drives/cockpit-file-sharing) | Samba/NFS/iSCSI/S3 management UI (detects Garage as an S3 backend) |
| [Garage](https://garagehq.deuxfleurs.fr/) | S3-compatible object storage |

## Layout

- `provision/provision.sh` — idempotent post-install script: adds the
  45Drives apt repo, installs Cockpit + `cockpit-file-sharing`, installs
  the latest DiskWeaver `.deb` release, installs Garage, enables
  everything. Self-contained — fetches `garage.toml.tmpl` by URL, so it
  works piped straight from `curl` with no local checkout needed.
- `provision/garage.toml.tmpl` — single-node Garage config template.
  `provision.sh` substitutes a freshly generated `rpc_secret`
  (`openssl rand -hex 32`) into it on first run; the tracked file itself
  never holds a real secret.

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
