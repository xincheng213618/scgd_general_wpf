# Scripts quick notes

## `build.py`

`build.py` now supports the hardened backend upload contract:

- Upload target: `PUT /upload/<folder>/<filename>`
- Authentication: HTTP Basic Auth
- Recommended credential source:
  - `COLORVISION_UPLOAD_USERNAME`
  - `COLORVISION_UPLOAD_PASSWORD`
- Optional overrides:
  - `COLORVISION_UPLOAD_URL`
  - `COLORVISION_UPLOAD_FOLDER`
  - `COLORVISION_REMOTE_UPLOAD=0` to disable remote upload

### Typical usage

```powershell
py Scripts\build.py --skip-build
```

### Typical authenticated remote publish

```powershell
$env:COLORVISION_UPLOAD_URL = "http://xc213618.ddns.me:9998"
$env:COLORVISION_UPLOAD_USERNAME = "your-user"
$env:COLORVISION_UPLOAD_PASSWORD = "your-password"
py Scripts\build.py
```

### Local fallback without backend upload

```powershell
py Scripts\build.py --skip-remote-upload
```

## Validation

A focused test file exists at `Scripts/test_build.py`.

