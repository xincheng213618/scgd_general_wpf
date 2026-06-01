# Physical Camera Management

This page explains what the physical camera layer is responsible for. It is more like a "hardware resource ledger," responsible for discovering, authorizing, and maintaining real cameras, rather than directly acting as a substitute for camera services.

## When to Come Here First

- Connecting a new camera for the first time
- Importing or updating a license
- Refreshing available camera IDs
- Managing calibration resources, groups, and physical configuration
- Logical camera services cannot find bindable objects

## What the Physical Camera Layer is Responsible For

- Creating physical camera objects
- Importing `.lic` or `.zip` licenses
- Maintaining physical camera configuration and resource trees
- Providing bindable camera codes and basic parameters for camera services

This means that issues like "has the hardware actually been recognized by the system" and "is the license valid" should typically be investigated at this layer first.

## Common Operation Order

1. Connect the device and confirm the system can see available camera IDs.
2. Create a camera in physical camera management, or import a license.
3. Check camera name, code, mode, and basic configuration.
4. If needed, prepare calibration, groups, and related resources.
5. Return to [Camera Service](./camera.md) and bind the logical service to this physical camera.

## Key Points to Confirm During Configuration

- Whether the license is valid
- Whether there are actually uncreated camera IDs
- Whether physical camera resources are complete
- Whether the camera mode matches the actual hardware
- Whether the logical camera service needs to be re-saved or refreshed after changes

## Common Issues

### Cannot Find Uncreated Cameras

- Confirm the hardware is connected and recognized by the system
- Refresh device IDs first and retry
- If the device was just plugged/unplugged, reopen the window and check again

### License Imported but Still Unavailable

- Confirm the imported file format is correct
- Confirm the camera code in the license matches the current hardware
- Check whether the database or local resources have been synchronized

### Physical Camera Configuration Changed, Existing Camera Service Behaves Abnormally

- Check whether the logical service needs to be re-saved or re-bound
- Pay attention to whether related services need to be restarted after configuration changes
- If path and resource adjustments are involved, confirm that related files have been placed in the new location

## Continue Reading

- [Camera Service](./camera.md)
- [Camera Parameter Configuration](./camera-configuration.md)
- [Calibration Service](./calibration.md)

## Notes

- This page only retains the device access and management perspective and no longer maintains detailed code analysis.
- The relevant implementation is primarily located in `Engine/ColorVision.Engine/Services/PhyCameras/`.