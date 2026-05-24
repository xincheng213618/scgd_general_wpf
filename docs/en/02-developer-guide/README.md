# Developer Guide

This chapter focuses on secondary development, extension points, and delivery processes; for class library details and module design, please refer to the API Reference and Architecture Design respectively.

## Common Scenarios Starting from Here

### Understanding Extension Mechanisms

- [Extensibility Overview](./core-concepts/extensibility.md)

### Modifying Engine or Template-Related Features

- [Engine Development Guide](./engine-development/README.md)
- [Architecture Design](../03-architecture/README.md)
- [Engine Component API](../04-api-reference/engine-components/README.md)

### Developing Plugins

- [Plugin Development Overview](./plugin-development/README.md)
- [Plugin Development Getting Started](./plugin-development/getting-started.md)
- [Plugin Lifecycle](./plugin-development/lifecycle.md)

### Build, Deploy & Update

- [Deployment Overview](./deployment/overview.md)
- [Auto Update System](./deployment/auto-update.md)
- [Build & Release Scripts](./scripts/README.md)

### Backend & Auxiliary Systems

- [Plugin Marketplace Backend](./backend/README.md)
- [Performance Optimization Overview](./performance/overview.md)
- [Socket Communication Module Optimization Roadmap](./performance/socket-protocol-optimization-roadmap.md)

## Recommended Reading Path

1. First read [Architecture Design](../03-architecture/README.md) to confirm module boundaries.
2. Then read [Extensibility Overview](./core-concepts/extensibility.md) to confirm extension points and plugin entry points.
3. Enter your target topic: plugins, Engine, deployment, or backend.
4. When you need class and interface details, switch to [API Reference](../04-api-reference/README.md).

## Chapter Boundaries

- This chapter prioritizes providing "how to enter the code" paths rather than replacing the API manual.
- Some detailed topics in the Engine subdirectory are still being consolidated, so the default entry has been changed to overview pages to avoid keeping unmaintained small pages in the main navigation.
- AI/Agent-related experimental materials are retained in subdirectories but are no longer part of the default reading path.

## Supplementary Entry Points

- [Project Structure Overview](../05-resources/project-structure/README.md)
- [Online Repository](https://github.com/xincheng213618/scgd_general_wpf)
- [Issue Tracker](https://github.com/xincheng213618/scgd_general_wpf/issues)