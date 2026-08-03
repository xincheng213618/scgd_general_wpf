#pragma once

#include <cstdint>
#include <string>
#include <opencv2/opencv.hpp>

#include "custom_structs.h"
#include "common.h"

#ifdef OPENCV_EXPORTS
#define COLORVISIONCORE_API __declspec(dllexport)
#else
#define COLORVISIONCORE_API __declspec(dllimport)
#endif

enum FocusAlgorithm {
    Variance = 0,
    StandardDeviation = 1,
    Tenengrad = 2,
    Laplacian = 3,
    VarianceOfLaplacian = 4,
    EnergyOfGradient = 5,
    SpatialFrequency = 6
    // CalResol �Ƚϸ��ӣ�ͨ����Ҫ�ض�ͼ�������ﲻ��Ϊͨ�öԽ��㷨
};

// ����������ö��
enum class StitchingErrorCode {
    SUCCESS = 0,          // �ɹ�
    EMPTY_INPUT = -1,     // ����Ϊ��
    FILE_NOT_FOUND = -2,  // �ļ�δ�ҵ�
    DIFFERENT_DIMENSIONS = -3, // �ߴ粻ͬ
    DIFFERENT_TYPE = -4,  // ���Ͳ�ͬ
    NO_VALID_IMAGES = -5 // û����Ч��ͼ��
};

extern "C" COLORVISIONCORE_API int M_ExtractChannel(HImage img, HImage* outImage, int channel);
extern "C" COLORVISIONCORE_API int M_PseudoColor(HImage img, HImage* outImage, uint min, uint max, cv::ColormapTypes types = cv::ColormapTypes::COLORMAP_JET, int channel = -1);
extern "C" COLORVISIONCORE_API int M_PseudoColorAutoRange(HImage img, HImage* outImage, uint min, uint max, cv::ColormapTypes types, int channel, uint dataMin, uint dataMax);
extern "C" COLORVISIONCORE_API int M_PseudoColorInto(HImage img, HImage outImage, uint min, uint max, cv::ColormapTypes types = cv::ColormapTypes::COLORMAP_JET, int channel = -1);
extern "C" COLORVISIONCORE_API int M_PseudoColorAutoRangeInto(HImage img, HImage outImage, uint min, uint max, cv::ColormapTypes types, int channel, uint dataMin, uint dataMax);
extern "C" COLORVISIONCORE_API int M_GetMinMax(HImage img, uint* outMin, uint* outMax, int channel = -1);

extern "C" COLORVISIONCORE_API int M_AutoLevelsAdjust(HImage img, HImage* outImage);

extern "C" COLORVISIONCORE_API int M_AutomaticColorAdjustment(HImage img, HImage* outImage);

extern "C" COLORVISIONCORE_API int M_AutomaticToneAdjustment(HImage img, HImage* outImage);

extern "C" COLORVISIONCORE_API int M_DrawPoiImage(HImage img, HImage* outImage, int radius, int* point, int pointCount, int thickness);

// rowGrayPixels is allocated by the DLL with CoTaskMemAlloc; release with M_FreeHImageData.
extern "C" COLORVISIONCORE_API int M_ConvertImage(HImage img, uchar** rowGrayPixels, int* length, int* scaleFactor, int targetPixelsX = 512, int targetPixelsY = 512);

extern "C" COLORVISIONCORE_API void M_FreeHImageData(unsigned char* data);

// Returns the raw pixel-unit focus measure for the selected algorithm.
// This value is not normalized to 0..1; thresholds depend on algorithm, bit depth,
// exposure, ROI, and preprocessing. Use SFR/MTF exports for calibrated optical
// resolution measurements.
extern "C" COLORVISIONCORE_API double M_CalArtculation(HImage img, FocusAlgorithm type, RoiRect roi);

extern "C" COLORVISIONCORE_API int M_GetWhiteBalance(HImage img, HImage* outImage, double redBalance, double greenBalance, double blueBalance);

extern "C" COLORVISIONCORE_API int M_ApplyGammaCorrection(HImage img, HImage* outImage, double gamma);

extern "C" COLORVISIONCORE_API int M_AdjustBrightnessContrast(HImage img, HImage* outImage, double alpha, double beta);

extern "C" COLORVISIONCORE_API int M_InvertImage(HImage img, HImage* outImage);

extern "C" COLORVISIONCORE_API int M_Threshold(HImage img, HImage* outImage, double thresh, double maxval, int type);

extern "C" COLORVISIONCORE_API int M_FindLuminousArea(HImage img, RoiRect roi,const char* config, char** result);

extern "C" COLORVISIONCORE_API int M_ConvertGray32Float(HImage img, HImage* outImage);

extern "C" COLORVISIONCORE_API int M_StitchImages(const char* config, HImage* outImage);

extern "C" COLORVISIONCORE_API int M_Fusion(const char* fusionjson, HImage* outImage);

extern "C" COLORVISIONCORE_API int M_RemoveMoire(HImage img, HImage* outImage);

extern "C" COLORVISIONCORE_API int M_ApplyGaussianBlur(HImage img, HImage* outImage, int kernelSize, double sigma);

extern "C" COLORVISIONCORE_API int M_ApplyMedianBlur(HImage img, HImage* outImage, int kernelSize);

extern "C" COLORVISIONCORE_API int M_ApplySharpen(HImage img, HImage* outImage);

extern "C" COLORVISIONCORE_API int M_ApplyCannyEdgeDetection(HImage img, HImage* outImage, double threshold1, double threshold2);

extern "C" COLORVISIONCORE_API int M_ApplyHistogramEqualization(HImage img, HImage* outImage);

extern "C" COLORVISIONCORE_API int M_FindLightBeads(HImage img, RoiRect roi, const char* config, char** result);

extern "C" COLORVISIONCORE_API int M_DetectKeyRegions(HImage img, RoiRect roi, const char* config, char** result);

// Surface defect / mura detector.
// Returns JSON with summary + defect list. Thresholds are relative ratios; values > 1 are treated as percentages.
extern "C" COLORVISIONCORE_API int M_DetectSurfaceDefects(HImage img, RoiRect roi, const char* config, char** result);

// P2 local image-analysis capabilities. Each function returns a CoTaskMemAlloc
// UTF-8 JSON buffer; release it with FreeResult. A positive return value is the
// allocated byte count (including the terminating null), while negative values
// are stable export-layer error codes.
extern "C" COLORVISIONCORE_API int M_DetectGhosts(HImage img, RoiRect roi, const char* config, char** result);
extern "C" COLORVISIONCORE_API int M_AnalyzeKeyboardHalo(HImage img, RoiRect roi, const char* config, char** result);
extern "C" COLORVISIONCORE_API int M_AnalyzeLedArray(HImage img, RoiRect roi, const char* config, char** result);
extern "C" COLORVISIONCORE_API int M_MatchRotatedTemplate(HImage img, HImage templateImage, RoiRect roi, const char* config, char** result);
extern "C" COLORVISIONCORE_API int M_CalBinocularFusion(HImage img, RoiRect roi, const char* config, char** result);
extern "C" COLORVISIONCORE_API int M_CalStereoBinocularFusion(
    HImage leftImage, HImage rightImage,
    RoiRect leftRoi, RoiRect rightRoi,
    const char* config, char** result);

extern "C" COLORVISIONCORE_API int FreeResult(char* result);

extern "C" COLORVISIONCORE_API int M_CalSFR(
    HImage img,
    double del,
    RoiRect roi,
    double* freq,   // 输出：频率数组
    double* sfr,    // 输出：SFR 数组
    int    maxLen,  // 输入：数组容量
    int* outLen,  // 输出：实际长度
    double* mtf10_norm,
    double* mtf50_norm,
    double* mtf10_cypix,
    double* mtf50_cypix);

// Multi-channel SFR calculation for RGB + L channels
// For 3-channel images: outputs R, G, B, L (4 channels)
// For single-channel images: outputs only L (1 channel)
// L is calculated with sfrmat5-compatible weights: 0.213*R + 0.715*G + 0.072*B.
extern "C" COLORVISIONCORE_API int M_CalSFRMultiChannel(
    HImage img,
    double del,
    RoiRect roi,
    double* freq,           // 输出：频率数组（所有通道共享）
    double* sfr_r,          // 输出：R通道 SFR（3通道时有效）
    double* sfr_g,          // 输出：G通道 SFR（3通道时有效）
    double* sfr_b,          // 输出：B通道 SFR（3通道时有效）
    double* sfr_l,          // 输出：L通道 SFR（总是输出）
    int    maxLen,          // 输入：数组容量
    int* outLen,            // 输出：实际长度
    int* channelCount,      // 输出：有效通道数（1或4）
    double* mtf10_norm_r, double* mtf50_norm_r, double* mtf10_cypix_r, double* mtf50_cypix_r,
    double* mtf10_norm_g, double* mtf50_norm_g, double* mtf10_cypix_g, double* mtf50_cypix_g,
    double* mtf10_norm_b, double* mtf50_norm_b, double* mtf10_cypix_b, double* mtf50_cypix_b,
    double* mtf10_norm_l, double* mtf50_norm_l, double* mtf10_cypix_l, double* mtf50_cypix_l);

// BMW target SFR 4-in-1.
// Returns JSON compatible with SFR2 result shape:
// { "result": [ { "name": "Point_1", "data": [
//   { "id": 0, "frequency": [...], "domainSamplingData": [...] }, ... ] } ] }
extern "C" COLORVISIONCORE_API int M_CalSFRBmw4In1(
    HImage img,
    RoiRect roi,
    const char* config,
    char** result);

// 9-point distortion measurement.
// Returns JSON with detected 3x3 points and horizontal/vertical TV + point9 metrics.
extern "C" COLORVISIONCORE_API int M_CalDistortionP9(
    HImage img,
    RoiRect roi,
    const char* config,
    char** result);

// Process-local calibration pipeline. The opaque context owns parsed files,
// large gain/offset tables and precomputed OpenCV maps across frames.
// Mutating/execution functions use the C calling convention and return 1 on
// success or a stable negative MCalibrationResult value on failure. The two
// accessor functions below return a UTF-8 byte count or an item count.
enum MCalibrationResult : std::int32_t {
    M_CALIBRATION_OK = 1,
    M_CALIBRATION_INVALID_ARGUMENT = -1,
    M_CALIBRATION_UNSUPPORTED = -2,
    M_CALIBRATION_LOAD_FAILED = -3,
    M_CALIBRATION_EXECUTE_FAILED = -4,
    M_CALIBRATION_INTERNAL_ERROR = -5,
};

struct MCalibrationExecutionOptionsV1 {
    std::uint32_t structSize;
    std::int32_t interleavedBgr;
    std::int32_t rgbType;
    std::uint32_t roiX;
    std::uint32_t roiY;
    std::uint32_t roiWidth;
    std::uint32_t roiHeight;
    std::uint32_t obLeft;
    std::uint32_t obRight;
    std::uint32_t obTop;
    std::uint32_t obBottom;
    float exposureX;
    float exposureY;
    float exposureZ;
};

static_assert(sizeof(MCalibrationExecutionOptionsV1) == 56, "Calibration options ABI layout changed");

enum MCalibrationCacheEntryFlagsV1 : std::uint32_t {
    M_CALIBRATION_CACHE_ENTRY_LOADING = 1U,
    M_CALIBRATION_CACHE_ENTRY_READY = 2U,
};

struct MCalibrationCacheStatsV1 {
    std::uint32_t structSize;
    std::uint32_t entryCount;
    std::uint64_t generation;
    std::uint64_t estimatedMemoryBytes;
    std::uint64_t budgetBytes;
    std::uint64_t hitCount;
    std::uint64_t missCount;
};

struct MCalibrationCacheEntryV1 {
    std::uint32_t structSize;
    std::int32_t calibrationType;
    std::uint32_t flags;
    // Required UTF-16 code-unit count including the null terminator.
    std::uint32_t pathCharacterCount;
    std::uint64_t generation;
    std::uint64_t fileBytes;
    std::uint64_t estimatedMemoryBytes;
    std::uint64_t hitCount;
    std::uint64_t lastAccessSequence;
    std::uint32_t activeOwnerCount;
    std::uint32_t reserved;
};

struct MCalibrationCacheReleaseResultV1 {
    std::uint32_t structSize;
    std::uint32_t releasedEntryCount;
    std::uint64_t releasedEstimatedMemoryBytes;
    std::uint32_t activeEntryCount;
    std::uint32_t activeOwnerCount;
    std::uint64_t activeEstimatedMemoryBytes;
    std::uint64_t generation;
};

static_assert(sizeof(MCalibrationCacheStatsV1) == 48, "Calibration cache stats ABI layout changed");
static_assert(sizeof(MCalibrationCacheEntryV1) == 64, "Calibration cache entry ABI layout changed");
static_assert(sizeof(MCalibrationCacheReleaseResultV1) == 40, "Calibration cache release ABI layout changed");

// Calls on a context are serialized internally. To keep an error associated
// with its failed call, callers must serialize that call together with
// M_CalibrationGetLastError. M_CalibrationDestroy must not overlap any other
// operation on the same context.
extern "C" COLORVISIONCORE_API int __cdecl M_CalibrationCreate(void** context);
extern "C" COLORVISIONCORE_API int __cdecl M_CalibrationDestroy(void* context);
extern "C" COLORVISIONCORE_API int __cdecl M_CalibrationClear(void* context);
extern "C" COLORVISIONCORE_API int __cdecl M_CalibrationLoadFileW(
    void* context,
    std::int32_t calibrationType,
    const wchar_t* filePath);
// Mutable RAW compatibility entry point. RAW and CIE ranges must not overlap;
// color-transform exposure values must be finite and positive.
extern "C" COLORVISIONCORE_API int __cdecl M_CalibrationExecute(
    void* context,
    std::uint32_t width,
    std::uint32_t height,
    std::uint32_t bitsPerChannel,
    std::uint32_t channels,
    std::uint8_t* rawData,
    std::uint64_t rawByteLength,
    float* cieData,
    std::uint64_t cieFloatCount,
    const MCalibrationExecutionOptionsV1* options);
// Read-only-source variant. correctedRawData may be null when the selected
// template contains a luminance/color transform. Source RAW, corrected RAW and
// CIE ranges must not overlap. Output buffers are undefined after a failure,
// while sourceRawData is never modified.
extern "C" COLORVISIONCORE_API int __cdecl M_CalibrationExecuteToV1(
    void* context,
    std::uint32_t width,
    std::uint32_t height,
    std::uint32_t bitsPerChannel,
    std::uint32_t channels,
    const std::uint8_t* sourceRawData,
    std::uint64_t sourceRawByteLength,
    std::uint8_t* correctedRawData,
    std::uint64_t correctedRawByteLength,
    float* cieData,
    std::uint64_t cieFloatCount,
    const MCalibrationExecutionOptionsV1* options);
// Returns the required UTF-8 byte count including the null terminator. If the
// supplied buffer is large enough it is populated as well.
extern "C" COLORVISIONCORE_API int __cdecl M_CalibrationGetLastError(
    void* context,
    char* buffer,
    std::uint32_t bufferLength);
extern "C" COLORVISIONCORE_API int __cdecl M_CalibrationGetItemCount(void* context);

// Process-wide, per-file parsed calibration cache. The default budget is 4
// GiB and may be overridden before first use with the
// COLORVISION_CALIBRATION_CACHE_MB environment variable (0 disables retained
// entries but still permits the current load). Query calls never affect LRU
// order, hit counts, or generation.
extern "C" COLORVISIONCORE_API int __cdecl M_CalibrationCacheGetStatsV1(
    MCalibrationCacheStatsV1* stats);
// Entries are ordered by most-recent access. pathCapacity is measured in
// UTF-16 wchar_t code units. A null/undersized path buffer is not an error: no
// partial path is copied and pathCharacterCount reports the required size.
// If cache mutation makes index invalid, INVALID_ARGUMENT is returned; callers
// should restart enumeration. A generation different from the preceding stats
// snapshot likewise means the enumeration should be retried.
extern "C" COLORVISIONCORE_API int __cdecl M_CalibrationCacheGetEntryV1(
    std::uint32_t index,
    MCalibrationCacheEntryV1* entry,
    wchar_t* path,
    std::uint32_t pathCapacity);
// Removes all cache-owned references. Existing contexts remain valid and own
// their data until destroyed; active* fields report that deferred memory at
// release time. Loads that have not yet published a context lease are canceled
// at the release boundary and counted as deferred active entries while their
// native loader unwinds (activeOwnerCount remains zero for those entries).
extern "C" COLORVISIONCORE_API int __cdecl M_CalibrationCacheReleaseV1(
    MCalibrationCacheReleaseResultV1* result);

// Zero-copy POI calculation over planar float CIE data (...XXX...YYY...ZZZ,
// or one Y plane). The input buffer is borrowed only for this call.
enum MPoiResultCode : std::int32_t {
    M_POI_OK = 1,
    M_POI_INVALID_ARGUMENT = -1,
    M_POI_INTERNAL_ERROR = -2,
};

struct MPoiRequestV1 {
    std::int32_t type; // 0: solid point, 1: circle, 2: rectangle
    std::int32_t x;
    std::int32_t y;
    std::int32_t width;
    std::int32_t height;
};

struct MPoiResultV1 {
    float X;
    float Y;
    float Z;
    float x;
    float y;
    float u;
    float v;
    float cct;
    float wave;
};

enum MPoiOptionsFlagsV2 : std::uint32_t {
    M_POI_OPTION_PERCENT_THRESHOLD = 1U,
    M_POI_OPTION_APPLY_MNP = 2U,
};

struct MPoiOptionsV2 {
    std::uint32_t structSize;
    std::uint32_t flags;
    std::int32_t filterMode; // 0: none, 1: per-plane, 2: shared XYZ mask, 3: preserve area
    std::int32_t xyzChannel; // selected plane for filterMode 2
    float threshold;
    float maxPercent;
    float scaleX;
    float scaleY;
    float scaleZ;
    std::uint32_t reserved[3];
};

static_assert(sizeof(MPoiRequestV1) == 20, "POI request ABI layout changed");
static_assert(sizeof(MPoiResultV1) == 36, "POI result ABI layout changed");
static_assert(sizeof(MPoiOptionsV2) == 48, "POI V2 options ABI layout changed");

extern "C" COLORVISIONCORE_API int __cdecl M_CalculatePoiBatchV1(
    std::int32_t width,
    std::int32_t height,
    std::int32_t bitsPerChannel,
    std::int32_t channels,
    const float* cieData,
    std::uint64_t cieFloatCount,
    const MPoiRequestV1* requests,
    std::uint32_t requestCount,
    MPoiResultV1* results);

extern "C" COLORVISIONCORE_API int __cdecl M_CalculatePoiBatchV2(
    std::int32_t width,
    std::int32_t height,
    std::int32_t bitsPerChannel,
    std::int32_t channels,
    const float* cieData,
    std::uint64_t cieFloatCount,
    const MPoiRequestV1* requests,
    std::uint32_t requestCount,
    const MPoiOptionsV2* options,
    MPoiResultV1* results);

typedef void(__stdcall* CVNativeLogCallback)(int source, int level, const char* message);

extern "C" COLORVISIONCORE_API void M_SetLogCallback(CVNativeLogCallback callback);
extern "C" COLORVISIONCORE_API void M_SetLogEnabled(int enabled);
extern "C" COLORVISIONCORE_API void M_SetLogLevel(int level);
extern "C" COLORVISIONCORE_API void M_EnableNativeSink(int enabled);


