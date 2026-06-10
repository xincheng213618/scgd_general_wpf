# Developer Guide

This chapter focuses on secondary development, extension points, and delivery processes; for module details and system design, please refer to the Module Handbook and Architecture Design respectively.

## Common Scenarios Starting from Here

### Understanding Extension Mechanisms

- [Extensibility Overview](./core-concepts/extensibility.md)

### Modifying Engine or Template-Related Features

- [Engine Business Scenario Playbook](../04-api-reference/engine-components/business-scenario-playbook.md)
- [Engine Development Guide](./engine-development/README.md)
- [Architecture Design](../03-architecture/README.md)
- [Engine Components & Handoff](../04-api-reference/engine-components/README.md)

### Developing Plugins

- [Plugin Runtime And Handoff Playbook](../04-api-reference/plugins/plugin-handoff-playbook.md)
- [Existing Plugin Field Acceptance And Handoff Checklist](../04-api-reference/plugins/plugin-field-acceptance.md)
- [Plugin Development Overview](./plugin-development/README.md)
- [Plugin Development Getting Started](./plugin-development/getting-started.md)
- [Plugin Lifecycle](./plugin-development/lifecycle.md)

### Maintaining Customer Projects

- [Project Package Runtime And Handoff Playbook](../04-api-reference/projects/project-package-playbook.md)
- [Project Guide](../00-projects/README.md)
- [Project Capability & Handoff Matrix](../04-api-reference/projects/project-capability-matrix.md)
- [Project Package Handoff Manual](../04-api-reference/projects/project-handoff.md)

### Testing And Validation

- [Testing and Validation Handoff](./testing.md)
- Start UI, host runtime, configuration, log, MCP/Copilot, PropertyGrid, and editor changes from `Test/ColorVision.UI.Tests/`.
- Use `Test/opencv_helper_test/` for native OpenCV helper and luminous-area validation.

### Build, Deploy & Update

- [Deployment Overview](./deployment/overview.md)
- [Auto Update System](./deployment/auto-update.md)
- [Build & Release Scripts](./scripts/README.md)
- [UI DLL Release Playbook](../04-api-reference/ui-components/ui-dll-release-playbook.md)
- [UI DLL Publishing](../04-api-reference/ui-components/publishing.md)

### Modifying UI Runtime Components

- [UI Runtime Component Handoff](../04-api-reference/ui-components/ui-runtime-handoff.md)
- [UI Control Catalog](../04-api-reference/ui-components/control-catalog.md)
- [UI DLL Component Handbook](../04-api-reference/ui-components/component-handbook.md)
- [UI Components & DLL Publishing](../04-api-reference/ui-components/README.md)

### Backend & Auxiliary Systems

- [Plugin Marketplace Backend](./backend/README.md)
- [Performance Optimization Overview](./performance/overview.md)
- [Socket Communication Module Optimization Roadmap](./performance/socket-protocol-optimization-roadmap.md)

## Recommended Reading Path

1. First read [Architecture Design](../03-architecture/README.md) to confirm module boundaries.
2. Then read [Extensibility Overview](./core-concepts/extensibility.md) to confirm extension points and plugin entry points.
3. Enter your target topic: plugins, Engine, deployment, or backend.
4. Before delivery, check [Testing and Validation Handoff](./testing.md) to choose the right verification commands.
5. When you need module details, delivery notes, or handoff anchors, switch to [Module Handbook](../04-api-reference/README.md).

## Chapter Boundaries

- This chapter prioritizes providing "how to enter the code" paths rather than replacing the module handbook.
- Some detailed topics in the Engine subdirectory are still being consolidated, so the default entry has been changed to overview pages to avoid keeping unmaintained small pages in the main navigation.
- AI/Agent-related experimental materials are retained in subdirectories but are no longer part of the default reading path.

## Supplementary Entry Points

- [Project Structure Overview](../05-resources/project-structure/README.md)
- [Online Repository](https://github.com/xincheng213618/scgd_general_wpf)
- [Issue Tracker](https://github.com/xincheng213618/scgd_general_wpf/issues)
