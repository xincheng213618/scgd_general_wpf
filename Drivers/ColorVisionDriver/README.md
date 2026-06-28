# ColorVisionDriver

ColorVisionDriver is a minimal Windows kernel driver skeleton for the future ColorVision driver layer.

Current scope:

- Create `\Device\ColorVisionDriver`
- Create user-mode link `\\.\ColorVisionDriver`
- Support `IOCTL_CVDRV_PING`
- Support `IOCTL_CVDRV_GET_VERSION`

It does not hook processes, files, registry, network, input, display, or camera streams. It is only a safe driver communication frame for later hardware diagnostics or vendor SDK integration.

## Build

This project is intentionally not referenced by the main ColorVision solution. Build it only on a machine with Visual Studio and WDK installed:

```powershell
msbuild Drivers\ColorVisionDriver\ColorVisionDriver.vcxproj /p:Configuration=Debug /p:Platform=x64
```

## Install

Install only in a test VM with test signing enabled. The normal ColorVision release/build path does not install this driver yet.

```powershell
pnputil /add-driver Drivers\ColorVisionDriver\x64\Debug\ColorVisionDriver.inf /install
```

## Architecture Position

```text
ColorVision UI
  -> ColorVisionServiceHost
  -> ColorVisionDriver
  -> vendor SDK / hardware driver package
```

The current repository still treats `cvCamera.dll` as the camera SDK boundary. This driver is a placeholder for ColorVision-owned kernel functionality if we decide it is really needed later.
