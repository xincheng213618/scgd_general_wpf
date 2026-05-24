# Module and Documentation Map

This document only retains the current repository structure and still valid documentation entry points, used to quickly locate "where is the code, what documentation to read first."

## Code Areas to Documentation Entry Points

| Code Area | Focus | Preferred Documentation Entry | Supplementary Entry |
| --- | --- | --- | --- |
| `ColorVision/` | Main program entry, main window, application startup | [Getting Started Guide](../../00-getting-started/README.md) | [Main Window Navigation](../../01-user-guide/interface/main-window.md) |
| `UI/` | WPF UI framework, themes, editors | [UI Components Overview](../../04-api-reference/ui-components/README.md) | [User Guide](../../01-user-guide/README.md) |
| `UI/ColorVision.SocketProtocol/` | TCP service, JSON/Text dispatch, message history, management window | [ColorVision.SocketProtocol](../../04-api-reference/ui-components/ColorVision.SocketProtocol.md) | [Socket Communication Module Optimization Roadmap](../../02-developer-guide/performance/socket-protocol-optimization-roadmap.md) |
| `Engine/ColorVision.Engine/Services/` | Device services, service coordination | [Device Services Overview](../../01-user-guide/devices/overview.md) | [Engine Development Guide](../../02-developer-guide/engine-development/README.md) |
| `Engine/ColorVision.Engine/Templates/` | Template system, parameterized algorithms, result processing | [Algorithms Overview](../../04-api-reference/algorithms/README.md) | [Templates Architecture Design](../../03-architecture/components/templates/design.md) |
| `Engine/FlowEngineLib/` | Flow nodes, execution model, visual flow | [FlowEngineLib Architecture](../../03-architecture/components/engine/flow-engine.md) | [FlowNode Development](../../04-api-reference/extensions/flow-node.md) |
| `Engine/cvColorVision/` | OpenCV integration, low-level vision processing | [Engine Components Overview](../../04-api-reference/engine-components/README.md) | [cvColorVision](../../04-api-reference/engine-components/cvColorVision.md) |
| `Plugins/` | Runtime plugins and extension capabilities | [Plugin Development Overview](../../02-developer-guide/plugin-development/overview.md) | [Standard Plugins Topic](../../04-api-reference/plugins/standard-plugins/pattern.md) |
| `Projects/` | Client projects, custom business assembly | [Component Interactions](../../03-architecture/overview/component-interactions.md) | [Project Structure Overview](./README.md) |
| `ColorVisionSetup/` | Installer and update flow | [Deployment Overview](../../02-developer-guide/deployment/overview.md) | [Auto Update System](../../02-developer-guide/deployment/auto-update.md) |
| `Backend/marketplace/` | Plugin marketplace backend | [Plugin Marketplace Backend](../../02-developer-guide/backend/README.md) | [Development Guide](../../02-developer-guide/README.md) |
| `Scripts/` | Build, packaging, and release scripts | [Build and Release Scripts](../../02-developer-guide/scripts/README.md) | [Deployment Overview](../../02-developer-guide/deployment/overview.md) |
| `docs/` | Current documentation site source | [Appendix & Resources](../README.md) | Current documentation |

## Search by Task

### Want to Add a Device Service

1. First read [Device Services Overview](../../01-user-guide/devices/overview.md)
2. Then read [Engine Development Guide](../../02-developer-guide/engine-development/README.md)
3. Finally go to [Engine Components Overview](../../04-api-reference/engine-components/README.md) to find specific module pages

### Want to Develop a Plugin

1. [Extensibility Overview](../../02-developer-guide/core-concepts/extensibility.md)
2. [Plugin Development Overview](../../02-developer-guide/plugin-development/overview.md)
3. [Plugin Development Getting Started](../../02-developer-guide/plugin-development/getting-started.md)

### Want to Understand Templates or Flows

1. [Algorithms Overview](../../04-api-reference/algorithms/README.md)
2. [FlowEngineLib Architecture](../../03-architecture/components/engine/flow-engine.md)
3. [Templates Architecture Design](../../03-architecture/components/templates/design.md)
4. [Templates API Reference](../../04-api-reference/algorithms/templates/api-reference.md)

### Want to Modify UI or Property Editing

1. [User Guide](../../01-user-guide/README.md)
2. [UI Components Overview](../../04-api-reference/ui-components/README.md)
3. [Property Editor](../../01-user-guide/interface/property-editor.md)

### Want to Check Build, Release, and Updates

1. [Deployment Overview](../../02-developer-guide/deployment/overview.md)
2. [Auto Update System](../../02-developer-guide/deployment/auto-update.md)
3. [Build and Release Scripts](../../02-developer-guide/scripts/README.md)

## Usage Principles

- Enter from chapter home pages first, then jump to specific topic pages.
- Historical drafts, orphaned documents, and old path pages are no longer used as primary entry points.
- If a perfectly matching topic page cannot be found, fall back to the overview page first rather than continuing to rely on old directory naming.