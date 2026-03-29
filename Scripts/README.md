# Scripts quick notes

## Shared backend upload contract

The hardened backend now protects upload/publish endpoints with HTTP Basic Auth.

- Legacy upload target: `PUT /upload/<folder>/<filename>`
- Publish API target: `POST /api/packages/publish`
- Recommended credential source:
  - `COLORVISION_UPLOAD_USERNAME`
  - `COLORVISION_UPLOAD_PASSWORD`
- Optional overrides:
  - `COLORVISION_UPLOAD_URL`
  - `COLORVISION_UPLOAD_FOLDER`
  - `COLORVISION_REMOTE_UPLOAD=0` to disable remote upload in `build.py`

## Script index

### `build.py`

Builds the main installer and publishes it through the authenticated backend upload endpoint.
Before a remote upload, it now probes:

- `GET /api/health`
- `GET /api/ready`

```powershell
py Scripts\build.py --skip-build
```

### `build_update.py`

Builds incremental update archives and uploads them through the same authenticated legacy upload endpoint.

```powershell
py Scripts\build_update.py
```

### `build_spectrum.py`

Builds the Spectrum plugin zip/cvxp artifacts. When `--upload` is used, the zip upload now uses the shared authenticated upload helper; the `.cvxp` package still copies to the mapped plugin server path.

```powershell
py Scripts\build_spectrum.py --upload
```

### `publish_plugin.py`

Publishes a plugin package through `/api/packages/publish` and now also requires Basic Auth.

```powershell
py Scripts\publish_plugin.py -p Spectrum -v 1.0.0.1 -f .\Spectrum-1.0.0.1.cvxp --username your-user --password your-password
```

## Typical authenticated environment setup

```powershell
$env:COLORVISION_UPLOAD_URL = "http://xc213618.ddns.me:9998"
$env:COLORVISION_UPLOAD_USERNAME = "your-user"
$env:COLORVISION_UPLOAD_PASSWORD = "your-password"
```

## Local fallback without backend upload

```powershell
py Scripts\build.py --skip-remote-upload
```

## Validation

Focused script tests now live in:

- `Scripts/test_build.py`
- `Scripts/test_file_manager.py`
- `Scripts/test_build_update.py`
- `Scripts/test_publish_plugin.py`

