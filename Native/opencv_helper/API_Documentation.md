# opencv_helper.dll API Documentation

The complete English function reference is maintained in
[opencv_helper API reference](../../docs/04-api-reference/engine-components/opencv-helper-api.md).
It includes image processing, SFR, detection, calibration/POI, video, parameter
units, return values and matching release functions. Use the matching repository
version when this source directory is delivered without `docs/`.

## Standalone integration prerequisites

- Windows/x64, the Visual Studio C++ runtime and matching OpenCV dependencies are
  required. Follow `opencv_helper.vcxproj` and the parent Native README for build
  inputs; copying this API file alone does not supply runtime DLLs.
- Use `Native/include/opencv_media_export.h`, `custom_structs.h` and
  `video_export.h` with the managed declarations in `UI/ColorVision.Core`.
  Preserve exported names, calling conventions and structure layouts.
- HImage uses Pack 8 and a one-byte bool (32 bytes on x64). VideoInfo and RoiRect
  use Pack 1. Do not use one packing rule for every structure.
- Keep borrowed input buffers alive; structure copies share the pixel pointer.
  An owned HImage is released exactly once. Release JSON with `FreeResult`, and
  M_ConvertImage byte buffers with `M_FreeHImageData`; never free borrowed pixels
  or release the same buffer through two paths.
- Interpret results by function family: image operations commonly use 0,
  calibration/POI mutations use 1, JSON exports return a positive allocated
  length, and focus returns a score. Check the detailed function contract before
  treating a result as success.

Module boundaries, build selection and native verification are described in the
[native integration guide](../../docs/02-developer-guide/engine-development/opencv-integration.md).
