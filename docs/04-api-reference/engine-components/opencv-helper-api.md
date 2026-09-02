---
knowledge_id: "engine.opencv-helper-api"
knowledge_type: "reference"
status: "current"
summary: "opencv_helper 英文 API 参考：校准/POI、图像处理、SFR、检测、视频与内存释放；核对真实参数单位和函数族错误码，声明的选项不等于当前 Engine 提供操作入口。"
aliases: ["opencv_helper API", "原生函数参考", "图像拼接", "伪彩原位输出", "POI 批量计算", "校准共享缓存", "StitchingErrorCode", "VideoInfo", "RoiRect", "COLORVISION_CALIBRATION_CACHE_MB", "M_CalibrationExecuteToV1", "M_CalibrationCacheReleaseV1", "M_CalculatePoiBatchV1", "M_CalculatePoiBatchV2", "M_AutoLevelsAdjust", "M_AutomaticColorAdjustment", "M_AutomaticToneAdjustment", "M_PseudoColor", "M_PseudoColorAutoRange", "M_PseudoColorInto", "M_GetMinMax", "M_ExtractChannel", "M_GetWhiteBalance", "M_ApplyGammaCorrection", "M_AdjustBrightnessContrast", "M_InvertImage", "M_Threshold", "M_RemoveMoire", "M_ConvertImage", "M_ConvertGray32Float", "M_DrawPoiImage", "M_StitchImages", "M_Fusion", "M_ApplyGaussianBlur", "M_ApplyMedianBlur", "M_ApplySharpen", "M_ApplyCannyEdgeDetection", "M_ApplyHistogramEqualization", "M_CalSFRMultiChannel", "M_CalArtculation", "M_FindLuminousArea", "M_FindLuminousAreaV2", "M_FindLightBeads", "M_DetectKeyRegions", "M_VideoOpen", "M_VideoReadFrame", "M_VideoSeek", "M_VideoGetCurrentFrame", "M_VideoSetPlaybackSpeed", "M_VideoSetResizeScale", "M_VideoPlay", "M_VideoPause", "M_VideoClose", "FreeResult", "M_FreeHImageData", "M_PseudoColorAutoRangeInto"]
code_paths: ["Native/opencv_helper/API_Documentation.md", "Native/include/opencv_media_export.h", "Native/include/custom_structs.h", "Native/include/video_export.h", "Native/opencv_helper/opencv_media_export.cpp", "Native/opencv_helper/algorithm.cpp", "Native/opencv_helper/video_export.cpp", "Native/opencv_helper/exports/calibration_export.cpp", "Native/opencv_helper/exports/poi_export.cpp", "Native/opencv_helper/exports/sfr_export.cpp", "Native/opencv_helper/exports/p2_export.cpp", "Native/opencv_helper/algorithm/calibration", "Native/opencv_helper/algorithm/poi/poi_batch.cpp", "Native/opencv_helper/algorithm/sfr/sfr_slanted.cpp", "Native/opencv_helper/algorithm/luminous_area/luminous_area_v2.cpp", "UI/ColorVision.Core/OpenCVMediaHelper.cs", "UI/ColorVision.Core/OpenCVCalibration.cs", "UI/ColorVision.Core/HImage.cs", "Engine/ColorVision.Engine/Services/POI/PoiMeasurementService.cs"]
test_paths: ["Test/opencv_helper_test/test_find_luminous_area.cpp", "Test/opencv_helper_test/test_calibration.cpp", "Test/opencv_helper_test/test_pseudo_color.cpp", "Test/opencv_helper_test/test_p2_algorithms.cpp", "Test/ColorVision.UI.Tests/LuminousAreaNativeInteropTests.cs"]
related: ["engine.native-integration", "ui.core", "ui.image-frames", "algorithms.local-native-analysis", "algorithms.poi-routes"]
---

# opencv_helper.dll API 参考

## Overview

`opencv_helper.dll` is a C++ dynamic link library that provides computer vision and image processing functions for the ColorVision WPF application. It serves as a bridge between OpenCV algorithms and C# code via P/Invoke.

本页保留英文函数参考，集中说明参数、结构布局、返回值和资源释放。按函数名或中文功能词检索即可定位章节；这不是自动生成的全部导出清单。

Start with the [native integration guide](../../02-developer-guide/engine-development/opencv-integration.md) for build inputs and module boundaries. Header declarations, implementations and managed declarations must agree. ImageEditor execution and drawing are covered by [local native analysis](../algorithms/local-native-analysis.md). Examples are call patterns with caller-provided images/buffers, not ready-to-run hardware tests.

---

## Table of Contents

- [Data Structures](#data-structures)
- [Calibration Context API](#calibration-context-api)
- [POI Batch API](#poi-batch-api)
- [Image Processing Functions](#image-processing-functions)
- [Filter Functions](#filter-functions)
- [SFR Functions](#sfr-spatial-frequency-response-functions)
- [Focus Evaluation](#focus-evaluation-functions)
- [Detection Functions](#detection-functions)
- [Video Processing](#video-processing-functions)
- [Utility Functions](#utility-functions)
- [P2 Local Analysis](#p2-local-analysis-functions)
- [Error Codes](#error-codes)
- [Thread Safety](#thread-safety)
- [Memory Management](#memory-management)
- [Build Information](#build-information)

---

## Data Structures

### HImage

Image data structure used for passing images between C# and C++.

```cpp
#pragma pack(push, 8)
struct HImage {
    int rows;           // Image height
    int cols;           // Image width
    int channels;       // Shared conversion: 1..CV_CN_MAX; algorithms may restrict this
    int depth;          // 8=CV_8U, 16=CV_16U, 32=CV_32F, 64=CV_64F
    int stride;         // Bytes per row; 0 means tightly packed, negative is invalid
    bool isDispose = false; // true: borrowed buffer; managed Dispose must not free it
    unsigned char* pData;  // Pointer to pixel data
};
#pragma pack(pop)
```

**C# Equivalent:**
```csharp
[StructLayout(LayoutKind.Sequential, Pack = 8)]
public struct HImage : IDisposable
{
    public int rows;
    public int cols;
    public int channels;
    public int depth;
    public int stride;
    [MarshalAs(UnmanagedType.I1)]
    public bool isDispose;
    public IntPtr pData;
    // ... methods
}
```

Use the actual definitions in `Native/include/custom_structs.h` and
`UI/ColorVision.Core/HImage.cs`, not a separately maintained interop struct. On
x64 the native layout is 32 bytes, with `isDispose` at offset 20 and `pData` at
offset 24; the bool is one byte. Native assertions guard the layout.

`isDispose` is not a "please free" flag: the managed `Dispose()` frees a non-null
`pData` with `FreeCoTaskMem` only when `isDispose == false`, then clears that
struct's pointer. With `true`, the buffer is borrowed and its owner must keep it
valid for the native call. Copying an `HImage` does not clone its pixels or make
two independent owners; do not dispose two owning copies of the same pointer.

### RoiRect

Region of Interest rectangle structure.

```cpp
#pragma pack(push, 1)
struct RoiRect {
    int x;      // Top-left X coordinate
    int y;      // Top-left Y coordinate
    int width;  // Rectangle width
    int height; // Rectangle height
};
#pragma pack(pop)
```

**C# Equivalent:**
```csharp
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct RoiRect
{
    public int X;
    public int Y;
    public int Width;
    public int Height;
}
```

### FocusAlgorithm (Enum)

Focus evaluation algorithm types.

```cpp
enum FocusAlgorithm {
    Variance = 0,              // Variance of pixel values
    StandardDeviation = 1,     // Standard deviation
    Tenengrad = 2,             // Tenengrad (Sobel-based)
    Laplacian = 3,             // Laplacian operator
    VarianceOfLaplacian = 4,   // Variance of Laplacian
    EnergyOfGradient = 5,      // Energy of gradient
    SpatialFrequency = 6       // Spatial frequency
};
```

### VideoInfo

Video file information structure.

```cpp
#pragma pack(push, 1)
struct VideoInfo {
    int totalFrames;    // Total frame count
    double fps;         // Frames per second
    int width;          // Frame width
    int height;         // Frame height
};
#pragma pack(pop)
```

Use `OpenCVMediaHelper.VideoInfo` on the managed side (`Pack = 1`). Its size is 20 bytes: `totalFrames` offset 0, `fps` offset 4, `width` offset 12 and `height` offset 16. Do not use HImage's Pack 8 for this structure.

## Calibration Context API

The calibration API keeps parsed correction tables, distortion maps, and scratch
buffers alive across frames. Rebuild a context only when the ordered calibration
file list, file contents, or source layout changes.

```cpp
// All image buffers, dimensions and options are supplied by the caller.
void* context = nullptr;
int status = M_CalibrationCreate(&context);
if (status != M_CALIBRATION_OK) return status;
status = M_CalibrationLoadFileW(context, calibrationType, filePath);
if (status == M_CALIBRATION_OK) {
    status = M_CalibrationExecuteToV1(
        context, width, height, bitsPerChannel, channels,
        sourceRawData, rawByteLength,
        correctedRawData, correctedRawByteLength,
        cieData, cieFloatCount, &options);
}
// Capture the last error here, before destroying the context, if status failed.
int destroyStatus = M_CalibrationDestroy(context);
return status == M_CALIBRATION_OK ? destroyStatus : status;
```

Supported legacy calibration values are `0` through `9` and `11` through `15`:
DarkNoise, three defect-point variants, DSNU, Uniformity, Luminance, OneColor,
FourColor, MultiColor, Distortion, ColorShift, LineArity, ColorDiff, and
AngleShift. Value `10` (`LumColor`) is reserved and intentionally rejected.

Execution preserves template order for RAW corrections. If one luminance/color
transform is loaded, it runs last and writes planar float CIE (`X`, then `Y`,
then `Z`); Luminance writes one float plane. At most one color transform may be
selected. Color-transform exposure values must be finite and positive.
`rawByteLength` and `cieFloatCount` are validated before processing.
Geometric transforms that cannot safely run in place share one context-owned
RAW-sized work buffer and ping-pong through it. Consecutive Distortion and
ColorDiff therefore require no intermediate full-frame copy.

`M_CalibrationExecuteToV1` borrows a read-only source RAW pointer. It can write
corrected RAW, planar CIE, or both without first copying the source in managed
code. When a template ends in a luminance/color transform, `correctedRawData`
may be null. Source RAW, corrected RAW, and CIE ranges must not overlap. The
older `M_CalibrationExecute` mutable-buffer entry point remains available for
ABI compatibility; its RAW and CIE ranges must also be distinct. Output buffers
are undefined after a failed ExecuteTo call; do not consume partially written
outputs as valid calibration results.

All functions use `__cdecl`. Calls on a live context are serialized internally,
but `M_CalibrationDestroy` must not overlap another call on the same context.
Use `M_CalibrationGetLastError` twice: first to obtain the required UTF-8 byte
count including the terminator, then to copy the message; both calls return that
required count. Calibration mutation/execution calls use `M_CALIBRATION_OK = 1`
and negative `MCalibrationResult` errors, while `M_CalibrationGetItemCount`
returns an item count. A failed call and both error-reading calls must
be kept in the same caller-side critical section so another call cannot replace
the context's last-error text between them.

Parsed calibration assets are retained process-wide by calibration type and
canonical file path, so different camera/template groups reuse the same file
data and precomputed maps while keeping independent execution contexts. File
size and last-write time invalidate stale generations. The default retained
memory budget is 4 GiB; set `COLORVISION_CALIBRATION_CACHE_MB` before first use
to override it (`0` disables retention). Use
`M_CalibrationCacheGetStatsV1`/`M_CalibrationCacheGetEntryV1` to inspect the
cache and `M_CalibrationCacheReleaseV1` to drop cache-owned references. Releasing
the cache never invalidates a live context; the release result reports memory
that remains temporarily owned by active contexts. An in-flight file load that
has not published its context lease is canceled at the release boundary; if a
lease was already reserved, it is reported as active before release returns.
The budget may be exceeded while every resident entry is active, but is trimmed
as soon as the last owner of an over-budget entry is destroyed.

## POI Batch API

The POI API borrows planar float CIE memory directly for the duration of one
call. It does not allocate or copy the full image. `MPoiRequestV1` supports
solid point, circle, and center-based rectangle regions; all results are written
to the caller-owned `MPoiResultV1` array.

`M_CalculatePoiBatchV1` preserves the legacy unfiltered calculation exactly.
`M_CalculatePoiBatchV2` adds a 48-byte `MPoiOptionsV2` structure for Value,
XYZ-mask, and NoArea filters, percentage thresholds, and final XYZ scaling.
Unknown flags and non-zero reserved fields are rejected so the ABI can evolve
safely. Percentage thresholds use the mean of the highest `maxPercent` samples.
XYZ-mask mode derives one deterministic common mask from the selected X, Y, or
Z plane; percentage mode also derives its threshold from that selected plane.
The mask is common to the selected output planes and does not depend on the
order of POI requests.

`PoiMeasurementService` is the Engine boundary for standard, unfiltered POI
measurement. It calls `OpenCVCalibration.M_CalculatePoiBatchV2` with
`PoiOptionsV2.Create()`; the native V2 implementation delegates to V1 when
filterMode and flags are both zero. Direct native callers may supply supported
V2 filter options. Buffers require 32-bit floats and one or three planar
channels; `cieFloatCount` counts floats, not bytes. The native entry requires
non-empty requests; the managed service returns an empty array before calling
native for an empty point list.

---

## Image Processing Functions

The zero-success image operations below can return more than `-1`. Their common
export guards translate exceptions to negative codes; consult the
[function-family error contracts](#error-codes) rather than treating every API
in this DLL as one shared status enum.

An HImage passed by value still shares its pixel pointer. Automatic color/tone adjustment can reuse and modify BGR8 input, and BGR32F preparation can replace NaNs in shared pixels. Supply a copy when the caller needs an immutable source. `GuardHImageExport` clears the output structure before processing; pass a fresh output slot, not one still owning an unreleased buffer.

### M_AutoLevelsAdjust

Automatic levels adjustment using histogram stretching.

```cpp
COLORVISIONCORE_API int M_AutoLevelsAdjust(HImage img, HImage* outImage);
```

**Parameters:**
- `img` - BGR or BGRA input; the export prepares BGR8 (normalizing non-8-bit data), so output does not retain the original depth or alpha.
- `outImage` - Output image pointer

**Returns:** 0 on success; negative on failure, including common export-guard errors.

**C# Usage:**
```csharp
[DllImport(LibPath, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
public static extern int M_AutoLevelsAdjust(HImage image, out HImage hImage);
```

---

### M_AutomaticColorAdjustment

Automatic color balance adjustment in Lab color space.

```cpp
COLORVISIONCORE_API int M_AutomaticColorAdjustment(HImage img, HImage* outImage);
```

**Parameters:**
- `img` - BGR or BGRA input; the export prepares BGR8 (normalizing non-8-bit data), so output does not retain the original depth or alpha.
- `outImage` - Output image pointer

**Returns:** 0 on success; negative on failure, including common export-guard errors.

---

### M_AutomaticToneAdjustment

Automatic tone adjustment using histogram clipping.

```cpp
COLORVISIONCORE_API int M_AutomaticToneAdjustment(HImage img, HImage* outImage);
```

**Parameters:**
- `img` - BGR or BGRA input; the export prepares BGR8 (normalizing non-8-bit data), so output does not retain the original depth or alpha.
- `outImage` - Output image pointer

**Returns:** 0 on success; negative on failure, including common export-guard errors.

---

### M_PseudoColor

Apply pseudo-color (false color) mapping to grayscale image.

```cpp
COLORVISIONCORE_API int M_PseudoColor(HImage img, HImage* outImage,
    uint min, uint max, cv::ColormapTypes types, int channel);
```

**Parameters:**
- `img` - Input image
- `outImage` - Output image pointer
- `min` - Minimum threshold for colormap
- `max` - Maximum threshold for colormap
- `types` - OpenCV colormap type (COLORMAP_JET, COLORMAP_HOT, etc.)
- `channel` - Channel to extract (-1 for grayscale conversion)

**Returns:** 0 on success; negative on failure, including common export-guard errors.

**C# Usage:**
```csharp
[DllImport(LibPath, CallingConvention = CallingConvention.Cdecl)]
public static extern int M_PseudoColor(HImage image, out HImage hImage,
    uint min, uint max, ColormapTypes colormapTypes, int channel);
```

---

### M_PseudoColorAutoRange

Apply pseudo-color using a caller-supplied data range. This function does not compute dataMin/dataMax itself.

```cpp
COLORVISIONCORE_API int M_PseudoColorAutoRange(HImage img, HImage* outImage,
    uint min, uint max, cv::ColormapTypes types, int channel,
    uint dataMin, uint dataMax);
```

**Parameters:**
- `dataMin` - Actual data minimum for scaling
- `dataMax` - Actual data maximum for scaling

For 16-bit input, normal mode scales pixels by 1/257 and shifts min/max thresholds by 8 bits; auto-range mode uses the supplied data range and stretches the LUT. Floating-point input is min/max-normalized to 8-bit, and auto-range resets its thresholds to 0/255; supplied dataMin/dataMax do not control that float branch. Output is BGR8.

### M_PseudoColorInto and M_PseudoColorAutoRangeInto

These variants take the destination `HImage` by value and reuse caller-owned storage. The output must have the input rows/columns and `CV_8UC3` layout; a mismatched destination returns -2. They do not allocate or transfer a new output HImage. Keep both buffers alive and use a separate destination; the export does not establish arbitrary overlapping-buffer safety. The managed `ApplyPseudoColor`/`ApplyPseudoColorAutoRange` wrappers allocate a fresh compatible destination and dispose it on a nonzero native return. The raw Into imports require the caller to provide storage.

---

### M_GetMinMax

Get minimum and maximum pixel values from image.

```cpp
COLORVISIONCORE_API int M_GetMinMax(HImage img, uint* outMin, uint* outMax, int channel);
```

**Parameters:**
- `img` - Input image
- `outMin` - Output minimum value
- `outMax` - Output maximum value
- `channel` - Valid channel index selects that channel. For multi-channel input, any index outside the valid range falls back to BGR-to-gray conversion; -1 does not compute extrema across every plane. Single-channel input is used directly.

The uint outputs clamp negative extrema to zero and discard fractional parts; they are not a lossless floating-point min/max API.

**Returns:** 0 on success; negative on failure, including common export-guard errors.

---

### M_ExtractChannel

Extract a specific channel from multi-channel image.

```cpp
COLORVISIONCORE_API int M_ExtractChannel(HImage img, HImage* outImage, int channel);
```

**Parameters:**
- `img` - Input image
- `outImage` - Output single-channel image
- `channel` - Zero-based index, `0 <= channel < img.channels`; out-of-range values fail. BGR channel indices are 0=B, 1=G, 2=R.

**Returns:** 0 on success; negative on failure, including common export-guard errors.

---

### M_GetWhiteBalance

Apply white balance correction with RGB gain factors.

```cpp
COLORVISIONCORE_API int M_GetWhiteBalance(HImage img, HImage* outImage,
    double redBalance, double greenBalance, double blueBalance);
```

**Parameters:**
- `redBalance` - Red channel gain
- `greenBalance` - Green channel gain
- `blueBalance` - Blue channel gain

Input must have three channels and gains must be finite. The implementation applies B/G/R gains and caps values at 255 for 8-bit input, otherwise at 65535; it does not preserve an arbitrary floating-point intensity range.

---

### M_ApplyGammaCorrection

Apply gamma correction to image.

```cpp
COLORVISIONCORE_API int M_ApplyGammaCorrection(HImage img, HImage* outImage, double gamma);
```

**Parameters:**
- `gamma` - Finite value greater than zero. The implementation uses `pow(value / maximum, 1 / gamma) * maximum`, with maximum 255 or 65535. Only 8-bit and 16-bit unsigned input is implemented; other depths raise an OpenCV error handled by the export guard.

---

### M_AdjustBrightnessContrast

Adjust brightness and contrast using linear transformation.

```cpp
COLORVISIONCORE_API int M_AdjustBrightnessContrast(HImage img, HImage* outImage,
    double alpha, double beta);
```

**Parameters:**
- `alpha` - Contrast factor (gain)
- `beta` - Brightness offset

**Formula:** `output = alpha * input + beta`

---

### M_InvertImage

Invert image colors (bitwise NOT operation).

```cpp
COLORVISIONCORE_API int M_InvertImage(HImage img, HImage* outImage);
```

---

### M_Threshold

Apply binary threshold to image.

```cpp
COLORVISIONCORE_API int M_Threshold(HImage img, HImage* outImage,
    double thresh, double maxval, int type);
```

**Parameters:**
- `thresh` - Threshold value
- `maxval` - Maximum value for binary thresholding
- `type` - Threshold type (THRESH_BINARY, THRESH_BINARY_INV, etc.)

---

### M_RemoveMoire

Remove moire patterns from image using multi-scale processing.

```cpp
COLORVISIONCORE_API int M_RemoveMoire(HImage img, HImage* outImage);
```

**Algorithm:** Gaussian blur → Downsample → Blur → Upsample → Sharpen

---

### M_ConvertImage

Convert image to downsampled grayscale byte array for display.

```cpp
COLORVISIONCORE_API int M_ConvertImage(HImage img, uchar** rowGrayPixels,
    int* length, int* scaleFactout, int targetPixelsX, int targetPixelsY);
```

**Parameters:**
- `rowGrayPixels` - Output byte array pointer allocated with `CoTaskMemAlloc`
- `length` - Output array length
- `scaleFactout` - Actual scale factor used
- `targetPixelsX/Y` - Positive target-area hints (header defaults 512/512), not exact output dimensions. The implementation chooses a scale and samples source pixels; it does not resize to exactly targetPixelsX by targetPixelsY.

Input may have one, three or four channels; color is converted to gray and non-8-bit values are min/max-normalized to 8-bit. Returned length and scaleFactor describe the allocated result. A current implementation gap remains: the local allowedFactors array has 12 elements, while FindClosestFactor defaults to reading 13. The documented candidate factors therefore cannot establish safe behavior for every size; existing stride/ownership tests do not prove this selection path free of out-of-bounds reads.

**Memory:** Caller must release `rowGrayPixels` with `M_FreeHImageData()`.

**C# safe wrapper:** Prefer `OpenCVMediaHelper.ConvertImageToGrayPixels(...)`, which copies the bytes to a managed `byte[]` and releases the native buffer in `finally`.

---

### M_ConvertGray32Float

Convert `CV_32FC1` to unsigned 16-bit grayscale. If min >= 0 and max <= 5, multiply by 65535 with saturation (values above 1 saturate). Otherwise, require max > min and map the full min/max range to 0/65535. A constant value outside the first branch fails; this is not a universal fixed-scale scientific conversion.

```cpp
COLORVISIONCORE_API int M_ConvertGray32Float(HImage img, HImage* outImage);
```

---

### M_DrawPoiImage

Draw circles at specified points on image.

```cpp
COLORVISIONCORE_API int M_DrawPoiImage(HImage img, HImage* outImage,
    int radius, int* points, int pointCount, int thickness);
```

**Parameters:**
- `radius` - Circle radius
- `points` - Array of [x1, y1, x2, y2, ...] coordinates
- `pointCount` - Number of coordinate integers, **twice the number of points**. It must be non-negative and even; for three points pass 6, not 3.
- `thickness` - Must be at least -1; -1 requests filled circles. Radius must be positive and a non-empty coordinate array must be valid. The export accepts one or three channels and returns a three-channel image.

---

### M_StitchImages

Combine vertical strips from same-size, same-type images. Output keeps one input image's dimensions; it is not a concatenated panorama. For N images of width W, start with the last image, then replace strip i of width floor(W/N) from image i for i=0..N-2. The final strip and any remainder stay from the last image.

```cpp
COLORVISIONCORE_API int M_StitchImages(const char* config, HImage* outImage);
```

**Config JSON Format:**
```json
{"ImageFiles": ["path1.jpg", "path2.jpg", "path3.jpg"]}
```

**Returns:** 0 on success; negative stitching or common export errors. The input string is interpreted through the GBK-to-UTF-8 conversion path before JSON parsing. Every ImageFiles entry must be a readable file of the same full width, height and type; a later unreadable/type-mismatched file can return DIFFERENT_DIMENSIONS. See [Stitching Errors](#stitching-errors) for overlapping numeric meanings.

---

### M_Fusion

Multi-focus image fusion using focus measure algorithm.

```cpp
COLORVISIONCORE_API int M_Fusion(const char* fusionjson, HImage* outImage);
```

**Config JSON Format:**
```json
["image1.jpg", "image2.jpg", "image3.jpg"]
```

---

## Filter Functions

### M_ApplyGaussianBlur

Apply Gaussian blur filter.

```cpp
COLORVISIONCORE_API int M_ApplyGaussianBlur(HImage img, HImage* outImage,
    int kernelSize, double sigma);
```

**Parameters:**
- `kernelSize` - Must be odd number (3, 5, 7, ...)
- `sigma` - Standard deviation (0 for auto)

---

### M_ApplyMedianBlur

Apply median blur filter (salt-and-pepper noise removal).

```cpp
COLORVISIONCORE_API int M_ApplyMedianBlur(HImage img, HImage* outImage, int kernelSize);
```

**Parameters:**
- `kernelSize` - Must be odd number

---

### M_ApplySharpen

Apply sharpening filter using Laplacian kernel.

```cpp
COLORVISIONCORE_API int M_ApplySharpen(HImage img, HImage* outImage);
```

**Kernel:**
```
[ 0 -1  0]
[-1  5 -1]
[ 0 -1  0]
```

---

### M_ApplyCannyEdgeDetection

Apply Canny edge detection to an 8-bit gray working image. Three/four-channel input is converted to gray; non-8-bit data uses the fixed 255/65535 scale. The output is a single-channel 8-bit edge image.

```cpp
COLORVISIONCORE_API int M_ApplyCannyEdgeDetection(HImage img, HImage* outImage,
    double threshold1, double threshold2);
```

---

### M_ApplyHistogramEqualization

Apply histogram equalization to an 8-bit gray working image. Three/four-channel input is converted to gray; non-8-bit input uses a fixed 255/65535 scale rather than per-image min/max normalization. Output is single-channel 8-bit.

```cpp
COLORVISIONCORE_API int M_ApplyHistogramEqualization(HImage img, HImage* outImage);
```

---

## SFR (Spatial Frequency Response) Functions

### M_CalSFRMultiChannel

Calculate slanted-edge SFR for one gray channel or four reported R/G/B/L curves from BGR/BGRA input. This description does not certify optical conformance to a standard.
For color images, luminance follows the sfrmat5 default weights: 0.213*R + 0.715*G + 0.072*B.

```cpp
COLORVISIONCORE_API int M_CalSFRMultiChannel(
    HImage img,
    double del,
    RoiRect roi,
    double* freq,
    double* sfr_r,
    double* sfr_g,
    double* sfr_b,
    double* sfr_l,
    int    maxLen,
    int*   outLen,
    int*   channelCount,
    double* mtf10_norm_r, double* mtf50_norm_r, double* mtf10_cypix_r, double* mtf50_cypix_r,
    double* mtf10_norm_g, double* mtf50_norm_g, double* mtf10_cypix_g, double* mtf50_cypix_g,
    double* mtf10_norm_b, double* mtf50_norm_b, double* mtf10_cypix_b, double* mtf50_cypix_b,
    double* mtf10_norm_l, double* mtf50_norm_l, double* mtf10_cypix_l, double* mtf50_cypix_l);
```

**Parameters:**
- `del` - Finite positive sampling pitch. The implementation does not impose a physical unit; output frequency uses the inverse of the supplied unit. Use del=1 for cycles/pixel, or a physical pitch with consistent downstream units.
- `roi` - Positive, fully contained ROI selects the slanted edge. An invalid or out-of-bounds ROI falls back to the full image rather than returning a dedicated ROI error.
- `freq` - Output frequency array, in inverse del units; the frequency axis includes edge-angle sampling correction.
- `sfr_r/g/b/l` - SFR curves for R/G/B/Luminance channels
- `maxLen` - Positive capacity of each output array; the export truncates curves to this capacity without an error. outLen is the number actually written.
- `outLen` - Actual output length
- `channelCount` - Number of channels calculated (1 or 4)
- `mtf10_norm/mtf50_norm` - Normalized MTF frequencies (0-1)
- `mtf10_cypix/mtf50_cypix` - Threshold frequencies in inverse del units despite the legacy suffix, capped at 0.495/del. Normalized values divide these by 0.5/del.

The caller owns all output arrays and must allocate their stated capacities. Mono input only requires L outputs; BGR/BGRA also requires R/G/B arrays and metrics. Scalar outputs are cleared before work, but failure does not make arbitrary curve-buffer contents meaningful.

**Returns:**
- 0 on success
- -1 on parameter error
- -2 on empty image
- -3 on calculation failure
- -4 on OpenCV exception, -5 on standard exception, -6 on unknown exception (`GuardSfrExport`)

**C# Usage:**
```csharp
[DllImport(LibPath, CallingConvention = CallingConvention.Cdecl)]
public static extern int M_CalSFRMultiChannel(
    HImage img, double del, RoiRect roi,
    [Out] double[] freq, [Out] double[] sfr_r, [Out] double[] sfr_g,
    [Out] double[] sfr_b, [Out] double[] sfr_l,
    int maxLen, out int outLen, out int channelCount,
    out double mtf10_norm_r, out double mtf50_norm_r,
    out double mtf10_cypix_r, out double mtf50_cypix_r,
    out double mtf10_norm_g, out double mtf50_norm_g,
    out double mtf10_cypix_g, out double mtf50_cypix_g,
    out double mtf10_norm_b, out double mtf50_norm_b,
    out double mtf10_cypix_b, out double mtf50_cypix_b,
    out double mtf10_norm_l, out double mtf50_norm_l,
    out double mtf10_cypix_l, out double mtf50_cypix_l);
```

---

### C++ Implementation Notes

SFR is implemented as a slanted-edge native module behind the stable C exports. Use `M_CalSFR` or `M_CalSFRMultiChannel` for cross-module calls so callers do not depend on `cv::Mat` or ColorVision C++ struct ABI.

---

## Focus Evaluation Functions

### M_CalArtculation

Calculate image sharpness/focus measure using various algorithms.

```cpp
COLORVISIONCORE_API double M_CalArtculation(HImage img, FocusAlgorithm type, RoiRect roi);
```

**Parameters:**
- `img` - Input image
- `type` - Focus algorithm type (see FocusAlgorithm enum)
- `roi` - A positive intersection with the image is cropped; a partially overlapping ROI is clipped. When there is no positive intersection, including an entirely outside or zero-size ROI, the implementation evaluates the full image.

**Returns:** Raw pixel-unit focus measure value (higher = sharper). The value is not normalized to `0..1`.

Input-preparation failure, a non-finite result, or an exception caught by
`GuardDoubleExportImpl` returns `-1.0`. Exclude this failure sentinel before
comparing scores. An unrecognized enum value currently uses Variance. Zero can be a valid score; some undersized-image branches also
return `0`, so zero alone is not a failure indicator.

There is no single industry-standard numeric scale or threshold for these general-purpose focus measures. Values depend on the selected algorithm, bit depth, exposure, ROI, demosaicing/preprocessing, and target content. Use the SFR/MTF APIs when a calibrated optical-resolution measurement is required.

**Algorithms:**

| Type | Description | Best For |
|------|-------------|----------|
| Variance | Variance of pixel values | General purpose |
| StandardDeviation | Std dev of pixel values | General purpose |
| Tenengrad | Sobel gradient magnitude | Edge detection |
| Laplacian | Mean absolute Laplacian response | Fine detail |
| VarianceOfLaplacian | Variance of Laplacian response | Focus comparison under controlled conditions |
| EnergyOfGradient | Gradient energy | Texture analysis |
| SpatialFrequency | Row/Column frequency | Periodic patterns |

For example, `VarianceOfLaplacian` returns the variance of the Laplacian response in the input image's pixel units. An 8-bit image and a 16-bit image are therefore not expected to share the same threshold unless the caller explicitly normalizes them before or after this API call.

**C# Usage:**
```csharp
[DllImport(LibPath, EntryPoint = "M_CalArtculation",
    CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
public unsafe static extern double M_CalArtculation(
    HImage image, FocusAlgorithm evaFunc, RoiRect roi);
```

---

## Detection Functions

### M_FindLuminousArea

Detect luminous area in image using threshold and contour analysis.

```cpp
COLORVISIONCORE_API int M_FindLuminousArea(HImage img, RoiRect roi,
    const char* config, char** result);
```

**Config JSON Format:**
```json
{"Threshold": -1, "UseRotatedRect": false}
```

Threshold -1 selects automatic thresholding; UseRotatedRect chooses four corners instead of a bounding box. A positive, fully contained ROI is used; other ROI values fall back to the whole image. Returned coordinates are local to the selected ROI.

**Output JSON Format (UseRotatedRect=false):**
```json
{"X": 100, "Y": 50, "Width": 200, "Height": 150}
```

**Output JSON Format (UseRotatedRect=true):**
```json
{"Corners": [[100,50], [300,50], [300,200], [100,200]]}
```

**Returns:** Result string length on success, negative on error

**Memory:** Caller must free result string using `FreeResult()`. Managed code should prefer `OpenCVMediaHelper.PtrToStringAnsiAndFree(...)`.

---

### M_FindLuminousAreaV2

Robustly locate a perspective luminous quadrilateral. The V2 detector uses
multi-scale coarse candidates, multiple edge candidates per caliper, robust
four-line fitting, and independent per-side quality checks. It does not fall
back to `M_FindLuminousArea`.

```cpp
COLORVISIONCORE_API int M_FindLuminousAreaV2(HImage image, RoiRect roi,
    const char* configJson, char** resultJson);
```

The recommended automatic configuration is an empty object:

```json
{}
```

Most callers should expose only `MinConfidence` (default `0.25`). Advanced
optional keys are `MinAreaRatio`, `MaxAreaRatio`, `SearchWidthRatio`,
`MinEdgeContrast`, `CaliperCount`, `MaxProcessingSize`, and `AllowBorder`
(default `true`; set it to `false` only when border-touching candidates must be
excluded). `MaxAreaRatio` defaults to `0.999`, so a near-full-frame display is
still detectable when its four boundaries remain observable.

Only an all-zero RoiRect selects the full image in V2. Any other ROI must have positive dimensions and lie fully inside the image, unlike the legacy fallback. Corners are ordered `LT, RT, RB, LB`. When an ROI is supplied, corner
coordinates are ROI-local, as with the legacy detector.

```json
{
  "Success": true,
  "Algorithm": "RobustV2",
  "Corners": [
    {"X": 100.1, "Y": 50.2},
    {"X": 300.0, "Y": 48.8},
    {"X": 305.4, "Y": 201.0},
    {"X": 96.2, "Y": 203.1}
  ],
  "Confidence": 0.93,
  "SideQuality": [
    {"Name": "Top", "Coverage": 1.0, "InlierRatio": 0.95,
     "ContrastP10": 0.21, "FitRms": 0.42, "MaxGap": 0.03,
     "Confidence": 0.91, "SampleCount": 40, "InlierCount": 38}
  ],
  "FailureReason": "",
  "Warnings": []
}
```

A positive return value always means JSON was allocated, including normal
algorithm rejection (`Success=false`, for example `NoSignal`, `NoCandidate`,
`InsufficientSideSupport`, `InsufficientIndependentGeometry`,
`UnstableCorners`, or `LowConfidence`). Automatic mode favors recovery: a
cross-threshold-stable coarse quadrilateral can extrapolate one weak side, or
two adjacent weak sides when two non-parallel sides retain dense measured line
support. It accepts geometrically recoverable border clipping and reports
partial/inferred-side warnings with a lower confidence. Comparable spatially
distinct candidates are ranked and the best is returned with
`AmbiguousCandidates` and `MultipleComparableCandidates` warnings. Cases
without enough independent geometry (including only two opposite/parallel
edges, no-signal, full-frame gradients, and noise-only images) remain rejected.
Negative values are reserved for invalid arguments/JSON or system exceptions.
Always release a non-null result with `FreeResult()`.

---

### M_FindLightBeads

Detect LED/light bead positions in grid pattern.

```cpp
COLORVISIONCORE_API int M_FindLightBeads(HImage img, RoiRect roi,
    const char* config, char** result);
```

**Config JSON Format:**
```json
{"Threshold": 20, "MinSize": 2, "MaxSize": 20, "Rows": 650, "Cols": 850}
```

**Output JSON Format:**
```json
{
    "Centers": [[100,50], [200,50]],
    "CenterCount": 2,
    "BlackCenters": [[150,50]],
    "BlackCenterCount": 1,
    "ExpectedCount": 552500,
    "MissingCount": 552498
}
```

---

The values above illustrate the JSON shape, not a measured test result. Defaults are shown in the config. MinSize/MaxSize filter contour bounding-box width and height with strict bounds. Centers and BlackCenters are ROI-local; an invalid ROI falls back to the full image. MissingCount is max(Rows*Cols-CenterCount, 0), not BlackCenterCount. The current dark-region loop returns after its first contour, so BlackCenters is not a guarantee that every missing bead was enumerated. Supply positive Rows/Cols; the export casts them to size_t when forming ExpectedCount.

### M_DetectKeyRegions

Automatically detect keyboard key regions in image.

```cpp
COLORVISIONCORE_API int M_DetectKeyRegions(HImage img, RoiRect roi,
    const char* config, char** result);
```

**Config JSON Format:**
```json
{"Threshold": -1, "MinArea": 500, "MaxArea": 0, "MarginRatio": 0.05}
```

**Output JSON Format:**
```json
{
    "KeyRegions": [
        {"X": 10, "Y": 20, "Width": 50, "Height": 50}
    ],
    "Count": 1
}
```

---

Threshold < 0 uses Otsu; MaxArea <= 0 uses 25% of the working image area. MarginRatio is clamped to 0..0.45 and shrinks each detected rectangle. A valid ROI is applied and its offset is added back to output, so KeyRegions uses full-image coordinates. Other ROIs fall back to the image. No detected keys returns -2 rather than successful empty JSON. The detector groups rows by vertical position and sorts each row by x.

## Video Processing Functions

### M_VideoOpen

Open video file for playback.

```cpp
COLORVISIONCORE_API int M_VideoOpen(const wchar_t* filePath, VideoInfo* info);
```

**Returns:** Handle (positive) on success; negative on failure. Invalid input or
an unopened source normally returns `-1`; `GuardVideoExport` also maps OpenCV,
standard and unknown exceptions to `-2`, `-3` and `-4` respectively.

Opening creates the producer/consumer workers in a paused state. A valid handle
does not mean playback has started or a frame callback has completed.

---

### M_VideoReadFrame

Read single frame from video.

```cpp
COLORVISIONCORE_API int M_VideoReadFrame(int handle, HImage* outImage);
```

Returns `0` with an owned output buffer on success. `-1` covers invalid output or
handle, `-2` a failed/empty read, and `-3` a known end-of-stream position. The
result of `MatToHImage` is also propagated, including conversion/allocation
failures (`-3` for allocation failure). The video exception guard reuses some
of these numbers, so the code alone does not always identify a unique cause.
Dispose a non-null owned output after use.

---

### M_VideoSeek

Queue a seek to a specific frame.

```cpp
COLORVISIONCORE_API int M_VideoSeek(int handle, int frameIndex);
```

`0` means the frame index passed validation and was stored in `seekRequestFrame`;
the producer performs the actual seek later. It does not prove that the target
frame has been decoded, delivered or rendered. Invalid handle/index returns
`-1`/`-2` respectively, with additional video guard errors possible.

---

### M_VideoGetCurrentFrame

Get the capture's current CAP_PROP_POS_FRAMES value under its mutex, or a negative error. This is capture position, not acknowledgement that a callback or UI has rendered that frame.

```cpp
COLORVISIONCORE_API int M_VideoGetCurrentFrame(int handle);
```

---

### M_VideoSetPlaybackSpeed

Set playback speed multiplier. A valid handle returns 0; non-positive speed is replaced by 1.0. The setter has no finite-value check, so callers must reject NaN/infinity. Return 0 does not confirm playback timing.

```cpp
COLORVISIONCORE_API int M_VideoSetPlaybackSpeed(int handle, double speed);
```

---

### M_VideoSetResizeScale

Set display resize scale for performance.

```cpp
COLORVISIONCORE_API int M_VideoSetResizeScale(int handle, double scale);
```

**Scale values:** 1.0, 0.5, 0.25 and 0.125 have dedicated processing paths, but
the native setter also accepts intermediate values. It changes non-positive
values to 0.125 and clamps values above 1.0 to 1.0; this is not a four-value enum. Reject non-finite values in the caller; these comparisons do not reject NaN.

---

### M_VideoPlay

Start video playback with callbacks.

```cpp
// Callback types
typedef void (__stdcall *VideoFrameCallback)(int handle, HImage* frame,
    int currentFrame, int totalFrames, void* userData);
typedef void (__stdcall *VideoStatusCallback)(int handle, int status, void* userData);

COLORVISIONCORE_API int M_VideoPlay(int handle,
    VideoFrameCallback frameCallback,
    VideoStatusCallback statusCallback,
    void* userData);
```

`InvokeFrame` passes the address of a stack-local `HImage`; that struct pointer
is borrowed only for the callback and must not be retained. Its `pData` is a
separately allocated owned buffer transferred to the receiver. Copy/render the
pixels and dispose the received `HImage` exactly once, or explicitly transfer
that buffer ownership to a longer-lived holder. The native callback path does
not free the buffer after calling the receiver.

Playback uses a latest-frame slot: the producer may overwrite an undelivered
frame, and the consumer takes the newest one. This is not a lossless per-frame
processing queue. Frame callbacks can run on native worker threads (including
the producer for a paused seek), not the WPF UI thread. Keep callback delegates
and `userData` alive while callbacks can still execute.

**Status codes:** 0=Paused, 1=Playing, 2=Ended

---

### M_VideoPause

Pause video playback.

```cpp
COLORVISIONCORE_API int M_VideoPause(int handle);
```

Pause changes the playback flag; it does not wait for an already-running callback
to finish.

---

### M_VideoClose

Close video and release resources.

```cpp
COLORVISIONCORE_API int M_VideoClose(int handle);
```

Close removes the handle, clears stored callbacks, stops workers and releases
capture resources. `StopVideoWorkers` normally joins the workers, but detaches
the current worker if close is called from that worker's callback. In that case
close can return before the current callback has returned. Do not treat every
successful close as a universal callback-completion barrier.

Callers must serialize close against other operations on the same handle. An
operation that already obtained the context can outlive handle removal, and
`M_VideoClose` releases `cap` without the `capMutex` used by manual frame reads.
The current implementation therefore does not establish arbitrary concurrent
close/read safety. This is an implementation limitation, not a claim that a
concurrency stress test passed.

---

## Utility Functions

### FreeResult

Free memory allocated for JSON result strings.

```cpp
COLORVISIONCORE_API int FreeResult(char* result);
```

**C# Usage:**
```csharp
[DllImport(LibPath, CallingConvention = CallingConvention.Cdecl)]
public static extern int FreeResult(IntPtr str);

// Usage
IntPtr resultPtr;
int len = M_FindLuminousArea(img, roi, config, out resultPtr);
string json = OpenCVMediaHelper.PtrToStringAnsiAndFree(resultPtr);
```

---

### M_FreeHImageData

Free image data allocated by DLL.

```cpp
void M_FreeHImageData(unsigned char* data);
```

Use this for every buffer that the DLL returns from `CoTaskMemAlloc`, including `HImage.pData` and the `rowGrayPixels` buffer returned by `M_ConvertImage`.

---

## P2 Local Analysis Functions

The P2 APIs are stateless native algorithms. They use the common signature
`HImage + RoiRect + JSON config` and return a UTF-8 JSON buffer allocated with
`CoTaskMemAlloc`. A positive return value is the buffer size including the null
terminator; release the buffer with `FreeResult`.

| Function | Purpose | Main config fields |
|----------|---------|--------------------|
| `M_DetectGhosts` | Bright-source and secondary ghost candidate measurement | Threshold/grid/area limits; optional `normalizeExposure`, percentile bounds, `backgroundKernel`, `multiScaleLevels`, `multiScaleFactor`, `multiScaleThresholdFactor`, `opticalCenter`, and directional-confidence limits |
| `M_AnalyzeKeyboardHalo` | Per-key inner brightness and surrounding halo ratio | `keyRects` (optional), `innerInsetRatio`, `haloGapRatio`, `haloWidthRatio`, `excludeKeyRectsFromHalo`, `gray`; `detection` controls automatic key detection when `keyRects` is omitted |
| `M_AnalyzeLedArray` | LED grid ordering, missing/extra points, spacing, rotation, brightness and area | `rows`, `cols`, assignment gates, brightness/area limits, `gray`; `detections` can be supplied or `detection` can drive automatic connected-component detection |
| `M_MatchRotatedTemplate` | Multi-angle/scale template matching with NMS and optional robust occlusion scoring | Angle fields, `scaleMin`, `scaleMax`, `scaleStep`, `featureMode` (`intensity`/`gradient`), `occlusionTolerance`, `scoreThreshold`, `maxMatches`, `nmsRadius`, `pyramidLevels`, `subpixel` |
| `M_CalBinocularFusion` | Five-cross selection and binocular geometry metrics | `threshold` (0–255; <= 0 uses Otsu), `blurKernel`, `morphKernel`, area limits, `pixelSize` (um), `focalLength` (mm), `virtualImageDistance` (mm), `opticalCenter`, `maxCandidates` |
| `M_CalStereoBinocularFusion` | Calibrated left/right five-cross detection, undistortion, triangulation, and reprojection validation | `leftDetection`, `rightDetection`, `calibration` (`leftCameraMatrix`, `rightCameraMatrix`, distortion arrays, `rotation`, `translation`), `minimumParallaxPixels`, `maximumReprojectionErrorPixels`, `requirePositiveDepth` |

Rectangles and points in JSON use object form (`x`, `y`, `width`, `height`).
Configured `keyRects`, LED `detections`, and all reported coordinates are in the
full-image coordinate system. A non-positive ROI size selects the whole image.
Ghost thresholds and keyboard/LED brightness values are normalized to `[0, 1]`.
When Ghost exposure normalization is enabled, `exposureLowUsed` and
`exposureHighUsed` report the original normalized input range. Background
subtraction and multi-scale processing are opt-in, so old configurations keep
their previous behavior. Candidate output adds direction, cross-scale support,
and aggregate confidence values.
Ghost and automatic Keyboard/LED analysis require a non-empty ROI to be fully
inside the image. Matching and binocular analysis clip a partially overlapping
ROI and report that adjustment in `warnings`; a non-overlapping ROI is invalid.

Stereo calibration follows the OpenCV convention
`X_right = rotation * X_left + translation`; translation and returned 3D points
are in millimetres. Camera matrices accept flat nine-value or nested 3x3 arrays,
and distortion vectors accept OpenCV's 4/5/8/12/14 layouts. Stereo output keeps
the left/right 2D detection diagnostics and reports per-point parallax,
reprojection error, validity, and confidence.

Example:

```csharp
string config = "{\"rows\":3,\"cols\":4,\"detection\":{\"threshold\":0.5,\"minArea\":10}}";
int length = OpenCVMediaHelper.M_AnalyzeLedArray(image, roi, config, out IntPtr result);
// The copy-and-free helper releases non-null output even if the native status failed.
string json = OpenCVMediaHelper.PtrToStringUtf8AndFree(result);
if (length <= 0) throw new InvalidOperationException($"LED analysis failed: {length}");
// Parse json and inspect its success/diagnostics separately.
```

P2 export errors are `-1` invalid arguments, `-3` allocation failure, `-4`
invalid JSON/config shape, `-5` OpenCV exception, `-6` standard exception, and
`-7` unknown exception. A valid algorithm run returns JSON even when
`success=false`, so callers retain its status and diagnostics.

The six managed P/Invoke declarations marshal config strings as UTF-8. Use
`PtrToStringUtf8AndFree` for their result buffers; the legacy ANSI helper remains
available for older exports whose historical encoding contract differs.

---

## Error Codes

### General Errors

There is no DLL-wide status enum. The following named codes belong to the
common export implementation in `opencv_media_export.cpp`; individual algorithm
bodies can add their own meanings. `GuardIntExportImpl` / `GuardHImageExportImpl`
propagate the body result and translate exceptions; they do not normalize every
function family into this table.

| Code | Common export meaning |
|------|-----------------------|
| -1 | Invalid argument |
| -2 | Algorithm failed |
| -3 | Allocation failed |
| -4 | JSON exception / invalid JSON |
| -5 | OpenCV exception |
| -6 | Standard exception |
| -7 | Unknown exception |

Zero means success for the zero-success image operations, not for every export.
Calibration mutation/execution and POI batch calls use `M_CALIBRATION_OK = 1`
and `M_POI_OK = 1`; calibration text/count queries return sizes or counts.
JSON-producing detection exports return a positive length, which may still
describe an algorithm rejection. Focus evaluation returns a score. Video open
returns a positive handle, video position returns a frame index, and video
operations use their own negative results and `GuardVideoExport` mappings.
In particular, video read `-3` may mean end-of-stream, `MatToHImage` allocation
failure, or a standard exception; inspect the call path/logs instead of assigning
a DLL-wide meaning.

### Stitching Errors

```cpp
enum class StitchingErrorCode {
    SUCCESS = 0,
    EMPTY_INPUT = -1,
    FILE_NOT_FOUND = -2,
    DIFFERENT_DIMENSIONS = -3,
    DIFFERENT_TYPE = -4,
    NO_VALID_IMAGES = -5
};
```

The enum is declared in `Native/include/opencv_media_export.h`. The current stitching body also returns DIFFERENT_DIMENSIONS for a type mismatch (DIFFERENT_TYPE is declared but not used there). Export-layer invalid JSON (-4), allocation (-3) and exception codes can overlap these values; interpret the failing stage rather than treating every negative value as a unique stitching diagnosis.

### SFR Errors

| Code | Meaning |
|------|---------|
| -1 | Null pointer parameter |
| -2 | Empty image |
| -3 | SFR calculation failed |
| -4 | OpenCV exception |
| -5 | Standard exception |
| -6 | Unknown exception |

---

## Thread Safety

- **Video functions:** Internal mutexes protect selected state, not arbitrary lifecycle concurrency. Serialize close against other same-handle calls and observe the callback lifetime limits above; seek/pause returns do not wait for rendering or all callbacks.
- **Image processing:** Keep input buffers alive and synchronize shared buffers/output locations. There is no blanket DLL-wide thread-safety guarantee or general "instance" lock for these free functions.
- **SFR/P2 calculations:** Use separate output locations and independently valid input buffers. These algorithms do not expose a shared mutable context; that observation does not certify all OpenCV/runtime dependencies or concurrent lifecycle operations.

---

## Memory Management

### C# Side Responsibilities

1. **Allocating HImage:** For an owned copy, allocate `pData` with `Marshal.AllocCoTaskMem()` and use `isDispose=false`. Borrowed inputs use `isDispose=true` and keep their original owner alive for the call.
2. **Freeing HImage:** Dispose each owned buffer exactly once, including a non-null output on a failure path. Do not additionally call `FreeCoTaskMem`/`M_FreeHImageData` after disposal, free borrowed storage, or dispose multiple copies of one owning struct.
3. **Freeing Results:** Free non-null JSON result buffers with `FreeResult()` in a failure-safe path; use the API's matching ANSI or UTF-8 copy-and-free helper (P2 uses UTF-8).
4. **Freeing Byte Arrays:** Use `M_FreeHImageData()` for `M_ConvertImage` buffers

### Example Memory Lifecycle

```csharp
// Owned, tightly packed BGR8 input; dimensions/pixelData must be valid.
int stride = checked(width * 3);
int byteCount = checked(stride * height);
HImage img = new HImage
{
    rows = height,
    cols = width,
    channels = 3,
    depth = 8,
    stride = stride,
    isDispose = false,
    pData = Marshal.AllocCoTaskMem(byteCount)
};
HImage outImg = default;

try
{
    // Copy data to unmanaged memory
    Marshal.Copy(pixelData, 0, img.pData, byteCount);

    // Process
    int status = OpenCVMediaHelper.M_AutoLevelsAdjust(img, out outImg);
    if (status != 0 || outImg.pData == IntPtr.Zero)
        throw new InvalidOperationException($"AutoLevels failed: {status}");

    // Copy/use result while outImg is still alive.
}
finally
{
    outImg.Dispose(); // Also covers an allocated output on a failure path.
    img.Dispose();
}
```

---

## Build Information

- **Target:** x64 Windows
- **Runtime Dependencies:**
  - OpenCV 4.x
  - Visual C++ Redistributable
- **Build/output selection:** Follow `Native/opencv_helper/opencv_helper.vcxproj`
  and `UI/ColorVision.Core/ColorVision.Core.csproj`, not a fixed net8 application
  output path. Core uses `OpenCvHelperBinary` to select the solution-level or
  project-level Release/x64 helper, copies it to output and packages it under
  `runtimes/win-x64/native`; the consuming project's target framework/output
  settings determine the application layout. See the native integration contract.

---

## See Also

- [OpenCV Documentation](https://docs.opencv.org/)
- [ISO 12233 SFR Standard](https://www.iso.org/standard/71616.html)
- ColorVision C# Wrapper: `UI/ColorVision.Core/OpenCVMediaHelper.cs`
