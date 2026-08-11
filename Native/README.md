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

## CUDA ABI contract

`x64/Release/opencv_cuda.dll` is a tracked release input. Any change to
`include/cuda_export.h`, `include/custom_structs.h`, or the managed interop declarations under
`UI/ColorVision.Core` must be reviewed against that DLL. The repository checker structurally
validates exported function return/parameter types and calling conventions, the managed library
name and P/Invoke signatures, the AMD64 `HImage` field/pack/offset/size contract, and native log
callback delegates. It also reads the tracked PE export table directly. The check never loads the
DLL, so it does not require a GPU, CUDA Toolkit, `dumpbin`, or another Python package:

```powershell
python Scripts/verify_native_contracts.py
```

PE export metadata does not contain C parameter types. The checker therefore combines a strict
source-side static ABI contract with exact reviewed-DLL byte propagation; it does not claim to
prove that an arbitrary replacement DLL was built from the current sources. Regenerating the
tracked DLL still requires the CUDA build plus ABI/GPU smoke validation before its new bytes are
accepted.

CI mutation tests prove that the guarded source ABI changes are rejected and additionally verify
the generated `ColorVision.Core` NuGet package. The main release scripts verify the copied runtime
DLL and the final full update ZIP against the same tracked bytes; the full-ZIP path is covered by a
`create_full_zip` integration regression.

## Migration note

This is phase 1 of the native layout cleanup. A later phase can decide whether to move native-only third-party assets under `Native/` as well.
