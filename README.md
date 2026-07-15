# diskweaver-appliance

Reproducible provisioning for a personal NAS: vanilla Ubuntu Server LTS +
Cockpit, with [DiskWeaver](https://github.com/acinep/DiskWeaver) for mixed-size RAID
pooling, 45Drives' `cockpit-file-sharing` for Samba/NFS/S3 management, and
[Garage](https://garagehq.deuxfleurs.fr/) as the S3-compatible object
store.

This is deliberately *not* a respun ISO or a fork of anyone else's
appliance. It boots stock Ubuntu Server install media and hands it an
[autoinstall](https://ubuntu.com/server/docs/install/autoinstall) config,
then a plain shell script installs and configures everything else. If this
repo disappeared, the result is still just a normal Ubuntu box with
standard packages on it — nothing here is a special format or a fork to
maintain.

## What it lays down

| Component | Role |
|---|---|
| Ubuntu Server 26.04 LTS | Base OS |
| Cockpit | Web admin UI |
| [DiskWeaver](../DiskWeaver) | Mixed-size RAID pooling (mdadm/LVM), daemon + Cockpit plugin |
| [`cockpit-file-sharing`](https://github.com/45Drives/cockpit-file-sharing) | Samba/NFS/iSCSI/S3 management UI (detects Garage as an S3 backend) |
| [Garage](https://garagehq.deuxfleurs.fr/) | S3-compatible object storage |

## Layout

- `autoinstall/user-data.tmpl` + `meta-data` — subiquity autoinstall
  template for unattended Ubuntu Server installs. Late-commands clone this
  repo into the target and run `provision/provision.sh` in a chroot.
- `provision/provision.sh` — idempotent post-install script: adds the
  45Drives apt repo, installs Cockpit + `cockpit-file-sharing`, installs
  the latest DiskWeaver `.deb` release, installs Garage, enables
  everything.
- `provision/garage.toml.tmpl` — single-node Garage config template.

## Secrets

`*.tmpl` files are committed with placeholders and are never served or run
as-is — they contain no real credentials. Before an install:

```bash
cp autoinstall/user-data.tmpl autoinstall/user-data   # gitignored
```

Fill in, in your local copy only:
- `identity.password` — hash a throwaway console-recovery password with
  `mkpasswd --method=SHA-512 --rounds=4096` (SSH is key-only by default,
  `ssh.allow-pw: false`; this password is just the physical-console
  fallback).
- `ssh.authorized-keys` — your real public key(s).

Same pattern for `provision/garage.toml.tmpl`'s `rpc_secret`
(`openssl rand -hex 32`) once it's rendered to `/etc/garage.toml` on the
target.

## Usage

1. Boot the target machine from stock [Ubuntu Server 26.04 LTS install
   media](https://ubuntu.com/download/server).
2. At the boot menu, point it at your rendered `autoinstall/user-data`
   (via a second USB partition, HTTP, or PXE — see Ubuntu's autoinstall
   docs for the mechanism that fits your setup). Never point it at the
   `.tmpl` file directly.
3. Installer runs unattended; on first boot the box has Cockpit, DiskWeaver,
   `cockpit-file-sharing`, and Garage installed and enabled.
4. Log into Cockpit, add your user to the `diskweaver` group (see
   [DiskWeaver's deployment doc](../DiskWeaver/docs/deployment.md)), and
   pool your disks from there.
5. Generate a real `rpc_secret` for `/etc/garage.toml` and
   `systemctl start garage`.

## Status

Early scaffold — untested end-to-end. Treat `autoinstall/user-data` and
`provision/provision.sh` as a starting point to iterate against real
hardware/VMs, not a finished installer.
