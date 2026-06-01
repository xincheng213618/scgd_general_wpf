# Camera Service

This page explains the role of the "camera service" layer in daily use, and its relationship with physical cameras, parameter configuration, and calibration templates.

## Camera Service and Physical Camera Are Not the Same Thing

- Physical Camera: real hardware, license, resources, and basic capabilities
- Camera Service: the logical device actually invoked by device lists, view windows, and workflows in the project
- A camera service typically must first bind to a physical camera before stable use of capture, video, calibration, and template functions

## What You Typically Do Here

- Edit camera service configuration
- Open camera view or live image
- Bind or switch physical cameras
- Enter auto-exposure, auto-focus, camera parameter templates
- Open calibration parameter editing window
- View local debugging entry points and camera logs

## Recommended Usage Order

1. First confirm hardware, license, and resource availability in [Physical Camera Management](./camera-management.md).
2. Return to the camera service and bind the current service instance to the correct physical camera.
3. Adjust exposure, gain, channels, and mode in [Camera Parameter Configuration](./camera-configuration.md).
4. Enter auto-exposure, auto-focus, or calibration templates when needed.
5. Verify results with live image, local window, or workflow execution.

## Capability Scope of This Page

In the current code, the camera service layer already includes several common entry points:

- Edit camera configuration
- Refresh device ID and physical camera binding
- Auto-exposure template
- Auto-focus template
- Camera parameter template
- Calibration template
- Camera logs and local window

Therefore, when encountering issues like "can the camera be used normally by the system," typically check the camera service first rather than directly looking at underlying camera resources.

## Common Issues

### Camera Service Exists but Cannot Open or Capture Images

- First check whether a physical camera is already bound
- Then check license, connection status, and camera logs
- If configuration was just changed, reopen related windows or restart the service before retrying

### Camera Service Not Synchronized After Changing Physical Camera Configuration

- Re-enter camera service configuration after saving
- Check whether the current service is bound to the expected physical camera
- Refresh device ID or rebind if necessary

### Video or Local Mode Abnormal

- First close the current video or live mode before reconnecting or restarting
- Prioritize determining whether it's a connection issue or a parameter issue
- Check camera logs to confirm backend service status when needed

## Continue Reading

- [Physical Camera Management](./camera-management.md)
- [Camera Parameter Configuration](./camera-configuration.md)
- [Calibration Service](./calibration.md)
- [Workflow Execution & Debugging](../workflow/execution.md)

## Notes

- This page only retains the usage perspective and no longer maintains code analysis manuscripts.
- Implementation entry points are primarily in `Engine/ColorVision.Engine/Services/Devices/Camera/` and `Engine/ColorVision.Engine/Services/PhyCameras/`.