# Device Service Overview

This page serves as the device chapter entry point, prioritizing answers to "what device pages are available, how to configure them generally, and where to check first when encountering problems."

## What is a Device Service

In ColorVision, devices are typically managed as "services." The main program maintains a device service list, and users view, configure, enable, and operate these services in the device window.

Device-related implementations are primarily located in:

- `Engine/ColorVision.Engine/Services/`
- `Engine/ColorVision.Engine/Services/Devices/`

Typical device categories visible in the current code directory include:

- Camera
- Calibration
- Motor
- FileServer
- FlowDevice
- Sensor
- SMU
- Spectrum

## How to Read This Chapter

### General Entry Points

- [Adding and Configuring Devices](./configuration.md)

### Specific Devices

- [Camera Service](./camera.md)
- [Camera Management](./camera-management.md)
- [Camera Parameter Configuration](./camera-configuration.md)
- [Calibration Service](./calibration.md)
- [Motor Service](./motor.md)
- [SMU Service](./smu.md)
- [Flow Device Service](./flow-device.md)
- [File Server](./file-server.md)

## Common Usage Order

1. First read [Adding and Configuring Devices](./configuration.md) to understand the basic process of adding and saving devices.
2. Then go to specific device pages to confirm what parameters and operations the device has.
3. If cameras are involved, continue with [Camera Management](./camera-management.md) and [Camera Parameter Configuration](./camera-configuration.md).
4. If you need the device to participate in automated workflows, then read [Workflow Overview](../workflow/README.md).

## What You Typically Encounter During Use

- A device service may bind to real hardware, or it may just be a type of communication or file-based service.
- Device list order, enabled status, and configuration content typically affect visibility in subsequent windows and workflows.
- Some devices, in addition to basic configuration, have independent physical device management, calibration, or parameter configuration pages.

## Troubleshooting Suggestions

### Device Does Not Appear in List

- First confirm whether it has been created and saved in the device configuration window.
- Then confirm whether the corresponding device dependencies are met, such as physical hardware, drivers, or communication environment.

### Device Appears but Cannot Work

- Prioritize checking the parameter descriptions in the device-specific page.
- Then check logs and connection status.
- If it fails when called in a workflow, cross-check with [Workflow Execution & Debugging](../workflow/execution.md).

## Notes

- This page only serves as an entry point and usage path guide, and no longer carries device service code analysis.
- Device implementation details are subject to the actual code under `Engine/ColorVision.Engine/Services/`.