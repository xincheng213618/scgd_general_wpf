# Build and Release Scripts

The ColorVision project includes a set of Python scripts for building applications, packaging plugins, publishing updates, and managing backend uploads.

## Script Overview

| Script | Function |
|------|------|
| `build.py` | Build main application installer and publish |
| `build_update.py` | Build incremental update package |
| `build_plugin.py` | Compatibility entry, internally forwards to `package_cvxp.py` |
| `generate_shared_files.py` | Scan host output directory to generate `shared_files.json` |
| `package_cvxp.py` | Strip and package/upload plugins based on `shared_files.json` |
| `package_plugin.bat` | One-click build for in-repo plugins and call `package_cvxp.py` |
| `package_project.bat` | One-click build for in-repo projects and call `package_cvxp.py` |
| `package_cvxp_demo.bat` | Minimal packaging example for external plugin authors |
| `build_spectrum.py` | Build Spectrum plugin |
| `publish_plugin.py` | Publish plugin to marketplace backend |
| `backend_client.py` | Backend upload shared module |
| `file_manager.py` | File management utility |

## Environment Configuration

### Authentication Configuration

Scripts use the following environment variables for backend authentication:

```powershell
# PowerShell
$env:COLORVISION_UPLOAD_URL = "http://xc213618.ddns.me:9998"
$env:COLORVISION_UPLOAD_USERNAME = "xincheng"
$env:COLORVISION_UPLOAD_PASSWORD = "xincheng"
```

```bash
# Bash (Git Bash/WSL)
export COLORVISION_UPLOAD_URL="http://xc213618.ddns.me:9998"
export COLORVISION_UPLOAD_USERNAME="xincheng"
export COLORVISION_UPLOAD_PASSWORD="xincheng"
```

::: tip
If environment variables are not set, the script will use default credentials `xincheng/xincheng`.
:::

### Optional Configuration

| Environment Variable | Description | Default |
|----------|------|--------|
| `COLORVISION_UPLOAD_URL` | Backend upload URL | `http://xc213618.ddns.me:9998` |
| `COLORVISION_UPLOAD_FOLDER` | Upload folder | `ColorVision` |
| `COLORVISION_UPLOAD_USERNAME` | Upload username | `xincheng` |
| `COLORVISION_UPLOAD_PASSWORD` | Upload password | `xincheng` |
| `COLORVISION_REMOTE_UPLOAD` | Enable remote upload | `1` (enabled) |

## build.py - Main Application Build

Build the main application installer and upload to backend.

### Usage

```powershell
# Full build (compile + package + upload)
py Scripts\build.py

# Skip build, only upload latest installer
py Scripts\build.py --skip-build

# Skip remote upload
py Scripts\build.py --skip-remote-upload
```

### Feature Description

1. Compile solution using MSBuild
2. Build installer using Advanced Installer
3. Perform backend preflight check (`/api/health` + `/api/ready`)
4. Upload installer to backend

### Prerequisites

- Visual Studio 2022+ (MSBuild)
- Advanced Installer
- Python dependencies: `requests`, `tqdm`

## build_update.py - Incremental Update Build

Create incremental update packages (only containing changed files).

### Usage

```powershell
py Scripts\build_update.py
```

### How It Works

1. Read `ColorVision.exe` to get current version
2. Find historical version as baseline
3. Compare file differences to generate incremental package
4. Upload incremental package to `Update/` directory

### Output Files

- `{History}/ColorVision-[{version}].zip` - Full package
- `{History}/update/ColorVision-Update-[{version}].cvx` - Incremental package

## build_plugin.py - Compatibility Entry

The old packaging implementation has been removed.

The current `build_plugin.py` is only kept as a compatibility entry that forwards common in-repo calls to `package_cvxp.py` and outputs migration prompts. New scripts should not use it as the main entry point.

### Usage

```powershell
py Scripts\build_plugin.py -t Projects -p ProjectARVR --no-upload
```

### Recommended Alternatives

- In-repo plugins: `Scripts\package_plugin.bat Pattern --no-upload`
- In-repo projects: `Scripts\package_project.bat ProjectARVR --no-upload`
- External repos: `py Scripts\package_cvxp.py --src-dir C:\src\MyPlugin\bin\x64\Release\net10.0-windows --no-upload`

## generate_shared_files.py - Shared File Table Generation

Scan the host application output directory to generate `shared_files.json`.

### Usage

```powershell
py Scripts\generate_shared_files.py

py Scripts\generate_shared_files.py `
    --root-dir C:\Users\17917\Desktop\scgd_general_wpf\ColorVision\bin\x64\Release\net10.0-windows `
    --output C:\temp\shared_files.json
```

### Output Content

- `generated_at`: Generation time
- `shared_files`: All relative file paths under the host directory

### Filtering Rules

- Automatically ignores `Plugins` directory
- Automatically ignores `Log` directory
- Usually only needs to be regenerated once after host shared files change

## package_cvxp.py - Single File Packaging and Upload

A single-file script that reads `shared_files.json`, strips shared files and `.pdb` files, generates `.cvxp`, and can directly upload.

### Usage

```powershell
# Local packaging only
py Scripts\package_cvxp.py --project-file Plugins\Pattern\Pattern.csproj --build --no-upload

# Specify build output directory
py Scripts\package_cvxp.py `
    --src-dir Plugins\Pattern\bin\x64\Release\net10.0-windows `
    --plugin-root Plugins\Pattern

# Only pass build output directory, auto-infer plugin root
py Scripts\package_cvxp.py `
    --src-dir C:\src\MyPlugin\bin\x64\Release\net10.0-windows `
    --no-upload
```

### Parameters

| Parameter | Description | Default |
|------|------|--------|
| `--src-dir` | Plugin build output directory | Empty |
| `--project-file` | Plugin `.csproj` path | Empty |
| `--plugin-root` | Plugin root directory, for supplementing extra files like `README.md` | Auto-inferred |
| `--plugin-name` | Plugin name | Auto-inferred |
| `--shared-files` | `shared_files.json` path; if not provided, preferentially reads file in same directory as script | Auto-find |
| `--output-dir` | `.cvxp` output directory | `Scripts/` |
| `--build` | Execute `dotnet build` before packaging | Off |
| `--dotnet` | `dotnet` command used by `--build` | `dotnet` |
| `--no-upload` | Package only, no upload | Off |
| `--keep-package` | Keep local package after upload | Off |

### Packaging Logic

1. Read `shared_files.json`
2. Traverse plugin output directory
3. Filter all `.pdb` files
4. Filter all shared files present in `shared_files.json`
5. Write `stripped_files.json`
6. Package as `.cvxp`
7. If `--no-upload` is not specified, upload package and `LATEST_RELEASE`

### Direct Output Directory Passthrough

When `--src-dir` points to a directory like `PluginName/bin/x64/Release/net10.0-windows` or `PluginName/bin/Release/net10.0-windows`, the script automatically identifies the `PluginName` directory as `plugin_root`, so even without passing `--plugin-root`, it can still include `README.md`, `CHANGELOG.md`, `manifest.json`, and `PackageIcon.png` from the project root directory.

## package_plugin.bat - In-Repo Plugin Shortcut Entry

This batch file is only for in-repo plugin projects. It automatically locates `.venv` and calls `package_cvxp.py --build`, so `.bat` files under each plugin directory only need a single forwarding line.

### Usage

```powershell
Scripts\package_plugin.bat Pattern --no-upload
```

## package_project.bat - In-Repo Project Shortcut Entry

This batch file is similar to `package_plugin.bat`, but the target directory changes to `Projects/*/*.csproj`. Suitable for client projects or project-based plugins.

### Usage

```powershell
Scripts\package_project.bat ProjectARVR --no-upload
```

## package_cvxp_demo.bat - External Delivery Example

This batch file is for external repository usage scenarios. Place `package_cvxp.py`, `shared_files.json`, and this demo in the same directory, modify `SRC_DIR` inside, and you can directly package.

### Usage

```powershell
Scripts\package_cvxp_demo.bat
```

## build_spectrum.py - Spectrum Plugin Build

A build script specifically optimized for the Spectrum plugin.

### Usage

```powershell
# Build and upload
py Scripts\build_spectrum.py --upload

# Build only, no upload
py Scripts\build_spectrum.py
```

### Features

- Supports both `.zip` and `.cvxp` output formats
- `.cvxp` packages copied to mapped plugin server path
- `.zip` packages use authenticated upload

## publish_plugin.py - Plugin Publishing

Publish plugin packages to the plugin marketplace via API.

### Usage

```powershell
# Basic publish
py Scripts\publish_plugin.py -p Spectrum -v 1.0.0.1 -f Spectrum-1.0.0.1.cvxp

# Full parameters
py Scripts\publish_plugin.py `
  -p Spectrum `
  -v 1.0.0.1 `
  -f Spectrum-1.0.0.1.cvxp `
  -n "Spectrum Plugin" `
  -d "Spectral Analysis Plugin" `
  -a "Author Name" `
  -c "Analysis" `
  --changelog CHANGELOG.md `
  --icon PackageIcon.png

# Specify backend URL
py Scripts\publish_plugin.py -p Spectrum -v 1.0.0.1 -f Spectrum-1.0.0.1.cvxp --api-url http://localhost:9999
```

### Parameters

| Parameter | Description | Required |
|------|------|------|
| `-p, --plugin-id` | Plugin unique ID | Yes |
| `-v, --version` | Version number (e.g., 1.0.0.1) | Yes |
| `-f, --file` | Package file path | Yes |
| `-n, --name` | Display name | No |
| `-d, --description` | Description | No |
| `-a, --author` | Author | No |
| `-c, --category` | Category | No |
| `-r, --requires` | Minimum engine version | No |
| `--changelog` | Changelog file or text | No |
| `--icon` | Icon file path | No |
| `--api-url` | Backend URL | No |
| `--username` | Username | No |
| `--password` | Password | No |

### Authentication

The publish interface requires Basic Auth authentication:

```powershell
# Method 1: Environment variables
$env:COLORVISION_UPLOAD_USERNAME = "your-user"
$env:COLORVISION_UPLOAD_PASSWORD = "your-password"

# Method 2: Command-line parameters
py Scripts\publish_plugin.py ... --username your-user --password your-password
```

## backend_client.py - Backend Client

Shared backend upload module providing authentication and upload functionality for other scripts.

### Key Features

- Authentication credential resolution (environment variables -> default values)
- Upload URL construction
- Backend preflight (health check + readiness check)
- Streaming PUT upload
- Authenticated multipart POST

### Usage Example

```python
from backend_client import (
    RemoteUploadSettings,
    preflight_remote_upload,
    upload_file,
    resolve_upload_credentials,
)

# Resolve credentials
username, password = resolve_upload_credentials()

# Configure upload settings
settings = RemoteUploadSettings(
    base_url="http://localhost:9998",
    folder_name="Plugins/MyPlugin",
    username=username,
    password=password,
)

# Preflight
if preflight_remote_upload(settings):
    # Upload file
    upload_file(settings, "path/to/file.cvxp")
```

### Preflight Logic

Two-step check before upload:

1. **Health Check** (`GET /api/health`) - Confirm backend service is available
2. **Readiness Check** (`GET /api/ready`) - Confirm backend is ready to receive uploads

If backend returns 404 (legacy backend), treat as compatibility mode and continue upload.

## file_manager.py - File Management

File management utility class.

### Features

- File upload management
- Path handling
- Progress display

### Usage Example

```python
from file_manager import FileManager

fm = FileManager()

# Upload file
fm.upload_file("path/to/file.zip", "ColorVision/Update")
```

## Script Testing

Each script has a corresponding test file:

| Test File | Description |
|----------|------|
| `test_backend_client.py` | Backend client test |
| `test_build.py` | Build script test |
| `test_file_manager.py` | File management test |
| `test_build_update.py` | Update build test |
| `test_publish_plugin.py` | Plugin publish test |

### Running Tests

```powershell
# Run single test
python Scripts\test_backend_client.py

# Using pytest
pytest Scripts\test_*.py -v
```

## Troubleshooting

### Upload Failure (401 Unauthorized)

- Check whether environment variables or default credentials are correct
- Confirm `upload_auth` configuration in backend `config.json`

### Upload Failure (Connection Error)

- Check whether backend service is running
- Confirm network connectivity
- Verify `COLORVISION_UPLOAD_URL` configuration

### Build Failure

- Confirm MSBuild path is correct
- Check whether Advanced Installer is installed
- Verify that the solution compiles normally

### Version Number Read Failure

- Confirm target DLL/EXE exists
- Check whether file version info is correctly embedded

## Best Practices

1. **Use Environment Variables** - Avoid hardcoding sensitive information in scripts
2. **Preflight Failure Handling** - Scripts provide clear error messages when backend is unavailable
3. **Version Number Management** - Ensure DLL/EXE version info matches release version
4. **Test First** - Use test scripts to verify functionality before official release