# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

ColorVision is a WPF-based professional vision inspection platform built on .NET 8/10. It uses a modular architecture supporting plugins, a visual flow engine, and device integration for photoelectric technology and color management.

**Key characteristics:**
- Windows-only WPF application (net10.0-windows, x64 primary target)
- Uses strong-name signing (conditional on ColorVision.snk existence)
- MVVM pattern with metadata-driven PropertyGrid system
- Plugin-based extensibility with runtime discovery

## Architecture

### Directory Structure

```
ColorVision/              # Main WPF application entry point
├── ColorVision.csproj
UI/                       # UI layer libraries
├── ColorVision.UI/       # Main UI framework, menus, hotkeys, settings
├── ColorVision.Themes/   # Theme management (dark/light/pink/cyan)
├── ColorVision.ImageEditor/  # Image editor with result overlays
├── ColorVision.Common/   # Shared UI utilities
└── ColorVision.*/        # Other UI modules (Database, Scheduler, etc.)
Engine/                   # Core engine layer
├── ColorVision.Engine/   # Main engine: devices, FlowEngine, Templates, MQTT, DB
├── FlowEngineLib/        # Visual flow engine primitives
├── cvColorVision/        # OpenCV integration (C# wrapper)
└── ColorVision.FileIO/   # File I/O processing
Plugins/                  # Runtime-discovered plugins
├── EventVWR/, Spectrum/, SystemMonitor/, Pattern/, etc.
Projects/                 # Customer-specific project bundles
├── ProjectARVR/, ProjectBlackMura/, ProjectKB/, etc.
Backend/                  # Python Flask backend for plugin marketplace
└── marketplace/          # Plugin distribution server
Scripts/                  # Python build and publish scripts
├── build.py, build_update.py, build_plugin.py, publish_plugin.py
docs/                     # VitePress documentation site
```

### Key Integration Points

1. **Device/Services**: Implement under `Engine/ColorVision.Engine/Services/**`
2. **Flow Engine**: Primitives in `Engine/FlowEngineLib/`; algorithm Templates in `Engine/ColorVision.Engine/Templates/**`
3. **Result Overlays**: Implement `IViewResult` + `IResultHandleBase`; visuals in `UI/ColorVision.ImageEditor/Draw/**`
4. **PropertyGrid**: Use `[Category]`, `[DisplayName]`, `[Description]`, custom `PropertyEditorType`, `PropertyVisibility` attributes
5. **Plugin Entry**: Implement `IPlugin`/`IPluginBase`; post-build copy to `ColorVision/bin/<Config>/net8.0-windows/Plugins/<Name>/`

## Build Commands

### Prerequisites
- Visual Studio 2022+ or MSBuild
- .NET 8.0 SDK (or .NET 10.0 for newer targets)
- Python 3.9+ (for build scripts and backend)

### Build

```powershell
# Restore dependencies
dotnet restore

# Build entire solution
dotnet build

# Build specific project
dotnet build ColorVision/ColorVision.csproj

# Build Release x64
dotnet build -c Release -p:Platform=x64

# Run main application
dotnet run --project ColorVision/ColorVision.csproj
```

### Build Scripts (Python)

Located in `Scripts/` directory:

```powershell
# Build main installer and upload
py Scripts\build.py

# Build main installer without remote upload
py Scripts\build.py --skip-remote-upload

# Build update packages
py Scripts\build_update.py

# Build plugin package
py Scripts\build_plugin.py -p PluginName

# Publish plugin to marketplace
py Scripts\publish_plugin.py -p PluginId -v 1.0.0.1 -f Package-1.0.0.1.cvxp
```

### Backend (Flask)

```bash
cd Backend/marketplace
pip install -r requirements.txt
python app.py                    # Default: http://localhost:9998
python app.py --storage H:\ColorVision --port 9999
```

### Documentation

```bash
# Dev server
npm run docs:dev

# Build docs
npm run docs:build

# Preview built docs
npm run docs:preview
```

### Clean Build

```powershell
# Clean script
.\clear-bin.ps1

# Or manual
dotnet clean
Remove-Item -Recurse -Force **/bin, **/obj
```

## Test Commands

```powershell
# Run xUnit tests (Windows only)
dotnet test Test/ColorVision.UI.Tests/

# Backend tests
cd Backend/marketplace
python test_app.py
python test_app_releases.py
```

## Key Configuration

- `Directory.Build.props`: Global MSBuild properties (signing, versions, TFMs)
- `ColorVision.snk`: Strong-name key (optional - signing disabled if missing)
- `Backend/marketplace/config.json`: Flask backend configuration
- `ColorVision/log4net.config`: Logging configuration (PreserveNewest)

## External Dependencies

Must exist at runtime:
- `DLL/CVCommCore.dll` - CopyLocal=true
- `DLL/MQTTMessageLib.dll` - CopyLocal=true
- `OpenCvSharp4.runtime.win` - Keep in output

## Code Conventions

1. **Target Framework**: `net8.0-windows` (with `x64` platform)
2. **Do not disable strong-name signing** if the key file exists
3. **UI Layering**: UI ↔ Engine via abstractions; avoid ad-hoc cross-layer calls
4. **Assets**: Use `CopyToOutputDirectory` for configs/assets as needed
5. **Plugins**: Class Library `net8.0-windows`; add `<UseWPF>true</UseWPF>` if UI needed

## Plugin Development Quick Start

1. Create Class Library targeting `net8.0-windows`
2. Add `<UseWPF>true</UseWPF>` if UI needed
3. Implement `IPlugin` or `IPluginBase` entry point
4. Configure post-build to copy to `ColorVision/bin/<Config>/net8.0-windows/Plugins/<Name>/`
5. See `docs/02-developer-guide/plugin-development/` for details

## References

- Architecture: `docs/03-architecture/README.md`
- Extensibility: `docs/02-developer-guide/core-concepts/extensibility.md`
- Plugin Dev: `docs/02-developer-guide/plugin-development/overview.md`
- Backend: `docs/02-developer-guide/backend/README.md`
- Build Scripts: `docs/02-developer-guide/scripts/README.md`
