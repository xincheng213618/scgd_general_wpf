# Native

This directory contains the repository's native C++ and CUDA source tree.

Current ABI, ownership, return-value and lifecycle guidance is in the
[native integration contract](../docs/02-developer-guide/engine-development/opencv-integration.md).
The English [helper API reference](./opencv_helper/API_Documentation.md) retains
per-function, calibration context, POI and focus details; it is not a generated
list of every export.

## Current layout

- `include/`: shared ABI headers consumed by the native projects
- `ColorVisionIcons64/`: native icon and resource DLL project used by the desktop app
- `opencv_helper/`: CPU and OpenCV-based native interop library
- `opencv_cuda/`: CUDA-accelerated native interop library
- `opencv_opengl/`: experimental OpenGL-related native module

## Boundaries

- Runtime DLL names remain unchanged: `opencv_helper.dll`, `opencv_cuda.dll`
- C# P/Invoke wrappers stay under `UI/ColorVision.Core`
- The Python backend remains outside this tree; canonical project knowledge is under `docs/`
- Third-party props and vendored packages are under the repository-level `packages/` directory

## Build prerequisites

Use Windows and Visual Studio Developer PowerShell with the C++ toolchain and
the OpenCV/SDK dependencies required by the project files. Keep native and
managed configurations/platforms aligned, normally Release/x64 for release
validation. A clean clone cannot assume that an existing helper DLL replaces
the native build prerequisites. Build commands write local outputs; running
native tests, using devices and publishing DLLs require separate authorization.

## CUDA ABI contract

### Why `opencv_cuda.dll` is tracked

The repository-root `x64/Release/opencv_cuda.dll` is a required, tracked
first-party input for managed builds, NuGet packaging and release. Do not delete
it merely because `x64/` is otherwise ignored. Replacing its reviewed bytes
requires a real CUDA build plus ABI/GPU smoke validation; removing the tracked
fallback additionally requires a reliable build/retrieval path for both CI and
local release. The rationale and acceptance conditions are maintained in the
native integration contract linked above.

Changes to `include/cuda_export.h`, `include/custom_structs.h` or managed interop
must be reviewed against the repository-root tracked DLL. The detailed checker
contract now lives in the native integration topic linked above, including
Pack8/one-byte-bool layout, calling conventions, export macros and package-byte
propagation. Do not weaken those checks to make a build pass.

Run the default gate from the repository root in Visual Studio Developer
PowerShell. It creates temporary files and evaluates Release/x64 MSBuild items;
it does not run cl/nvcc or load the DLL, but requires matching CUDA
BuildCustomizations:

```powershell
python Scripts/verify_native_contracts.py
```

The ordinary CI mode `--static-native-project-only` omits evaluated MSBuild
metadata, so it is not a substitute for the default gate. PE exports do not prove
C parameter types or that an arbitrary replacement DLL came from these sources;
accepting new tracked bytes still needs a real CUDA build and ABI/GPU smoke
validation. A source/documentation check does not authorize replacing the DLL.
