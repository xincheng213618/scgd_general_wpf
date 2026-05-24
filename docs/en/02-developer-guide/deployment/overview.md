# Deployment Overview

This page only retains deployment entry points still in use by the current repository, focusing on Windows desktop applications, installers, and update mechanisms.

## Current Deployment Targets

- `ColorVision/`: Main application
- `ColorVisionSetup/`: Installer and update-related programs
- `Scripts/`: Build, packaging, and publishing auxiliary scripts
- `Plugins/`: Plugin directory loaded at runtime

## Current Recommended Paths

### Development or Test Environment

Build and run the main application directly from source:

```powershell
dotnet restore
dotnet build -p:Platform=x64
dotnet run --project ColorVision/ColorVision.csproj
```

### Delivery Environment

- Use the installer to deliver the complete desktop application
- Include plugin directory and runtime dependencies as needed
- If involving online updates, continue reading [Auto Update System](./auto-update.md)

## Deployment Pre-Check Items

- Target environment is Windows
- Main application built for x64
- Runtime dependencies and native DLLs correctly output with the package
- Required configuration files copied to output directory

## Supporting Documents

- [Getting Started](../../00-getting-started/README.md)
- [System Requirements](../../00-getting-started/prerequisites.md)
- [Auto Update System](./auto-update.md)
- [Build and Release Scripts](../scripts/README.md)

## Notes

- Old Docker, cloud deployment, production cluster descriptions are no longer default deployment paths.
- If a specific project has a special delivery method, it should be maintained separately in the corresponding project directory or project documentation, rather than continuing to pile up in the general deployment page.