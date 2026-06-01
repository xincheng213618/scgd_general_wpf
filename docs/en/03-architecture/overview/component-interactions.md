# Component Interactions

This page no longer maintains a "module interaction matrix" that covers the entire system but is prone to inaccuracy. The more valuable approach now is to only describe a few interaction main chains that truly exist in the code and most commonly influence modification directions.

## First, Look at These Main Chains

### 1. Main Application Entry Chain

The main application entry is in `ColorVision/`.

It first handles:

- Basic DLL preloading
- Configuration, logging, theme, and language initialization
- File processing command-line branches
- Decision on whether to load plugins
- Launch wizard or startup window

This layer is more like a "host" and "entry scheduler," not specific business logic itself.

### 2. UI Workbench Chain

After entering the main workspace, windows, menus, panels, and editors under `UI/` handle user operations and forward actions to the engine layer.

The most common interaction relationships here are:

- Main window and desktop menu provide entry points
- Property editor, image editor, database browser, and other modules handle their respective interactive views
- Actual business actions continue to be dispatched to `Engine/`

### 3. Service Tree and Device Chain

`ServiceManager` is one of the most critical coordination objects at runtime. It will:

- Read resource and service definitions from the database
- Generate terminal service and device service objects
- Organize group resources
- Connect flow display areas and device display controls into the unified display manager

Therefore, questions like "why does a device appear in the tree," "why can the display area be opened," "why didn't a certain device enter runtime" — typically start by looking at `ServiceManager` rather than directly at an individual device class.

### 4. Template Registration and Editing Chain

The key interaction of the template system is not an independent table, but rather:

- Relevant assemblies are loaded by the main application or plugins
- `TemplateControl` scans types implementing `IITemplateLoad` within them
- These types register templates into the global template dictionary via `Load()`
- Template editors, flow windows, and specific business functions then consume these templates

So problems like "why didn't the template appear" are typically issues with the loading chain, database state, or assembly loading chain, not just problems with the editing window itself.

### 5. Flow Execution Chain

The main chain during flow execution is roughly:

1. `DisplayFlow` selects and refreshes the current flow template.
2. `FlowControl` starts or stops the current flow.
3. `FlowEngineLib` executes the node graph.
4. `MQTTRCService` provides service tokens and status updates.
5. Device nodes or algorithm nodes return results.
6. Execution status, batch records, log text, and node messages are updated back to the interface.

This chain illustrates a common fact: flow is not an isolated module; it naturally depends on templates, device services, and registration center state.

### 6. Plugin Extension Chain

After a plugin is loaded, it is not just an extra DLL, but brings new assemblies into the entire runtime:

- New menu or window entry points
- New services or provider implementations
- New templates, result views, or extension point implementations

Therefore, the interaction between plugins and the main application is more like "merging capabilities into the existing runtime," rather than running externally in parallel.

## How to Use This Page When Modifying Code

### Modifying Main Window or User Entry Points

Priority to follow:

- Main application entry
- UI menus/panels
- Corresponding engine services

### Modifying Device Behavior

Priority to follow:

- `ServiceManager`
- Specific `DeviceService`
- Corresponding MQTT or runtime state objects

### Modifying Templates or Flows

Priority to follow:

- `TemplateControl`
- Template editor
- `DisplayFlow` / `FlowControl` / `FlowEngineLib`

### Modifying Plugin Extensions

Priority to follow:

- `PluginLoader`
- Plugin assemblies
- Menus, services, templates, or view interfaces implemented by plugins

## What This Page No Longer Does

This page no longer maintains these high-risk contents:

- Module matrix tables that look complete but are disconnected from current implementation
- Large lists of unverified event names and interface inventories
- Theoretical dependency diagrams disconnected from actual code directories

If a specific topic needs deeper design analysis, it should go back to the corresponding topic page rather than spreading an over-promising master table here.

## Continue Reading

- [System Architecture Overview](./system-overview.md)
- [Architecture Runtime](./runtime.md)
- [FlowEngineLib Architecture](../components/engine/flow-engine.md)
- [Templates Architecture Design](../components/templates/design.md)

## Notes

- This page only retains the most important real interaction main chains, no longer maintaining the draft matrix.