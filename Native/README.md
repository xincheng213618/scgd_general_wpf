# Native

This directory contains the repository's native C++ and CUDA source tree.

## Current layout

- `include/`: shared ABI headers consumed by the native projects
- `ColorVisionIcons64/`: native icon and resource DLL project used by the desktop app
- `opencv_helper/`: CPU and OpenCV-based native interop library
- `opencv_cuda/`: CUDA-accelerated native interop library
- `opencv_opengl/`: experimental OpenGL-related native module

## Boundaries

- Runtime DLL names remain unchanged: `opencv_helper.dll`, `opencv_cuda.dll`
- C# P/Invoke wrappers stay under `UI/ColorVision.Core`
- Python backend and documentation remain outside this tree
- Third-party props and vendored packages remain under the repository-level `packages/` directory in this first migration phase

## Migration note

This is phase 1 of the native layout cleanup. A later phase can decide whether to move native-only third-party assets under `Native/` as well.