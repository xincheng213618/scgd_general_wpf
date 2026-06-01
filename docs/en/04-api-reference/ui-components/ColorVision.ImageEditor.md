# ColorVision.ImageEditor

This page only describes the main control chain and extension points currently implemented in UI/ColorVision.ImageEditor, no longer continuing the old documentation's writing style of "feature encyclopedia + tutorial examples + performance number promises."

## Module Positioning

ColorVision.ImageEditor is currently not a simple image display control, but a combined module of "image host + zoomable canvas + drawing tools + openers + runtime tool overlays + settings system."

Its main line is closer to:

- `ImageView` as the host
- `EditorContext` as the current view runtime container
- `DrawCanvas` as the real drawing canvas
- `IEditorToolFactory` responsible for discovering and assembling tools, menus, openers
- `IImageOpen` responsible for opening chains for different file types

## Most Critical Directories

From the project directory, the most worthwhile to read first are:

- `ImageView.xaml(.cs)`: Main control and runtime orchestration entry point
- `EditorContext.cs`: Runtime container for each view instance
- `DrawCanvas.cs`: Visual tree and drawing canvas
- `EditorToolFactory.cs`: Tool, menu, opener discovery and assembly
- `Abstractions/`: Editor extension point boundaries
- `Draw/`: Primitives, tools, selection boxes, annotation import/export
- `EditorTools/`: Non-primitive tools such as zoom, pseudo-color, 3D, algorithm, fullscreen
- `Video/`: Video opener
- `Layers/`: Layer/channel switching semantics
- `Realtime/`, `Settings/`: Real-time image and settings related support

## Key Entry Point Types

### ImageView

`ImageView` is the main entry point of the current editor module. It is responsible for:

- Initializing `EditorContext`
- Binding `DrawCanvas`, `Zoombox`, context menus, and status bar
- Executing `IImageComponent` one-time initialization
- Managing standard file commands
- Wiring configuration changes, zoom changes, and overlay refreshes
- Handling image open, cleanup, save, annotation import/export, and other flows

If you want to understand this module, the primary entry point is `ImageView.xaml.cs`.

### EditorContext

`EditorContext` is the runtime container for each `ImageView` instance, currently centrally storing:

- Current `ImageView`
- `DrawCanvas`
- `Zoombox`
- `ImageViewConfig`
- `DrawEditorManager`
- `IEditorToolFactory`
- Current opener, context menu, primitive list
- A set of lightweight service registries

It is both a runtime state container and carries some local service locator characteristics, which is also an important implementation boundary of the current module.

### DrawCanvas

`DrawCanvas` is the real drawing hosting layer. It is not just a display control, but also responsible for:

- Maintaining the visual object collection
- Performing hit testing
- Handling primitive addition and removal
- Maintaining undo/redo
- Serving as the target for mouse events hooked by a large number of drawing tools

### IEditorToolFactory

Although named `IEditorToolFactory`, it is currently a concrete class, not an interface. During construction, it reflectively scans and assembles:

- `IDVContextMenu`
- `IIEditorToolContextMenu`
- `IEditorTool`
- `IImageComponent`
- `IImageOpen`

Additionally, it maintains the active view of "global tools + current opener runtime tools" and rebuilds the toolbar with `GuidId`-based overrides.

This is also where the initialization cost and extension capability of the current ImageEditor are most concentrated.

### IImageOpen and Its Extension Interfaces

The current open chain does not rely on a unified file manager, but is handled by individual `IImageOpen` implementations.

Additionally, `IImageOpen` can also optionally implement:

- `IImageOpenEditorToolProvider`
- `IImageOpenEditorToolLifecycle`

This allows certain special file types to temporarily take over or override toolbar capabilities after opening, rather than stacking all branches into global tools.

### VideoOpen / Window3D / ModelViewer3DControl

Video and 3D capabilities are currently real sub-features within the editor module, but they are additional openers or tools, not the sole main line of the entire module:

- `Video/VideoOpen.cs`: Video opener
- `EditorTools/ThreeD/Window3D.xaml.cs`: Image-to-3D surface window
- `EditorTools/ThreeD/ModelViewer3DControl.xaml.cs`: OBJ/STL viewer control

Old documentation expanded these capabilities heavily, but a more reliable reading approach is still to first understand the `ImageView` and tool factory main chain.

## Current Runtime Main Chain

The existing control chain is roughly:

1. Create `ImageView`.
2. Initialize `EditorContext`, `SelectionVisual`, `CompactInspectorPresenter`.
3. Create `IEditorToolFactory` and reflectively assemble global tools, context menus, image components, openers.
4. Execute all `IImageComponent.Execute(ImageView)`.
5. After the user opens a file, select `IImageOpen` based on extension.
6. The opener calls `SetImageSource(...)` and optionally provides its own runtime tools.
7. `DrawCanvas`, overlays, status bar, layer switching, pseudo-color, etc., continue working around the current image context.

## What Boundaries the Current Implementation Has

### ImageView Is Not a Pure Display Control

`SetImageSource(...)` currently does not just set `ImageShow.Source` — it may also trigger pseudo-color configuration, calibration services, and other editor runtime side effects.

For pure display scenarios, note toggles like `EnableEditorImageServices` and do not default to treating the entire ImageView as a side-effect-free picture box.

### Tool Discovery Is Reflection-Driven

`IEditorToolFactory` currently performs multiple rounds of scanning and creation for each view instance. This is a real and important control chain. The old documentation's "static tool architecture diagram" would obscure this fact.

### EditorContext Is Both a State Container and Has Service Locator Properties

The current design does not completely decouple "configuration," "tool state," and "runtime services" — instead, they are partially concentrated in `EditorContext`, `ImageViewConfig`, and a small number of services. This is the current reality that must be acknowledged when reading and during subsequent refactoring.

### Annotation Import/Export Already Has Actual Landing Points

Primitive persistence is not just at the conceptual level; it currently actually lands in `Draw/Annotations/` and the import/export entry points of `ImageView`. When reading annotation capabilities, you should look directly at this chain rather than generalizing it as "any drawing tool can auto-persist."

## How to Better Read This Module Currently

### To View the Main Control Chain and Initialization Orchestration

Read first:

- `ImageView.xaml.cs`
- `EditorContext.cs`
- `EditorToolFactory.cs`

### To View Drawing and Selection Logic

Read first:

- `DrawCanvas.cs`
- `Draw/`
- `Abstractions/Draw/`

### To View the File Opening Chain and Runtime Tool Overrides

Read first:

- `Abstractions/IImageEditor.cs`
- `Video/VideoOpen.cs`
- Directories where specific opener implementations reside

### To View Sub-Capabilities Like Annotations, Pseudo-Color, 3D

Read first:

- `Draw/Annotations/`
- `EditorTools/PseudoColor/`
- `EditorTools/ThreeD/`

## What This Page No Longer Does

This page no longer continues to maintain these high-risk contents:

- Extensive performance number promises
- Tutorial-style example sets covering the entire module
- Writing video or 3D as the sole main line of the entire module
- Fabricating unified view models or abstract interfaces that do not match current code

## Continue Reading

- [UI Components Overview](./README.md)
- [ColorVision.UI](./ColorVision.UI.md)
- [ColorVision.Themes](./ColorVision.Themes.md)