# Scripts quick notes

Full reader-facing guide: `docs/02-developer-guide/scripts/README.md`.

## Safe entry points

| Task | Command |
| --- | --- |
| Main application release | `Scripts\release.bat` |
| Publish plugin package | `Scripts\package_plugin.bat <PluginName>` |
| Publish project package | `Scripts\package_project.bat <ProjectName>` |
| Publish an existing output directory | `py Scripts\package_cvxp.py --src-dir <output-dir>` |
| Refresh host shared-file manifest | `py Scripts\generate_shared_files.py` |

`build.py` and `build_update.py` are release internals. Do not use them as normal manual release entry points; `build_update.py` executes package generation and upload when run.

## Upload environment

Use environment variables for remote uploads:

```powershell
$env:COLORVISION_UPLOAD_URL = "http://<host>:<port>"
$env:COLORVISION_UPLOAD_FOLDER = "ColorVision"
$env:COLORVISION_UPLOAD_USERNAME = "<user>"
$env:COLORVISION_UPLOAD_PASSWORD = "<password>"
```

Optional proxy flag:

```powershell
$env:COLORVISION_UPLOAD_USE_SYSTEM_PROXY = "1"
```

Do not put real credentials in docs or checked-in command examples.

## Current script map

| Script | Purpose |
| --- | --- |
| `release.bat` | Normal release wrapper |
| `build.py` | Release internal: main installer build/upload |
| `build_update.py` | Release internal: incremental package build/upload |
| `package_cvxp.py` | `.cvxp` package creation, upload, and cleanup |
| `package_plugin.bat` | Repo plugin wrapper around `package_cvxp.py --build` |
| `package_project.bat` | Repo project wrapper around `package_cvxp.py --build` |
| `generate_shared_files.py` | Generate `shared_files.json` from a host output directory |
| `build_spectrum.py` | Spectrum-specific build path |
| `backend_client.py` | Shared upload/auth/preflight helper |
| `file_manager.py` | Legacy upload/path helper |

If a file is not present in `Scripts/`, do not document it as an active entry point.

## Tests

```powershell
$env:PYTHONPATH='Scripts'
python -m unittest `
  Scripts.test.test_backend_client `
  Scripts.test.test_build `
  Scripts.test.test_build_update `
  Scripts.test.test_file_manager
```
