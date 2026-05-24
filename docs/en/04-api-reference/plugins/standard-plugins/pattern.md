# Pattern / Chart Card Generation

This page no longer writes Pattern as a "standard plugin that fully exists in the current repository and can be directly developed." Based on current source code status, the independent `Plugins/Pattern/` plugin implementation described in old documentation can no longer be matched in the repository.

## Current Conclusion

From the current source tree:

- What actually exists under the `Plugins/` directory are projects like `Conoscope/`, `EventVWR/`, `Spectrum/`, `SystemMonitor/`, `WindowsServicePlugin/`.
- There is no `Plugins/Pattern/` directory in the current repository, nor a corresponding `.csproj`, window, `manifest.json`, or independent plugin entry implementation.
- Types mentioned in old documentation such as `IPattern`, `IPatternBase<T>`, `PatternManager`, and batch generation interfaces also have no corresponding real definitions in the current repository source code.

Therefore, this page can now only serve as a "note on remaining landing points of chart card related capabilities in the repository," and can no longer serve as an independent plugin API reference.

## Related Code Still Matchable in the Repository

Although the independent Pattern plugin project is missing, the repository still retains two code lines related to PG/chart card switching:

### `Engine/cvColorVision/PG.cs`

This portion is a P/Invoke wrapper for PG-related native interfaces in `cvCamera.dll`, with clearly visible capabilities currently including:

- `CM_InitPG`
- `CM_ConnectToPG`
- `CM_StartPG`
- `CM_StopPG`
- `CM_ReSetPG`
- `CM_SwitchUpPG`
- `CM_SwitchDownPG`
- `CM_SwitchFramePG`

This indicates that the more reliable "chart card related capability" in the current repository is PG device control, rather than a high-level plugin project with its own pattern editor.

### `Engine/FlowEngineLib/PG/PGLoopNode.cs`

On the FlowEngine side, there is also `PGLoopNode`, which converts PG parameters in loop nodes into command lists and dispatches them through the flow execution chain:

- `Start` -> `CM_StartPG`
- `Stop` -> `CM_StopPG`
- `Reset` -> `CM_ReSetPG`
- `Up` / `Down` -> Switch chart card
- `Specify` -> `CM_SwitchFramePG`

This is more like the capability to "control PG device chart card switching in a flow," rather than the complete plugin from old documentation that locally generates 11 pattern types and exports PNG/JPEG/BMP.

## Why Old Documentation Can No Longer Be Used

Several categories of content in the old page can no longer be proven with source code:

- Claimed existence of an independent `Pattern` plugin project and menu entry.
- Claimed existence of extension interfaces like `IPattern` / `IPatternBase<T>`.
- Claimed support for 11 pattern types, local template management, batch export, preview optimization, and an entire set of high-level features.
- Provided extensive sample APIs and extension code that do not exist in the current repository.

Continuing to retain this content would only mislead readers into thinking corresponding implementations can still be directly found in the current repository.

## A More Reasonable Way to Understand Currently

If you are now tracing "chart card" capabilities in this repository, you should preferentially understand it as follows:

1. First view it as part of the PG device control chain, rather than an independent WPF plugin.
2. First look at `cvColorVision/PG.cs` to confirm what PG commands the low-level layer can send.
3. Then look at `FlowEngineLib/PG/PGLoopNode.cs` to understand how flows batch or loop PG switching.
4. If you need to look up higher-level chart card generation UI, first confirm whether the corresponding source code has been moved out of the repository, located in other projects, or is merely a documentation residual.

## How to Understand These Entry Points Currently

- The root-level [Plugins/README.md](../../../../Plugins/README.md) now clearly distinguishes between "plugin directories that actually exist in the current source code" and historical residual names.
- This page in the API reference only retains notes on the remaining landing points of Pattern-related capabilities in the current repository, no longer writing it as an existing standard plugin.
- If there are expression differences between the two entry points, the source code directory and runtime loading behavior should take precedence.

## Continue Reading

- [Engine Components Overview](../../engine-components/README.md)
- [FlowEngineLib Architecture](../../../03-architecture/components/engine/flow-engine.md)
- [Algorithm System Overview](../../algorithms/overview.md)