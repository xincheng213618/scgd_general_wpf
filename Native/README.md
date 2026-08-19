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

### Why `opencv_cuda.dll` is tracked

`x64/Release/opencv_cuda.dll` is a deliberate first-party release artifact, not an accidentally
committed build output. The ordinary GitHub-hosted Windows build is not guaranteed to provide the
exact CUDA 12.9 and supported Visual Studio integration required by `opencv_cuda.vcxproj`, and
`build.sln` intentionally does not build that project. Managed builds, NuGet packaging, and the
local release wrapper therefore consume the reviewed DLL from this stable path so that they do not
depend on a CUDA-capable build environment.

Do not remove the DLL merely because `x64/` is otherwise ignored. Removing it is safe only after a
clean GitHub Actions runner can build it before the managed solution, a cache miss always falls back
to a real CUDA build, and the local release workflow can either build or retrieve the same artifact.
The intended CI optimization is to cache only the generated DLL, keyed by the CUDA sources, shared
ABI headers, CUDA/OpenCV property sheets, OpenCV import libraries, and toolchain version; caching the
CUDA Toolkit itself is too large and the cache must never be the only way to obtain the DLL. Complete
the existing ABI checks and a GPU smoke test before accepting a newly generated binary and deleting
the tracked fallback. This decision was last reviewed on 2026-08-17.

`x64/Release/opencv_cuda.dll` is a tracked release input. Any change to
`include/cuda_export.h`, `include/custom_structs.h`, or the managed interop declarations under
`UI/ColorVision.Core` must be reviewed against that DLL. The repository checker structurally
validates exported function return/parameter types and calling conventions, the managed library
name and P/Invoke signatures, the AMD64 `HImage` field/pack/offset/size contract, and native log
callback delegates. CUDA string imports explicitly use `CharSet.Ansi`, and module-level attributes
that could silently alter other P/Invoke defaults are rejected across `ColorVision.Core` sources.
`HImage` uses an explicit native `pack(push, 8)` scope and managed `Pack = 8`;
the native build also asserts standard layout, one-byte `bool`, size, and every x64 offset. The
export build branch must expand `COLORVISIONCORE_API` to `__declspec(dllexport)`, and Release|x64
must define `OPENCVCUDA_EXPORTS`. The checker also reads the tracked PE export table directly and
never loads the DLL. Its default release mode additionally asks Visual Studio MSBuild to evaluate
the real Release|x64 `ClCompile`/`CudaCompile` items, including CUDA host definitions. A unique
temporary project imports the probe last, captures the items at the `ClCompile`/`CudaBuild`
consumption boundaries, and removes them before either compiler can run; the temporary project,
probe, and isolated output directory are always removed. This does not invoke `cl`/`nvcc` or require
a GPU, but it does fail closed when the matching CUDA BuildCustomizations are unavailable:

```powershell
python Scripts/verify_native_contracts.py
```

The ordinary Windows CI runner uses `--static-native-project-only`. That portable layer still
checks the source, project XML, PE exports and propagated package bytes, and prints that evaluated
MSBuild metadata was not verified; it is not a substitute for the default release-runner gate.

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
