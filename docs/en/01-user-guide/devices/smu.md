# SMU Service

The SMU service is used for source-measurement integrated operations, such as measuring current at a given voltage, measuring voltage at a given current, and executing a sweep based on a template. It is suitable for scenarios requiring spot measurement, IV sweep, or linking electrical results to subsequent workflows.

## When to Read This Page

- You need to add or configure an SMU device service
- You need to switch measurements between A/B channels
- You need to perform a manual spot measurement
- You need to execute a set of sweep parameters, such as an IV sweep
- You want to confirm where results are displayed and how they are used by subsequent windows

## The Most Critical Concepts in This Service

### Channels

The current implementation supports at least per-channel configuration and results, commonly:

- Channel A
- Channel B

Manual operations and result display must first confirm the current channel; do not mistake Channel A results for Channel B.

### Source Type

The SMU can switch between two common modes:

- Voltage as source, measure current
- Current as source, measure voltage

The display configuration saves manual spot measurement parameters corresponding to the current channel and source type; sweep operations do not use these display parameters but rather separate sweep template parameters.

### Manual Spot Measurement and Sweep Are Not the Same

- Manual spot measurement typically uses measurement values and limits from the current display configuration
- Sweep uses start point, end point, point count, and limits from the `SMUParam` template

If you changed the display configuration but sweep results did not change, first confirm whether you are actually executing a template sweep.

## Common Usage Order

1. First confirm in the device list that the SMU service has been created and can connect normally.
2. Enter the SMU window and first confirm the current channel and source type.
3. For manual spot measurement, first set measurement values and limits, then execute a reading.
4. For sweep, first confirm the `SMUParam` template in use, then execute the sweep.
5. After results appear, first check the result view in the current window, then decide whether to pass them to workflows or other services.

## Key Points for Manual Spot Measurement

- First confirm whether it is voltage source or current source
- Then confirm whether you are entering measurement values or limits
- If limit protection is enabled, parameters exceeding device limits will not be sent
- After readings appear, confirm they belong to the current channel

## Key Points for Sweep

Sweep typically depends on template parameters, not temporarily entered display values. Common content of concern includes:

- Start value
- End value
- Limit
- Point count
- Current channel
- Current source type

To perform an IV sweep, first confirm whether the source type and upper/lower limits in the template match, then execute the sweep; do not just change current values in the display panel.

## Where Results Appear

- Single measurement results return to the SMU result view
- Sweep results also add new result items in the SMU view
- Voltage and current readings for the currently selected channel sync back to display configuration
- Some voltage and current results also link to other windows or services that depend on these two values

## Common Issues

### Spot Measurement Returns No Result

- First confirm the device has been opened and the correct channel is selected
- Then confirm whether the current source type, measurement value, and limits are reasonable
- If limit protection is enabled, first check whether parameters exceed the device's allowed range

### Sweep Results Inconsistent with Manual Settings

- First confirm that the sweep template, not manual spot measurement, was executed
- Then confirm the start/end values, limits, and point count in the template
- Do not mistake current values in display configuration for sweep parameters

### Data from Two Channels Appears Mixed

- First confirm whether the current display is A or B
- Then confirm which channel the most recent operation targeted
- When viewing results, prioritize per-channel understanding; do not only focus on the last reading

### Results Appear but Not Used by Subsequent Workflow

- First confirm whether the subsequent workflow or window reads the current channel's value
- Then confirm whether values should truly be taken from the SMU result view rather than display configuration
- If necessary, cross-check with [Workflow Overview](../workflow/README.md) and [Workflow Execution & Debugging](../workflow/execution.md)

## Continue Reading

- [Device Service Overview](./overview.md)
- [Workflow Overview](../workflow/README.md)
- [Workflow Execution & Debugging](../workflow/execution.md)
- [Log Viewer](../interface/log-viewer.md)

## Notes

- This page only retains the usage perspective of the SMU service and no longer maintains MQTT message and class structure analysis.
- The current implementation is primarily located in `Engine/ColorVision.Engine/Services/Devices/SMU/`.