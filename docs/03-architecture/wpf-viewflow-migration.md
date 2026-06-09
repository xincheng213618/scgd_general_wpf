# WPF ViewFlow Migration

## Background

`ViewFlow` is currently a WPF `UserControl` that hosts the WinForms `STNodeEditor` through `WindowsFormsHost`. This keeps the existing node model working, but the visible editor surface is still WinForms, so it does not benefit from WPF rendering, styling, input routing, focus behavior, or theming.

The migration goal is to provide a WPF editor surface while reusing the existing `STNode` types, flow serialization format, and `FlowEngineLib` execution model.

## Current Structure

### Visible Flow Entry

- `Engine/ColorVision.Engine/Templates/Flow/ViewFlow.xaml`
  - Hosts `ST.Library.UI.NodeEditor.STNodeEditor` inside `WindowsFormsHost`.
  - Keeps the rest of the workflow toolbar, progress, logs, and run/stop controls in WPF.
- `Engine/ColorVision.Engine/Templates/Flow/ViewFlow.xaml.cs`
  - Configures commands such as save, clear, refresh, auto layout, import/export, and module import.
  - Applies theme colors directly to `STNodeEditor`.
  - Registers `STNodeEditor` with `FlowEngineControl.AttachNodeEditor`.
  - Creates `STNodeEditorHelper` for context menus, copy/paste, flow validation, property panel activation, and layout helpers.
  - Handles WinForms mouse/key events for delete, pan, zoom, and node movement.

### Node Editor Library

- `Engine/ST.Library.UI/NodeEditor/STNodeEditor.cs`
  - WinForms `Control`.
  - Owns `STNodeCollection`, active/selected/hover nodes, canvas offset/scale, drawing, hit testing, selection, connection interaction, serialization, and assembly/type loading.
  - Saves and loads the `.stn/.cvflow` canvas format.
  - Publishes `NodeAdded`, `NodeRemoved`, `ActiveChanged`, `SelectedChanged`, `OptionConnected`, and `OptionDisConnected`.
- `Engine/ST.Library.UI/NodeEditor/STNode.cs`
  - Existing reusable node model.
  - Stores position, size, title, options, mark text, lock flags, and node-specific property serialization.
  - Its `Owner` is currently typed as `STNodeEditor`, so node changes call back into the WinForms editor for invalidation, bounds updates, and line path rebuilds.
- `Engine/ST.Library.UI/NodeEditor/STNodeOption.cs`
  - Stores input/output port metadata and connected options.
  - Connection validation and data transfer are part of the reusable node model.
  - Notifies the owning `STNodeEditor` through the node owner chain.
- `Engine/ST.Library.UI/NodeEditor/STNodeTypeRegistry.cs`
  - Runtime registry for all loaded `STNode` subclasses.
  - Used by `STNodeEditor.LoadAssembly`, canvas loading, and node creation menus.

### Flow Execution Integration

- `Engine/FlowEngineLib/FlowEngineControl.cs`
  - Keeps a reference to `STNodeEditor`.
  - `AttachNodeEditor(STNodeEditor)` listens to `NodeAdded` and registers `BaseStartNode` and `CVBaseServerNode`.
  - Runtime load methods call `NodeEditor.LoadCanvas(...)`.
  - Execution starts from registered `BaseStartNode` instances.
- `Engine/ColorVision.Engine/Templates/Flow/STNodeEditorHelper.cs`
  - Depends on `STNodeEditor` for copy/paste, layout, flow validation, import-as-module, and property panel activation.
  - Also contains WinForms-only right-click menu code.

## Reusable Versus UI-Specific Code

Reusable without rewriting:

- `STNode`, `STNodeOption`, `STNodeOptionCollection`, node subclasses in `FlowEngineLib`, and node property descriptors.
- `.stn/.cvflow` save/load format in `STNodeEditor`.
- `FlowEngineControl` registration and execution behavior.
- `STNodeEditorHelper` logic for copy/paste, flow validation, auto layout, and module import.

WinForms-specific and targeted for replacement:

- Visible `STNodeEditor` rendering.
- Mouse/keyboard interaction routed through WinForms events.
- `ContextMenuStrip` node creation and node context menus.
- `WindowsFormsHost` focus workaround in `ViewFlow.Save`.

## Migration Strategy

The first WPF implementation uses a WPF editor surface backed by the existing node core:

1. Add `ST.Library.UI.NodeEditor.Wpf.WpfSTNodeEditor`.
2. Keep an internal non-visible `STNodeEditor` as `CoreEditor`.
3. Render nodes, options, grid, selection, and connections in WPF from `CoreEditor.Nodes` and `STNodeOption.ConnectedOption`.
4. Forward save/load, selection, active node, canvas offset/scale, and connection operations to `CoreEditor`.
5. Build WPF-specific input handling:
   - Left-click select and activate node.
   - Drag selected nodes.
   - Ctrl+left-drag or middle-drag pan.
   - Mouse wheel zoom at cursor.
   - Drag from an output/input option to another option to connect.
   - Delete removes selected nodes.
   - Context menu creates nodes from `STNodeTypeRegistry`.
6. Update `ViewFlow` to replace `WindowsFormsHost` with the WPF control.
7. Continue passing `CoreEditor` into `FlowEngineControl` and existing helper logic so serialization and execution remain compatible.

This approach removes the visible WinForms host first while keeping the node and flow runtime stable. A later deeper refactor can extract an editor interface from `STNodeEditor` and remove the hidden core if needed.

## Compatibility Requirements

- Existing saved flow data must continue to load and save byte-compatible `.stn/.cvflow` canvas data.
- Existing `FlowEngineLib` nodes must not need to be rewritten.
- `FlowEngineControl.AttachNodeEditor` must continue to receive a valid `STNodeEditor` during this phase.
- Property panel activation must still receive the selected `STNode`.
- Existing commands in `ViewFlow` must continue to work.

## Validation Checklist

- `dotnet build Engine/ST.Library.UI/ST.Library.UI.csproj`
- `dotnet build Engine/ColorVision.Engine/ColorVision.Engine.csproj`
- WPF editor loads an existing flow from base64/file.
- Save produces non-empty canvas data.
- Node creation through context menu works.
- Node selection, deletion, movement, pan, and zoom work.
- Option connection and disconnection update flow validation and save data.
- Auto layout still changes node positions and updates WPF rendering.
- Flow execution still finds a start node through `FlowEngineControl`.
