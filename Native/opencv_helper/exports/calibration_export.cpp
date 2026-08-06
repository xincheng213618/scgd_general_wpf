#include "../algorithm/calibration/calibration_context.h"
#include "../native_log.h"
#include "../../include/opencv_media_export.h"

#include <algorithm>
#include <cstring>
#include <limits>
#include <new>
#include <string>

namespace {

using cvcore::calibration::CalibrationContext;
using cvcore::calibration::CalibrationType;
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

} // namespace

#define fail(...) failImpl(__func__, __VA_ARGS__)

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
