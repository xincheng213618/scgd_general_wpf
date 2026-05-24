# Motor Service

The motor service is used to integrate motor devices into ColorVision's unified device system, providing common control actions such as open, close, move, return to origin, and read current position. It is suitable for scenarios requiring position control in both manual debugging and automated workflows.

## When to Read This Page

- You need to add or configure a motor service
- You need to manually move the motor to a specified position
- You need to confirm whether the current position has been updated
- You are troubleshooting issues like "motor visible but not moving" or "position not refreshing"

## Common Usage Order

1. First create and save the motor service in the device list.
2. Check whether communication configuration and timeout parameters are reasonable.
3. Execute an open action first to confirm the device is in an operable state.
4. Then perform move, return to origin, or read position actions.
5. If business requirements require closing the device after completion, execute the close action.

## Common Operations in This Service

### Open and Close

Many subsequent actions assume the device is already in an operable state. When encountering "command sent but no response," first confirm whether the device was actually successfully opened rather than directly suspecting the move command.

### Move to Specified Position

Common parameters of interest include:

- Target position
- Absolute move or relative move
- Timeout duration

During debugging, first move within a small range to confirm direction and behavior, then make larger step adjustments.

### Return to Origin

When the current position is not trustworthy, or a unified starting point needs to be re-established, prioritize returning to origin before continuing with subsequent actions.

### Get Current Position

Current position is not guessed; it should be actively read back for confirmation. Especially after automated workflows or multiple manual moves, reading the position first before proceeding is usually more stable.

## Common Issues

### Motor Service Exists but Cannot Open

- First check whether communication configuration is correct
- Then confirm whether the device is online
- If the open action fails, subsequent move and read position actions are typically not reliable

### Move Command Sent but Position Unchanged

- First confirm whether the device was successfully opened
- Then confirm target position, absolute/relative mode, and timeout duration
- After operation, actively read the current position once; do not just check whether the interface refreshes

### Current Position Display Incorrect

- First re-execute a get position action
- Then confirm whether the previous move failed but the interface still retains old values
- If return to origin is involved, first confirm whether the return to origin action actually completed

### Motor Action Fails When Called in Workflow

- First verify manually whether this motor can currently open and move normally
- Then check whether parameters in the workflow match manual testing
- If necessary, cross-check with [Workflow Execution & Debugging](../workflow/execution.md) and [Log Viewer](../interface/log-viewer.md)

## Continue Reading

- [Device Service Overview](./overview.md)
- [Workflow Overview](../workflow/README.md)
- [Workflow Execution & Debugging](../workflow/execution.md)
- [Common Issues](../troubleshooting/common-issues.md)

## Notes

- This page only retains the usage perspective of the motor service and no longer maintains class diagram and message processing analysis.
- The current implementation is primarily located in `Engine/ColorVision.Engine/Services/Devices/Motor/`.