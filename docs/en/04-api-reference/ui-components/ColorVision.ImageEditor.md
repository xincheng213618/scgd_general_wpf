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

## Using It as a DLL

### When to Reference It

- A window needs to embed `ImageView` for interactive image viewing.
- Engine or project packages need to display algorithm results as ROI, POI, lines, rectangles, polygons, text, or curve overlays.
- The UI needs annotation import/export, pseudo-color, CIE diagrams, histograms, 3D surface viewing, or realtime image display.
- A special file type needs a custom opener or temporary toolbar.

### Extending Image Opening

1. Implement `IImageOpen`.
2. Declare supported file extensions and opening logic in the opener.
3. If toolbar behavior must be overridden, implement `IImageOpenEditorToolProvider`.
4. If lifecycle callbacks are needed, implement `IImageOpenEditorToolLifecycle`.
5. Open a real file and confirm `EditorToolFactory` scans and assembles the opener.

### Extending Drawing or Overlay

| Need | Preferred entry |
| --- | --- |
| Add a visual primitive | `Draw/`, `Abstractions/Draw/`, matching manager |
| Persist a visual primitive | `Draw/Annotations/`, `AnnotationMapper` |
| Add a toolbar tool | `EditorTools/`, `IEditorTool` |
| Add a context menu | `IIEditorToolContextMenu` or `IDVContextMenu` |
| Add result overlay | Reuse existing primitives and `DrawCanvas`, then connect the Engine result handler |

### Release Notes

`ImageEditor` contains shaders, CIE CSV, colormap images, icons, and OpenCV runtime dependencies. After publishing, verify normal image opening, CIE, pseudo-color, 3D, and annotation import/export at least once.

### DLL Release Acceptance

| Acceptance item | What to check | Pass condition |
| --- | --- | --- |
| Target framework | `ColorVision.ImageEditor.csproj` targets `net10.0-windows7.0` with `AnyCPU;x64` | The net10 host loads the DLL and x64 runtime has native dependencies. |
| Package metadata | `GeneratePackageOnBuild`, `PackageReadmeFile`, `README.md` | Package README and symbol package exist. |
| Core dependencies | `ColorVision.Common`, `ColorVision.Core`, `ColorVision.Themes`, `ColorVision.UI` | Shared DLL versions in the output match the ImageEditor build. |
| Image runtime | `OpenCvSharp4`, `OpenCvSharp4.runtime.win` | Normal images, TIF, large images, and video-related entries do not report missing runtime. |
| Resource files | `EditorTools/Filters/Shaders/*.ps`, `Assets/Colormap/colorscale_*.jpg`, `Assets/Data/CIE_cc_1931_2deg.csv` | Filters, pseudo-color, CIE background, and spectral locus render. |
| Tool discovery | `EditorToolFactory`, `IImageOpen`, `IEditorTool` | Settings page lists tools and openers; file opening hits the expected opener. |
| Visuals and overlay | `DrawCanvas`, `Draw/Annotations/`, `AnnotationMapper` | ROI, POI, lines, text, import/export, and coordinates match. |
| Advanced windows | CIE, histogram, 3D, realtime image | Each window opens once and is not blank. |

### Field First Checks

| Symptom | Check first | Judgement point |
| --- | --- | --- |
| Image area is blank | Opener selection, `SetImageSource(...)`, image format | Confirm whether `EditorToolFactory` assembled an `IImageOpen`. |
| TIF or large image fails to open | `OpenCvSharp4.runtime.win`, `ColorVision.Core` native dependencies | DLL load success does not prove native runtime is complete. |
| Toolbar or context menu entries are missing | `IEditorToolFactory`, tool visibility settings, reflection scan result | The settings page listing is the first check. |
| ROI/POI coordinates are offset | `DrawCanvas`, zoom scale, crop/rotate behavior | Then check whether the Engine result coordinate system was converted. |
| Pseudo-color or filter has no effect | Shader resources and `colorscale_*.jpg` | Missing resources are more common than algorithm errors. |
| CIE window is blank | `CIE_cc_1931_2deg.csv`, CIE image resources | Missing CSV or background image causes incomplete rendering. |
| 3D window is blank | `Window3D`, HelixToolkit, GPU/driver | Verify with a sample image and rotation/zoom before blaming data source. |
| Settings are not saved | `ImageViewConfig`, settings file permissions | Hide/show a tool and reopen an image to verify persistence. |

## Component Details And Handoff Checks

This section is organized around what a maintainer must verify after publishing `ColorVision.ImageEditor.dll` or debugging image UI issues.

### Runtime Component Matrix

| Component family | Key classes/windows | Source entry | Runtime role | Minimum acceptance |
| --- | --- | --- | --- | --- |
| Image host | `ImageView`, `ImageViewModel` | `UI/ColorVision.ImageEditor/ImageView.xaml(.cs)` | Image loading, status bar, toolbar, annotations, context menus. | Open PNG/JPG/TIF and verify zoom and pan. |
| Runtime context | `EditorContext`, `IEditorContextService` | `EditorContext.cs`, `Abstractions/IEditorContextService.cs` | Store current image, tools, canvas, services, and opener state. | Switching images clears previous opener tools. |
| Drawing canvas | `DrawCanvas`, `DrawEditorContext` | `DrawCanvas.cs`, `Draw/` | Display image, host visuals, hit testing, undo/redo. | Draw rectangle/line/text and undo/redo. |
| Tool factory | `IEditorToolFactory` | `EditorToolFactory.cs` | Reflectively assemble tools, context menus, openers, image components. | Settings page lists loaded tools and openers. |
| Openers | `IImageOpen`, `CommonImageOpen`, `Opentif`, `VideoOpen` | `Abstractions/IImageEditor.cs`, `Tif/`, `Video/` | Open images, TIF, video, and specialized files by extension. | Open one sample for each relevant opener. |
| Toolbar | `IEditorTool`, `IEditorToggleTool`, `IEditorCustomControlTool` | `Abstractions/IEditorTool.cs`, `EditorTools/` | Zoom, save, import/export, pseudo-color, filters, histogram, 3D. | Tools show, hide, click, and save configuration. |
| Context menus | `IDVContextMenu`, `IIEditorToolContextMenu` | `Abstractions/`, `EditorTools/*/*ContextMenu.cs` | Add context-aware menu items. | Image and tool context menus open without errors. |
| Visuals and annotations | `Draw/`, `AnnotationMapper`, `AnnotationDocument` | `Draw/`, `Draw/Annotations/` | ROI, POI, lines, text, rulers, and annotation import/export. | Export and re-import annotations with matching positions. |
| Pseudo-color and filters | `PseudoColorEditorTool`, `DisplayShaderFilterEditorTool` | `EditorTools/PseudoColor/`, `EditorTools/Filters/` | Show colormaps, thresholds, highlights, and shader filters. | Switch two colormaps and one shader filter. |
| CIE | `CieDiagramEditorTool`, `CieDiagramView` | `CieDiagramEditorTool.cs`, `Cie/` | Display CIE 1931/1976 diagrams and overlays. | CIE window opens with background and data points. |
| 3D | `View3DEditorTool`, `Window3D`, `ModelViewer3DControl` | `EditorTools/ThreeD/` | Image-surface 3D and OBJ/STL model viewing. | 3D scene is non-empty and can rotate/zoom. |
| Realtime | `RealtimeImageViewService`, `RealtimeFramePresenter` | `Realtime/` | Present realtime frames, frame stats, and camera overlays. | Continuous frame refresh does not block the UI. |
| Layers | `ImageLayerDescriptor`, `BitmapImageLayerController` | `Layers/` | Manage channel/layer switching and layer descriptors. | Multi-channel or layered image switching works. |
| Settings | `ImageViewSettingsWindow`, `ImageViewWorkspaceSettingsView` | `Settings/` | Manage tool visibility, defaults, and opener support list. | Tool visibility persists after reopening an image. |

### Package Resource Checks

`ColorVision.ImageEditor.csproj` packages several WPF resources. Verify package contents, not only the DLL.

| Resource | Project file entry | Missing symptom |
| --- | --- | --- |
| Shaders | `EditorTools/Filters/Shaders/*.ps` | Filters, pseudo-color highlighting, or threshold display fail. |
| Colormaps | `Assets/Colormap/colorscale_*.jpg` | Pseudo-color list is empty or switching fails. |
| CIE data | `Assets/Data/CIE_cc_1931_2deg.csv` | CIE background or spectral locus is incomplete. |
| Icons/images | `Assets/Image/*.ico`, `*.png` | CIE, title, or resource icons are missing. |
| OpenCV runtime | `OpenCvSharp4.runtime.win`, `ColorVision.Core` native dependencies | Images, video, or OpenCV tools fail to open. |

### Result Overlay Boundary

| Issue | Inspect ImageEditor | Inspect Engine/project |
| --- | --- | --- |
| Visual does not show or style is wrong | `Draw/` visuals, `DrawCanvas`, zoom/hit testing | Result handler visual parameters |
| Result coordinates are offset | Display scale, `Zoombox`, visual layout scale | Engine result coordinate system, crop/rotate/channel conversion |
| Annotation import/export fails | `Draw/Annotations/`, `AnnotationMapper` | Whether the project uses the same annotation format |
| Image opens but result layer is absent | Whether the opener set image context | `IViewResult` / `IResultHandleBase` registration |
| Project CSV lacks fields | Usually not ImageEditor | Project mapping, DAO, export logic |

ImageEditor is responsible for visualizing and interacting with results. Customer OK/NG decisions, MES fields, and CSV mappings belong to project or Engine result chains.

### Required Smoke Tests After Publishing

| Smoke test | Action | Pass condition |
| --- | --- | --- |
| Normal images | Open PNG/JPG/BMP/TIF. | Display, zoom, and pan work. |
| TIF/OpenCV | Open TIF or a large image. | No missing OpenCV runtime error. |
| Visual editing | Draw rectangle, line, text, undo/redo. | Visual position, selection box, and property bar agree. |
| Annotations | Export then re-import annotations. | Visual count and coordinates match. |
| Pseudo-color/filter | Switch two colormaps and one shader filter. | Display changes correctly without missing resources. |
| CIE | Open CIE tool. | Background, locus, and points render. |
| 3D | Open image 3D or model viewer. | Scene is non-empty and can rotate/zoom. |
| Result overlay | Open one real algorithm result. | ROI/POI/text layers align with the source image. |
| Settings | Hide/show one tool in ImageView settings. | Setting persists after reopening. |

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
