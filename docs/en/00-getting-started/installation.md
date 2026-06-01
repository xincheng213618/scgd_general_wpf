# Installation Guide

This page retains only the installation steps and post-installation verification methods that users actually need. Installer internals, packaging scripts, and Advanced Installer configuration have been moved to the development and deployment documentation.

## Pre-Installation Check

Before starting, read [System Requirements](./prerequisites.md) and confirm the following:

- The current environment is Windows 10 1903+ or Windows 11
- x64 environment is used
- Write permission to the installation directory; administrator privileges are recommended if service configuration is involved
- Network environment is available if online updates or remote service access is needed

## Method 1: Install via Installer Package

This is the recommended method for end users.

### Step 1: Obtain the Installer Package

- Obtain the ColorVision installer from team distribution channels, release pages, or internal delivery packages
- If both a full installer and an incremental update package are provided, use the full installer for first-time installation

### Step 2: Launch the Installer

- Double-click the installer to launch the installation wizard
- If the system prompts for permission, allow the installation to continue as needed
- If the installer prompts for missing prerequisites, complete them as instructed first

### Step 3: Select Installation Location and Components

- Choose the target directory in the installation wizard
- Decide whether to create desktop shortcuts, Start menu entries, etc.
- If the delivery package includes plugins or project resources, keeping the default installation options is usually safer

### Step 4: Complete Installation

- Wait for file copying and initialization to finish
- Once complete, you can check the option to launch the main program
- If an older version exists, the installer may prompt to upgrade, overwrite, or uninstall the old version before continuing

## Method 2: Build and Run from Source

This is the path for developers and debugging scenarios.

### Minimal Steps

```powershell
dotnet restore
dotnet build .\ColorVision\ColorVision.csproj -p:Platform=x64
dotnet run --project .\ColorVision\ColorVision.csproj -p:Platform=x64
```

### Notes

- The current repository is primarily based on Windows WPF and x64; do not treat the main program as a cross-platform project
- If you only want to verify that the build succeeds, run `dotnet build` first, then proceed to run and debug
- If the output directory is occupied by a running program, close the old process first, or use a temporary output directory for build verification

## Minimal Post-Installation Verification

Whether installed via installer or run from source, the following checks are recommended:

1. The main program can launch normally and display the main window
2. No obvious startup-level errors in the log directory or log window
3. After plugin scanning completes, help and plugin-related menus can be opened normally
4. An image can be opened, confirming basic UI interaction works

If all these steps pass, the installation chain is basically functional. Continue reading [First Run Guide](./first-steps.md) and [Quick Start](./quick-start.md).

## Common Installation Issues

### Installer Cannot Start

- First confirm the installer package is not corrupted; re-obtain it if necessary
- Check whether the current system version meets [System Requirements](./prerequisites.md)
- If blocked by system policies, try re-running with an account that has sufficient privileges

### Installation Complete but Program Fails to Start

- Check logs or error messages first
- Confirm the installation directory and user documents directory have read/write permissions
- For source builds, prioritize checking whether the output directory is missing runtime dependency files

### Abnormal Behavior After Upgrade

- Prioritize checking for residual configuration or file locks from old versions
- Close the main program and related background services first if necessary, then reinstall or restart

## Where to Go Next

- For basic operations after first launch: go to [First Run Guide](./first-steps.md)
- To quickly complete a minimal end-to-end experience: go to [Quick Start](./quick-start.md)
- To understand the implementation of installer, updater, and delivery pipeline: go to [Deployment Overview](../02-developer-guide/deployment/overview.md)