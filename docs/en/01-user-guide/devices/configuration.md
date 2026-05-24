# Adding and Configuring Devices

This page only covers user operation paths: how to add a device service, how to save, and where to look first when modifying parameters.

## Common Scenarios

- A new device is connected and needs a corresponding service created in the system first.
- A device already exists but its name, communication parameters, or binding object needs to be modified.
- After project switching, device order needs to be adjusted, old devices disabled, or new devices added.

## Basic Process

1. Open the device management window.
2. Enter the device settings or editing interface.
3. Add a device service of the corresponding type, or select an existing device for modification.
4. Fill in necessary parameters and save.
5. Return to the device list and confirm the device appears in the available list.

If the device is still not visible in subsequent workflows, camera windows, or result interfaces, first return to the device list to confirm whether this step actually took effect.

## What to Typically Confirm During Configuration

### Basic Information

- Whether the device name is easy to distinguish
- Whether the device type is selected correctly
- Whether the current project references the correct device instance

### Communication or Binding Information

- Whether network address, port, topic, or communication identifier is correct
- Whether the corresponding physical device or resource object is bound
- Whether local drivers, server-side, or third-party programs are already prepared

### Visibility After Saving

- Whether the device already appears in the device list
- Whether the device status is normal
- Whether the device can be selected in related windows or workflow nodes

## Recommended Reading by Device Type

- Camera-related: see [Camera Service](./camera.md), [Camera Management](./camera-management.md), [Camera Parameter Configuration](./camera-configuration.md)
- Calibration-related: see [Calibration Service](./calibration.md)
- Motor-related: see [Motor Service](./motor.md)
- File or workflow-type devices: see [File Server](./file-server.md) and [Flow Device Service](./flow-device.md)

## Common Issues

### Device Not in List After Adding

- Check whether the save was actually successful
- Check whether you are operating in the wrong project or wrong configuration context
- Reopen the device window to confirm whether it's just the list not refreshing

### Parameters Changed but Device Behavior Unchanged

- Check whether reconnection, regeneration of control items, or reopening of related windows is needed
- Check whether you modified the device instance actually in use

### Configuration Page Unclear What to Fill

- First go to the corresponding device topic page for parameter descriptions
- If the device depends on a physical device object, complete physical device management or calibration steps first

## Notes

- This page no longer retains code analysis manuscripts and class diagrams.
- Specific implementation details are subject to the actual code under `Engine/ColorVision.Engine/Services/`.