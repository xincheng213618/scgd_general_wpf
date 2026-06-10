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

## What to Record for Device Handoff

Do not hand over a device with only "configured" written down. The next maintainer needs enough evidence to identify, connect, test, and trace the device inside workflows.

| Record | What to capture | Why it matters |
| --- | --- | --- |
| Device identity | name, Code, device type, real hardware or service | Prevents workflows from binding to the wrong device |
| Communication config | IP, port, serial, baud rate, MQTT topics, file paths | Recreates the field connection conditions |
| Minimal action | camera capture, motor home, SMU point measurement, file list | Proves the device is usable, not just listed |
| Workflow binding | workflow, node, or project window that references it | Explains "manual works, workflow fails" cases |
| Log evidence | successful connection time, error, timeout, permission issue | Gives remote support a starting point |
| Rollback path | previous config, driver, firmware, or project package | Allows recovery after upgrade problems |

## Field Smoke Acceptance

| Step | Action | Pass standard |
| --- | --- | --- |
| 1 | Open and refresh the device list | Target device is visible, name and Code match the handoff record |
| 2 | Open the device detail or dedicated window | Key parameters are visible and save state is clear |
| 3 | Run one safe minimal action | Response, log, and state change are explainable |
| 4 | Open the workflow or project window that depends on it | The same device can be selected, not an old device with a similar name |
| 5 | Run a minimal or simulated workflow | The device node is reached and result or failure reason is traceable |

If step 3 fails, start at the device layer. If step 3 passes but step 5 fails, inspect workflow binding, template parameters, and project mapping first.

## Device Issue Triage

| Symptom | Check first | Then check |
| --- | --- | --- |
| Device missing from list | Created, saved, and correct type | Plugin/project package provides the device entry |
| Device exists but cannot open | Hardware online, driver, port/IP, permission | Connection and timeout errors in logs |
| Manual action works but workflow fails | Device Code referenced by the workflow node | Node parameters, template version, project window selection |
| Result exists but image/data is wrong | Device parameters and acquisition conditions | Downstream template, export fields, database record |
| Broken only after upgrade | Config overwritten | Old package/config mixed with new DLLs |

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
- For field delivery, also record the overall result in [Field Operation Acceptance Checklist](../field-operation-acceptance.md).
