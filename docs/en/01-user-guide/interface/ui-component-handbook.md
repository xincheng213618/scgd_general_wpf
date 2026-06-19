# UI Component User Handbook

This page explains common ColorVision UI components from the operator, test engineer, and field handoff perspective. It answers when to open each component, where to find it, what it can do, what counts as success, and what to check first when it fails.

For DLL publishing or source changes, use the [UI DLL Component Handbook](../../04-api-reference/ui-components/component-handbook.md) and [UI Control Catalog](../../04-api-reference/ui-components/control-catalog.md). This page intentionally stays at the user-manual level.

## When to Read This Page

- You see a UI component name but do not know what operation it owns.
- You need to explain the main window, settings, logs, database browser, Socket, scheduler, image editor, and plugin marketplace to field users.
- You need to decide whether an issue belongs to UI operation, devices, workflow, plugins, project packages, or package publishing.
- You need to know what a window should prove after it opens.

## Component Overview

| UI component | When to use it | Common entry | Main actions | Success standard | First checks if failed |
| --- | --- | --- | --- | --- | --- |
| Main window workbench | Daily starting point after launch | Opens after startup | Identify menu, search, workspace, status bar | Devices, workflows, images, logs, plugins can be found | Plugin loading, permission, language, layout |
| Top menu | Open global tools, devices, plugins, help | Main window menu bar | Choose tool or plugin feature by goal | Target window or feature opens | Menu permission, plugin state, hotkey conflict |
| Search box | Locate a function when the path is unknown | Main window search area | Enter keyword and open command/page | Target entry is found and opens | Keyword, plugin loading, search index |
| Status bar | Check services, plugins, Socket, scheduler state | Bottom of main window | Read hints or click status icon | State matches field reality | Provider loading, service config, logs |
| Settings window | Modify global or module configuration | Settings/options menu entries | Find setting, edit, save, restart-verify | Value persists after restart | Config path, permission, field type, readonly item |
| Property editor | Edit device, template, flow-node, and config parameters | Property panel or config dialog | Read grouped parameters, edit, save | Object behavior changes as expected | Metadata, validation, readonly field, save path |
| Log viewer | Diagnose startup, device, workflow, plugin issues | Help -> Log, default `Ctrl+L` | Filter by time, level, keyword | First meaningful error is found | Log level, timestamp, module name |
| Terminal | Run field commands, scripts, or file checks | Terminal entry or workspace component | Run command and inspect output | Command returns a clear result | Current directory, permission, environment |
| Image editor | View images, overlays, ROI, video, 3D, pseudo-color | Image result, file open, workspace editor | Zoom, annotate, measure, import/export annotations | Image and overlays align correctly | File path, file still writing, coordinate mapping |
| Database browser | Inspect MySQL/SQLite table data | Tools -> Database Browser | Choose source/table, search, page, small edits | Target record is found or absence is confirmed | Connection, filter, page, primary key |
| Socket manager | Inspect local TCP server state and messages | Socket status icon | Check enabled state, port, connection, message history | External system can connect and exchange messages | Port conflict, protocol mode, handler loading |
| Scheduler task window | View and maintain Quartz scheduled jobs | Tools -> Task Manager | Create, pause, resume, run now, view history | Next fire time and execution records are correct | Cron, task assembly, execution exception |
| Workspace/file tree | Manage `.cvsln`, project files, editors | Solution workspace | Create, open, edit files, switch layout | Files open with the correct editor | File type, editor registration, layout cache |
| Plugin marketplace | Install, view, and update plugins/DLLs | Help -> Marketplace | View plugin/version, download or update | Package downloads and host can load it | Administrator permission, network, manifest, version |
| Downloader | Download plugins, update packages, resources | Marketplace or download window | Add task, watch progress, retry | File lands completely in target directory | aria2c, path permission, network |
| Third-party apps window | Open external tools quickly | Tools or desktop helper entry | Scan apps, add custom tools | External application starts | Path, shortcut, permission |
| Wizard window | Run step-by-step initialization/config | Wizard entry | Fill steps, next, finish | Each step validates and config is generated | Required fields, device connection, output path |
| Theme and common dialogs | Change appearance and confirm operations | Settings, theme menu, system prompts | Switch theme, confirm message, inspect loading state | Theme applies and prompts do not block key work | Resource dictionary, theme config, window focus |

## Basic Interface Components

### Main Window Workbench

The main window is the entry point for daily work. On first launch, confirm four things before entering a business flow:

| Check | Expected result | First checks if abnormal |
| --- | --- | --- |
| Menu | File, Tools, Help, plugin or project menus are visible | Plugin loading, permission |
| Search | Keywords return commands or pages | Search config, module registration |
| Workspace | Windows, images, workflows, or editors can open | Layout cache, workspace state |
| Status bar | Services, Socket, scheduler, and log hints appear | Status-bar providers, service config |

The main window organizes entries; it is not itself a business workflow. If a function is missing, first decide whether the menu was not registered, the plugin was not loaded, permission is insufficient, or the project package is disabled.

### Menu, Search, and Status Bar

| Component | Usage suggestion | Handoff note |
| --- | --- | --- |
| Menu | Use it for fixed entries and administrator tools | Menu items can come from the host, UI modules, plugins, or project packages |
| Search | Use it first when new users do not know the entry path | No search result may mean the plugin is not loaded |
| Status bar | Use it first for service and background task state | Socket and scheduler status icons can open their management windows |

If a button does not respond, open the [Log Viewer](./log-viewer.md) and inspect recent `Error` or `Warn` entries around the click time.

## Configuration and Parameter Components

### Settings Window

The settings window is for global and module configuration. For field changes:

1. Decide whether the target is global, device, workflow, or project configuration.
2. Record old values before changing ports, paths, database, Socket, and file-server parameters.
3. Save changes, then restart the app or refresh the relevant service when required.
4. Verify with the status bar, logs, device page, or project page.

Do not use the settings window as a database maintenance tool. Table data, history, and business results belong in [Database Operations](../data-management/database.md).

### Property Editor

The property editor edits object parameters such as devices, templates, flow nodes, drawing objects, plugin config, and project config.

| Operation | Recommended approach | Failure symptom |
| --- | --- | --- |
| Understand a parameter | Read category, display name, and description | Only raw field names appear, meaning metadata is incomplete |
| Edit a path | Use file/folder selector controls | Manual path typo or permission issue |
| Edit an enum | Use dropdown options | Runtime mode is not the expected one |
| Edit list/dictionary | Use the collection editor window | JSON format error or save failure |
| Verify changes | Run the smallest business action again | Value did not persist or object did not refresh |

If the expected editor control does not appear, developers should inspect `Category`, `DisplayName`, `Description`, custom editor type, and visibility metadata on the target property.

## Diagnostic Components

### Log Viewer

The log viewer is the first diagnostic entry. Common field searches:

| Issue type | Suggested keywords | Next page |
| --- | --- | --- |
| Startup failure | `Error`, `DllNotFoundException`, plugin name, config file name | [Common Issues](../troubleshooting/common-issues.md) |
| Device cannot connect | Device name, port, IP, `timeout`, service name | [Device Services Overview](../devices/overview.md) |
| Workflow failure | Flow name, node name, template name, `failed` | [Workflow Execution & Debugging](../workflow/execution.md) |
| Plugin missing | Plugin directory, `manifest`, `deps.json`, DLL name | [Existing Plugin Capabilities](../../04-api-reference/plugins/README.md) |
| Data not written | Table name, batch id, SN, export path | [Data Management Overview](../data-management/README.md) |

When logs are noisy, narrow by timestamp first and then by level. Do not search only for the final error; the real cause is often a few lines earlier.

### Terminal

The terminal is useful for field environment checks, such as listing directories, testing network, running scripts, or launching helper tools.

- Confirm current directory and command environment first.
- For commands that modify files, confirm the target path.
- Before running scripts, read their docs or parameter help.
- When a command fails, compare the output with log entries from the same time window.

The terminal is mainly for delivery engineers and developer support, not ordinary operators.

## Data, Communication, and Scheduling Components

### Database Browser

The database browser is for confirming whether results were written, records are searchable, and table structure matches the current version.

| Scenario | Operation order | Success standard |
| --- | --- | --- |
| Search one SN or batch | Choose source -> choose database/table -> search SN/batch | Matching record and timestamp are visible |
| Confirm workflow writes data | Run minimal workflow -> refresh table -> search by time | New record appears with complete fields |
| Small maintenance edit | Confirm primary key and write permission -> edit -> save -> refresh | Value remains correct after refresh |

If buttons are disabled, check whether the source is readonly, the table has a primary key, and the connection is valid.

### Socket Manager

The Socket manager inspects the local TCP server state and message flow. It is usually opened from the Socket icon in the status bar.

| Check | Normal result | Abnormal direction |
| --- | --- | --- |
| Enabled state | Socket server is enabled | Config is disabled |
| Listening port | IP/port matches external system config | Port conflict, firewall, wrong IP |
| Connection state | State changes when external system connects | Network, protocol mode, client address |
| Message history | Request and response are persisted | Message database, handler, logs |

If a project package is triggered over Socket, continue to the [Project Guide](../../00-projects/README.md) and the corresponding project page to confirm event names, fields, and response format.

### Scheduler Task Window

The scheduler task window maintains Quartz jobs. It does not design the workflow itself.

| Operation | Description |
| --- | --- |
| Create task | Select job type, name, group, Cron, priority, timeout, and custom config |
| Pause/resume | Temporarily disable or resume an existing job |
| Run now | Trigger once without waiting for the next Cron time |
| Execution history | Inspect success, failure, elapsed time, and error details |

If a job is missing, confirm that the assembly containing it was loaded. If a job does not run, inspect Cron, scheduler state, and execution history.

## Image and Result Components

### Image Editor

The image editor is not only an image viewer. It hosts result viewing, ROI/POI, annotations, overlays, video, pseudo-color, histogram, 3D, and CIE tools.

| Scenario | Recommended operation | Success standard |
| --- | --- | --- |
| View inspection result | Open result image -> compare overlay -> inspect properties | Original image, result layer, and coordinates align |
| Annotate ROI/POI | Select drawing tool -> draw object -> save/export annotations | Annotation can be imported again or recognized by result page |
| Adjust display | Zoom, pan, pseudo-color, histogram | Key area is visible without changing source data |
| View video | Play/pause, seek, adjust preview scale | Video is smooth and frame is correct |
| View 3D/CIE | Switch to matching tool | Data is suitable for visualization and window remains responsive |

If image display is abnormal, first confirm the file finished writing, then inspect coordinate mapping and overlay source.

## Workspace and Helper Components

### Workspace, File Tree, and Editors

The workspace owns `.cvsln`, file tree, editor tabs, terminal, and multi-image viewing. It organizes project files; it does not execute Engine workflows.

| Component | Purpose | First checks if abnormal |
| --- | --- | --- |
| File tree | Browse project folders, create files or projects | Path, permission, template registration |
| Editor system | Open text, image, Markdown, hex, and other files by extension | Editor registration, file extension |
| Layout manager | Save and restore docked panels | Layout cache, window state |
| Multi-image viewer | Browse images and thumbnails in batches | File type, thumbnail cache |
| Local RBAC | Manage users, roles, permissions, sessions | Login state, database, permission mode |

If the goal is to run a customer project workflow, leave the file tree and open the project window or [Project Guide](../../00-projects/README.md).

### Plugin Marketplace, Downloader, Third-Party Apps, and Wizards

| Component | Best used for | First checks if failed |
| --- | --- | --- |
| Plugin marketplace | View plugins, download packages, inspect DLL versions and update plan | Administrator permission, marketplace URL, manifest, version compatibility |
| Downloader | Download plugins, update packages, or external resources | Network, aria2c, target path permission |
| Third-party apps window | Manage quick entries for external tools | Shortcut, program path, launch permission |
| Wizard window | Finish initialization or configuration step by step | Required fields, device state, output path |

The marketplace can prove a package downloaded and installed, but it does not prove the plugin business workflow works. For business capability, read [Existing Plugin Capabilities](../../04-api-reference/plugins/README.md).

## Component Boundaries

| Symptom | Start here |
| --- | --- |
| Window cannot open, menu missing, button unresponsive | UI operation and logs |
| Device state abnormal, camera cannot capture, motor does not move | Device services |
| Flow node fails or result is missing | Workflow and Engine |
| Socket connects but fields are wrong | Project protocol or Socket handler |
| Plugin missing or version incompatible | Plugin loading and marketplace |
| UI DLL or native DLL missing | UI DLL publishing and installer package |
| Database cannot find result | Data management, workflow write path, project export |

## Handoff Checklist

During UI operation handoff, demonstrate at least once:

- Open log viewer, settings, database browser, scheduler task window, and plugin marketplace from the main window.
- Change one safe configuration item and confirm it persists after save/restart.
- Open an image, zoom, annotate, inspect properties, and export annotations.
- Open database browser and search one result record by time or SN.
- If Socket is enabled, open the Socket manager from the status bar and confirm port, connection, and message history.
- If scheduling is enabled, open the task window, inspect a task, run it once, and view history.
- If delivering a plugin or project package, confirm the corresponding menu, status icon, or project window appears and opens.

## Continue Reading

- [Main Window Tour](./main-window.md)
- [Property Editor](./property-editor.md)
- [Log Viewer](./log-viewer.md)
- [Image Editor Overview](../image-editor/overview.md)
- [Database Operations](../data-management/database.md)
- [UI DLL Component Handbook](../../04-api-reference/ui-components/component-handbook.md)
