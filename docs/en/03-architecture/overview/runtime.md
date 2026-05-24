# Architecture Runtime

This page only describes the main application runtime chain visible in the current code, no longer maintaining English draft metadata and generic startup sequence diagrams.

## First, How to Understand the Runtime

The current desktop application's runtime is not a single model of "initialize all modules at once then display the main interface," but is divided into several actual branches:

- Command-line only processes files and returns directly
- Normal desktop startup and enters wizard or startup window
- After abnormal previous exit, first ask whether to disable plugins before continuing startup

## Main Application Startup Chain

From the current implementation of `ColorVision/App.xaml.cs`, the common startup order is roughly:

1. Set working directory and preload key DLLs as needed.
2. Initialize configuration, logging, theme, and language.
3. Parse command-line arguments.
4. For file processing branches like `input` or `export`, directly process and return.
5. Perform single instance check, and forward command-line arguments to the already running instance if needed.
6. Clean up zombie processes in non-debug state.
7. Decide whether to disable plugins based on last startup status.
8. Initialize WinForms visual styles.
9. Display `WizardWindow` or `StartWindow` based on wizard completion status.

The most important thing here is not remembering all steps, but knowing that startup does not always directly enter the main window.

## When Plugins Enter

The current main application decides whether to load plugins before entering the wizard or startup window.

Several key points of plugin loading are:

- Scan `Plugins/` directory
- Read `manifest.json` in each plugin directory
- Optionally read `.deps.json`
- Check `ColorVision.*` dependency versions
- Finally load plugin assemblies using `Assembly.LoadFrom(...)`

If the previous exit was abnormal, it will first ask whether to disable plugins at startup, indicating that whether plugins participate in this runtime is a clear branch, not always unconditionally loaded.

## After Entering the Main Workspace, Which Runtime Objects Are Most Critical

### Service Tree

`ServiceManager` loads the service tree after the database connection becomes available, organizing data from resource tables into:

- `TypeServices`
- `TerminalServices`
- `DeviceServices`
- `GroupResources`

Then it connects flow display areas and device display controls into the unified display manager.

### Registration Center and Service Tokens

`MQTTRCService` at runtime handles:

- Maintaining connection status with the registration center
- Querying currently available service tokens
- Updating service status
- Synchronizing service status back to device service objects

So many problems like "flow won't run" or "device is online but status not updating" are essentially not UI problems, but issues where the runtime state here is not ready.

### Template Registration

The template system is not manually hardcoded registration one by one. Currently `TemplateControl` scans `IITemplateLoad` implementations in loaded assemblies after the database connection becomes available, and calls their `Load()` to complete template registration.

This means whether templates are visible and editable depends on two prerequisites:

- Relevant assemblies have been loaded
- Database connection has been established

## How Flow Execution Connects at Runtime

When users enter a flow window, the runtime main chain continues to extend to:

- `DisplayFlow` handles refreshing the current flow template, starting or stopping the flow
- `FlowControl` handles starting and stopping running flows
- `FlowEngineLib` handles specific node execution
- `MQTTRCService` provides service tokens and status updates needed for flow execution

During execution, the runtime also continuously updates:

- Currently executing node
- Execution log text
- Batch progress
- Node records and message records

## Most Common Runtime Failure Points

### Startup Phase

- Plugin dependencies not met
- Abnormal previous exit requiring manual decision on whether to disable plugins
- Command-line branch returns early, mistakenly thinking the program did not continue starting

### Service Preparation Phase

- Database not connected, causing service tree or templates not to load normally
- Registration center or MQTT side not ready, causing service tokens to be empty

### Execution Phase

- Flow template not selected or start node missing
- Device service exists, but runtime state has not yet synchronized to executable state

## Continue Reading

- [System Architecture Overview](./system-overview.md)
- [Component Interactions](./component-interactions.md)
- [Workflow](../../01-user-guide/workflow/README.md)
- [Log Viewer](../../01-user-guide/interface/log-viewer.md)

## Notes

- This page only retains the runtime chain supported by current code, no longer maintaining unified startup sequence diagrams disconnected from implementation.
- Relevant entry points are primarily in `ColorVision/App.xaml.cs`, `UI/ColorVision.UI/Plugins/PluginLoader.cs`, `Engine/ColorVision.Engine/Services/ServiceManager.cs`, `Engine/ColorVision.Engine/Services/RC/MQTTRCService.cs`, and `Engine/ColorVision.Engine/Templates/TemplateContorl.cs`.