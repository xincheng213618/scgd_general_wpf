---
knowledge_id: "engine.opencv-helper-api"
knowledge_type: "reference"
status: "current"
summary: "opencv_helper 英文 API 参考：校准/POI、图像处理、SFR、检测与内存释放；核对真实参数单位和函数族错误码，声明的选项不等于当前 Engine 提供操作入口。"
aliases: ["opencv_helper API", "原生函数参考", "图像拼接", "伪彩原位输出", "POI 批量计算", "校准共享缓存", "StitchingErrorCode", "RoiRect", "COLORVISION_CALIBRATION_CACHE_MB", "M_CalibrationExecuteToV1", "M_CalibrationCacheReleaseV1", "M_CalculatePoiBatchV1", "M_CalculatePoiBatchV2", "MPoiOptionsV2", "PoiOptionsFlagsV2", "M_POI_OPTION_PRESERVE_NON_POSITIVE_VALUES", "PoiMeasurementService", "M_AutoLevelsAdjust", "M_AutomaticColorAdjustment", "M_AutomaticToneAdjustment", "M_PseudoColor", "M_PseudoColorAutoRange", "M_PseudoColorInto", "M_GetMinMax", "M_ExtractChannel", "M_GetWhiteBalance", "M_ApplyGammaCorrection", "M_AdjustBrightnessContrast", "M_InvertImage", "M_Threshold", "M_RemoveMoire", "M_ConvertImage", "M_ConvertGray32Float", "M_DrawPoiImage", "M_StitchImages", "M_Fusion", "M_ApplyGaussianBlur", "M_ApplyMedianBlur", "M_ApplySharpen", "M_ApplyCannyEdgeDetection", "M_ApplyHistogramEqualization", "M_CalSFRMultiChannel", "M_AnalyzeSfrV2", "斜边清晰度", "ESF", "MTF50", "MTF10", "RGB 子像素", "M_CalArtculation", "M_FindLuminousArea", "M_FindLuminousAreaV2", "M_FindLightBeads", "M_DetectKeyRegions", "FreeResult", "M_FreeHImageData", "M_PseudoColorAutoRangeInto"]
code_paths: ["Native/opencv_helper/API_Documentation.md", "Native/include/opencv_media_export.h", "Native/include/custom_structs.h", "Native/opencv_helper/opencv_media_export.cpp", "Native/opencv_helper/algorithm.cpp", "Native/opencv_helper/exports/calibration_export.cpp", "Native/opencv_helper/exports/poi_export.cpp", "Native/opencv_helper/exports/sfr_export.cpp", "Native/opencv_helper/exports/p2_export.cpp", "Native/opencv_helper/algorithm/calibration", "Native/opencv_helper/algorithm/poi/poi_batch.cpp", "Native/opencv_helper/algorithm/sfr/sfr_slanted.cpp", "Native/opencv_helper/algorithm/luminous_area/luminous_area_v2.cpp", "UI/ColorVision.Core/OpenCVMediaHelper.cs", "UI/ColorVision.Core/SfrAnalysis.cs", "UI/ColorVision.ImageEditor/EditorTools/Algorithms/Calculate/SFR", "UI/ColorVision.Core/OpenCVCalibration.cs", "UI/ColorVision.Core/HImage.cs", "Engine/ColorVision.Engine/Services/POI/PoiMeasurementService.cs"]
related: ["engine.native-integration", "ui.core", "ui.image-frames", "algorithms.local-native-analysis", "algorithms.poi-routes", "algorithms.find-cross"]
---

# opencv_helper.dll API 参考

## Overview

`opencv_helper.dll` exposes OpenCV algorithms to ColorVision through C exports. Use `Native/include/opencv_media_export.h` and `Native/include/custom_structs.h` for signatures and layout. Managed declarations are in `UI/ColorVision.Core/OpenCVMediaHelper.cs`, `OpenCVCalibration.cs` and `HImage.cs`. This page records behavior that matters when crossing the ABI. [Native integration](../../02-developer-guide/engine-development/opencv-integration.md) · [ImageEditor native analysis](../algorithms/local-native-analysis.md)

## Data Structures

### HImage

The shared image descriptor is declared in `Native/include/custom_structs.h` and `UI/ColorVision.Core/HImage.cs`. On x64 it uses pack 8 and is 32 bytes. `isDispose` is a one-byte bool at offset 20 and `pData` is at offset 24. `depth` is the sample width (`8/16/32/64`), and `stride` is bytes per row (`0` for tightly packed, negative invalid).

`isDispose=false` marks a buffer owned by the descriptor. Managed `Dispose()` frees it with `FreeCoTaskMem` and clears that descriptor's pointer. `true` borrows the buffer, which the owner keeps valid until the call returns. Copies share the pointer and must not be disposed as independent owners.

### RoiRect

`RoiRect` uses pack 1 and four signed 32-bit fields for the top-left coordinates, width and height. Read the declaration in `Native/include/custom_structs.h` and `UI/ColorVision.Core/HImage.cs`. The behavior of empty or invalid ROIs is export-specific.

### FocusAlgorithm (Enum)

Use the persisted enum values in `Native/include/opencv_media_export.h` and their managed counterparts in `OpenCVMediaHelper.cs`. The focus score semantics are described under [M_CalArtculation](#m-calartculation).

## Calibration Context API

Reuse the calibration context while the ordered correction files, their contents and the source layout are unchanged. Create with `M_CalibrationCreate`, load the ordered files with `M_CalibrationLoadFileW`, execute, read the last error before destroying the context on failure, and release it with `M_CalibrationDestroy`. Caller-owned buffers and lifetime rules are below.

Supported legacy calibration values are `0` through `9` and `11` through `15`:
DarkNoise, three defect-point variants, DSNU, Uniformity, Luminance, OneColor,
FourColor, MultiColor, Distortion, ColorShift, LineArity, ColorDiff, and
AngleShift. Value `10` (`LumColor`) is reserved and intentionally rejected.

RAW corrections run in template order. At most one luminance/color transform runs afterward and writes float planes (`X`, `Y`, `Z` in order, or one plane for luminance). Color-transform exposures must be finite and positive. Check size arguments in bytes for RAW and float elements for CIE.

`M_CalibrationExecuteRawWithFlipV1` is equivalent to applying RAW correction and then flipping (`0` up/down, `1` left/right, `-1` both). It rejects templates containing luminance/color conversion. Planar RAW is flipped plane by plane. Failed calls leave unusable output.

Engine uses this entry for an unflipped RAW frame when the helper backend runs standard correction. Full CIE, metadata-only and legacy paths retain their existing behavior. For performance comparisons add `CalibrationAlgorithm` and `MirrorImage`, because the fused path includes mirroring in the former. `AngleShift` uses the same coordinate and zero-fill rules in full-frame and tiled interpolation. Its implementation controls the IPP and buffer thresholds.

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
The `M_POI_OPTION_PRESERVE_NON_POSITIVE_VALUES` flag (`4`, managed
`PoiOptionsFlagsV2.PreserveNonPositiveValues`) skips the fixed replacement of
three-channel XYZ values `<= 0` after optional scaling. Color metrics are then
derived from those values, without a finite-value or valid-color guarantee.
Without this flag, V1 and V2 replace non-positive XYZ with `0.000001`; this
native comparison does not replace NaN. Single-channel Y bypasses this clamp
regardless of the flag. The configurable result-display replacement is a
separate step described in [CVCIE POI results](./cvcie-results.md).

The request/result/options sizes remain 20/36/48 bytes and existing signatures
are unchanged. Callers that request flag `4` need a V2 DLL supporting that flag:
an older V2 validator may reject it, and a DLL without the V2 export cannot
serve the call. `PoiMeasurementService` has no legacy retry; nonzero native
status becomes `InvalidOperationException` with the returned error code.
Unknown flags and non-zero reserved fields return `M_POI_INVALID_ARGUMENT`.
Percentage thresholds use the mean of the highest `maxPercent` samples.
XYZ-mask mode derives one deterministic common mask from the selected X, Y, or
Z plane; percentage mode also derives its threshold from that selected plane.
The mask is common to the selected output planes and does not depend on the
order of POI requests.

`PoiMeasurementService` is the Engine boundary for standard, unfiltered POI
measurement. Its public `Calculate` calls
`OpenCVCalibration.M_CalculatePoiBatchV2` with `PoiOptionsV2.Create()`
(filterMode/flags `0`, scales `1`); the native V2 implementation then delegates
to V1. The internal `CalculateRaw` path instead sets flag `4`; its CVCIE
caller then applies result settings, including when the display replacement
switch is off. Direct native callers may supply supported V2 filter options. Buffers require 32-bit floats and one or three planar
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

Histogram stretching on BGR/BGRA input. The export prepares BGR8, which may normalize the input and drops alpha.

---

### M_AutomaticColorAdjustment

Lab color balance. It prepares BGR8 from BGR/BGRA input and may modify shared BGR8 pixels.

---

### M_AutomaticToneAdjustment

Histogram-clipped tone adjustment. It prepares BGR8 from BGR/BGRA input and may modify shared BGR8 pixels.

---

### M_PseudoColor

False-color mapping. `min` and `max` set the mapping thresholds, `types` selects an OpenCV colormap, and `channel=-1` requests grayscale conversion.

---

### M_PseudoColorAutoRange

Uses caller-supplied `dataMin` and `dataMax`. For 16-bit input, normal mapping scales by 1/257 and shifts thresholds by 8 bits. Auto-range instead stretches the LUT using the supplied range. Float inputs are normalized to 8 bit and auto-range uses thresholds 0/255 independently of the supplied range. Output is BGR8.

### M_PseudoColorInto and M_PseudoColorAutoRangeInto

These variants take the destination `HImage` by value and reuse caller-owned storage. The output must have the input rows/columns and `CV_8UC3` layout; a mismatched destination returns -2. They do not allocate or transfer a new output HImage. Keep both buffers alive and use a separate destination; the export does not establish arbitrary overlapping-buffer safety. The managed `ApplyPseudoColor`/`ApplyPseudoColorAutoRange` wrappers allocate a fresh compatible destination and dispose it on a nonzero native return. The raw Into imports require the caller to provide storage.

---

### M_GetMinMax

对单通道直接统计最小值和最大值，并按有效索引统计彩色通道。彩色输入的索引无效时按 BGR 顺序转成灰度。接口借用源缓冲，返回的无符号值将负数截到零并舍去小数。

### M_GetRgbMinMax

ImageView 的 RGB24/RGB48 Composite 自动范围使用此接口。按 RGB 顺序统计灰度范围，沿用 `M_GetMinMax` 的缓冲区和返回值契约。

---

### M_ExtractChannel

The channel index is zero-based and must exist. BGR order is B, G, R. Output has one channel.

---

### M_GetWhiteBalance

Requires three channels and finite gains. Gains are passed as R/G/B and applied in pixel order B/G/R. Values are capped at 255 for 8-bit input, at 65535 otherwise.

---

### M_ApplyGammaCorrection

Accepts 8-bit and 16-bit unsigned input and finite `gamma > 0`. Uses `pow(value / maximum, 1 / gamma) * maximum` with the maximum for that input width.

---

### M_AdjustBrightnessContrast

Uses `output = alpha * input + beta`, with `alpha` as gain and `beta` as offset.

---

### M_InvertImage

Bitwise inversion.

---

### M_Threshold

Uses the supplied OpenCV threshold mode. See the native declaration for argument types.

---

### M_RemoveMoire

Multi-scale moiré reduction. Read the filter sequence in the native implementation.

---

### M_ConvertImage

Returns downsampled grayscale bytes. `targetPixelsX/Y` are positive area hints and `scaleFactout` reports the chosen scale. Output samples the source at the chosen stride rather than resizing to an exact target size. Color input is converted to gray and non-8-bit data is normalized to 8 bit. Ties between candidate scales choose the smaller scale. Output length is `max(width / scale, 1) × max(height / scale, 1)`.

Use `M_FreeHImageData` for `rowGrayPixels`. `OpenCVMediaHelper.ConvertImageToGrayPixels` copies the payload into a managed array and releases the native buffer.

---

### M_ConvertGray32Float

Convert `CV_32FC1` to unsigned 16-bit grayscale. If min >= 0 and max <= 5, multiply by 65535 with saturation (values above 1 saturate). Otherwise, require max > min and map the full min/max range to 0/65535. A constant value outside the first branch fails; this is not a universal fixed-scale scientific conversion.


---

### M_DrawPoiImage

Accepts one or three input channels and returns a three-channel image. `points` is an interleaved x/y integer array. `pointCount` counts coordinate integers, so three points take 6. It must be non-negative and even. Radius must be positive, a nonempty array must be valid, and `thickness=-1` requests filled circles.

---

### M_StitchImages

Combine vertical strips from same-size, same-type images. Output keeps one input image's dimensions; it is not a concatenated panorama. For N images of width W, start with the last image, then replace strip i of width floor(W/N) from image i for i=0..N-2. The final strip and any remainder stay from the last image.

**Config JSON Format:**
```json
{"ImageFiles": ["path1.jpg", "path2.jpg", "path3.jpg"]}
```

**Returns:** 0 on success; negative stitching or common export errors. The input string is interpreted through the GBK-to-UTF-8 conversion path before JSON parsing. Every ImageFiles entry must be a readable file of the same full width, height and type; a later unreadable/type-mismatched file can return DIFFERENT_DIMENSIONS. See [Stitching Errors](#stitching-errors) for overlapping numeric meanings.

---

### M_Fusion

Multi-focus fusion. The config is a JSON array of input image paths.
**Config JSON Format:**
```json
["image1.jpg", "image2.jpg", "image3.jpg"]
```

---

## Filter Functions

### M_ApplyGaussianBlur

Use an odd `kernelSize`, with `sigma=0` for automatic selection.

---

### M_ApplyMedianBlur

Use an odd `kernelSize`.

---

### M_ApplySharpen

Laplacian sharpening. The native implementation owns the kernel.

---

### M_ApplyCannyEdgeDetection

Apply Canny edge detection to an 8-bit gray working image. Three/four-channel input is converted to gray; non-8-bit data uses the fixed 255/65535 scale. The output is a single-channel 8-bit edge image.


---

### M_ApplyHistogramEqualization

Apply histogram equalization to an 8-bit gray working image. Three/four-channel input is converted to gray; non-8-bit input uses a fixed 255/65535 scale rather than per-image min/max normalization. Output is single-channel 8-bit.


---

## SFR (Spatial Frequency Response) Functions

ImageEditor 的“框选斜边测清晰度（SFR/MTF）”使用诊断接口 `M_AnalyzeSfrV2(HImage, RoiRect, const char* config, char** result)`，托管入口为 `SfrAnalyzer.Analyze`。选择一条完整斜边、两侧保留均匀平台；也可从已有矩形右键进入。窗口持有输入帧租约，参数重算使用同一快照；主图换帧不会偷偷改变本次测量。参数仅在当前窗口生效，完整 JSON 保存本次参数与来源版本。

MTF 是边缘信号经 ESF（边缘过渡）、LSF（导数）、加窗及傅里叶幅值归一化得到的空间频率响应。MTF50/10 表示响应首次向下穿越 50%/10% 的频率，不是百分制清晰度分数。主图频率为 `cycles/input-pixel`，默认显示 `0..0.5`；0.5 是输入图像的 Nyquist，展开更高频段仅供诊断。L 是 `0.213 R + 0.715 G + 0.072 B` 的组合信号，不等同于经过光度标定的亮度。

V2 接受单通道、BGR、BGRA（忽略 alpha），位深为 unsigned 8/16 bit 或 32/64 bit float。保留精度，不进行 ROI min/max 拉伸到 8 bit。配置 JSON 的 `encoding` 为 `unknown`（默认，仅诊断）、`linear`、`srgb` 或 `power`；`decodeExponent=2.2` 仅用于 power，线性信号等于归一化输入的该次幂。`blackLevel=0`、`whiteLevel=0` 默认按类型使用 255/65535/1 满量程；装入 16 位容器的低位深图或标定浮点图须填实际范围。非有限值或超出范围的像素拒绝处理，不能靠自动增益掩盖输入差异。

`minimumContrast=0.02` 是解码后平台差，`minimumSnr=10` 是平台差/合并平台标准差，`maximumFitRms=0.35` 是直线边缘拟合残差像素值，均为可配置的诊断门限，不是 ISO 或客户验收规格。固定使用一次直线拟合、4 倍分箱、加窗 LSF 和微分响应补偿；定向后 ROI 至少 40×32 像素，边缘距左右边界保留至少约 12 像素，倾角范围 1°..15°（建议约 5°），边缘附近分箱覆盖至少 95%。单 ROI 最多 1600 万像素、边长最多 8192。过强平台纹理、多个边缘、不足的采样覆盖等给出逐通道原因，不能把放宽门限当作修复。

`displayTarget=true` 增加显示系统测量提示。可见 RGB 子像素、摩尔纹、相机去马赛克与栅格相位会干扰单斜边模型；平滑 RGB 斜边仍可能可测。质量检查是启发式诊断，不能识别所有显示像素污染；通过检查不证明结果只来自镜头或光学系统。窗口不自动做周期平均、模糊或强制曲线单调，因为这些处理会改变被测响应。比较重复性需要多帧原始图、相同曝光/ROI/编码和拍摄倍率，单张图不能给出帧间置信区间。

V2 正返回值为包含末尾 NUL 的 UTF-8 JSON 字节数，由 `FreeResult` 释放。JSON `algorithmVersion="2.0"`、`unit="cycles/pixel"`、`nyquist=0.5`；每个通道有 `valid/reason/warnings`、平台/拟合/覆盖诊断及其 `plateausAvailable/fitAvailable/samplingAvailable`，未执行的阶段不能解释为零误差。有效通道分别携带 `frequencies/mtf`、`edgePositions/esf`、`lsfPositions/lsf`，坐标不能混用；LSF 为实际用于运算的居中加窗导数。`mtf50/mtf10` 没有在 Nyquist 内交点时为 null，失败通道的指标为 null、曲线为空。`sourceDepth` 使用 OpenCV depth 枚举；窗口完整测量另存来源的 bit depth。全零 ROI 表示全图，其余 ROI 必须完整在图内，无旧版全图回退。错误码 -1 参数/ROI、-2 空图、-3 分配失败、-4 解析/输入/计算异常、-6 未知异常；输出指针在失败时清空。

窗口同时列出 L/R/G/B 的 MTF50、MTF10、指定频率响应、Nyquist 响应和指定响应交点；提供 ROI 与拟合线、质量表、完整 MTF 数据、ESF/LSF 和术语说明。CSV 输出完整曲线，JSON 另包含完整诊断、采集时间、文档/帧版本、ROI、输入尺寸及参数。未知编码、削顶或显示对象提示独立于可计算状态，不能误当成产品“合格”。这一路径与 Engine 历史 SFR/SFR2、远端 MTF 模板及统一平台 `FieldSfr` 的输入/结果契约独立，不替换服务算法或历史 DAO。

Compare the results on representative customer display samples before using this mode for acceptance. The implementation does not certify ISO 12233 conformance. [Imatest sharpness](https://www.imatest.com/imaging/sharpness/) · [MTF measurement consistency](https://www.imatest.com/docs/mtf-measurement-consistency/)

### M_CalSFRMultiChannel

Calculate slanted-edge SFR for one gray channel or four reported R/G/B/L curves from BGR/BGRA input. This description does not certify optical conformance to a standard.
For color images, luminance follows the sfrmat5 default weights: 0.213*R + 0.715*G + 0.072*B.

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

---

### C++ Implementation Notes

SFR is implemented behind C exports. New diagnostic callers use `M_AnalyzeSfrV2`; `M_CalSFR` and `M_CalSFRMultiChannel` retain their legacy capped metrics and numeric behavior for compatibility. Legacy success alone does not establish a usable edge. Callers must not depend on `cv::Mat` or ColorVision C++ struct ABI.

---

## Focus Evaluation Functions

### M_CalArtculation

Calculate image sharpness/focus measure using various algorithms.

An overlapping ROI is clipped to the image. A zero-sized or disjoint ROI uses the full image.
**Returns:** Raw pixel-unit focus measure value (higher = sharper). The value is not normalized to `0..1`.

Input-preparation failure, a non-finite result, or an exception caught by
`GuardDoubleExportImpl` returns `-1.0`. Exclude this failure sentinel before
comparing scores. An unrecognized enum value currently uses Variance. Zero can be a valid score; some undersized-image branches also
return `0`, so zero alone is not a failure indicator.

There is no single industry-standard numeric scale or threshold for these general-purpose focus measures. Values depend on the selected algorithm, bit depth, exposure, ROI, demosaicing/preprocessing, and target content. Use the SFR/MTF APIs when a calibrated optical-resolution measurement is required.

Select the measure using the declared `FocusAlgorithm`. Compare scores only under controlled input conditions. In particular, `VarianceOfLaplacian` depends on the input pixel units, so a threshold from an 8-bit image does not transfer directly to 16-bit input.

---

## Detection Functions

### M_FindLuminousArea

Detect luminous area in image using threshold and contour analysis.

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

The recommended automatic configuration is an empty object:

```json
{}
```

Most callers should expose only `MinConfidence` (default `0.25`). The native config parser owns the advanced keys. `AllowBorder` defaults to true and `MaxAreaRatio` to `0.999`, so near-full-frame displays qualify when four boundaries remain recoverable.

Only an all-zero RoiRect selects the full image in V2. Any other ROI must have positive dimensions and lie fully inside the image, unlike the legacy fallback. Corners are ordered `LT, RT, RB, LB`. When an ROI is supplied, corner
coordinates are ROI-local, as with the legacy detector.

The JSON gives `Success`, `Algorithm`, ordered `Corners`, `Confidence`, `SideQuality`, `FailureReason` and `Warnings`. Treat warnings and per-side evidence as part of the result.

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

### M_FindCrossLocal

The [local FindCross reference](../algorithms/detectors/find-cross.md) owns `M_FindCrossLocal`, `M_FindCrossLocalGetLastError`, production/diagnostic options, global coordinates, return codes and managed ownership. It also distinguishes the integer compatibility center from `CenterSubpixel` and the Pattern detector from explicit OuterPanel assistance.

### M_FindLightBeads

Detect LED/light bead positions in grid pattern.

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

## Utility Functions

### FreeResult

Free memory allocated for JSON result strings.

Use `OpenCVMediaHelper.PtrToStringAnsiAndFree` or `PtrToStringUtf8AndFree` according to the export's encoding. Release non-null results even when the status reports failure.

---

### M_FreeHImageData

Free image data allocated by DLL.

Use this for every buffer that the DLL returns from `CoTaskMemAlloc`, including `HImage.pData` and the `rowGrayPixels` buffer returned by `M_ConvertImage`.

---

## P2 Local Analysis Functions

The P2 APIs are stateless native algorithms. They use the common signature
`HImage + RoiRect + JSON config` and return a UTF-8 JSON buffer allocated with
`CoTaskMemAlloc`. A positive return value is the buffer size including the null
terminator; release the buffer with `FreeResult`.

| Function | Purpose |
| --- | --- |
| `M_DetectGhosts` | Bright source and secondary ghost candidates |
| `M_AnalyzeKeyboardHalo` | Per-key brightness and halo ratio |
| `M_AnalyzeLedArray` | LED grid assignment, geometry and brightness |
| `M_MatchRotatedTemplate` | Rotation and scale matching |
| `M_CalBinocularFusion` | Five-cross and binocular geometry |
| `M_CalStereoBinocularFusion` | Calibrated 3D reconstruction and reprojection validation |

For the managed/native parameter definitions and interactive routing, see [Local Native Analysis](../algorithms/local-native-analysis.md).

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

Pass a separate output pointer for each call. Copy and release any result before interpreting the native status, then inspect `success` and diagnostics separately.
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
describe an algorithm rejection. Focus evaluation returns a score.

### Stitching Errors

`StitchingErrorCode` is declared in `Native/include/opencv_media_export.h`. The current body returns `DIFFERENT_DIMENSIONS` for type mismatches too. Export-layer invalid JSON (`-4`), allocation (`-3`) and exception codes overlap these values, so inspect the failing stage.

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

Use an owned input and a fresh output slot. Keep the input valid through the native call, copy or consume valid output before disposal, then release both owned buffers in `finally`. For actual marshaling code, start from the corresponding wrapper in `OpenCVMediaHelper.cs`.

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
