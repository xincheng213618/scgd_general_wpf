# OpenCV and Native Integration Handoff

This page documents the real OpenCV/native boundary in the current repository. Engine has the `cvColorVision` SDK and native wrapper layer; UI/Core has P/Invoke wrappers for `opencv_helper.dll` and `opencv_cuda.dll`; media handling includes `.cvraw` / `.cvcie` parsing and thumbnails.

## Current Layers

| Layer | Folder or File | Responsibility |
| --- | --- | --- |
| Device SDK wrappers | `Engine/cvColorVision/` | Camera, spectrometer, sensor, OLED algorithm, MQTTMessageLib data types, native exports |
| UI/Core native wrappers | `UI/ColorVision.Core/` | `HImage`, `OpenCVMediaHelper`, `OpenCVCuda`, `ImageCompute`, native log bridge |
| File parsing/display | `Engine/ColorVision.Engine/Media/` | `.cvraw`, `.cvcie`, thumbnails, CIE export, mouse probe, image tools |
| Test project | `Test/opencv_helper_test/` | C++ validation project, currently focused on `M_FindLuminousArea` |
| Documentation | [cvColorVision](../../04-api-reference/engine-components/cvColorVision.md), [ColorVision.Core](../../04-api-reference/ui-components/ColorVision.Core.md) | Module boundaries and DLL publishing notes |

## Choose the Change Point

| Need | Primary Location |
| --- | --- |
| Add camera, spectrometer, or sensor SDK export | Matching wrapper under `Engine/cvColorVision/` |
| Add image processing function for WPF | `UI/ColorVision.Core/OpenCVMediaHelper.cs` or `OpenCVCuda.cs` |
| Add `.cvraw` / `.cvcie` open or thumbnail behavior | `Engine/ColorVision.Engine/Media/` |
| Change luminous-area, pseudo-color, SFR, white balance helper | Native `opencv_helper.dll` plus C# signature |
| Change CUDA fusion | `opencv_cuda.dll`, `OpenCVCuda`, `ImageCompute` |
| Validate native helper | `Test/opencv_helper_test/` |

## P/Invoke Rules

- C# signatures must match native exports: calling convention, encoding, struct layout, and release ownership.
- `HImage` carries a native buffer; release allocated output on failure.
- Helpers returning `IntPtr` strings must document whether `FreeResult()` is required.
- x64 is the primary delivery target; native DLLs, test project, and host platform must match.
- Validate `opencv_helper.dll`, `opencv_cuda.dll`, OpenCV runtime, and output folders together.
- Do not describe `cvColorVision` as a pure managed algorithm library; it is mainly native binding and message data types.

## `.cvraw` / `.cvcie` Chain

| Entry | Purpose |
| --- | --- |
| `FileCVCIE` | Reads CIE/RAW headers and image data |
| `CVRawOpen` | Opens `.cvraw` in the image editor with CIE probe and tools |
| `CVRawThumbnailProvider` | Generates thumbnails for `.cvraw` / `.cvcie` |
| `ColorVision.ShellExtension` | Windows Explorer thumbnail extension with separate package/register flow |

File format changes must validate host open, thumbnail, export, ShellExtension, and old file compatibility.

## Validation Commands

```powershell
dotnet build UI/ColorVision.Core/ColorVision.Core.csproj -c Release -p:Platform=x64
dotnet build Engine/cvColorVision/cvColorVision.csproj -c Release -p:Platform=x64
msbuild Test/opencv_helper_test/opencv_helper_test.vcxproj /p:Configuration=Debug /p:Platform=x64
Test/opencv_helper_test/build_test_find_luminous.bat
```

If the current machine lacks Visual Studio C++ or OpenCV native dependencies, record the reason and the build machine that will complete validation.

## Acceptance Checklist

| Item | Validation |
| --- | --- |
| P/Invoke signature | Debug/Release x64 load DLLs without `BadImageFormatException` or missing entry points |
| Memory | Repeated image processing does not grow process memory one-way |
| Image output | Size, channels, depth, stride, and color order are correct |
| File open | `.cvraw` / `.cvcie` open, thumbnails generate, old files do not crash |
| Algorithm helper | `M_FindLuminousArea` native tests pass and result JSON is explainable |
| Packaging | Host, plugins, or project packages include required native DLLs and runtime files |

## Related Documents

- [cvColorVision](../../04-api-reference/engine-components/cvColorVision.md)
- [ColorVision.Core](../../04-api-reference/ui-components/ColorVision.Core.md)
- [ColorVision.ShellExtension](../../04-api-reference/engine-components/ColorVision.ShellExtension.md)
- [Testing and Validation Handoff](../testing.md)
