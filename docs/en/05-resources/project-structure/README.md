# Project Structure Overview

This document provides a quick explanation of the main directory division of the current repository and gives the most appropriate documentation entry points for each directory.

## Main Directory Partition

| Directory | Role | Recommended First Reading |
| --- | --- | --- |
| `ColorVision/` | Main WPF application entry point and main window | [Getting Started Guide](../../00-getting-started/README.md) / [Main Window Navigation](../../01-user-guide/interface/main-window.md) |
| `UI/` | UI framework, themes, property editor, image editor | [UI Components Overview](../../04-api-reference/ui-components/README.md) |
| `UI/ColorVision.SocketProtocol/` | Local TCP service, message history and management window | [ColorVision.SocketProtocol](../../04-api-reference/ui-components/ColorVision.SocketProtocol.md) / [Socket Communication Optimization Roadmap](../../02-developer-guide/performance/socket-protocol-optimization-roadmap.md) |
| `Engine/` | Core engine, device services, template system, flow execution | [Engine Development Guide](../../02-developer-guide/engine-development/README.md) / [Engine Components Overview](../../04-api-reference/engine-components/README.md) |
| `Plugins/` | Runtime plugins and extension capabilities | [Plugin Development Overview](../../02-developer-guide/plugin-development/overview.md) |
| `Projects/` | Client project packages and business customization implementations | [Component Interactions](../../03-architecture/overview/component-interactions.md) |
| `Backend/marketplace/` | Plugin marketplace backend service | [Plugin Marketplace Backend](../../02-developer-guide/backend/README.md) |
| `Scripts/` | Build, packaging, and release scripts | [Build and Release Scripts](../../02-developer-guide/scripts/README.md) |
| `ColorVisionSetup/` | Installer and updater | [Auto Update System](../../02-developer-guide/deployment/auto-update.md) |
| `Test/` | xUnit, native helper, backend, and script validation | [Testing And Validation Handoff](../../02-developer-guide/testing.md) |
| `docs/` | VitePress documentation source | Current documentation / [Module and Documentation Map](./module-documentation-map.md) |

## Reading by Role

### New Users or Implementation Staff

1. [Getting Started Guide](../../00-getting-started/README.md)
2. [User Guide](../../01-user-guide/README.md)
3. [FAQ](../../01-user-guide/troubleshooting/common-issues.md)

### Engine or Algorithm Development

1. [Architecture Design](../../03-architecture/README.md)
2. [Engine Development Guide](../../02-developer-guide/engine-development/README.md)
3. [Algorithms Overview](../../04-api-reference/algorithms/README.md)

### Plugin Development

1. [Extensibility Overview](../../02-developer-guide/core-concepts/extensibility.md)
2. [Plugin Development Overview](../../02-developer-guide/plugin-development/overview.md)
3. [Existing Plugin Capabilities](../../04-api-reference/plugins/README.md)
4. [Current Plugin Documentation Coverage](../../04-api-reference/plugins/current-plugin-coverage.md)

### Documentation Maintenance

1. [Appendix & Resources](../README.md)
2. [Module and Documentation Map](./module-documentation-map.md)

## Notes

- What is provided here is the "where to start reading" entry point and does not replace detailed API or topic pages.
- If a directory lacks standalone documentation, enter from the overview page of the adjacent chapter first rather than continuing to proliferate new scattered index pages.
