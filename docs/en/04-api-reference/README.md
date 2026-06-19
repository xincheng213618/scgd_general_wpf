# Module Handbook

This section is the source-code handoff entry. It is no longer a loose API index; it explains how the current UI libraries, Engine runtime, plugins, and customer project packages map back to the repository.

## Start Here

1. [UI Components & DLL Publishing](./ui-components/README.md): DLL/NuGet packages, runtime discovery, dependencies, resources, component catalog, and publishing checks.
2. [Engine Components & Handoff](./engine-components/README.md): device services, templates, flow execution, MQTT, results, and data.
3. [Plugins](./plugins/README.md): the plugins that really exist under `Plugins/`, including runtime loading, field acceptance, and rollback boundaries.
4. [Project Packages](./projects/README.md): customer-specific packages under `Projects/`.
5. [Algorithms & Templates](./algorithms/README.md): template and algorithm integration details.

## Current Map

| Section | Source | Handoff Focus |
| --- | --- | --- |
| [UI Components](./ui-components/README.md) | `UI/` | DLL/NuGet publishing, runtime discovery, component handoff, and dependency direction |
| [Engine Components](./engine-components/README.md) | `Engine/` | business runtime chain and scenario matrix |
| [Algorithms & Templates](./algorithms/README.md) | `Engine/ColorVision.Engine/Templates/` | algorithm templates and result handling |
| [Plugins](./plugins/README.md) | `Plugins/` | manifest, menu entry, runtime boundary, capability matrix, field acceptance, rollback |
| [Project Packages](./projects/README.md) | `Projects/` | customer workflows, Recipe/Fix, Socket/MES, delivery matrix |

If documentation and source code disagree, prefer the current source, project files, manifests, and runtime behavior.
