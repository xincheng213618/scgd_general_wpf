#include "../algorithm/calibration/calibration_context.h"
#include "../algorithm/calibration/calibration_color_eval.h"
#include "../native_log.h"
#include "../../include/opencv_media_export.h"

#include <algorithm>
#include <cstring>
#include <cmath>
#include <limits>
#include <new>
#include <string>

namespace {

using cvcore::calibration::CalibrationContext;
using cvcore::calibration::CalibrationFileDigest;
using cvcore::calibration::CalibrationType;
using cvcore::calibration::ColorTransform;
using cvcore::calibration::ExecutionOptions;
using cvcore::calibration::ImageView;

thread_local std::string globalError;

CalibrationContext* asContext(void* context) noexcept
{
    return static_cast<CalibrationContext*>(context);
}

void setGlobalError(const char* message) noexcept
{
    try {
        globalError = message == nullptr ? "Unknown native calibration error" : message;
    }
    catch (...) {
        globalError.clear();
    }
}

int failImpl(const char* operation, void* context, int result, const char* message) noexcept
{
    bool contextRecorded = false;
    if (context != nullptr) {
        try {
            asContext(context)->recordError(message == nullptr ? "Unknown native calibration error" : message);
            contextRecorded = true;
        }
        catch (...) {
        }
    }
    if (!contextRecorded) {
        setGlobalError(message);
    }

    const auto level = result == M_CALIBRATION_INTERNAL_ERROR
        ? cvnative::LogLevel::Error
        : result == M_CALIBRATION_LOAD_FAILED || result == M_CALIBRATION_EXECUTE_FAILED
        ? cvnative::LogLevel::Warn
        : cvnative::LogLevel::Debug;
    cvnative::LogFailure(level, "calibration.export", operation, result, message);
    return result;
}

ExecutionOptions convertOptions(const MCalibrationExecutionOptionsV1* value)
{
    ExecutionOptions result;
    if (value == nullptr) {
        return result;
    }

    result.interleavedBgr = value->interleavedBgr != 0;
    result.rgbType = value->rgbType;
    result.roi = { value->roiX, value->roiY, value->roiWidth, value->roiHeight };
    result.ob = { value->obLeft, value->obRight, value->obTop, value->obBottom };
    result.exposure = { value->exposureX, value->exposureY, value->exposureZ };
    return result;
}

void storeColorTransform(const ColorTransform& value, bool interleavedBgr, MRawColorTransformV1& result)
{
    result = {};
    result.structSize = sizeof(result);
    result.calibrationType = static_cast<std::int32_t>(value.type);
    result.channels = value.channels;
    result.kind = value.kind;
    result.interleavedBgr = interleavedBgr;
    std::copy(value.coefficients.begin(), value.coefficients.end(), result.coefficients);
}

bool overlapsColorTransform(const void* pixels, std::uint64_t byteLength, const MRawColorTransformV1* transform)
{
    if (pixels == nullptr || byteLength == 0 || transform == nullptr) return false;
    const auto pixelsAddress = reinterpret_cast<std::uintptr_t>(pixels);
    const auto transformAddress = reinterpret_cast<std::uintptr_t>(transform);
    return pixelsAddress <= transformAddress
        ? transformAddress - pixelsAddress < byteLength
        : pixelsAddress - transformAddress < sizeof(*transform);
}

} // namespace

#define fail(...) failImpl(__func__, __VA_ARGS__)

extern "C" COLORVISIONCORE_API int __cdecl M_CalibrationGetColorTransformV1(
    void* context, const MCalibrationExecutionOptionsV1* options, MRawColorTransformV1* transform)
{
    if (context == nullptr || options == nullptr || options->structSize < sizeof(*options)
        || transform == nullptr || transform->structSize < sizeof(*transform))
        return fail(context, M_CALIBRATION_INVALID_ARGUMENT, "Invalid color snapshot arguments");
    try {
        ColorTransform value;
        if (!asContext(context)->colorTransform(convertOptions(options), value))
            return fail(context, M_CALIBRATION_UNSUPPORTED, "Context has no unique color transform");
        for (double factor : value.coefficients)
            if (!std::isfinite(factor)) return fail(context, M_CALIBRATION_INVALID_ARGUMENT, "Non-finite color factor");
        storeColorTransform(value, options->interleavedBgr != 0, *transform);
        return M_CALIBRATION_OK;
    }
    catch (const std::exception& ex) { return fail(context, M_CALIBRATION_INTERNAL_ERROR, ex.what()); }
    catch (...) { return fail(context, M_CALIBRATION_INTERNAL_ERROR, "Unable to capture color transform"); }
}

extern "C" COLORVISIONCORE_API int __cdecl M_CalibrationCreate(void** context)
{
    if (context == nullptr) {
        return fail(nullptr, M_CALIBRATION_INVALID_ARGUMENT, "Calibration context output pointer is null");
    }
    *context = nullptr;

    try {
        *context = new CalibrationContext();
        globalError.clear();
        cvnative::LogEvent(cvnative::LogLevel::Info, "calibration.lifecycle", __func__, "context created");
        return M_CALIBRATION_OK;
    }
    catch (const std::bad_alloc&) {
        return fail(nullptr, M_CALIBRATION_INTERNAL_ERROR, "Unable to allocate calibration context");
    }
    catch (const std::exception& ex) {
        return fail(nullptr, M_CALIBRATION_INTERNAL_ERROR, ex.what());
    }
    catch (...) {
        return fail(nullptr, M_CALIBRATION_INTERNAL_ERROR, "Unknown error while creating calibration context");
    }
}

extern "C" COLORVISIONCORE_API int __cdecl M_CalibrationDestroy(void* context)
{
    if (context == nullptr) {
        return M_CALIBRATION_OK;
    }
    try {
        delete asContext(context);
        globalError.clear();
        cvnative::LogEvent(cvnative::LogLevel::Info, "calibration.lifecycle", __func__, "context destroyed");
        return M_CALIBRATION_OK;
    }
    catch (...) {
        return fail(nullptr, M_CALIBRATION_INTERNAL_ERROR, "Unknown error while destroying calibration context");
    }
}

extern "C" COLORVISIONCORE_API int __cdecl M_CalibrationClear(void* context)
{
    if (context == nullptr) {
        return fail(nullptr, M_CALIBRATION_INVALID_ARGUMENT, "Calibration context is null");
    }
    try {
        const int result = asContext(context)->clear() ? M_CALIBRATION_OK : M_CALIBRATION_INTERNAL_ERROR;
        if (result == M_CALIBRATION_OK) {
            globalError.clear();
        }
        else {
            cvnative::LogFailure(cvnative::LogLevel::Error, "calibration.export", __func__, result);
        }
        return result;
    }
    catch (const std::exception& ex) {
        return fail(context, M_CALIBRATION_INTERNAL_ERROR, ex.what());
    }
    catch (...) {
        return fail(context, M_CALIBRATION_INTERNAL_ERROR, "Unknown error while clearing calibration context");
    }
}

extern "C" COLORVISIONCORE_API int __cdecl M_CalibrationTransferWorkBuffersV1(
    void* destinationContext, void* sourceContext)
{
    if (destinationContext == nullptr || sourceContext == nullptr) {
        return fail(destinationContext, M_CALIBRATION_INVALID_ARGUMENT, "Calibration destination or source context is null");
    }
    try {
        asContext(destinationContext)->transferWorkBuffersFrom(*asContext(sourceContext));
        globalError.clear();
        return M_CALIBRATION_OK;
    }
    catch (const std::exception& ex) {
        return fail(destinationContext, M_CALIBRATION_INTERNAL_ERROR, ex.what());
    }
    catch (...) {
        return fail(destinationContext, M_CALIBRATION_INTERNAL_ERROR, "Unknown error while transferring calibration work buffers");
    }
}

extern "C" COLORVISIONCORE_API int __cdecl M_CalibrationLoadFileW(
    void* context,
    std::int32_t calibrationType,
    const wchar_t* filePath)
{
    if (context == nullptr || filePath == nullptr || *filePath == L'\0') {
        return fail(context, M_CALIBRATION_INVALID_ARGUMENT, "Calibration context or file path is invalid");
    }

    try {
        const auto type = static_cast<CalibrationType>(calibrationType);
        if (!cvcore::calibration::isSupportedCalibration(type)) {
            return fail(context, M_CALIBRATION_UNSUPPORTED, type == CalibrationType::LumColor
                ? "LumColor is reserved and has no implementation"
                : "Unsupported calibration type");
        }
        const int result = asContext(context)->load(type, std::filesystem::path(filePath))
            ? M_CALIBRATION_OK
            : M_CALIBRATION_LOAD_FAILED;
        if (result == M_CALIBRATION_OK) {
            globalError.clear();
            cvnative::LogEvent(cvnative::LogLevel::Info, "calibration.lifecycle", __func__, "calibration loaded");
        }
        else {
            cvnative::LogFailure(cvnative::LogLevel::Warn, "calibration.export", __func__, result);
        }
        return result;
    }
    catch (const std::exception& ex) {
        return fail(context, M_CALIBRATION_INTERNAL_ERROR, ex.what());
    }
    catch (...) {
        return fail(context, M_CALIBRATION_INTERNAL_ERROR, "Unknown error while loading calibration file");
    }
}

extern "C" COLORVISIONCORE_API int __cdecl M_CalibrationGetInputDepthV1(void* context, std::uint32_t* bitsPerChannel)
{
    if (context == nullptr || bitsPerChannel == nullptr)
        return fail(context, M_CALIBRATION_INVALID_ARGUMENT, "Context and input depth output are required");
    try {
        if (!asContext(context)->requiredInputDepth(*bitsPerChannel))
            return M_CALIBRATION_INVALID_ARGUMENT;
        globalError.clear();
        return M_CALIBRATION_OK;
    }
    catch (const std::exception& ex) {
        return fail(context, M_CALIBRATION_INTERNAL_ERROR, ex.what());
    }
    catch (...) {
        return fail(context, M_CALIBRATION_INTERNAL_ERROR, "Unknown error while reading calibration input depth");
    }
}

extern "C" COLORVISIONCORE_API int __cdecl M_CalibrationLoadMapWithSha256V1W(
    void* context, std::int32_t calibrationType, const wchar_t* filePath,
    std::uint8_t* sha256, std::uint32_t sha256Size)
{
    CalibrationFileDigest digest{};
    if (context == nullptr || filePath == nullptr || *filePath == L'\0'
        || sha256 == nullptr || sha256Size != digest.size()) {
        return fail(context, M_CALIBRATION_INVALID_ARGUMENT, "Calibration context, file path, or SHA-256 buffer is invalid");
    }

    const auto type = static_cast<CalibrationType>(calibrationType);
    if (type != CalibrationType::Dsnu && type != CalibrationType::Uniformity) {
        return fail(context, M_CALIBRATION_UNSUPPORTED, "Verified map loading supports DSNU and Uniformity");
    }
    try {
        if (!asContext(context)->load(type, std::filesystem::path(filePath), &digest)) {
            cvnative::LogFailure(cvnative::LogLevel::Warn, "calibration.export", __func__, M_CALIBRATION_LOAD_FAILED);
            return M_CALIBRATION_LOAD_FAILED;
        }
        std::copy(digest.begin(), digest.end(), sha256);
        globalError.clear();
        cvnative::LogEvent(cvnative::LogLevel::Info, "calibration.lifecycle", __func__, "calibration loaded");
        return M_CALIBRATION_OK;
    }
    catch (const std::exception& ex) {
        return fail(context, M_CALIBRATION_INTERNAL_ERROR, ex.what());
    }
    catch (...) {
        return fail(context, M_CALIBRATION_INTERNAL_ERROR, "Unknown error while verifying a calibration map");
    }
}

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
    const MCalibrationExecutionOptionsV1* options)
{
    if (context == nullptr || rawData == nullptr) {
        return fail(context, M_CALIBRATION_INVALID_ARGUMENT, "Calibration context or RAW data is null");
    }
    if (options != nullptr && options->structSize < sizeof(MCalibrationExecutionOptionsV1)) {
        return fail(context, M_CALIBRATION_INVALID_ARGUMENT, "Calibration execution options have an incompatible size");
    }
    if (rawByteLength > (std::numeric_limits<std::size_t>::max)()
        || cieFloatCount > (std::numeric_limits<std::size_t>::max)()) {
        return fail(context, M_CALIBRATION_INVALID_ARGUMENT, "Calibration buffer length exceeds the process address space");
    }

    try {
        const ImageView raw{ width, height, bitsPerChannel, channels, rawData, static_cast<std::size_t>(rawByteLength) };
        const int result = asContext(context)->execute(
            raw, cieData, static_cast<std::size_t>(cieFloatCount), convertOptions(options))
            ? M_CALIBRATION_OK
            : M_CALIBRATION_EXECUTE_FAILED;
        if (result == M_CALIBRATION_OK) {
            globalError.clear();
        }
        else {
            cvnative::LogFailure(cvnative::LogLevel::Warn, "calibration.export", __func__, result);
        }
        return result;
    }
    catch (const std::exception& ex) {
        return fail(context, M_CALIBRATION_INTERNAL_ERROR, ex.what());
    }
    catch (...) {
        return fail(context, M_CALIBRATION_INTERNAL_ERROR, "Unknown error while executing calibration");
    }
}

extern "C" COLORVISIONCORE_API int __cdecl M_CalibrationExecuteRawWithFlipV1(
    void* context,
    std::uint32_t width,
    std::uint32_t height,
    std::uint32_t bitsPerChannel,
    std::uint32_t channels,
    std::uint8_t* rawData,
    std::uint64_t rawByteLength,
    const MCalibrationExecutionOptionsV1* options,
    std::int32_t flipMode)
{
    if (context == nullptr || rawData == nullptr || flipMode < -1 || flipMode > 1
        || (options != nullptr && options->structSize < sizeof(*options))
        || rawByteLength > (std::numeric_limits<std::size_t>::max)()) {
        return fail(context, M_CALIBRATION_INVALID_ARGUMENT, "Invalid RAW calibration/mirror arguments");
    }
    try {
        ExecutionOptions executionOptions = convertOptions(options);
        executionOptions.rawOutputFlip = flipMode;
        const ImageView raw{ width, height, bitsPerChannel, channels, rawData, static_cast<std::size_t>(rawByteLength) };
        const int result = asContext(context)->execute(raw, nullptr, 0, executionOptions)
            ? M_CALIBRATION_OK : M_CALIBRATION_EXECUTE_FAILED;
        if (result == M_CALIBRATION_OK) globalError.clear();
        else cvnative::LogFailure(cvnative::LogLevel::Warn, "calibration.export", __func__, result);
        return result;
    }
    catch (const std::exception& ex) {
        return fail(context, M_CALIBRATION_INTERNAL_ERROR, ex.what());
    }
    catch (...) {
        return fail(context, M_CALIBRATION_INTERNAL_ERROR, "Unknown error while calibrating/mirroring RAW");
    }
}

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
    const MCalibrationExecutionOptionsV1* options)
{
    if (context == nullptr || sourceRawData == nullptr) {
        return fail(context, M_CALIBRATION_INVALID_ARGUMENT, "Calibration context or source RAW data is null");
    }
    if (correctedRawData == nullptr && correctedRawByteLength != 0) {
        return fail(context, M_CALIBRATION_INVALID_ARGUMENT, "Corrected RAW length is nonzero for a null output pointer");
    }
    if (options != nullptr && options->structSize < sizeof(MCalibrationExecutionOptionsV1)) {
        return fail(context, M_CALIBRATION_INVALID_ARGUMENT, "Calibration execution options have an incompatible size");
    }
    if (sourceRawByteLength > (std::numeric_limits<std::size_t>::max)()
        || correctedRawByteLength > (std::numeric_limits<std::size_t>::max)()
        || cieFloatCount > (std::numeric_limits<std::size_t>::max)()) {
        return fail(context, M_CALIBRATION_INVALID_ARGUMENT, "Calibration buffer length exceeds the process address space");
    }

    try {
        const ImageView sourceRaw{
            width, height, bitsPerChannel, channels,
            const_cast<std::uint8_t*>(sourceRawData),
            static_cast<std::size_t>(sourceRawByteLength)
        };
        ImageView correctedRaw{
            width, height, bitsPerChannel, channels,
            correctedRawData,
            static_cast<std::size_t>(correctedRawByteLength)
        };
        ImageView* correctedRawView = correctedRawData == nullptr ? nullptr : &correctedRaw;
        const int result = asContext(context)->executeTo(
            sourceRaw, correctedRawView, cieData,
            static_cast<std::size_t>(cieFloatCount), convertOptions(options))
            ? M_CALIBRATION_OK
            : M_CALIBRATION_EXECUTE_FAILED;
        if (result == M_CALIBRATION_OK) {
            globalError.clear();
        }
        else {
            cvnative::LogFailure(cvnative::LogLevel::Warn, "calibration.export", __func__, result);
        }
        return result;
    }
    catch (const std::exception& ex) {
        return fail(context, M_CALIBRATION_INTERNAL_ERROR, ex.what());
    }
    catch (...) {
        return fail(context, M_CALIBRATION_INTERNAL_ERROR, "Unknown error while executing read-only-source calibration");
    }
}

extern "C" COLORVISIONCORE_API int __cdecl M_CalibrationExecuteFrameV2(
    void* context,
    const MCalibrationFrameRequestV2* request,
    std::uint8_t* rawData,
    std::uint64_t rawByteLength,
    float* cieData,
    std::uint64_t cieByteLength,
    MRawColorTransformV1* transform)
{
    if (context == nullptr || request == nullptr || request->structSize != sizeof(*request)
        || request->rawLayout > M_CALIBRATION_RGB_PLANAR || request->outputMode > M_CALIBRATION_DEFERRED_COLOR
        || request->rawFlip > M_CALIBRATION_FLIP_BOTH || rawData == nullptr
        || rawByteLength > (std::numeric_limits<std::size_t>::max)()
        || cieByteLength > (std::numeric_limits<std::size_t>::max)()
        || cieByteLength % sizeof(float) != 0) {
        return fail(context, M_CALIBRATION_INVALID_ARGUMENT, "Invalid calibration frame request");
    }

    const bool useColor = request->outputMode != M_CALIBRATION_RAW_ONLY;
    if ((useColor && transform == nullptr)
        || (request->outputMode == M_CALIBRATION_FULL_CIE
            ? cieData == nullptr || request->rawFlip != M_CALIBRATION_FLIP_NONE
            : cieData != nullptr || cieByteLength != 0)) {
        return fail(context, M_CALIBRATION_INVALID_ARGUMENT, "Calibration outputs do not match the requested mode");
    }
    if (overlapsColorTransform(rawData, rawByteLength, transform)
        || overlapsColorTransform(cieData, cieByteLength, transform)) {
        return fail(context, M_CALIBRATION_INVALID_ARGUMENT, "Color transform and pixel buffers must not overlap");
    }

    try {
        ExecutionOptions options;
        options.interleavedBgr = request->rawLayout != M_CALIBRATION_RGB_PLANAR;
        options.preserveRawLayout = true;
        std::copy_n(request->sensorRoi, options.roi.size(), options.roi.begin());
        std::copy_n(request->opticalBlack, options.ob.size(), options.ob.begin());
        std::copy_n(request->exposure, options.exposure.size(), options.exposure.begin());
        switch (request->rawFlip) {
        case M_CALIBRATION_FLIP_NONE: break;
        case M_CALIBRATION_FLIP_VERTICAL: options.rawOutputFlip = 0; break;
        case M_CALIBRATION_FLIP_HORIZONTAL: options.rawOutputFlip = 1; break;
        case M_CALIBRATION_FLIP_BOTH: options.rawOutputFlip = -1; break;
        }

        const ImageView raw{ request->width, request->height, request->bitsPerChannel,
            request->rawLayout == M_CALIBRATION_MONO ? 1U : 3U, rawData, static_cast<std::size_t>(rawByteLength) };
        ColorTransform color;
        if (!asContext(context)->execute(raw, cieData, static_cast<std::size_t>(cieByteLength) / sizeof(float),
                options, useColor ? &color : nullptr)) {
            cvnative::LogFailure(cvnative::LogLevel::Warn, "calibration.export", __func__, M_CALIBRATION_EXECUTE_FAILED);
            return M_CALIBRATION_EXECUTE_FAILED;
        }

        if (useColor) {
            MRawColorTransformV1 result;
            storeColorTransform(color, options.interleavedBgr, result);
            const std::size_t pixels = static_cast<std::size_t>(raw.width) * raw.height;
            if (!cvcore::calibration::color::frameValuesAreFinite(raw.data, pixels, raw.bitsPerChannel, result, cieData)) {
                return fail(context, M_CALIBRATION_EXECUTE_FAILED,
                    "Color output exceeds the finite float range for this frame; check calibration coefficients, gain and exposure");
            }
            *transform = result;
        }
        else if (transform != nullptr) *transform = {};
        globalError.clear();
        return M_CALIBRATION_OK;
    }
    catch (const std::exception& ex) {
        return fail(context, M_CALIBRATION_INTERNAL_ERROR, ex.what());
    }
    catch (...) {
        return fail(context, M_CALIBRATION_INTERNAL_ERROR, "Unknown error while executing calibration frame request");
    }
}

extern "C" COLORVISIONCORE_API int __cdecl M_CalibrationGetLastError(
    void* context,
    char* buffer,
    std::uint32_t bufferLength)
{
    try {
        std::string message = context == nullptr ? globalError : asContext(context)->lastError();
        if (message.empty()) message = globalError;
        if (message.size() >= static_cast<std::size_t>((std::numeric_limits<int>::max)())) {
            cvnative::LogFailure(cvnative::LogLevel::Error, "calibration.export", __func__, M_CALIBRATION_INTERNAL_ERROR,
                "last-error message exceeds the supported return length");
            return M_CALIBRATION_INTERNAL_ERROR;
        }

        const auto required = static_cast<int>(message.size() + 1);
        if (buffer != nullptr && bufferLength >= static_cast<std::uint32_t>(required)) {
            std::memcpy(buffer, message.c_str(), static_cast<std::size_t>(required));
        }
        return required;
    }
    catch (...) {
        cvnative::LogException("calibration.export", __func__, M_CALIBRATION_INTERNAL_ERROR, "unknown");
        return M_CALIBRATION_INTERNAL_ERROR;
    }
}

extern "C" COLORVISIONCORE_API int __cdecl M_CalibrationGetItemCount(void* context)
{
    if (context == nullptr) {
        return fail(nullptr, M_CALIBRATION_INVALID_ARGUMENT, "Calibration context is null");
    }
    try {
        const auto count = asContext(context)->itemCount();
        if (count > static_cast<std::size_t>((std::numeric_limits<int>::max)())) {
            return fail(context, M_CALIBRATION_INTERNAL_ERROR, "Calibration item count exceeds the supported return range");
        }
        return static_cast<int>(count);
    }
    catch (...) {
        return fail(context, M_CALIBRATION_INTERNAL_ERROR, "Unknown error while reading calibration item count");
    }
}

extern "C" COLORVISIONCORE_API int __cdecl M_CalibrationGetItemEstimatedBytesV1(
    void* context, std::uint32_t itemIndex, std::uint64_t* estimatedBytes)
{
    if (context == nullptr || estimatedBytes == nullptr)
        return fail(context, M_CALIBRATION_INVALID_ARGUMENT, "Context and item size output are required");
    try {
        if (!asContext(context)->itemEstimatedBytes(itemIndex, *estimatedBytes))
            return fail(context, M_CALIBRATION_INVALID_ARGUMENT, "Calibration item index is out of range");
        globalError.clear();
        return M_CALIBRATION_OK;
    }
    catch (const std::exception& ex) {
        return fail(context, M_CALIBRATION_INTERNAL_ERROR, ex.what());
    }
    catch (...) {
        return fail(context, M_CALIBRATION_INTERNAL_ERROR, "Unknown error while reading calibration item size");
    }
}

extern "C" COLORVISIONCORE_API int __cdecl M_CalibrationCacheGetStatsV1(
    MCalibrationCacheStatsV1* stats)
{
    if (stats == nullptr || stats->structSize < sizeof(MCalibrationCacheStatsV1)) {
        return fail(nullptr, M_CALIBRATION_INVALID_ARGUMENT,
            "Calibration cache stats structure is null or incompatible");
    }
    try {
        const auto snapshot = cvcore::calibration::calibrationFileCacheStats();
        *stats = MCalibrationCacheStatsV1{
            sizeof(MCalibrationCacheStatsV1),
            snapshot.entryCount,
            snapshot.generation,
            snapshot.estimatedMemoryBytes,
            snapshot.budgetBytes,
            snapshot.hitCount,
            snapshot.missCount,
        };
        globalError.clear();
        return M_CALIBRATION_OK;
    }
    catch (const std::exception& ex) {
        return fail(nullptr, M_CALIBRATION_INTERNAL_ERROR, ex.what());
    }
    catch (...) {
        return fail(nullptr, M_CALIBRATION_INTERNAL_ERROR,
            "Unknown error while reading calibration cache stats");
    }
}

extern "C" COLORVISIONCORE_API int __cdecl M_CalibrationCacheSetBudgetV1(std::uint64_t budgetBytes)
{
    try {
        cvcore::calibration::setCalibrationFileCacheBudget(budgetBytes);
        globalError.clear();
        return M_CALIBRATION_OK;
    }
    catch (const std::exception& ex) {
        return fail(nullptr, M_CALIBRATION_INTERNAL_ERROR, ex.what());
    }
    catch (...) {
        return fail(nullptr, M_CALIBRATION_INTERNAL_ERROR, "Unknown error while setting the calibration cache budget");
    }
}

extern "C" COLORVISIONCORE_API int __cdecl M_CalibrationCacheGetEntryV1(
    std::uint32_t index,
    MCalibrationCacheEntryV1* entry,
    wchar_t* path,
    std::uint32_t pathCapacity)
{
    if (entry == nullptr || entry->structSize < sizeof(MCalibrationCacheEntryV1)) {
        return fail(nullptr, M_CALIBRATION_INVALID_ARGUMENT,
            "Calibration cache entry structure is null or incompatible");
    }
    try {
        cvcore::calibration::CalibrationFileCacheEntry snapshot;
        if (!cvcore::calibration::calibrationFileCacheEntry(index, snapshot)) {
            return fail(nullptr, M_CALIBRATION_INVALID_ARGUMENT,
                "Calibration cache entry index is outside the current snapshot");
        }
        const std::wstring nativePath = snapshot.file.native();
        if (nativePath.size() >= (std::numeric_limits<std::uint32_t>::max)()) {
            return fail(nullptr, M_CALIBRATION_INTERNAL_ERROR,
                "Calibration cache path is too long for the public ABI");
        }
        const auto required = static_cast<std::uint32_t>(nativePath.size() + 1);
        *entry = MCalibrationCacheEntryV1{
            sizeof(MCalibrationCacheEntryV1),
            static_cast<std::int32_t>(snapshot.type),
            snapshot.flags,
            required,
            snapshot.generation,
            snapshot.fileBytes,
            snapshot.estimatedMemoryBytes,
            snapshot.hitCount,
            snapshot.lastAccessSequence,
            snapshot.activeOwnerCount,
            0,
        };
        if (path != nullptr && pathCapacity >= required) {
            std::memcpy(path, nativePath.c_str(), static_cast<std::size_t>(required) * sizeof(wchar_t));
        }
        globalError.clear();
        return M_CALIBRATION_OK;
    }
    catch (const std::exception& ex) {
        return fail(nullptr, M_CALIBRATION_INTERNAL_ERROR, ex.what());
    }
    catch (...) {
        return fail(nullptr, M_CALIBRATION_INTERNAL_ERROR,
            "Unknown error while reading a calibration cache entry");
    }
}

extern "C" COLORVISIONCORE_API int __cdecl M_CalibrationCacheReleaseV1(
    MCalibrationCacheReleaseResultV1* result)
{
    if (result == nullptr || result->structSize < sizeof(MCalibrationCacheReleaseResultV1)) {
        return fail(nullptr, M_CALIBRATION_INVALID_ARGUMENT,
            "Calibration cache release structure is null or incompatible");
    }
    try {
        const auto released = cvcore::calibration::releaseCalibrationFileCache();
        *result = MCalibrationCacheReleaseResultV1{
            sizeof(MCalibrationCacheReleaseResultV1),
            released.releasedEntryCount,
            released.releasedEstimatedMemoryBytes,
            released.activeEntryCount,
            released.activeOwnerCount,
            released.activeEstimatedMemoryBytes,
            released.generation,
        };
        globalError.clear();
        return M_CALIBRATION_OK;
    }
    catch (const std::exception& ex) {
        return fail(nullptr, M_CALIBRATION_INTERNAL_ERROR, ex.what());
    }
    catch (...) {
        return fail(nullptr, M_CALIBRATION_INTERNAL_ERROR,
            "Unknown error while releasing the calibration file cache");
    }
}
