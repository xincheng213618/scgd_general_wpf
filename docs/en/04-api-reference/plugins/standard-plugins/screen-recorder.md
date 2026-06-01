# ScreenRecorder Status Note

This page no longer writes ScreenRecorder as a standard plugin implementation in the current repository, because in the current `scgd_general_wpf` workspace, the corresponding source code project can no longer be found.

## Actual State in the Current Workspace

Verified against the current repository structure:

- There is no `ScreenRecorder/` source code directory under `Plugins/`.
- There is no corresponding plugin project file in the workspace.
- The current plugin index page [Plugins/README.md](../../../../Plugins/README.md) does not list it as an existing plugin directory.
- It is retained in the current documentation sidebar only to keep the historical status note page accessible, not to indicate that a corresponding plugin implementation exists in the current repository.

Therefore, this page cannot continue to retain the old-style "high-performance screen recording plugin API manual" writing approach, as that would mistakenly write historical descriptions as current implementation.

## What Information This Page Now Retains

Currently only one conclusion is retained:

ScreenRecorder-related documentation in this workspace is a historical residual page, not an API reference page verifiable against current source code.

If this plugin is reintroduced later, new documentation should be rewritten based on at least these real anchor points:

- Plugin directory and project file
- `manifest.json`
- Menu or provider integration points
- Recording window and recording source management implementation
- Configuration and output landing points

Until these code elements reappear, descriptions such as encoding formats, recording source types, or overlay APIs should not be supplemented.

## Why Old Documentation Is No Longer Maintained

The old page described ScreenRecorder as a currently existing screen recording plugin and provided recording sources, encoders, overlays, and advanced feature lists. But in the current source tree, these contents have no verifiable implementation.

Continuing to polish that old draft would only make the documentation look more complete while becoming increasingly disconnected from the current repository.

## Continue Reading

- [Plugins/README.md](../../../../Plugins/README.md)
- [docs/02-developer-guide/plugin-development/overview.md](../../../02-developer-guide/plugin-development/overview.md)
- [docs/04-api-reference/plugins/standard-plugins/pattern.md](./pattern.md)