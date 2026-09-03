# OpenCV native SDK build

This vendored SDK was built from the official OpenCV 4.14.0 tags:

- [`opencv` 4.14.0](https://github.com/opencv/opencv/tree/4.14.0): `0654a42e19215ef25b1d367d822f3c630447e7c7`
- [`opencv_contrib` 4.14.0](https://github.com/opencv/opencv_contrib/tree/4.14.0): `a8e9acd62cabd30419dba83007f2ac0d07de5e2c`
- Generator: Visual Studio 18 2026, x64, shared libraries

The important CMake options are:

```text
OPENCV_ENABLE_NONFREE=ON
WITH_IPP=ON
WITH_OPENCL=ON
OPENCV_DNN_OPENCL=ON
WITH_FFMPEG=ON
WITH_DSHOW=ON
WITH_MSMF=ON
CV_ENABLE_INTRINSICS=ON
CPU_BASELINE=SSE3
CPU_DISPATCH=SSE4_1;SSE4_2;AVX;FP16;AVX2;AVX512_SKX
BUILD_SHARED_LIBS=ON
BUILD_opencv_world=OFF
BUILD_TESTS=OFF
BUILD_PERF_TESTS=OFF
BUILD_EXAMPLES=OFF
```

`WITH_TBB` and `WITH_OPENMP` are intentionally disabled because this Windows build uses the Concurrency parallel backend. `ENABLE_FAST_MATH` and `ENABLE_LTO` are disabled to preserve numerical behavior and toolchain compatibility. OpenCV's own CUDA modules are disabled; ColorVision provides its separate CUDA bridge in `Native/opencv_cuda`.

The repository keeps 13 directly linked module import libraries for Release and Debug. The runtime set also contains `opencv_dnn4140.dll` and `opencv_dnn4140d.dll`, which are transitive dependencies of the corresponding `opencv_video` and `opencv_ximgproc` DLLs. The FFmpeg plugin is shared by both configurations and has no debug suffix.
