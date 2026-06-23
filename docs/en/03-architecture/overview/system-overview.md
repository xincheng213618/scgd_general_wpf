# System Architecture Overview

This page no longer attempts to present the entire repository as a standard textbook using an abstract layered approach. Instead, it directly explains how the system is organized based on the current code repository's main directories, and where to typically start when reading code.

## First, How to Understand This Repository

From the current directory structure, ColorVision is closer to a Windows WPF platform centered on a desktop main application, built around engine, UI, plugins, project packages, and installation/update systems.

The most important top-level areas are:

- `ColorVision/`: Main application entry and main window
- `UI/`: WPF UI framework, themes, property editor, image editor, database and desktop menus, etc.
- `Engine/`: Device services, template system, flow execution, OpenCV integration, file processing
- `Plugins/`: Runtime plugin extensions
- `Projects/`: Client projects and customized business combinations
- `ColorVisionSetup/`: Installer and update-related programs
- `Web/Backend/`: Plugin marketplace backend
- `Scripts/`: Build, packaging, and release scripts

## Structure by System Role

### Main Application Layer

`ColorVision/` is the desktop application entry, responsible for main window, application startup, global configuration, update entry, and overall workbench organization.

If you are tracing "what happens first after the program starts," usually start here, then cross-reference `UI/` and `Engine/`.

### UI Layer

`UI/` is not a single project, but a collection of interface-related modules. Currently the more critical ones include:

- `ColorVision.UI/`: Common UI framework and capabilities like menus, panels, property editors
- `ColorVision.Themes/`: Themes and visual resources
- `ColorVision.ImageEditor/`: Image viewing, annotation, and result display
- `ColorVision.Database/`: Database browser and other database-related UI capabilities
- `ColorVision.UI.Desktop/`: Desktop-level menu and settings entry points

### Engine Layer

`Engine/` is the system's business core, but is also not a single-project namespace. It currently consists of several major pieces:

- `ColorVision.Engine/`: Device services, template system, flow windows, MQTT, and business coordination
- `FlowEngineLib/`: Flow node editing and execution foundation
- `cvColorVision/`: Low-level vision processing and OpenCV-related integration
- `ColorVision.FileIO/`: File read/write processing
- `ColorVision.ShellExtension/`: External integration-related extensions

### Plugin and Project Layer

- `Plugins/` provides runtime plugin extensions, such as Conoscope, Spectrum, SystemMonitor, etc.
- `Projects/` houses client projects or business packaging implementations, typically recombining existing engine and UI capabilities into specific solutions

### Delivery and Peripheral Layer

- `ColorVisionSetup/` handles installer and update-side programs
- `Web/Backend/` handles plugin marketplace backend
- `Scripts/` and root directory batch scripts handle build, packaging, and release

## Most Common Main Chains at Runtime

If following user operations from top to bottom, the most common chain is typically:

1. User enters a feature from the main window of `ColorVision/`.
2. Corresponding window or panel in `UI/` handles display and interaction.
3. Device services, templates, or flow logic in `Engine/ColorVision.Engine/` take over business processing.
4. When flow execution is needed, further call `Engine/FlowEngineLib/`.
5. When image or algorithm processing is needed, continue linking `Engine/cvColorVision/`, `UI/ColorVision.ImageEditor/`, or specific template implementations.
6. If functionality comes from external extensions, enter implementations in `Plugins/` or `Projects/`.

## Common Entry Points When Reading Code

### Understanding Main Interface and Entry Points

First read:

- `ColorVision/`
- `UI/ColorVision.UI/`
- `UI/ColorVision.UI.Desktop/`

### Understanding Devices, Templates, and Flows

First read:

- `Engine/ColorVision.Engine/Services/`
- `Engine/ColorVision.Engine/Templates/`
- `Engine/FlowEngineLib/`

### Understanding Image Results and Display

First read:

- `UI/ColorVision.ImageEditor/`
- `Engine/cvColorVision/`

### Understanding Extension Capabilities

First read:

- `Plugins/`
- `Projects/`
- [Plugin Development Overview](../../02-developer-guide/plugin-development/overview.md)

## What This Page No Longer Does

This page no longer maintains these distortion-prone contents:

- Fictional standardized six-layer architecture naming
- Module name lists inconsistent with current directories
- Generalized textbook-style summaries like "dependency injection container," "object pool," "report template"

If a specific topic needs more detailed runtime relationships, flow execution chains, or template structures, it should be explained in the corresponding topic page, not covered all at once here.

## Continue Reading

- [Architecture Runtime](./runtime.md)
- [Component Interactions](./component-interactions.md)
- [FlowEngineLib Architecture](../components/engine/flow-engine.md)
- [Templates Architecture Design](../components/templates/design.md)

## Notes

- This page only serves as the system entry map under the current repository structure, no longer maintaining abstract layered drafts disconnected from code directories.