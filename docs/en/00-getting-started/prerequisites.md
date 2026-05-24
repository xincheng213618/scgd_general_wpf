# System Requirements

This page only describes the environment required before installing and running ColorVision, and no longer expands on installer implementation, repository structure, or architecture design.

## Target Audience

- End users who want to install the ready-made package and start using it
- Developers who want to build and run the main program from source
- Colleagues who want to confirm system requirements before proceeding to installation steps

## Runtime Environment Requirements

### Operating System

- Windows 10 1903 or later, or Windows 11
- x64 system environment recommended
- An account with administrator privileges is recommended for initial installation, upgrades, or system service configuration

### Hardware & Display

- 1920x1080 or higher resolution recommended
- Sufficient memory and disk space recommended for image processing and workflow execution
- If connecting cameras, spectrometers, motors, or other devices, ensure corresponding drivers and communication environments are ready in advance

### Network & Permissions

- If checking for updates, accessing plugin marketplace, or connecting to remote services after initial installation, ensure network availability
- If the deployment environment has strict restrictions, ensure the program directory, log directory, and user documents directory have write permissions in advance

## Notes for Running from Installer Package

- The installer will check and deploy required components following its own process; if prompted for missing prerequisites, complete them as instructed
- Certain service management, device driver configuration, or system-level write operations may require administrator privileges
- If an older version exists in the environment, it is recommended to complete the upgrade or uninstall before performing a new installation

## Requirements for Building from Source

The current repository is primarily based on Windows WPF and x64. The following environment is recommended:

- .NET 8.0 SDK or .NET 10.0 SDK
- Visual Studio 2022 or later (recommended)
- Git and PowerShell (for cloning the repository and executing scripts)

It is recommended to build the main program using the x64 platform:

```powershell
dotnet restore
dotnet build .\ColorVision\ColorVision.csproj -p:Platform=x64
```

## Runtime Dependency Notes (Source Build Scenario)

If you are running directly from source rather than deploying via an installer package, also note that the runtime output should include the following dependencies:

- OpenCvSharp Windows runtime
- `DLL/CVCommCore.dll`
- `DLL/MQTTMessageLib.dll`
- `log4net.config` and related resource files required by the main program

These are typically handled by project references and copy rules; if the program builds but fails to start, prioritize checking whether the output directory is missing any of the above dependencies.

## What This Page Does Not Cover

- For installation steps themselves, see [Installation Guide](./installation.md)
- For basic operations after first launch, see [First Run Guide](./first-steps.md)
- For a quick minimal end-to-end experience, see [Quick Start](./quick-start.md)
- To understand system modules and design boundaries, go to [Architecture Design](../03-architecture/README.md)