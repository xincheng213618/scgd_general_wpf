# Workflow Execution & Debugging

This page only retains execution entry points and troubleshooting paths verifiable in the current code. For most users, the most critical aspect of workflow execution is not "advanced debugging terminology," but first confirming that the correct workflow is selected, services are online, a start node exists, and how to narrow the scope when failures occur.

## Currently Available Execution Entry Points

From the current implementation, workflow execution has at least these two clear entry points:

- Start execution: `F6`
- Stop execution: `F7`

If you see old documentation mentioning `F5`, `F10`, breakpoints, or single-step execution, please follow the current interface and code bindings, and do not continue troubleshooting based on old shortcuts.

## What to Confirm Before Execution

### A Valid Workflow Template is Selected

If no valid template is currently selected, execution will not actually begin. First confirm that the template selected in the dropdown is the workflow you want to run, not just that the workflow window is open.

### A Start Node Exists in the Workflow

The current execution checks for a start node before running. Without a start node, the workflow will fail directly and will not enter the actual execution phase.

### Registry Center and Service Tokens Are Available

The current implementation first checks the registry center connection and available service list. When service tokens are empty, the interface will prompt you to refresh services before retrying.

### Preprocessing Passes

There is also a preprocessing layer before the workflow officially begins. If preprocessing fails, the workflow will be canceled and will not continue running.

## Common Execution Order

1. First confirm the current workflow content and start node in [Workflow Design](./design.md).
2. Select the correct workflow template in the execution window.
3. Confirm that related device services are online.
4. Press `F6` or click the run entry point to start execution.
5. During execution, observe the log area, current running node, and progress changes.
6. Press `F7` or use the stop entry point when you need to abort.

## What You Can See During Execution

### Current Running Node

During execution, the interface continuously displays the name of the currently running node. If a workflow appears "stuck," first check which node it stopped at rather than immediately suspecting the entire workflow.

### Execution Progress and Duration

The current implementation records workflow duration, last execution time, and estimates current progress based on the last duration. This estimate is suitable for quickly judging the approximate stage but should not be treated as strict business completion.

### Results and Status

After workflow completion, the batch status, duration, and result summary are written; when execution is stopped, the status is recorded as canceled.

## How to Narrow Scope During Debugging

### First Find the First Failed Node

Rather than judging "the entire workflow failed," it is more valuable to first identify the first node that turned red or did not continue advancing, then go back to the corresponding device, template, or input data to investigate.

### First Distinguish Between Not Started and Midway Failure

Two common types of problems:

- Blocked before execution: template not selected, start node missing, registry center not connected, service tokens empty, preprocessing failed
- Execution started but failed midway: a node returned an error, timed out, or message mismatch

### Log Area Over Guessing

The current execution window continuously updates log text. When you see a failure, first check which node it last stopped at, whether preprocessing failure, execution cancellation, or status messages appeared before and after, then decide which layer to go back to for troubleshooting.

## What to Record for Workflow Handoff

A workflow handoff is not just a workflow file. The next maintainer needs to know the devices, templates, inputs, results, and external systems it depends on.

| Record | What to capture | Why it matters |
| --- | --- | --- |
| Workflow template | name, version, import source, last editor | Prevents running an old workflow |
| Start conditions | start node, SN/batch input, project window or external trigger | Explains why a workflow did not start |
| Device dependencies | camera, motor, SMU, file service bindings | Isolates device-layer failures |
| Template dependencies | image template, calibration template, thresholds | Explains result drift |
| Data destinations | database table, export file, image folder, Socket/MES response | Confirms where results should appear |
| Failure evidence | first failed node, log timestamp, error message | Lets the next person reproduce the failure |

## Minimal Retest Script

For field retest or upgrade verification, do not start with the full production chain. Use a minimal run first.

1. Open workflow design and confirm the template and start node exist.
2. Open execution and select the same workflow template.
3. Confirm related device services are online; run a device smoke action if needed.
4. Prepare a safe SN, image, or test input that will not affect production.
5. Press `F6`, then record start time, current node, final state, and duration.
6. Query the same run in logs, images, database, export file, or external response.
7. If stopping is required, press `F7` and confirm the state is recorded as canceled, not unknown.

## Failure Triage Table

| Failure point | Typical behavior | Check first |
| --- | --- | --- |
| Before execution | Run does nothing, service refresh prompt, missing start node | registry center, service list, workflow template, start node |
| Preprocessing | canceled immediately or preprocessing failed | inputs, template validity, project window context |
| Device node | timeout, no device response, abnormal return code | device page, hardware, device Code, MQTT/serial/IP |
| Template node | completes but result is wrong | template version, thresholds, image source, calibration data |
| Data node | workflow completes but result is not visible | database write, batch/SN, export target, permissions |
| External system | ColorVision completes but MES/Socket gets nothing | protocol, port, project handler, response fields |

## Common Issues

### Pressed Run but Workflow Did Not Start

- First confirm whether the registry center is connected
- Then confirm whether the service list has been refreshed
- Check whether a valid workflow template is selected
- Check whether a start node actually exists in the workflow

### Workflow Started but Stopped Quickly

- First check whether it was a preprocessing failure
- Then check the last node and corresponding status in the log
- If it's a device-related node, go back to the corresponding device page to confirm connection and configuration

### Progress Not Moving, Appears Stuck

- First check the current running node name
- Then determine whether a node is waiting for device or message response
- If necessary, press `F7` to stop, then independently verify the device service that node depends on

### Manual Execution Succeeds but Fails in Workflow

- First confirm whether the workflow references the same set of devices and templates
- Then confirm whether the pre-execution environment matches manual testing
- If necessary, cross-check with [Device Service Overview](../devices/overview.md) and [Log Viewer](../interface/log-viewer.md)

## Continue Reading

- [Workflow Design](./design.md)
- [Device Service Overview](../devices/overview.md)
- [Log Viewer](../interface/log-viewer.md)
- [Data Management](../data-management/README.md)
- [Field Operation Acceptance Checklist](../field-operation-acceptance.md)

## Notes

- This page only retains currently verifiable execution and troubleshooting entry points and no longer maintains descriptions of single-step, breakpoint, etc. without implementation basis.
- Related implementations are primarily located in `Engine/ColorVision.Engine/Templates/Flow/DisplayFlow.xaml.cs`, `ViewFlow.xaml.cs`, `FlowControl.cs`, and `EngineCommands.cs`.
