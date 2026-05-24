# Camera Parameter Configuration

This page explains what you typically need to change in the "Camera Service Configuration Page." What is adjusted here are logical camera service parameters, some of which have default values derived from the bound physical camera.

## Common Configuration Content

### Binding Information

- Current service instance name
- Bound physical camera or CameraCode
- Which camera service is actually being invoked in the current project

### Acquisition Parameters

- Channels and bit depth
- Capture mode
- Exposure, gain, saturation
- Auto-exposure toggle and related parameters

### Extended Parameters

- Video-related configuration
- File cache and save format
- Motor, CFW, autofocus, and other subordinate configurations

## Recommended Adjustment Order

1. First bind the correct physical camera.
2. Let the service synchronize one round of basic parameters.
3. Then adjust exposure, gain, channels, and mode.
4. After saving, verify with live image or a workflow run.
5. When advanced tuning is needed, proceed to the template or calibration page.

## Which Parameters Are Most Likely to Interact

- Camera mode and channels
- Bit depth and acquisition mode
- Exposure time and gain
- Auto-exposure and manual exposure parameters
- Physical camera default values and current service display configuration

If the image is noticeably abnormal after a modification, prioritize checking whether these groups changed together rather than focusing only on a single parameter.

## Common Issues

### Parameters Changed but No Effect

- Confirm that the modified parameters belong to the camera service currently in use
- Reopen the view or restart the service after saving
- Check whether the configuration was overwritten by physical camera sync

### Unreasonable Exposure or Channel Settings

- First confirm which channels the current camera mode supports
- Do not mix three-channel and single-channel parameters
- When overexposure or noise occurs, prioritize rolling back exposure and gain

### Workflow Results Still Abnormal After Saving

- First verify with live image whether capture is normal
- Then check whether template parameters in the workflow are still old values
- Cross-check calibration and auto-exposure templates if necessary

## Continue Reading

- [Camera Service](./camera.md)
- [Physical Camera Management](./camera-management.md)
- [Calibration Service](./calibration.md)
- [Workflow Design](../workflow/design.md)

## Notes

- This page only retains the configuration and troubleshooting perspective and no longer maintains analysis manuscripts expanded line-by-line from source code.
- The relevant implementation is primarily located in `Engine/ColorVision.Engine/Services/Devices/Camera/`.