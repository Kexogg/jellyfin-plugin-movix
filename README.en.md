# Movix connector for Jellyfin

[English](README.en.md) | [Русский](README.md)

[![CI](https://github.com/kexogg/jellyfin-plugin-movix/actions/workflows/ci.yml/badge.svg)](https://github.com/kexogg/jellyfin-plugin-movix/actions/workflows/ci.yml)

Jellyfin plugin that connects a Movix subscription to Jellyfin Live TV. It registers a native M3U tuner and XMLTV
guide in Jellyfin, and resolves each channel URL immediately before playback.

This is an unofficial interoperability project. It is not affiliated with Dom.Ru, Movix, or Jellyfin. Use it only with
your own active subscription and in accordance with the service terms. It does not bypass DRM; channels without a
supported HLS resource are unavailable.

## Requirements

- Jellyfin Server 12.0.x
- A valid Dom.Ru Movix subscription

## Install from the plugin repository

1. Open **Dashboard - Plugins - Repositories** in Jellyfin.
2. Add a repository with this URL:

   ```text
   https://raw.githubusercontent.com/kexogg/jellyfin-plugin-movix/master/manifest.json
   ```

3. Open **Catalog**, select **Movix connector for Jellyfin**, and install it.
4. Restart Jellyfin.
5. Open **Dashboard - Plugins - My Plugins - Movix connector for Jellyfin**.
6. Authenticate, then select **Connect to Jellyfin Live TV**.
7. Test a channel in the Jellyfin web client. Additionally, test DLNA playback.

Updates published to the same repository appear in Jellyfin's plugin catalog automatically.

## Data and security

The plugin stores only the resulting device/subscriber token and a random device ID in its private configuration
directory. Passwords, phone numbers, and SMS codes are not persisted. Disconnecting removes the tuner and listings
provider created by the plugin, attempts to unbind its Movix device, and deletes the saved session.

Internal playlist, guide, and stream endpoints accept only loopback requests. Administrator diagnostics are available at
`/Movix/Admin/Diagnostics`.

## Publishing a release

[build.yaml](build.yaml) is the source of metadata for plugin metadata and versioning. To publish:

1. Update `version` and `changelog` in `build.yaml` and commit the change.
2. Create and publish a GitHub Release tagged `v<version>`, for example `v0.1.0.0`.
3. Wait for the **Release plugin** workflow to finish.

