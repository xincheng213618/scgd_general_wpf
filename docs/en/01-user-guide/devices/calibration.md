# Calibration Service

The calibration service is used to bind calibration workflows to physical cameras, manage calibration templates, trigger calibration data generation, and handle raw files and cache when needed. It is not simply a "parameter page," but a service layer connecting physical cameras, template editing, and calibration results.

## When to Read This Page

- You need to create or configure a calibration service
- You need to bind a calibration service to a specific physical camera
- You need to edit calibration templates
- You need to trigger a calibration-related data acquisition
- You are troubleshooting issues like "why can't I start calibration" or "why won't the template open"

## Two Things to Confirm Before Use

### First Bind a Physical Camera

In the current implementation, the calibration service directly depends on the physical camera code. If the corresponding physical camera is not configured first, many calibration-related operations will simply not proceed.

### Then Confirm Database Availability

If MySQL is enabled in the current environment but the database is not connected, calibration template editing will not enter normally. In this case, fix the database connection first; do not repeatedly click the template window.

## Common Usage Order

1. First create or confirm the calibration service in the device list.
2. Bind it to the correct physical camera.
3. Open the calibration template editing entry and confirm that the template content corresponds to the current camera.
4. When calibration data needs to be retrieved, execute the relevant calibration actions.
5. If the physical camera configuration changes, remember to go back and confirm whether the calibration service is still correctly bound.

## Most Common Operations in This Service

### Managing Physical Cameras

If the camera corresponding to the current calibration service is not ready yet, go to the physical camera management page first rather than troubleshooting solely within the calibration window.

Continue reading:

- [Camera Management](./camera-management.md)
- [Camera Parameter Configuration](./camera-configuration.md)

### Editing Calibration Templates

Calibration template editing depends on the currently bound physical camera. Whether the template window can open normally is itself a valuable health check:

- If it cannot open, prioritize checking camera binding and database connection
- If it can open but the content is wrong, prioritize checking whether the current camera and template objects are correct

### Handling Raw Files and Cache

The calibration service also involves raw file lists, file downloads, and cache cleanup. When encountering issues like "old data interfering with current calibration," start with the cache and raw file scope rather than immediately suspecting the entire service is unavailable.

## Common Issues

### Clicked Edit Calibration Template but Did Not Enter the Editor

- First confirm that the corresponding physical camera is bound
- If MySQL is enabled, confirm that the database connection is normal
- Then confirm that the correct calibration service instance is opened

### Camera Has Been Changed, but Calibration Results Still Look Old

- First confirm whether the `CameraCode` bound to the calibration service has been updated
- Then confirm whether the current configuration was saved after the physical camera switch
- If necessary, re-enter the template editor to confirm the current template object

### Raw Files Obtained During Calibration Are Incorrect

- First confirm that the current device code and device type match the target device
- Then confirm whether the raw file list was requested rather than old cache
- If cache interference is suspected, perform a cache cleanup first and recheck

### Calibration Service Exists but Does Not Take Effect in Workflows

- First confirm whether the bound camera and template are valid
- Then cross-check with [Workflow Overview](../workflow/README.md) and [Workflow Execution & Debugging](../workflow/execution.md)
- Also use [Log Viewer](../interface/log-viewer.md) to see the specific failure stage

## Continue Reading

- [Device Service Overview](./overview.md)
- [Camera Service](./camera.md)
- [Camera Management](./camera-management.md)
- [Workflow Execution & Debugging](../workflow/execution.md)

## Notes

- This page only retains the usage path for calibration service and no longer maintains class structure and MQTT detail analysis.
- The current implementation is primarily located in `Engine/ColorVision.Engine/Services/Devices/Calibration/`.