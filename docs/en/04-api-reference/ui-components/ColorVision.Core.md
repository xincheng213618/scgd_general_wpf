# ColorVision.Core

This page only describes the native interop layer currently implemented in UI/ColorVision.Core, no longer continuing the old documentation's "high-level image API manual" and non-existent managed method examples.

## Module Positioning

ColorVision.Core is currently closer to a native image and video capability bridging layer, primarily responsible for:

- Defining cross-managed/unmanaged boundary data structures like `HImage`
- Calling `opencv_helper.dll` and `opencv_cuda.dll` via P/Invoke
- Providing WPF-side bitmap conversion and update helpers
- Exposing native entry points for pseudo-color, image enhancement, focus evaluation, video, etc.

It is not a well-packaged high-level image processing framework. Many capabilities are still native export wrappers at the `extern` method level.

## Most Critical Files

From the project directory, the most worthwhile to read first are:

- `HImage.cs`: Image data structure
- `HImageExtension.cs`: Bridge between `HImage` and WPF image objects
- `OpenCVMediaHelper.cs`: Main collection of native export wrappers
- `OpenCVCuda.cs`: CUDA-related native entry points
- `ColormapTypes.cs`: Pseudo-color enumeration
- `NativeLogBridge.cs`: Native log bridge
- `nvcuda.cs`: CUDA-related P/Invoke definitions

## Key Entry Point Types

### HImage

`HImage` is currently not the managed class with extensive instance methods found in old documentation, but a struct carrying native image buffers. Its core fields include:

- `rows`
- `cols`
- `channels`
- `depth`
- `stride`
- `pData`

It also implements `Dispose()`, responsible for releasing image memory allocated by `Marshal.AllocHGlobal`.

This means one of the most important responsibilities of the current module is safely passing image buffers across the native/managed boundary.

### HImageExtension

`HImageExtension` provides bridging helpers rather than a complete processing algorithm library. It is currently primarily responsible for:

- Deriving `PixelFormat` from channel count and bit depth
- Copying `HImage` content to `WriteableBitmap`
- Providing async bitmap update paths
- Assisting in converting native image data into WPF-displayable objects

Therefore, its value is primarily in the display chain, not the algorithm chain.

### OpenCVMediaHelper

Although named `OpenCVMediaHelper`, it currently carries a large number of `opencv_helper.dll` export wrappers, not just video-related interfaces, also including:

- Pseudo-color and auto-range pseudo-color
- Min/max value extraction
- Auto brightness, auto color, auto hue
- Channel extraction
- Brightness/contrast, Gamma, invert, threshold, sharpen, filter, edge detection
- SFR and focus evaluation
- Several recognition or detection type entry points
- Video-related structures and functions

So a more accurate understanding is: it is the main native image capability export surface, not just a "video helper class."

### OpenCVCuda

`OpenCVCuda` is currently not the general CUDA device management layer claimed in old documentation. What it currently exposes is a small number of `opencv_cuda.dll` exports, focused on fusion-related entry points, for example:

- `CM_Fusion`
- `CM_Fusion_Async`
- `CM_Fusion_Batch`

Therefore, when describing CUDA capabilities, write based on current actual exports and do not expand it into a complete GPU capability master entry point.

### ColormapTypes and NativeLogBridge

- `ColormapTypes` handles unified pseudo-color mapping enumeration.
- `NativeLogBridge` handles bridging native-side logs to the managed logging system.

Both files are very small, but they are important boundary points for the pseudo-color chain and debug chain respectively.

## Current Runtime Main Chain

This module is currently closer to the following chain:

1. Upper-layer modules call `OpenCVMediaHelper` or `OpenCVCuda` via P/Invoke.
2. Native DLLs return `HImage` or write to `HImage` output parameters.
3. The WPF display chain updates image data to `WriteableBitmap` via `HImageExtension`.
4. Upper-layer modules like `ColorVision.ImageEditor` continue to do interaction, drawing, and display around these bitmaps.

## Using It as a DLL

### When to Reference It

- Upper image modules need to exchange image buffers with `opencv_helper.dll`.
- Native image data must be converted to WPF `WriteableBitmap`.
- Pseudo-color, image enhancement, filtering, thresholding, focus evaluation, SFR, or fusion native methods are required.
- Native logs need to be bridged into managed logging.

### DLL Release Acceptance

| Check | What to Inspect | Passing Standard |
| --- | --- | --- |
| Target framework outputs | `net8.0-windows7.0`, `net10.0-windows7.0` | Both TFMs produce DLL, `.nupkg`, and `.snupkg` |
| Native runtime | NuGet package and host output | `opencv_helper.dll` and OpenCV 4130 DLLs are under `runtimes/win-x64/native` or final output |
| Optional CUDA package | `opencv_cuda.dll` | Included when the source file exists; non-CUDA paths do not fail when it is absent |
| P/Invoke entry points | `OpenCVMediaHelper`, `OpenCVCuda`, `NativeLogBridge` | No `DllNotFoundException`, `EntryPointNotFoundException`, or x86/x64 mismatch |
| Image memory | `HImage.Dispose()`, `Marshal.FreeHGlobal` | Batch conversions release memory and repeated image opens do not keep growing |
| WPF display bridge | `HImageExtension` | `HImage` dimensions, channels, depth, stride, and WPF `PixelFormat` line up |
| Upper-layer regression | `ColorVision.ImageEditor` | At least one image can open, render, and refresh after pseudo-color or enhancement |

### Field First Checks

| Symptom | First Check |
| --- | --- |
| Startup or call fails with `DllNotFoundException` | Check `runtimes/win-x64/native` and the main app output for native DLLs |
| `BadImageFormatException` appears | Check that host, plugins, and native DLLs are all x64 |
| Image is black or colors are shifted | Check `HImage` rows, cols, channels, depth, stride, and WPF `PixelFormat` derivation |
| Memory grows after batch processing | Check whether `HImage.Dispose()` is called and who owns native output buffers |
| CUDA fusion is unavailable | Check `opencv_cuda.dll`, NVIDIA driver, CUDA runtime, and whether a non-CUDA fallback exists |
| Native logs do not reach managed logs | Check `NativeLogBridge` initialization order and whether helper/cuda DLLs loaded successfully |

## What Boundaries the Current Implementation Has

### Do Not Write It as a High-Level OO API

The current code does not have these typical high-level interfaces written in old documentation:

- `HImage.Load(...)`
- `HImage.ToBitmapSource()`
- `OpenCVCuda.GetCudaDeviceCount()`
- `OpenCVCuda.IsCudaAvailable()`

Such writing would mislead readers into searching for non-existent managed wrappers.

### HImage's Resource Semantics Are Important

`HImage` is not an ordinary managed object — it contains unmanaged pointers and explicit release logic. When discussing this module, memory and ownership boundaries are more important than "class design."

### Upper-Layer Business Semantics Are Not Here

Core only handles bridging native capabilities and does not handle toolbars, interactions, or document state orchestration like ImageEditor does. When reading, be clear that it is only the lower-level capability foundation.

## How to Better Read This Module Currently

### To View Image Data Structure and Display Bridging

Read first:

- `HImage.cs`
- `HImageExtension.cs`

### To View Native Export Surface

Read first:

- `OpenCVMediaHelper.cs`
- `OpenCVCuda.cs`

### To View Pseudo-Color and Log Boundaries

Read first:

- `ColormapTypes.cs`
- `NativeLogBridge.cs`

## What This Page No Longer Does

This page no longer continues to maintain these high-risk contents:

- Non-existent managed high-level method examples
- Writing `OpenCVCuda` as a complete device management layer
- Extensive update logs and version lists
- Describing Core as a complete upper-layer image processing framework

## Continue Reading

- [UI Components Overview](./README.md)
- [ColorVision.ImageEditor](./ColorVision.ImageEditor.md)
