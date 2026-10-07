# OpenCV native SDK build

This SDK uses the official [OpenCV 5.0.0](https://github.com/opencv/opencv/tree/5.0.0) and [opencv_contrib 5.0.0](https://github.com/opencv/opencv_contrib/tree/5.0.0) sources. Both Debug and Release use Visual Studio 18 2026, x64, MSVC v145 (14.51), C++17 and shared OpenCV libraries. The installed compiler directory is `x64/vc18`; the directory name describes the toolchain, not the OpenCV version.

The source archives have these SHA-256 hashes:

| Archive | SHA-256 |
| --- | --- |
| OpenCV 5.0.0 | `E2ABAA1EA2443FB163299369506521518EE02D8E51B88A144A128C4E492123BD` |
| opencv_contrib 5.0.0 | `EFBB9526083FEAFB2BC38EBC365654D30C62D280975A9275266D6A0470F231E4` |

`ColorVision-build.cmake` records the inherited feature settings. It retains IPP, OpenCL, nonfree algorithms, FFmpeg, DirectShow, Media Foundation, SSE3 baseline and SSE4.1/SSE4.2/AVX/FP16/AVX2/AVX512 dispatch. The Windows parallel backend remains Concurrency; TBB and OpenMP stay disabled. Fast math and LTO are disabled. Precompiled headers are disabled because the initial OpenCV 5 build produced invalid PCH/internal compiler errors; runtime optimization remains enabled. Configuration verification checks that requested codecs and backends are available.

OpenCV 5 needs an external OpenEXR installation. This build uses static [OpenEXR 3.4.16](https://github.com/AcademySoftwareFoundation/openexr/tree/v3.4.16) and [Imath 3.2.2](https://github.com/AcademySoftwareFoundation/Imath/tree/v3.2.2), with `/MD` for Release and `/MDd` for Debug. OpenEXR's OpenJPH and libdeflate dependencies are also static. Build and install both configurations before configuring OpenCV; pass that installation through `CMAKE_PREFIX_PATH`.

Example from a Visual Studio Developer PowerShell, with the downloaded sources under `$nativeSdkRoot`:

```powershell
$nativeSdkRoot = 'C:\Users\17917\Desktop\opencv 500'
$nativeSettings = Join-Path (Get-Location) 'packages\opencv\ColorVision-build.cmake'
cmake -S "$nativeSdkRoot\opencv-5.0.0" -B "$nativeSdkRoot\build" -G 'Visual Studio 18 2026' -A x64 -T 'v145,host=x64' -C "$nativeSettings" "-DOPENCV_EXTRA_MODULES_PATH=$nativeSdkRoot\opencv_contrib-5.0.0\modules" "-DCMAKE_PREFIX_PATH=$nativeSdkRoot\openexr-install" "-DCMAKE_INSTALL_PREFIX=$nativeSdkRoot\install-vc18"
if ($LASTEXITCODE -ne 0) { throw 'OpenCV configuration failed.' }
foreach ($nativeConfiguration in @('Release', 'Debug')) {
    cmake --build "$nativeSdkRoot\build" --config $nativeConfiguration --parallel 6
    if ($LASTEXITCODE -ne 0) { throw "OpenCV $nativeConfiguration build failed." }
    cmake --install "$nativeSdkRoot\build" --config $nativeConfiguration
    if ($LASTEXITCODE -ne 0) { throw "OpenCV $nativeConfiguration installation failed." }
}
```

The full external installation contains 53 modules. The repository keeps installed headers and the modules declared in `OpenCV.Release.x64.props` / `OpenCV.Debug.x64.props`: 16 directly linked modules plus the transitive DNN runtime, and the shared FFmpeg plugin. Debug DLLs/import libraries have a `d` suffix; FFmpeg is shared by both configurations. `OpenCV-ThirdParty-Licenses.txt` accompanies the delivered binaries.

OpenCV's CUDA modules remain disabled. ColorVision's separate `opencv_cuda` project uses CUDA 13.2 and v145; its GPU targets start at Turing (compute capability 7.5), with SASS through Blackwell and forward PTX. CUDA 13.2 no longer builds the previous Pascal/Volta targets. Exported C interfaces and neutral pixel buffers remain the boundary between OpenCvSharp, the native helper and vendor libraries.
