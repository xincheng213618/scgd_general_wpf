# Native subsystem guidance

This file adds to the repository-root `AGENTS.md` for work under `Native/`.

- Build native projects with the installed Visual Studio C++ toolchain and keep configuration/platform pairs aligned, normally `Release|x64` for release validation.
- Preserve the public ABI: exported names, calling conventions, parameter and structure layout, memory ownership and matching release functions, and the runtime DLL names `opencv_helper.dll` and `opencv_cuda.dll` are compatibility contracts.
- Implement OpenCV algorithms under `Native/opencv_helper/**`, declare exports in `Native/include/opencv_media_export.h`, expose managed calls through `UI/ColorVision.Core/OpenCVMediaHelper.cs`, and cover them in `Test/opencv_helper_test/`.
- The BMW 4-in-1 SFR entry point is `M_CalSFRBmw4In1`; its implementation belongs in `Native/opencv_helper/algorithm/sfr/sfr_bmw4.*`.
- Validate the changed native project and its closest caller/test. Run these commands from Visual Studio Developer PowerShell:

```powershell
msbuild .\Native\opencv_helper\opencv_helper.vcxproj /p:Configuration=Release /p:Platform=x64
msbuild .\Test\opencv_helper_test\opencv_helper_test.vcxproj /p:Configuration=Release /p:Platform=x64
```

- Treat compiler, OpenCV, CUDA, and device-SDK availability as environmental dependencies. Report a missing dependency precisely instead of weakening the project configuration to bypass it.
