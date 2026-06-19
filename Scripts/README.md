# Scripts quick notes

## Shared backend upload contract

The hardened backend now protects upload/publish endpoints with HTTP Basic Auth.

Shared helper module:

- `Scripts/backend_client.py` centralizes auth resolution, upload URL building, `/api/health` + `/api/ready` preflight, streamed PUT upload, and authenticated multipart POST helpers.
- If the remote server is older and returns `404` for `/api/health` or `/api/ready`, the scripts now treat that as "legacy server, continue upload" instead of aborting.

- Legacy upload target: `PUT /upload/<folder>/<filename>`
- Publish API target: `POST /api/packages/publish`
- Recommended credential source:
  - `COLORVISION_UPLOAD_USERNAME`
  - `COLORVISION_UPLOAD_PASSWORD`
- Built-in fallback for your current setup: `xincheng / xincheng`
- Optional overrides:
  - `COLORVISION_UPLOAD_URL`
  - `COLORVISION_UPLOAD_FOLDER`

## Script index

### `build.py`

Builds the main installer and publishes it through the authenticated backend upload endpoint.
Before a remote upload, it now probes:

- `GET /api/health`
- `GET /api/ready`

Use `Scripts\release.bat` for normal releases. It calls this script and then builds/uploads the update package.

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

### `build_plugin.py`

Deprecated compatibility wrapper.
The old packaging logic has been removed; this script now forwards to `package_cvxp.py` and prints a migration hint.

```powershell
py Scripts\build_plugin.py -t Projects -p ProjectARVR --no-upload
```

### `generate_shared_files.py`

Scans a host ColorVision output directory and writes `shared_files.json`.
The generated manifest only keeps `version`, `generated_at`, and `shared_files`, and it skips the `Plugins` and `Log` folders automatically.
This is usually a one-time refresh step, not something you need to run before every plugin package.

```powershell
py Scripts\generate_shared_files.py
```

### `package_cvxp.py`

Single-file packager that reads `shared_files.json`, strips matching files plus `.pdb`, creates the `.cvxp`, and can upload it through the legacy authenticated PUT endpoint.
If `--shared-files` is omitted, it looks for `shared_files.json` next to `package_cvxp.py` first.
If only `--src-dir` is provided and the path looks like `.../PluginName/bin/x64/Release/net10.0-windows`, it also infers the plugin root automatically.

```powershell
py Scripts\package_cvxp.py --project-file Plugins\Pattern\Pattern.csproj --build --no-upload

py Scripts\package_cvxp.py --src-dir C:\path\to\MyPlugin\bin\x64\Release\net10.0-windows --no-upload
```

### `package_plugin.bat`

Repo-local helper batch file that calls `package_cvxp.py --build` for a plugin project, so per-plugin `.bat` files stay minimal.

```powershell
Scripts\package_plugin.bat Pattern --no-upload
```

### `package_project.bat`

Repo-local helper batch file for `Projects/*/*.csproj`, matching the plugin helper but targeting the `Projects` directory.

```powershell
Scripts\package_project.bat ProjectARVR --no-upload
```

### `package_cvxp_demo.bat`

Minimal external demo batch file. Edit `SRC_DIR`, keep `shared_files.json` next to the script, then run it.

```powershell
Scripts\package_cvxp_demo.bat
```

### `publish_plugin.py`

Publishes a plugin package through `/api/packages/publish`, requires Basic Auth, and now also runs backend preflight before publishing.

```powershell
py Scripts\publish_plugin.py -p Spectrum -v 1.0.0.1 -f .\Spectrum-1.0.0.1.cvxp --username your-user --password your-password
```

## Typical authenticated environment setup

```powershell
$env:COLORVISION_UPLOAD_URL = "http://xc213618.ddns.me:9998"
$env:COLORVISION_UPLOAD_USERNAME = "xincheng"
$env:COLORVISION_UPLOAD_PASSWORD = "xincheng"
```

If you do not set these two environment variables, the scripts now also fall back to `xincheng / xincheng` by default.

## Validation

Focused script tests now live in:

- `Scripts/test_backend_client.py`
- `Scripts/test_build.py`
- `Scripts/test_file_manager.py`
- `Scripts/test_build_update.py`
- `Scripts/test_publish_plugin.py`

