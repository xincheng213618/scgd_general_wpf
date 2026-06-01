# API Reference

This chapter now only retains stable entry points that have been consolidated into "current implementation guides," no longer flattening all topic pages into a single layer.

## Recommended Entry Points

### UI & Client Layer

- [UI Components Overview](./ui-components/README.md)

### Engine & Runtime Layer

- [Engine Components Overview](./engine-components/README.md)

### Template & Algorithm Integration Layer

- [Algorithms & Templates Overview](./algorithms/README.md)
- [Algorithm System Overview](./algorithms/overview.md)

### Plugin & Extension Layer

- [Plugins & Status Page](./plugins/README.md)
- [Extension Points Overview](./extensions/README.md)

## Current Organization Principles

- The top-level homepage only links to consolidated overview pages, no longer treating all individual pages as first-screen entry points.
- Sub-topic pages remain in their respective directories, but by default should be read alongside source code.
- If documentation is inconsistent with implementation, source code, XML comments, and actual runtime behavior take precedence.

## Current Chapter Boundaries

- `ui-components/` primarily covers WPF UI-side modules and desktop shell layer.
- `engine-components/` primarily covers runtime modules under the Engine directory, not a complete algorithm encyclopedia.
- `algorithms/` primarily covers the Templates system and algorithm integration chain, not all underlying image operator directories.
- `plugins/` primarily covers standard plugins in the current workspace that still match source code, plus status description pages for a small number of legacy plugins.
- `extensions/` currently mainly retains Flow node extension and other extension point topics that directly map to actual code.

## Supplementary Reading Approach

- Pages under `plugins/` contain both plugin topics "matching current source code" and "historical status description" pages; read the section overview before entering individual pages.
- `extensions/` currently has a very narrow scope, mainly covering Flow node extension and similar topics that can be directly anchored to code, not a complete extension mechanism encyclopedia.
- Both types of pages are better suited as on-demand reference entry points, rather than replacing the user guide or developer guide as the main entry.

## Recommended Reading Order

1. Start with [UI Components Overview](./ui-components/README.md) to understand the client shell and UI infrastructure.
2. Then read [Engine Components Overview](./engine-components/README.md) to understand services, templates, and flow runtime.
3. Finally, read [Algorithms & Templates Overview](./algorithms/README.md) and [Algorithm System Overview](./algorithms/overview.md) to connect templates and algorithm integration chain.
4. When you need to check plugin status or extension points, enter [Plugins & Status Page](./plugins/README.md) and [Extension Points Overview](./extensions/README.md).