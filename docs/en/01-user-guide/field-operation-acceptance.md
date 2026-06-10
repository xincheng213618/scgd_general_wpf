# Field Operation Acceptance Checklist

Use this page for first delivery, upgrades, field retests, or operator training. It proves that ColorVision is usable from the user side by checking UI, devices, workflows, data, external systems, plugins, project packages, and rollback evidence.

If you do not know where to start, read [User Operation Workflow Matrix](./operation-workflow-matrix.md) first. If an item fails, continue to the linked topic page.

## Acceptance Table

| Item | Minimal action | Pass standard | First page if failed |
| --- | --- | --- | --- |
| Host startup | Launch ColorVision and open main window | Main window, menus, status bar, and log entry appear | [Main Window](./interface/main-window.md), [Log Viewer](./interface/log-viewer.md) |
| UI entries | Open settings, logs, database, Socket, scheduler, marketplace | Each window opens without startup-level errors | [UI Component User Handbook](./interface/ui-component-handbook.md) |
| Config save | Change one safe config item, save, restart | Value persists and service state is correct | [Property Editor](./interface/property-editor.md) |
| Device services | Inspect key camera/motor/SMU/file services | Device exists, status refreshes, minimal action succeeds | [Device Service Overview](./devices/overview.md) |
| Camera capture | Capture one image or preview live image | Image is generated and opens in image editor | [Camera Service](./devices/camera.md), [Image Editor](./image-editor/overview.md) |
| Workflow design | Open field workflow template | Start node and key parameters are correct | [Workflow Design](./workflow/design.md) |
| Workflow run | Run one minimal workflow or project flow | Completes or first failed node is clear | [Workflow Execution & Debugging](./workflow/execution.md) |
| Image/overlay | Open result image and inspect ROI/POI/overlay | Image, layer, and coordinates align | [Image Editor](./image-editor/overview.md) |
| Database write | Query one result by SN/time/batch | SQLite/MySQL record exists with core fields | [Database Operations](./data-management/database.md) |
| Export file | Export CSV/Excel/PDF/image/project result | File exists and fields match customer format | [Data Export & Import](./data-management/export-import.md) |
| Socket/MES/Modbus | Send one field command or sample | External system triggers and receives correct status/data | [SocketProtocol](../04-api-reference/ui-components/ColorVision.SocketProtocol.md) |
| Plugin capability | Open field plugin and run minimal function | Menu, window, device connection, result/export work | [Existing Plugin Capabilities](../04-api-reference/plugins/README.md) |
| Project package | Open project, enter SN, run minimal flow | Customer result and response match project page | [Project Guide](../00-projects/README.md) |
| Rollback evidence | Find previous package, config, and database backup | Site can return to previous working state | Plugin/project release evidence pages |

## Device Acceptance

Do not accept a device only because it appears in the list. Prove it can be operated and referenced by workflows or project packages.

| Check | Pass standard |
| --- | --- |
| Device resource | Key devices exist and names/codes distinguish real site devices |
| Communication parameters | IP, port, serial, baud rate, device id, or file path match site |
| Minimal action | Camera captures, motor moves or homes, SMU reads, file server downloads/uploads |
| Workflow reference | Flow node or project window can select the correct device |
| Log evidence | Connection, timeout, driver, and permission errors are resolved or recorded |

If manual operation works but workflow fails, inspect node binding and template parameters. If manual operation also fails, inspect hardware, driver, port/IP, and service config first.

## Workflow Acceptance

The goal is not to run every production branch. Prove that the current version can enter, run, locate failures, and review results.

| Check | Pass standard |
| --- | --- |
| Template version | Selected Flow template matches field requirement |
| Start node | Execution has a start point and node state updates |
| Key inputs | Device, template, image, SN, batch id, or project config is available |
| Failure location | First failed node and log line can be identified |
| Result review | Result list, image, database, or exported file points to the same run |

## Data And Export Acceptance

| Deliverable | Acceptance method | Focus |
| --- | --- | --- |
| SQLite/MySQL | Query by SN, time, or batch | batch, template, result fields |
| CSV/Excel/PDF | Open file and verify fields/units | field order, PASS/FAIL, legacy compatibility |
| Image/overlay | Open result image and annotations | point/box/layer coordinate alignment |
| Socket/MES response | Save request and response sample | status code, error message, `Data` field |
| Summary/text | Verify yield, failure items, grouping | folder, file name, model grouping, statistics scope |

If export is empty, confirm source data exists first. Then check whether the displayed batch and export target are the same before inspecting project exporters or field mapping.

## External System Acceptance

Keep at least one raw request and response sample.

| Type | Minimal evidence |
| --- | --- |
| JSON Socket | `EventName`, SN, request JSON, response JSON, project window state |
| Text Socket | Raw command such as `T00XX,SN;`, return code, data |
| MES/serial | STX/ETX raw message, device id, return code, timeout |
| Modbus | IP, port, register address, trigger value, completion write-back |
| File server | Request path, returned file list, download/upload path |

## Handoff Record Template

```text
site/customer:
host version:
project package:
plugin package:
config folder:
device smoke result:
workflow smoke result:
image/overlay result:
database query result:
export file sample:
external protocol sample:
known failures:
rollback package/config:
operator trained:
owner/date:
```

## Continue Reading

- [User Operation Workflow Matrix](./operation-workflow-matrix.md)
- [UI Component User Handbook](./interface/ui-component-handbook.md)
- [Device Service Overview](./devices/overview.md)
- [Workflow Execution & Debugging](./workflow/execution.md)
- [Data Management](./data-management/README.md)
- [Project Guide](../00-projects/README.md)
- [Existing Plugin Capabilities](../04-api-reference/plugins/README.md)

