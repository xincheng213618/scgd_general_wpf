# Plugins and Status

This chapter only retains two types of content:

- Plugin topic pages that can still be directly matched against source code in the current workspace
- Old plugin pages where corresponding source code is missing or no longer fully maintained, thus rewritten as "historical status notes"

It no longer serves as a "complete plugin directory," nor does it assume that every page listed here represents a plugin project directly developable in the current source tree.

## First, Understand the Boundaries of This Chapter

- The current plugin loading model should be based on the actual implementation of `manifest.json` and `UI/ColorVision.UI/Plugins/PluginLoader.cs`.
- Plugin API reference pages only cover a few topics that have been consolidated in current documentation, not a complete mirror of the `Plugins/` directory.
- If documentation descriptions do not match the current source code directory, the source code directory and runtime loading behavior should take precedence.

## What Pages Are Currently Included

### Topics Still Directly Matchable Against Source Code

- [Spectrum Plugin](./standard-plugins/spectrum.md)
- [SystemMonitor Plugin](./standard-plugins/system-monitor.md)
- [EventVWR Plugin](./standard-plugins/eventvwr.md)
- [Windows Service Plugin](./standard-plugins/windows-service.md)

### Historical Status Note Pages

- [Pattern / Chart Card Generation](./standard-plugins/pattern.md)
- [ImageProjector (Historical Status)](./standard-plugins/image-projector.md)
- [ScreenRecorder (Historical Status)](./standard-plugins/screen-recorder.md)

The purpose of retaining these pages is to explain "whether they can still be matched against source code in the current repository and where to find the current status," rather than continuing to serve as feature commitment pages.

## How to Read This Chapter More Effectively

1. First read [Plugin Development Overview](../../02-developer-guide/plugin-development/overview.md) to understand plugin entry points, artifact forms, and runtime boundaries.
2. Then confirm whether the target plugin currently has corresponding source code in the `Plugins/` directory.
3. If a page is explicitly written as "historical status," treat it as a status note rather than a current development manual.
4. If tracing the runtime loading chain, cross-reference with `PluginLoader` and the `manifest.json` in each plugin directory.

## Currently Known Gaps

- The current API reference does not cover all real projects in the `Plugins/` directory.
- Plugins like Conoscope that currently still have source code do not yet have separate API reference pages.
- Therefore, this chapter is more suitable as an "organized topic entry point" rather than a "plugin full index."

## Continue Reading

- [API Reference Overview](../README.md)
- [Plugin Development Overview](../../02-developer-guide/plugin-development/overview.md)
- [FlowEngineLib Node Extensions](../extensions/flow-node.md)