# cvColorVision

This page only describes the `cvColorVision` module actually available in the current repository, no longer maintaining the old "feature promotion + extensive fictional examples + pure managed algorithm library" draft.

## What This Module Is Now

Based on current source code status, `cvColorVision` is not a module primarily implementing business algorithms in C#, but a thick native interop bridge. Its most core role is currently:

- Exposing capabilities of `cvCamera.dll` and `cvOled.dll` to C# via `DllImport`.
- Centralizing low-level interfaces for camera, color, chart cards, source meters, OLED algorithms, etc. into a unified namespace.
- Providing thin wrapper call surfaces for `ColorVision.Engine`, plugins, and device services.

Therefore, it is closer to a "native capability binding layer" rather than the pure managed vision framework described in old documentation.

## Most Critical Files

- `Engine/cvColorVision/cvCameraCSLib.cs`
- `Engine/cvColorVision/ConvertXYZ.cs`
- `Engine/cvColorVision/CvOledDLL.cs`
- `Engine/cvColorVision/PG.cs`
- `Engine/cvColorVision/PassSx.cs`
- `Engine/cvColorVision/Algorithms.cs`

If you just want to understand how the module interfaces with underlying DLLs and what capabilities are currently exposed, these files already cover the main body.

## How the Current Control Surface Is Partitioned

### Camera and General Vision Interface

`cvCameraCSLib.cs` is the current largest binding surface. Based on the code, it covers not just camera on/off, but a total collection of a large number of native entry points, including:

- Camera open, close, live preview, frame capture
- Configuration JSON read/write
- Auto exposure, ROI, callback registration
- XYZ/xy/uv/CCT/Wave sampling
- TIFF export and data split/merge
- Auto focus, lens position, Canon-related controls
- Various vision inspection and image processing functions

Therefore, this is not a small wrapper with only a few dozen camera APIs, but the current densest P/Invoke convergence point.

### Color and Chromaticity Sampling

`ConvertXYZ.cs` further breaks down the XYZ-related entry points of `cvCamera.dll` into a more focused binding surface, currently primarily around:

- XYZ buffer initialization and release
- Circle / Rect region sampling
- xyz, uv, CCT, dominant wavelength, etc. export
- Batch point sampling

This shows that the current color sampling chain is not an independent C# calculator, but runs around native buffers and sampling functions.

### OLED-Specific Algorithms

`CvOledDLL.cs` currently specifically binds `cvOled.dll`, providing:

- Parameter loading
- Image reading
- Pixel point search
- Pixel reconstruction
- Moiré filtering

Therefore, OLED-related capabilities are currently a separate DLL surface, not implemented mixed within the camera interface.

### Chart Cards and Peripheral Interfaces

`PG.cs` is currently a thin wrapper for chart card device control, providing:

- PG initialization
- TCP/serial connection
- Start / Stop / Reset
- Up/down switching and specific frame switching

`PassSx.cs` provides native call wrappers for source meter/power supply, covering:

- Opening and closing devices
- Setting source mode
- Setting 2-wire / 4-wire and front/rear ports
- Reading voltage and current
- Executing stepping and scanning

This shows that `cvColorVision` currently does not only handle image processing, but also takes on low-level bindings for multiple types of peripherals.

### Very Thin Algorithm Entry Points

Files like `Algorithms.cs` demonstrate another characteristic of the module: some wrappers are very thin, simply exposing individual low-level functions in the most direct form.

So the responsibility of this layer is not to uniformly design all API styles, but to map low-level capabilities as completely as possible.

## Handoff Acceptance

When taking over this module, the main task is to verify that managed declarations, native DLLs, and device workflows still agree:

| Check | Where to Look | Passing Standard |
| --- | --- | --- |
| Native DLLs are present | `cvCamera.dll`, `cvOled.dll`, and dependencies | Release/x64 output can load the DLLs without `DllNotFoundException` |
| Platform bitness matches | Project platform, DLL bitness, `DllImport` declarations | The x64 main path avoids `BadImageFormatException`, and calling conventions/entry names match |
| Camera baseline chain | `cvCameraCSLib.cs` | Initialization, enumeration/open, frame capture, close, and release run through the real device flow |
| XYZ sampling chain | `ConvertXYZ.cs` | `CM_InitXYZ`, `CM_SetBufferXYZ`, sampling calls, `CM_ReleaseBuffer`, and `CM_UnInitXYZ` are used in a clear order |
| OLED algorithm chain | `CvOledDLL.cs` | `CvOledInit`, `CvLoadParam`, image load/point search/rebuild, and `CvOledRealse` are verified with one parameter set |
| PG chart-card chain | `PG.cs` | Initialization, connection, Start/Stop/Reset, up/down switch, or specific-frame switch can be called by device services |
| Source meter/power chain | `PassSx.cs` | Open, set source mode, read voltage/current, step/sweep, and close have an explicit call order |
| Spectrometer chain | `Spectrometer.cs` | `CM_CreateEmission`, initialization, wavelength/calibration file loading, data read, and release are verified as paired operations |
| Error-code translation | `CM_GetErrorMessage(...)` | Native return codes are not swallowed; logs or upper-level exceptions contain diagnosable information |

## Change Boundary

| Change Type | Should This Module Change | Notes |
| --- | --- | --- |
| DLL entry names, parameters, calling conventions, or struct layouts change | Yes | This is the module's core boundary; device or minimal native smoke testing is required |
| Template judgment or OK/NG business rules after capture change | Usually no | Start with `ColorVision.Engine/Templates`, project bundles, and flow nodes |
| CVCIE/CVRAW file-format changes | Usually no | Start with `ColorVision.FileIO`; this module only exposes native capabilities |
| WPF buttons, menus, or image overlay display changes | Usually no | Start with UI, ImageEditor, and result display chains |
| A new customer project needs existing native capability | Maybe | Reuse existing declarations first; extend here only when the DLL adds entries or signature changes |

## First Checks

| Symptom | First Check |
| --- | --- |
| Startup or call fails with `DllNotFoundException` | Check whether the DLL and all dependencies are published to the x64 output directory |
| `EntryPointNotFoundException` occurs | Check `EntryPoint`, DLL version, and vendor-exported symbols |
| `BadImageFormatException` occurs | Check x86/x64 mixing first, then AnyCPU configuration |
| Call crashes or raises `AccessViolationException` | Check `DllImport` parameter types, array lengths, pointer lifetimes, and release order |
| XYZ, CCT, xy/uv values are clearly wrong | Check whether `CM_SetBufferXYZ` rows/cols/bpp/channels match the sampling area |
| PG or source meter does not respond | Check connection mode, port/IP, Start/Stop order, and whether device services swallow native return codes |

## Most Common Mistakes to Avoid

### It Is Not a Pure C# Algorithm Center

Most key capabilities currently come from native DLLs, with C# code primarily handling declarations, light auxiliary wrappers, and data type bridging. Continuing to write it as "main algorithm implementation in managed layer" would invert the real code structure.

### `cvCameraCSLib` Does Not Only Handle Camera

The filename easily leads to misjudgment, but it currently actually exposes many color sampling, image processing, auto focus, and detection functions, making it one of the overall binding entry points.

### The Interface Granularity Here Is Not Uniform

Some files like `cvCameraCSLib.cs` are very thick, while some like `Algorithms.cs`, `PG.cs`, `CvOledDLL.cs` are very thin. Documentation should no longer rigidly write them into a neatly uniform layered API system.

### It Is More Like a Foundation Layer "Called by Upper Layers"

Currently `ColorVision.Engine`, device services, and some plugins call the native interfaces exposed here; `cvColorVision` itself does not handle host-level windows, templates, or workflow orchestration.

## Recommended Reading Order

1. `Engine/cvColorVision/cvCameraCSLib.cs`
2. `Engine/cvColorVision/ConvertXYZ.cs`
3. `Engine/cvColorVision/CvOledDLL.cs`
4. `Engine/cvColorVision/PG.cs`
5. `Engine/cvColorVision/PassSx.cs`

This allows seeing the thickest overall binding surface first, then expanding to OLED, chart card, and source meter dedicated interfaces.

## Continue Reading

- [docs/04-api-reference/engine-components/ColorVision.Engine.md](./ColorVision.Engine.md)
- [docs/03-architecture/overview/system-overview.md](../../03-architecture/overview/system-overview.md)
- [docs/04-api-reference/engine-components/ColorVision.FileIO.md](./ColorVision.FileIO.md)
