# ImageProjector Status Note

This page no longer writes ImageProjector as a standard plugin implementation in the current repository, because in the current `scgd_general_wpf` workspace, the corresponding source code project can no longer be found.

## Actual State in the Current Workspace

Verified against the current repository structure:

- There is no `ImageProjector/` source code directory under `Plugins/`.
- There is no corresponding plugin project file in the workspace.
- The current plugin index page [Plugins/README.md](../../../../Plugins/README.md) does not list it as an existing plugin directory.
- It is retained in the current documentation sidebar only to keep the historical status note page accessible, not to indicate that a corresponding plugin implementation exists in the current repository.

Therefore, this page cannot continue to retain the old-style "multi-monitor projection tool complete manual" writing approach, as that would write historical features as current source code facts.

## What Information This Page Now Retains

Currently only one conclusion is retained:

ImageProjector-related documentation in this workspace is a historical residual page, not an API reference page verifiable against current source code.

If this plugin is reintroduced later, new documentation should be rewritten based on at least these real anchor points:

- Plugin directory and project file
- `manifest.json`
- Menu or provider integration points
- Main window or projection window implementation
- Configuration landing points

Until these code elements reappear, feature descriptions, configuration tables, or API lists should not be supplemented.

## Why Old Documentation Is No Longer Maintained

The old page described ImageProjector as a currently existing plugin and provided a complete feature list, display mode descriptions, and component structure. But in the current source tree, these descriptions have no verifiable implementation to support them.

Continuing to write documentation this way would disguise "features that may have existed in the past" as "current repository facts." This is exactly what this round of cleanup aims to avoid.

## Continue Reading

- [Plugins/README.md](../../../../Plugins/README.md)
- [docs/02-developer-guide/plugin-development/overview.md](../../../02-developer-guide/plugin-development/overview.md)
- [docs/04-api-reference/plugins/standard-plugins/pattern.md](./pattern.md)