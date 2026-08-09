#include "../../Native/include/opencv_media_export.h"

#include <Windows.h>
#include <bcrypt.h>

#include <opencv2/calib3d.hpp>
#include <opencv2/core.hpp>
#include <opencv2/imgproc.hpp>

#include <array>
#include <algorithm>
#include <atomic>
#include <chrono>
#include <cmath>
#include <condition_variable>
#include <cstdint>
#include <cstring>
#include <cwctype>
#include <exception>
#include <filesystem>
#include <fstream>
#include <iomanip>
#include <iostream>
#include <limits>
#include <memory>
#include <mutex>
#include <sstream>
#include <stdexcept>
#include <string>
#include <string_view>
#include <thread>
#include <utility>
#include <vector>

#pragma comment(lib, "bcrypt.lib")

namespace {

struct ContextDeleter {
    void operator()(void* value) const noexcept
    {
        if (value != nullptr) {
            M_CalibrationDestroy(value);
        }
    }
};

using Context = std::unique_ptr<void, ContextDeleter>;

struct RawImage {
    std::uint32_t width = 0;
    std::uint32_t height = 0;
    std::uint32_t bitsPerChannel = 0;
    std::uint32_t channels = 0;
    std::array<float, 3> exposure{};
    std::vector<std::uint8_t> data;
};

struct CalibrationFile {
    std::int32_t type;
    const char* name;
    std::filesystem::path path;
    const char* expectedHash;
};

struct TemporaryDirectory {
    explicit TemporaryDirectory(std::filesystem::path value)
        : path(std::move(value))
    {
        std::filesystem::create_directories(path);
    }

    ~TemporaryDirectory()
    {
        std::error_code error;
        std::filesystem::remove_all(path, error);
    }

    std::filesystem::path path;
};

std::string nativeError(void* context)
{
    const int required = M_CalibrationGetLastError(context, nullptr, 0);
    if (required <= 1) return {};
    std::vector<char> buffer(static_cast<std::size_t>(required));
    M_CalibrationGetLastError(context, buffer.data(), static_cast<std::uint32_t>(buffer.size()));
    return buffer.data();
}

void requireResult(int result, void* context, const std::string& operation)
{
    if (result == M_CALIBRATION_OK) return;
    throw std::runtime_error(operation + " failed (" + std::to_string(result) + "): " + nativeError(context));
}

Context createContext()
{
    void* value = nullptr;
    requireResult(M_CalibrationCreate(&value), nullptr, "M_CalibrationCreate");
    if (value == nullptr) throw std::runtime_error("M_CalibrationCreate returned a null context");
    return Context(value);
}

template<typename T>
T readValue(std::ifstream& stream)
{
    T value{};
    stream.read(reinterpret_cast<char*>(&value), sizeof(value));
    if (!stream) throw std::runtime_error("Unexpected end of CVRAW header");
    return value;
}

RawImage readCvRaw(const std::filesystem::path& path)
{
    std::ifstream stream(path, std::ios::binary);
    if (!stream) throw std::runtime_error("Unable to open CVRAW: " + path.string());

    std::array<char, 5> magic{};
    stream.read(magic.data(), magic.size());
    if (std::string_view(magic.data(), magic.size()) != "CVCIE") {
        throw std::runtime_error("Invalid CVRAW magic");
    }

    const auto version = readValue<std::uint32_t>(stream);
    const auto fileNameLength = readValue<std::int32_t>(stream);
    if (fileNameLength < 0) throw std::runtime_error("Invalid CVRAW embedded filename length");
    stream.seekg(fileNameLength, std::ios::cur);
    if (version == 3) {
        readValue<std::int32_t>(stream);
    }
    else if (version != 1 && version != 2) {
        throw std::runtime_error("Unsupported CVRAW version");
    }

    readValue<float>(stream); // gain
    const auto channelCount = readValue<std::int32_t>(stream);
    if (channelCount <= 0 || channelCount > 16) throw std::runtime_error("Invalid CVRAW channel count");

    RawImage result;
    result.channels = static_cast<std::uint32_t>(channelCount);
    for (int index = 0; index < channelCount; ++index) {
        const float value = readValue<float>(stream);
        result.exposure[static_cast<std::size_t>((std::min)(index, 2))] = value;
    }
    if (channelCount == 1) {
        result.exposure[1] = result.exposure[0];
        result.exposure[2] = result.exposure[0];
    }
    else if (channelCount == 2) {
        result.exposure[2] = result.exposure[1];
    }

    result.width = readValue<std::uint32_t>(stream);
    result.height = readValue<std::uint32_t>(stream);
    result.bitsPerChannel = readValue<std::uint32_t>(stream);
    const auto byteCount64 = static_cast<std::uint64_t>(result.width) * result.height
        * result.channels * (result.bitsPerChannel / 8);
    if (byteCount64 > (std::numeric_limits<std::size_t>::max)()) {
        throw std::runtime_error("CVRAW is too large for this process");
    }
    const std::uint64_t declaredLength = version == 2
        ? static_cast<std::uint64_t>(readValue<std::int64_t>(stream))
        : readValue<std::uint32_t>(stream);
    if (declaredLength != byteCount64) throw std::runtime_error("CVRAW payload length mismatch");

    result.data.resize(static_cast<std::size_t>(byteCount64));
    stream.read(reinterpret_cast<char*>(result.data.data()), static_cast<std::streamsize>(result.data.size()));
    if (!stream) throw std::runtime_error("Unable to read the complete CVRAW payload");
    return result;
}

std::string sha256(const void* data, std::size_t length)
{
    BCRYPT_ALG_HANDLE algorithm = nullptr;
    BCRYPT_HASH_HANDLE hash = nullptr;
    DWORD objectLength = 0;
    DWORD hashLength = 0;
    DWORD copied = 0;
    std::vector<std::uint8_t> object;
    std::vector<std::uint8_t> digest;

    auto check = [](NTSTATUS status, const char* operation) {
        if (status < 0) throw std::runtime_error(std::string(operation) + " failed");
    };

    check(BCryptOpenAlgorithmProvider(&algorithm, BCRYPT_SHA256_ALGORITHM, nullptr, 0), "BCryptOpenAlgorithmProvider");
    try {
        check(BCryptGetProperty(algorithm, BCRYPT_OBJECT_LENGTH,
            reinterpret_cast<PUCHAR>(&objectLength), sizeof(objectLength), &copied, 0), "BCryptGetProperty object");
        check(BCryptGetProperty(algorithm, BCRYPT_HASH_LENGTH,
            reinterpret_cast<PUCHAR>(&hashLength), sizeof(hashLength), &copied, 0), "BCryptGetProperty hash");
        object.resize(objectLength);
        digest.resize(hashLength);
        check(BCryptCreateHash(algorithm, &hash, object.data(), objectLength, nullptr, 0, 0), "BCryptCreateHash");

        auto* bytes = static_cast<const std::uint8_t*>(data);
        while (length != 0) {
            const auto chunk = static_cast<ULONG>((std::min)(length, static_cast<std::size_t>((std::numeric_limits<ULONG>::max)())));
            check(BCryptHashData(hash, const_cast<PUCHAR>(bytes), chunk, 0), "BCryptHashData");
            bytes += chunk;
            length -= chunk;
        }
        check(BCryptFinishHash(hash, digest.data(), hashLength, 0), "BCryptFinishHash");
    }
    catch (...) {
        if (hash != nullptr) BCryptDestroyHash(hash);
        BCryptCloseAlgorithmProvider(algorithm, 0);
        throw;
    }
    BCryptDestroyHash(hash);
    BCryptCloseAlgorithmProvider(algorithm, 0);

    std::ostringstream output;
    output << std::hex << std::uppercase << std::setfill('0');
    for (const auto value : digest) output << std::setw(2) << static_cast<int>(value);
    return output.str();
}

MCalibrationExecutionOptionsV1 optionsFor(const RawImage& raw)
{
    MCalibrationExecutionOptionsV1 options{};
    options.structSize = sizeof(options);
    options.interleavedBgr = 1;
    options.exposureX = raw.exposure[0];
    options.exposureY = raw.exposure[1];
    options.exposureZ = raw.exposure[2];
    return options;
}

void execute(
    void* context,
    const RawImage& raw,
    std::vector<std::uint8_t>& working,
    std::vector<float>* cie = nullptr)
{
    auto options = optionsFor(raw);
    requireResult(M_CalibrationExecute(
        context,
        raw.width,
        raw.height,
        raw.bitsPerChannel,
        raw.channels,
        working.data(),
        working.size(),
        cie == nullptr ? nullptr : cie->data(),
        cie == nullptr ? 0 : cie->size(),
        &options), context, "M_CalibrationExecute");
}

void executeTo(
    void* context,
    const RawImage& raw,
    const std::vector<std::uint8_t>& source,
    std::vector<std::uint8_t>* correctedRaw,
    std::vector<float>* cie)
{
    auto options = optionsFor(raw);
    requireResult(M_CalibrationExecuteToV1(
        context,
        raw.width,
        raw.height,
        raw.bitsPerChannel,
        raw.channels,
        source.data(),
        source.size(),
        correctedRaw == nullptr ? nullptr : correctedRaw->data(),
        correctedRaw == nullptr ? 0 : correctedRaw->size(),
        cie == nullptr ? nullptr : cie->data(),
        cie == nullptr ? 0 : cie->size(),
        &options), context, "M_CalibrationExecuteToV1");
}

void requireHash(const char* stage, const std::string& actual, const char* expected)
{
    std::cout << "calibration_hash," << stage << ',' << actual << std::endl;
    if (actual != expected) {
        throw std::runtime_error(std::string(stage) + " hash mismatch; expected " + expected);
    }
}

void writeText(const std::filesystem::path& path, std::string_view value)
{
    std::ofstream output(path, std::ios::binary);
    output.write(value.data(), static_cast<std::streamsize>(value.size()));
    if (!output) throw std::runtime_error("Unable to write synthetic calibration file");
}

std::vector<std::uint8_t> referenceFisheyeDistortion(
    const std::vector<std::uint8_t>& sourceBytes,
    int width,
    int height,
    int cameraCenterX,
    int cameraCenterY)
{
    std::array<float, 9> cameraValues{
        10.0F, 0.0F, static_cast<float>(width / 2),
        0.0F, 10.0F, static_cast<float>(height / 2),
        0.0F, 0.0F, 1.0F
    };
    std::array<float, 4> distortionValues{};
    cv::Mat camera(3, 3, CV_32FC1, cameraValues.data());
    cv::Mat distortion(4, 1, CV_32FC1, distortionValues.data());
    const cv::Size size(width, height);
    cv::Mat newCamera;
    cv::fisheye::estimateNewCameraMatrixForUndistortRectify(
        camera, distortion, size, cv::Matx33d::eye(), newCamera, 0.0, size);
    cv::Mat mapX;
    cv::Mat mapY;
    cv::fisheye::initUndistortRectifyMap(
        camera, distortion, cv::Matx33d::eye(), newCamera, size,
        CV_32FC1, mapX, mapY);

    cv::Mat source(height, width, CV_16UC3,
        const_cast<std::uint8_t*>(sourceBytes.data()));
    cv::Mat translated;
    const cv::Matx23f translation(
        1.0F, 0.0F, static_cast<float>(width / 2 - cameraCenterX),
        0.0F, 1.0F, static_cast<float>(height / 2 - cameraCenterY));
    cv::warpAffine(
        source, translated, translation, size,
        cv::INTER_LINEAR, cv::BORDER_CONSTANT);
    cv::Mat result;
    cv::remap(
        translated, result, mapX, mapY,
        cv::INTER_LINEAR, cv::BORDER_CONSTANT);

    std::vector<std::uint8_t> bytes(result.total() * result.elemSize());
    std::memcpy(bytes.data(), result.data, bytes.size());
    return bytes;
}

template<typename T>
void writeBinaryValue(std::ofstream& output, const T& value)
{
    output.write(reinterpret_cast<const char*>(&value), sizeof(value));
}

template<typename T>
void writeMap(
    const std::filesystem::path& path,
    std::uint32_t dataBits,
    std::uint32_t typeBits,
    const std::vector<T>& values,
    std::uint32_t channels = 1,
    std::uint32_t width = 4,
    std::uint32_t height = 4)
{
    std::ofstream output(path, std::ios::binary);
    writeBinaryValue(output, height);
    writeBinaryValue(output, width);
    writeBinaryValue(output, dataBits);
    writeBinaryValue(output, channels);
    writeBinaryValue(output, typeBits);
    output.write(reinterpret_cast<const char*>(values.data()),
        static_cast<std::streamsize>(values.size() * sizeof(T)));
    if (!output) throw std::runtime_error("Unable to write synthetic map calibration");
}

void runOne(
    std::int32_t type,
    const std::filesystem::path& file,
    std::uint32_t width,
    std::uint32_t height,
    std::uint32_t bitsPerChannel,
    std::uint32_t channels,
    std::vector<std::uint8_t>& raw,
    std::vector<float>* cie = nullptr,
    bool interleavedBgr = true)
{
    Context context = createContext();
    requireResult(M_CalibrationLoadFileW(context.get(), type, file.c_str()), context.get(), "load synthetic calibration");
    MCalibrationExecutionOptionsV1 options{};
    options.structSize = sizeof(options);
    options.interleavedBgr = interleavedBgr ? 1 : 0;
    options.exposureX = 1.0F;
    options.exposureY = 1.0F;
    options.exposureZ = 1.0F;
    requireResult(M_CalibrationExecute(
        context.get(), width, height, bitsPerChannel, channels, raw.data(),
        raw.size(), cie == nullptr ? nullptr : cie->data(), cie == nullptr ? 0 : cie->size(),
        &options), context.get(), "execute synthetic calibration");
}

void verifyReadOnlySingle(
    std::int32_t type,
    const std::filesystem::path& file,
    std::uint32_t width,
    std::uint32_t height,
    std::uint32_t bitsPerChannel,
    std::uint32_t channels,
    const std::vector<std::uint8_t>& source,
    bool interleavedBgr = true)
{
    Context context = createContext();
    requireResult(M_CalibrationLoadFileW(context.get(), type, file.c_str()), context.get(), "load read-only single calibration");
    RawImage image;
    image.width = width;
    image.height = height;
    image.bitsPerChannel = bitsPerChannel;
    image.channels = channels;
    image.exposure = { 1.0F, 1.0F, 1.0F };
    std::vector<std::uint8_t> mutableResult = source;
    MCalibrationExecutionOptionsV1 options = optionsFor(image);
    options.interleavedBgr = interleavedBgr ? 1 : 0;
    requireResult(M_CalibrationExecute(context.get(), width, height, bitsPerChannel, channels,
        mutableResult.data(), mutableResult.size(), nullptr, 0, &options), context.get(), "execute mutable single calibration");

    const std::vector<std::uint8_t> sourceBefore = source;
    std::vector<std::uint8_t> readOnlyResult(source.size(), 0xA5);
    requireResult(M_CalibrationExecuteToV1(context.get(), width, height, bitsPerChannel, channels,
        source.data(), source.size(), readOnlyResult.data(), readOnlyResult.size(), nullptr, 0, &options),
        context.get(), "execute read-only single calibration");
    if (source != sourceBefore) throw std::runtime_error("Single read-only calibration modified its source");
    if (readOnlyResult != mutableResult) throw std::runtime_error("Single read-only calibration differs from mutable output");
}

std::vector<std::uint16_t> executeSyntheticSharedMap(void* context)
{
    std::vector<std::uint16_t> pixels(16, 10);
    MCalibrationExecutionOptionsV1 options{};
    options.structSize = sizeof(options);
    options.interleavedBgr = 1;
    options.exposureX = 1.0F;
    options.exposureY = 1.0F;
    options.exposureZ = 1.0F;
    requireResult(M_CalibrationExecute(
        context, 4, 4, 16, 1,
        reinterpret_cast<std::uint8_t*>(pixels.data()), pixels.size() * sizeof(std::uint16_t),
        nullptr, 0, &options), context, "execute shared-map calibration");
    return pixels;
}

void requireUniformPixels(
    const std::vector<std::uint16_t>& pixels,
    std::uint16_t expected,
    const std::string& operation)
{
    if (!std::all_of(pixels.begin(), pixels.end(), [expected](auto value) { return value == expected; })) {
        throw std::runtime_error(operation + " produced an unexpected value");
    }
}

template<typename Worker>
void runTwoWorkersConcurrently(Worker worker)
{
    std::mutex startMutex;
    std::condition_variable startCondition;
    std::size_t waiting = 0;
    bool start = false;
    std::array<std::exception_ptr, 2> failures{};

    auto run = [&](std::size_t index) {
        {
            std::unique_lock lock(startMutex);
            ++waiting;
            startCondition.notify_all();
            startCondition.wait(lock, [&] { return start; });
        }
        try {
            worker(index);
        }
        catch (...) {
            failures[index] = std::current_exception();
        }
    };

    std::thread first(run, 0);
    std::thread second(run, 1);
    {
        std::unique_lock lock(startMutex);
        startCondition.wait(lock, [&] { return waiting == 2; });
        start = true;
    }
    startCondition.notify_all();
    first.join();
    second.join();

    for (const auto& failure : failures) {
        if (failure != nullptr) std::rethrow_exception(failure);
    }
}

void verifyConcurrentSharedMapLoad(const std::filesystem::path& directory)
{
    constexpr std::uint32_t width = 2048;
    constexpr std::uint32_t height = 2048;
    const std::size_t pixelCount = static_cast<std::size_t>(width) * height;
    const auto file = directory / "concurrent_dsnu.dat";
    writeMap(file, 16, 16, std::vector<std::uint16_t>(pixelCount, 1), 1, width, height);

    std::array<std::vector<std::uint16_t>, 2> outputs;
    runTwoWorkersConcurrently([&](std::size_t index) {
        Context context = createContext();
        requireResult(M_CalibrationLoadFileW(context.get(), 4, file.c_str()), context.get(),
            "concurrent first shared-map load");

        outputs[index].assign(pixelCount, 10);
        MCalibrationExecutionOptionsV1 options{};
        options.structSize = sizeof(options);
        options.interleavedBgr = 1;
        requireResult(M_CalibrationExecute(
            context.get(), width, height, 16, 1,
            reinterpret_cast<std::uint8_t*>(outputs[index].data()),
            outputs[index].size() * sizeof(std::uint16_t), nullptr, 0, &options),
            context.get(), "concurrent shared-map execute");
    });

    requireUniformPixels(outputs[0], 9, "first concurrent shared-map context");
    requireUniformPixels(outputs[1], 9, "second concurrent shared-map context");
    if (outputs[0] != outputs[1]) {
        throw std::runtime_error("Concurrent shared-map contexts produced different output");
    }

    // A failed producer must wake every waiter and must not poison this key.
    const auto invalidFile = directory / "concurrent_invalid_dsnu.dat";
    writeText(invalidFile, "truncated");
    std::array<int, 2> loadResults{};
    std::array<std::string, 2> loadErrors{};
    runTwoWorkersConcurrently([&](std::size_t index) {
        Context context = createContext();
        loadResults[index] = M_CalibrationLoadFileW(context.get(), 4, invalidFile.c_str());
        loadErrors[index] = nativeError(context.get());
    });
    for (std::size_t index = 0; index < loadResults.size(); ++index) {
        if (loadResults[index] != M_CALIBRATION_LOAD_FAILED || loadErrors[index].empty()) {
            throw std::runtime_error("Concurrent invalid shared-map load did not fail cleanly");
        }
    }

    const auto invalidWriteTime = std::filesystem::last_write_time(invalidFile);
    writeMap(invalidFile, 16, 16, std::vector<std::uint16_t>(16, 1));
    std::filesystem::last_write_time(invalidFile, invalidWriteTime + std::chrono::seconds(2));
    Context recovered = createContext();
    requireResult(M_CalibrationLoadFileW(recovered.get(), 4, invalidFile.c_str()), recovered.get(),
        "recover shared-map cache after concurrent load failure");
    requireUniformPixels(executeSyntheticSharedMap(recovered.get()), 9,
        "shared-map cache recovered after concurrent load failure");
}

template<typename RewriteFile>
void verifySharedMapContextLifecycle(
    std::int32_t type,
    const std::filesystem::path& file,
    std::uint16_t initialExpected,
    std::uint16_t changedExpected,
    RewriteFile rewriteFile)
{
    Context first = createContext();
    Context second = createContext();
    requireResult(M_CalibrationLoadFileW(first.get(), type, file.c_str()), first.get(), "load first shared-map context");
    requireResult(M_CalibrationLoadFileW(second.get(), type, file.c_str()), second.get(), "load second shared-map context");
    requireUniformPixels(executeSyntheticSharedMap(first.get()), initialExpected, "first shared-map context");
    requireUniformPixels(executeSyntheticSharedMap(second.get()), initialExpected, "second shared-map context");

    // Releasing one owner must not invalidate the shared immutable item held by
    // the other template/context.
    first.reset();
    requireUniformPixels(executeSyntheticSharedMap(second.get()), initialExpected, "surviving shared-map context");

    const auto previousWriteTime = std::filesystem::last_write_time(file);
    rewriteFile();
    std::filesystem::last_write_time(file, previousWriteTime + std::chrono::seconds(2));

    // A metadata change creates a new cached generation. Existing contexts must
    // continue to own their old generation while a new context sees new data.
    Context changed = createContext();
    requireResult(M_CalibrationLoadFileW(changed.get(), type, file.c_str()), changed.get(), "load changed shared-map context");
    requireUniformPixels(executeSyntheticSharedMap(changed.get()), changedExpected, "changed shared-map context");
    requireUniformPixels(executeSyntheticSharedMap(second.get()), initialExpected, "old shared-map generation");

    requireResult(M_CalibrationClear(second.get()), second.get(), "clear old shared-map context");
    second.reset();
    requireUniformPixels(executeSyntheticSharedMap(changed.get()), changedExpected, "shared-map context after old owner release");

    changed.reset();
    Context reloaded = createContext();
    requireResult(M_CalibrationLoadFileW(reloaded.get(), type, file.c_str()), reloaded.get(), "reload shared map after last owner release");
    requireUniformPixels(executeSyntheticSharedMap(reloaded.get()), changedExpected, "reloaded shared-map context");
}

MCalibrationCacheStatsV1 readCalibrationCacheStats()
{
    MCalibrationCacheStatsV1 stats{};
    stats.structSize = sizeof(stats);
    requireResult(M_CalibrationCacheGetStatsV1(&stats), nullptr,
        "read calibration file cache stats");
    return stats;
}

MCalibrationCacheReleaseResultV1 releaseCalibrationCache()
{
    MCalibrationCacheReleaseResultV1 result{};
    result.structSize = sizeof(result);
    requireResult(M_CalibrationCacheReleaseV1(&result), nullptr,
        "release calibration file cache");
    return result;
}

void verifyReleaseLinearizesWithInFlightLoad(const std::filesystem::path& directory)
{
    releaseCalibrationCache();
    constexpr std::uint32_t width = 4096;
    constexpr std::uint32_t height = 4096;
    const auto file = directory / "release_inflight_dsnu.dat";
    {
        const std::size_t pixels = static_cast<std::size_t>(width) * height;
        writeMap(file, 16, 16, std::vector<std::uint16_t>(pixels, 1), 1, width, height);
    }

    Context context = createContext();
    std::atomic<int> loadResult{ 0 };
    std::string loadError;
    std::thread loader([&] {
        const int result = M_CalibrationLoadFileW(context.get(), 4, file.c_str());
        if (result != M_CALIBRATION_OK) {
            loadError = nativeError(context.get());
        }
        loadResult.store(result, std::memory_order_release);
    });

    // Wait until the call is represented in the cache. It may still be loading
    // or may already have published its reserved owner; release must linearize
    // correctly in either state.
    bool observed = false;
    const auto deadline = std::chrono::steady_clock::now() + std::chrono::seconds(10);
    while (std::chrono::steady_clock::now() < deadline) {
        const MCalibrationCacheStatsV1 stats = readCalibrationCacheStats();
        if (stats.entryCount != 0) {
            observed = true;
            break;
        }
        std::this_thread::yield();
    }
    if (!observed) {
        loader.join();
        throw std::runtime_error("In-flight calibration load never appeared in cache stats");
    }

    const MCalibrationCacheReleaseResultV1 released = releaseCalibrationCache();
    loader.join();
    const int result = loadResult.load(std::memory_order_acquire);
    if (released.releasedEntryCount != 1
        || released.activeEntryCount != 1
        || released.activeEstimatedMemoryBytes == 0) {
        throw std::runtime_error("Concurrent cache release did not report deferred load memory");
    }
    if (result == M_CALIBRATION_OK) {
        if (released.activeOwnerCount != 1 || M_CalibrationGetItemCount(context.get()) != 1) {
            throw std::runtime_error("Published load was not reserved before concurrent release");
        }
    }
    else if (result == M_CALIBRATION_LOAD_FAILED) {
        if (released.activeOwnerCount != 0
            || loadError.find("canceled by cache release") == std::string::npos
            || M_CalibrationGetItemCount(context.get()) != 0) {
            throw std::runtime_error("In-flight load was not canceled at the release epoch");
        }
    }
    else {
        throw std::runtime_error("Concurrent cache release returned an unexpected load result");
    }
    if (readCalibrationCacheStats().entryCount != 0) {
        throw std::runtime_error("Concurrent release allowed an old producer to repopulate the cache");
    }
}

void verifyProcessFileCacheAcrossGroups(const std::filesystem::path& directory)
{
    releaseCalibrationCache();
    const MCalibrationCacheStatsV1 baseline = readCalibrationCacheStats();

    const auto dsnu = directory / "group_common_dsnu.dat";
    const auto uniformity = directory / "group_common_uniformity.dat";
    writeMap(dsnu, 16, 16, std::vector<std::uint16_t>(16, 1));
    writeMap(uniformity, 32, 16, std::vector<float>(16, 2.0F));

    std::array<std::filesystem::path, 3> identityFiles;
    for (std::size_t index = 0; index < identityFiles.size(); ++index) {
        identityFiles[index] = directory / ("group_identity_" + std::to_string(index) + ".json");
        writeText(identityFiles[index], R"({"bpp":16,"Texp_x":1.0,"DarkNoiseRatio":1.0})");
    }

    auto runGroup = [&](const std::filesystem::path& identity, const std::filesystem::path& dsnuPath) {
        Context context = createContext();
        requireResult(M_CalibrationLoadFileW(context.get(), 0, identity.c_str()), context.get(),
            "load group identity calibration");
        requireResult(M_CalibrationLoadFileW(context.get(), 4, dsnuPath.c_str()), context.get(),
            "load group shared DSNU");
        requireResult(M_CalibrationLoadFileW(context.get(), 5, uniformity.c_str()), context.get(),
            "load group shared Uniformity");
        std::vector<std::uint16_t> output = executeSyntheticSharedMap(context.get());
        return std::pair<Context, std::vector<std::uint16_t>>(
            std::move(context), std::move(output));
    };

    // A, B, and C have distinct ordered file groups. Every context is
    // destroyed before the next is created, so common-file hits prove that
    // retention is process-wide rather than a consequence of overlapping
    // context lifetimes.
    std::vector<std::uint16_t> reference;
    for (const auto& identity : identityFiles) {
        auto [context, output] = runGroup(identity, dsnu);
        requireUniformPixels(output, 18, "per-file cache group output");
        if (reference.empty()) reference = output;
        else if (output != reference) throw std::runtime_error("A/B/C cached group output differs");
    }

    const MCalibrationCacheStatsV1 afterGroups = readCalibrationCacheStats();
    if (afterGroups.entryCount != 5
        || afterGroups.hitCount < baseline.hitCount + 4
        || afterGroups.missCount < baseline.missCount + 5
        || afterGroups.estimatedMemoryBytes == 0
        || afterGroups.budgetBytes == 0) {
        throw std::runtime_error("Per-file cache A/B/C hit accounting is invalid");
    }

    bool sawDsnu = false;
    bool sawUniformity = false;
    for (std::uint32_t index = 0; index < afterGroups.entryCount; ++index) {
        MCalibrationCacheEntryV1 entry{};
        entry.structSize = sizeof(entry);
        requireResult(M_CalibrationCacheGetEntryV1(index, &entry, nullptr, 0), nullptr,
            "query calibration cache entry size");
        if (entry.generation != afterGroups.generation
            || entry.pathCharacterCount == 0
            || entry.estimatedMemoryBytes == 0
            || entry.activeOwnerCount != 0) {
            throw std::runtime_error("Calibration cache entry metadata is invalid");
        }
        std::vector<wchar_t> path(entry.pathCharacterCount);
        entry.structSize = sizeof(entry);
        requireResult(M_CalibrationCacheGetEntryV1(
            index, &entry, path.data(), static_cast<std::uint32_t>(path.size())), nullptr,
            "query calibration cache entry path");
        if (path.back() != L'\0') throw std::runtime_error("Calibration cache path is not terminated");
        if (entry.calibrationType == 4) {
            sawDsnu = true;
            if (entry.hitCount != 2) throw std::runtime_error("DSNU cache did not hit in groups B and C");
        }
        if (entry.calibrationType == 5) {
            sawUniformity = true;
            if (entry.hitCount != 2) throw std::runtime_error("Uniformity cache did not hit in groups B and C");
        }
    }
    if (!sawDsnu || !sawUniformity) throw std::runtime_error("Common cache entries are missing");

    // Windows path identity is case-insensitive. Loading the same file through
    // an upper-cased spelling must hit the existing per-file entry.
    std::wstring upperDsnu = dsnu.native();
    std::transform(upperDsnu.begin(), upperDsnu.end(), upperDsnu.begin(), [](wchar_t value) {
        return static_cast<wchar_t>(std::towupper(static_cast<std::wint_t>(value)));
    });
    {
        Context caseContext = createContext();
        requireResult(M_CalibrationLoadFileW(caseContext.get(), 4, upperDsnu.c_str()), caseContext.get(),
            "load case-variant DSNU path");
        requireUniformPixels(executeSyntheticSharedMap(caseContext.get()), 9,
            "case-variant DSNU output");
    }
    const MCalibrationCacheStatsV1 afterCaseVariant = readCalibrationCacheStats();
    if (afterCaseVariant.entryCount != afterGroups.entryCount
        || afterCaseVariant.hitCount != afterGroups.hitCount + 1) {
        throw std::runtime_error("Case-variant path created a duplicate cache entry");
    }

    // A real metadata change must create a new generation while leaving other
    // common files hot.
    const auto oldWriteTime = std::filesystem::last_write_time(dsnu);
    writeMap(dsnu, 16, 16, std::vector<std::uint16_t>(16, 2));
    std::filesystem::last_write_time(dsnu, oldWriteTime + std::chrono::seconds(2));
    auto [changedContext, changedOutput] = runGroup(identityFiles[0], dsnu);
    requireUniformPixels(changedOutput, 16, "metadata-invalidated group output");

    const MCalibrationCacheReleaseResultV1 released = releaseCalibrationCache();
    if (released.releasedEntryCount == 0
        || released.releasedEstimatedMemoryBytes == 0
        || released.activeEntryCount != 3
        || released.activeOwnerCount != 3
        || released.activeEstimatedMemoryBytes == 0) {
        throw std::runtime_error("Calibration cache release accounting is invalid");
    }
    if (readCalibrationCacheStats().entryCount != 0) {
        throw std::runtime_error("Calibration cache release left indexed entries behind");
    }
    requireUniformPixels(executeSyntheticSharedMap(changedContext.get()), 16,
        "active context after cache release");
}

void runSmallBudgetCacheCoverage()
{
    TemporaryDirectory directory(
        std::filesystem::temp_directory_path()
        / ("colorvision_calibration_small_cache_" + std::to_string(GetCurrentProcessId())));
    releaseCalibrationCache();
    const MCalibrationCacheStatsV1 baseline = readCalibrationCacheStats();
    if (baseline.budgetBytes != 1024ULL * 1024) {
        throw std::runtime_error("Small-cache test requires COLORVISION_CALIBRATION_CACHE_MB=1");
    }

    constexpr std::uint32_t width = 1024;
    constexpr std::uint32_t height = 1024;
    const std::size_t pixels = static_cast<std::size_t>(width) * height;
    const auto firstFile = directory.path / "active_A_dsnu.dat";
    const auto secondFile = directory.path / "active_B_dsnu.dat";
    writeMap(firstFile, 16, 16, std::vector<std::uint16_t>(pixels, 1), 1, width, height);
    writeMap(secondFile, 16, 16, std::vector<std::uint16_t>(pixels, 2), 1, width, height);

    Context first = createContext();
    Context second = createContext();
    requireResult(M_CalibrationLoadFileW(first.get(), 4, firstFile.c_str()), first.get(),
        "load active cache entry A");
    requireResult(M_CalibrationLoadFileW(second.get(), 4, secondFile.c_str()), second.get(),
        "load active cache entry B");
    const MCalibrationCacheStatsV1 overBudget = readCalibrationCacheStats();
    if (overBudget.entryCount != 2 || overBudget.estimatedMemoryBytes <= overBudget.budgetBytes) {
        throw std::runtime_error("Active cache assets were incorrectly forced under the soft budget");
    }

    Context third = createContext();
    requireResult(M_CalibrationLoadFileW(third.get(), 4, firstFile.c_str()), third.get(),
        "reload active cache entry A");
    const MCalibrationCacheStatsV1 afterHit = readCalibrationCacheStats();
    if (afterHit.hitCount != overBudget.hitCount + 1 || afterHit.entryCount != 2) {
        throw std::runtime_error("Active LRU entry was evicted and reloaded under budget pressure");
    }

    // The soft budget may be exceeded while the only candidates are active,
    // but it must converge immediately as their final context leases end.
    first.reset();
    third.reset();
    const MCalibrationCacheStatsV1 afterFirstOwners = readCalibrationCacheStats();
    if (afterFirstOwners.entryCount != 1
        || afterFirstOwners.estimatedMemoryBytes <= afterFirstOwners.budgetBytes) {
        throw std::runtime_error("Cache did not trim the first inactive over-budget entry");
    }
    second.reset();
    const MCalibrationCacheStatsV1 afterAllOwners = readCalibrationCacheStats();
    if (afterAllOwners.entryCount != 0 || afterAllOwners.estimatedMemoryBytes != 0) {
        throw std::runtime_error("Cache remained over budget after all active owners ended");
    }
    releaseCalibrationCache();
}

void runSyntheticCoverage()
{
    TemporaryDirectory directory(
        std::filesystem::temp_directory_path()
        / ("colorvision_calibration_" + std::to_string(GetCurrentProcessId())));

    verifyConcurrentSharedMapLoad(directory.path);
    verifyReleaseLinearizesWithInFlightLoad(directory.path);
    verifyProcessFileCacheAcrossGroups(directory.path);

    const auto dark = directory.path / "dark.json";
    writeText(dark, R"({"bpp":16,"Texp_x":1.0,"DarkNoiseRatio":2.0})");
    std::vector<std::uint16_t> darkPixels(16, 10);
    std::vector<std::uint8_t> darkRaw(darkPixels.size() * sizeof(std::uint16_t));
    std::memcpy(darkRaw.data(), darkPixels.data(), darkRaw.size());
    const std::vector<std::uint8_t> darkSource = darkRaw;
    runOne(0, dark, 4, 4, 16, 1, darkRaw);
    verifyReadOnlySingle(0, dark, 4, 4, 16, 1, darkSource);
    std::memcpy(darkPixels.data(), darkRaw.data(), darkRaw.size());
    for (std::size_t index = 0; index < darkPixels.size(); ++index) {
        const std::uint16_t expected = index < 4 ? 20 : 10;
        if (darkPixels[index] != expected) throw std::runtime_error("DarkNoise compatibility behavior changed");
    }

    const auto defects = directory.path / "defects.dat";
    {
        std::ofstream output(defects, std::ios::binary);
        const std::uint32_t count = 1;
        const std::uint32_t row = 3;
        const std::uint32_t column = 3;
        writeBinaryValue(output, count);
        writeBinaryValue(output, row);
        writeBinaryValue(output, column);
    }
    for (const std::int32_t type : { 1, 2, 3 }) {
        std::vector<std::uint16_t> pixels(49, 7);
        pixels[24] = 60000;
        std::vector<std::uint8_t> raw(pixels.size() * sizeof(std::uint16_t));
        std::memcpy(raw.data(), pixels.data(), raw.size());
        const std::vector<std::uint8_t> defectSource = raw;
        runOne(type, defects, 7, 7, 16, 1, raw);
        verifyReadOnlySingle(type, defects, 7, 7, 16, 1, defectSource);
        std::memcpy(pixels.data(), raw.data(), raw.size());
        if (pixels[24] != 7) throw std::runtime_error("Defect-point median replacement failed");
    }

    {
        Context context = createContext();
        requireResult(M_CalibrationLoadFileW(context.get(), 3, defects.c_str()), context.get(), "load ROI defect calibration");
        MCalibrationExecutionOptionsV1 options{};
        options.structSize = sizeof(options);
        options.interleavedBgr = 1;
        options.roiX = 1;
        options.roiY = 1;
        std::vector<std::uint16_t> pixels(49, 7);
        pixels[16] = 60000;
        std::vector<std::uint8_t> first(pixels.size() * sizeof(std::uint16_t));
        std::memcpy(first.data(), pixels.data(), first.size());
        std::vector<std::uint8_t> second = first;
        requireResult(M_CalibrationExecute(context.get(), 7, 7, 16, 1,
            first.data(), first.size(), nullptr, 0, &options), context.get(), "first ROI defect execute");
        requireResult(M_CalibrationExecute(context.get(), 7, 7, 16, 1,
            second.data(), second.size(), nullptr, 0, &options), context.get(), "second ROI defect execute");
        if (first != second) throw std::runtime_error("Defect-point ROI coordinates drifted between executions");
    }

    const auto dsnu = directory.path / "dsnu.dat";
    writeMap(dsnu, 16, 16, std::vector<std::uint16_t>(16, 1));
    std::vector<std::uint16_t> mapPixels(16, 10);
    std::vector<std::uint8_t> mapRaw(mapPixels.size() * sizeof(std::uint16_t));
    std::memcpy(mapRaw.data(), mapPixels.data(), mapRaw.size());
    runOne(4, dsnu, 4, 4, 16, 1, mapRaw);
    std::memcpy(mapPixels.data(), mapRaw.data(), mapRaw.size());
    if (!std::all_of(mapPixels.begin(), mapPixels.end(), [](auto value) { return value == 9; })) {
        throw std::runtime_error("DSNU synthetic correction failed");
    }
    verifySharedMapContextLifecycle(4, dsnu, 9, 8, [&] {
        writeMap(dsnu, 16, 16, std::vector<std::uint16_t>(16, 2));
    });

    const auto dsnu8 = directory.path / "dsnu8.dat";
    writeMap(dsnu8, 16, 8, std::vector<std::uint16_t>(16, 1));
    std::vector<std::uint8_t> mapRaw8(16, 10);
    runOne(4, dsnu8, 4, 4, 8, 1, mapRaw8);
    if (!std::all_of(mapRaw8.begin(), mapRaw8.end(), [](auto value) { return value == 9; })) {
        throw std::runtime_error("8-bit DSNU synthetic correction failed");
    }

    const auto uniformity = directory.path / "uniformity.dat";
    writeMap(uniformity, 32, 16, std::vector<float>(16, 2.0F));
    std::fill(mapPixels.begin(), mapPixels.end(), 10);
    std::memcpy(mapRaw.data(), mapPixels.data(), mapRaw.size());
    runOne(5, uniformity, 4, 4, 16, 1, mapRaw);
    std::memcpy(mapPixels.data(), mapRaw.data(), mapRaw.size());
    if (!std::all_of(mapPixels.begin(), mapPixels.end(), [](auto value) { return value == 20; })) {
        throw std::runtime_error("Uniformity synthetic correction failed");
    }
    verifySharedMapContextLifecycle(5, uniformity, 20, 30, [&] {
        writeMap(uniformity, 32, 16, std::vector<float>(16, 3.0F));
    });

    const auto lineArity = directory.path / "linearity.dat";
    {
        std::ofstream output(lineArity, std::ios::binary);
        const std::uint32_t count = 16;
        writeBinaryValue(output, count);
        const std::vector<float> factors(16, 1.5F);
        output.write(reinterpret_cast<const char*>(factors.data()), factors.size() * sizeof(float));
    }
    std::fill(mapPixels.begin(), mapPixels.end(), 10);
    std::memcpy(mapRaw.data(), mapPixels.data(), mapRaw.size());
    const std::vector<std::uint8_t> lineAritySource = mapRaw;
    runOne(13, lineArity, 4, 4, 16, 1, mapRaw);
    verifyReadOnlySingle(13, lineArity, 4, 4, 16, 1, lineAritySource);
    std::memcpy(mapPixels.data(), mapRaw.data(), mapRaw.size());
    if (!std::all_of(mapPixels.begin(), mapPixels.end(), [](auto value) { return value == 15; })) {
        throw std::runtime_error("LineArity synthetic correction failed");
    }

    const auto luminance = directory.path / "luminance.json";
    const auto oneColor = directory.path / "one.json";
    const auto fourColor = directory.path / "four.json";
    const auto multiColor = directory.path / "multi.json";
    writeText(luminance, R"({"bpp":16,"Texp_x":1.0,"Texp_y":1.0,"Texp_z":1.0,"Gain_x":1.0,"Gain_y":1.0,"Gain_z":1.0,"a":1.0,"b":0.0,"c":0.0,"d":0.0})");
    writeText(oneColor, R"({"bpp":16,"Texp_x":1.0,"Texp_y":1.0,"Texp_z":1.0,"Gain_x":1.0,"Gain_y":1.0,"Gain_z":1.0,"a":1.0,"b":1.0,"c":1.0,"d":0.0})");
    writeText(fourColor, R"({"bpp":16,"Texp_x":1.0,"Texp_y":1.0,"Texp_z":1.0,"Gain_x":1.0,"Gain_y":1.0,"Gain_z":1.0,"a":1.0,"b":0.0,"c":0.0,"d":0.0,"e":1.0,"f":0.0,"g":0.0,"h":0.0,"i":1.0})");
    writeText(multiColor, R"({"bpp":16,"pa":[1.0,0.0,0.0,0.0,1.0,0.0,0.0,0.0,1.0],"Gain":[1.0,1.0,1.0]})");

    std::vector<std::uint16_t> monoPixels{ 1, 2, 3, 4 };
    std::vector<std::uint8_t> monoRaw(monoPixels.size() * sizeof(std::uint16_t));
    std::memcpy(monoRaw.data(), monoPixels.data(), monoRaw.size());
    std::vector<float> monoCie(4);
    runOne(6, luminance, 2, 2, 16, 1, monoRaw, &monoCie);
    for (std::size_t index = 0; index < monoCie.size(); ++index) {
        if (monoCie[index] != monoPixels[index]) throw std::runtime_error("Luminance synthetic conversion failed");
    }

    const std::vector<std::uint16_t> bgrPixels{
        3, 2, 1, 6, 5, 4,
        9, 8, 7, 12, 11, 10
    };
    std::vector<std::uint8_t> colorRaw(bgrPixels.size() * sizeof(std::uint16_t));
    std::vector<float> colorCie(12);
    for (const auto& [type, file] : std::array<std::pair<std::int32_t, std::filesystem::path>, 3>{ {
        { 7, oneColor }, { 8, fourColor }, { 9, multiColor }
    } }) {
        std::memcpy(colorRaw.data(), bgrPixels.data(), colorRaw.size());
        runOne(type, file, 2, 2, 16, 3, colorRaw, &colorCie);
        if (!std::all_of(colorCie.begin(), colorCie.end(), [](float value) { return std::isfinite(value); })) {
            throw std::runtime_error("Color conversion generated a non-finite value");
        }
        const std::array<float, 4> expectedX{ 1, 4, 7, 10 };
        const std::array<float, 4> expectedY{ 2, 5, 8, 11 };
        const std::array<float, 4> expectedZ{ 3, 6, 9, 12 };
        if (!std::equal(expectedX.begin(), expectedX.end(), colorCie.begin())
            || !std::equal(expectedY.begin(), expectedY.end(), colorCie.begin() + 4)
            || !std::equal(expectedZ.begin(), expectedZ.end(), colorCie.begin() + 8)) {
            throw std::runtime_error("Color conversion XYZ values changed for type "
                + std::to_string(type) + "; first pixel="
                + std::to_string(colorCie[0]) + ","
                + std::to_string(colorCie[4]) + ","
                + std::to_string(colorCie[8]));
        }
    }

    const std::vector<std::uint16_t> planarPixels{
        1, 4, 7, 10,
        2, 5, 8, 11,
        3, 6, 9, 12
    };
    std::memcpy(colorRaw.data(), planarPixels.data(), colorRaw.size());
    runOne(8, fourColor, 2, 2, 16, 3, colorRaw, &colorCie, false);
    for (std::size_t index = 0; index < colorCie.size(); ++index) {
        if (colorCie[index] != planarPixels[index]) throw std::runtime_error("Planar FourColor conversion failed");
    }

    const auto distortion = directory.path / "distortion.json";
    const auto colorShift = directory.path / "color_shift.json";
    const auto colorDiff = directory.path / "color_diff.json";
    const auto angleShift = directory.path / "angle_shift.json";
    writeText(distortion, R"({"alpha":0.0,"cameraMatrix":[1.0,0.0,1.5,0.0,1.0,1.5,0.0,0.0,1.0],"distCoeffs":[0.0,0.0,0.0,0.0,0.0],"h":4,"w":4,"useFisheye":false})");
    writeText(colorShift, R"({"fillOffset":false,"offset":[{"X":0,"Y":0},{"X":0,"Y":0},{"X":0,"Y":0}]})");
    writeText(colorDiff, R"({"CalibDis":1.0,"CenterCol":2,"CenterRow":2,"ColRowCoeffs_GB":[0.0,0.0],"ColRowCoeffs_GR":[0.0,0.0],"ColorDiffCoeffs_GB":[0.0],"ColorDiffCoeffs_GR":[0.0],"MeasDis":1.0,"h":4,"w":4})");
    writeText(angleShift, R"({"optical_center_x":2,"optical_center_y":2,"interpolate_ratio":1.0,"coefficient_order":0,"target_row":5,"target_col":5,"coeff_r":[0.0],"coeff_g":[0.0],"coeff_b":[0.0],"rowColShift":[0.0,0.0]})");

    std::vector<std::uint16_t> geometricPixels(4 * 4 * 3);
    for (std::size_t index = 0; index < geometricPixels.size(); ++index) geometricPixels[index] = static_cast<std::uint16_t>(index + 1);
    std::vector<std::uint8_t> geometricRaw(geometricPixels.size() * sizeof(std::uint16_t));
    for (const auto& [type, file] : std::array<std::pair<std::int32_t, std::filesystem::path>, 3>{ {
        { 11, distortion }, { 12, colorShift }, { 14, colorDiff }
    } }) {
        std::memcpy(geometricRaw.data(), geometricPixels.data(), geometricRaw.size());
        const std::vector<std::uint8_t> geometricSource = geometricRaw;
        runOne(type, file, 4, 4, 16, 3, geometricRaw);
        verifyReadOnlySingle(type, file, 4, 4, 16, 3, geometricSource);
        std::vector<std::uint16_t> result(geometricPixels.size());
        std::memcpy(result.data(), geometricRaw.data(), geometricRaw.size());
        if (type == 11 || type == 12) {
            if (result != geometricPixels) throw std::runtime_error("Identity geometric calibration changed pixels");
        }
        else {
            for (int row = 0; row < 4; ++row) {
                for (int column = 0; column < 4; ++column) {
                    const std::size_t pixel = static_cast<std::size_t>(row * 4 + column) * 3;
                    const bool border = row == 0 || row == 3 || column == 0 || column == 3;
                    if (result[pixel + 1] != geometricPixels[pixel + 1]
                        || result[pixel] != (border ? 0 : geometricPixels[pixel])
                        || result[pixel + 2] != (border ? 0 : geometricPixels[pixel + 2])) {
                        throw std::runtime_error("ColorDiff identity-map behavior changed");
                    }
                }
            }
        }
    }

    Context geometricChain = createContext();
    requireResult(M_CalibrationLoadFileW(geometricChain.get(), 11, distortion.c_str()),
        geometricChain.get(), "load chained Distortion");
    requireResult(M_CalibrationLoadFileW(geometricChain.get(), 14, colorDiff.c_str()),
        geometricChain.get(), "load chained ColorDiff");
    std::memcpy(geometricRaw.data(), geometricPixels.data(), geometricRaw.size());
    RawImage geometricImage;
    geometricImage.width = 4;
    geometricImage.height = 4;
    geometricImage.bitsPerChannel = 16;
    geometricImage.channels = 3;
    execute(geometricChain.get(), geometricImage, geometricRaw);
    std::vector<std::uint16_t> chainedResult(geometricPixels.size());
    std::memcpy(chainedResult.data(), geometricRaw.data(), geometricRaw.size());
    for (int row = 0; row < 4; ++row) {
        for (int column = 0; column < 4; ++column) {
            const std::size_t pixel = static_cast<std::size_t>(row * 4 + column) * 3;
            const bool border = row == 0 || row == 3 || column == 0 || column == 3;
            if (chainedResult[pixel + 1] != geometricPixels[pixel + 1]
                || chainedResult[pixel] != (border ? 0 : geometricPixels[pixel])
                || chainedResult[pixel + 2] != (border ? 0 : geometricPixels[pixel + 2])) {
                throw std::runtime_error("Distortion/ColorDiff ping-pong output changed");
            }
        }
    }

    const auto dsnuColor = directory.path / "dsnu_color.dat";
    const auto uniformityColor = directory.path / "uniformity_color.dat";
    writeMap(dsnuColor, 16, 16, std::vector<std::uint16_t>(4 * 4 * 3, 1), 3);
    writeMap(uniformityColor, 32, 16, std::vector<float>(4 * 4 * 3, 2.0F), 3);
    Context readOnlySourceChain = createContext();
    for (const auto& [type, file] : std::array<std::pair<std::int32_t, std::filesystem::path>, 5>{ {
        { 4, dsnuColor }, { 5, uniformityColor }, { 11, distortion },
        { 14, colorDiff }, { 8, fourColor }
    } }) {
        requireResult(M_CalibrationLoadFileW(readOnlySourceChain.get(), type, file.c_str()),
            readOnlySourceChain.get(), "load read-only-source chain");
    }

    RawImage readOnlySourceImage;
    readOnlySourceImage.width = 4;
    readOnlySourceImage.height = 4;
    readOnlySourceImage.bitsPerChannel = 16;
    readOnlySourceImage.channels = 3;
    readOnlySourceImage.exposure = { 1.0F, 1.0F, 1.0F };
    std::vector<std::uint8_t> sourceRaw(geometricPixels.size() * sizeof(std::uint16_t));
    std::memcpy(sourceRaw.data(), geometricPixels.data(), sourceRaw.size());
    const std::vector<std::uint8_t> originalSourceRaw = sourceRaw;
    std::vector<std::uint8_t> mutableRaw = sourceRaw;
    std::vector<float> mutableCie(4 * 4 * 3);
    execute(readOnlySourceChain.get(), readOnlySourceImage, mutableRaw, &mutableCie);

    std::vector<std::uint8_t> correctedRaw(sourceRaw.size(), 0xA5);
    std::vector<float> readOnlyCie(mutableCie.size(), -1.0F);
    executeTo(readOnlySourceChain.get(), readOnlySourceImage, sourceRaw, &correctedRaw, &readOnlyCie);
    if (sourceRaw != originalSourceRaw) throw std::runtime_error("Read-only-source calibration modified its source RAW");
    if (correctedRaw != mutableRaw) throw std::runtime_error("Read-only-source corrected RAW differs from mutable calibration");
    if (std::memcmp(readOnlyCie.data(), mutableCie.data(), mutableCie.size() * sizeof(float)) != 0) {
        throw std::runtime_error("Read-only-source CIE differs from mutable calibration");
    }

    std::fill(readOnlyCie.begin(), readOnlyCie.end(), -2.0F);
    executeTo(readOnlySourceChain.get(), readOnlySourceImage, sourceRaw, nullptr, &readOnlyCie);
    if (sourceRaw != originalSourceRaw) throw std::runtime_error("CIE-only calibration modified its source RAW");
    if (std::memcmp(readOnlyCie.data(), mutableCie.data(), mutableCie.size() * sizeof(float)) != 0) {
        throw std::runtime_error("CIE-only read-only-source result differs from mutable calibration");
    }

    // Exercise odd ping-pong parity: the first basic stage writes to scratch,
    // then exactly one distinct-output stage must land in the requested RAW.
    Context oddDistinctChain = createContext();
    requireResult(M_CalibrationLoadFileW(oddDistinctChain.get(), 4, dsnuColor.c_str()),
        oddDistinctChain.get(), "load odd-chain DSNU");
    requireResult(M_CalibrationLoadFileW(oddDistinctChain.get(), 11, distortion.c_str()),
        oddDistinctChain.get(), "load odd-chain Distortion");
    std::vector<std::uint8_t> oddMutableRaw = sourceRaw;
    execute(oddDistinctChain.get(), readOnlySourceImage, oddMutableRaw);
    std::vector<std::uint8_t> oddReadOnlyRaw(sourceRaw.size(), 0xA5);
    executeTo(oddDistinctChain.get(), readOnlySourceImage, sourceRaw, &oddReadOnlyRaw, nullptr);
    if (sourceRaw != originalSourceRaw || oddReadOnlyRaw != oddMutableRaw) {
        throw std::runtime_error("Odd distinct-stage read-only calibration selected the wrong output slot");
    }

    auto options = optionsFor(readOnlySourceImage);
    const int overlapResult = M_CalibrationExecuteToV1(
        readOnlySourceChain.get(), 4, 4, 16, 3,
        sourceRaw.data(), sourceRaw.size(), sourceRaw.data(), sourceRaw.size(),
        readOnlyCie.data(), readOnlyCie.size(), &options);
    if (overlapResult != M_CALIBRATION_EXECUTE_FAILED
        || nativeError(readOnlySourceChain.get()).find("overlap") == std::string::npos
        || sourceRaw != originalSourceRaw) {
        throw std::runtime_error("Overlapping read-only-source buffers were not rejected safely");
    }

    std::vector<std::uint8_t> mutableOverlapRaw = sourceRaw;
    const int mutableOverlapResult = M_CalibrationExecute(
        readOnlySourceChain.get(), 4, 4, 16, 3,
        mutableOverlapRaw.data(), mutableOverlapRaw.size(),
        reinterpret_cast<float*>(mutableOverlapRaw.data()), mutableCie.size(), &options);
    if (mutableOverlapResult != M_CALIBRATION_EXECUTE_FAILED
        || nativeError(readOnlySourceChain.get()).find("overlap") == std::string::npos
        || mutableOverlapRaw != sourceRaw) {
        throw std::runtime_error("Overlapping mutable RAW/CIE buffers were not rejected safely");
    }

    constexpr std::uint32_t hugeWidth = 0x80000000U;
    constexpr std::uint32_t hugeHeight = 0x40000000U;
    constexpr std::uint64_t hugeSamples = static_cast<std::uint64_t>(hugeWidth) * hugeHeight * 3U;
    const int cieByteOverflowResult = M_CalibrationExecute(
        readOnlySourceChain.get(), hugeWidth, hugeHeight, 8, 3,
        mutableOverlapRaw.data(), hugeSamples, readOnlyCie.data(), hugeSamples, &options);
    if (cieByteOverflowResult != M_CALIBRATION_EXECUTE_FAILED
        || nativeError(readOnlySourceChain.get()).find("overflows") == std::string::npos) {
        throw std::runtime_error("Mutable calibration accepted an overflowing CIE byte count");
    }

    MCalibrationExecutionOptionsV1 invalidExposure = options;
    invalidExposure.exposureY = 0.0F;
    const int exposureResult = M_CalibrationExecuteToV1(
        readOnlySourceChain.get(), 4, 4, 16, 3,
        sourceRaw.data(), sourceRaw.size(), nullptr, 0,
        readOnlyCie.data(), readOnlyCie.size(), &invalidExposure);
    if (exposureResult != M_CALIBRATION_EXECUTE_FAILED
        || nativeError(readOnlySourceChain.get()).find("exposure") == std::string::npos
        || sourceRaw != originalSourceRaw) {
        throw std::runtime_error("Invalid color-calibration exposure was not rejected safely");
    }

    constexpr int fisheyeWidth = 12;
    constexpr int fisheyeHeight = 10;
    std::vector<std::uint16_t> fisheyePixels(fisheyeWidth * fisheyeHeight * 3);
    for (std::size_t index = 0; index < fisheyePixels.size(); ++index) {
        fisheyePixels[index] = static_cast<std::uint16_t>((index * 37 + 11) % 4096);
    }
    std::vector<std::uint8_t> fisheyeSource(fisheyePixels.size() * sizeof(std::uint16_t));
    std::memcpy(fisheyeSource.data(), fisheyePixels.data(), fisheyeSource.size());
    const std::array<std::pair<int, int>, 5> fisheyeCenters{{
        { 6, 5 },   // zero offset
        { 4, 4 },   // positive X/Y offset
        { 8, 7 },   // negative X/Y offset
        { -6, 5 },  // positive X offset equals width
        { 6, 15 },  // negative Y offset equals height
    }};
    for (std::size_t index = 0; index < fisheyeCenters.size(); ++index) {
        const auto [centerX, centerY] = fisheyeCenters[index];
        std::ostringstream calibration;
        calibration
            << "{\"alpha\":0.0,\"cameraMatrix\":[10.0,0.0," << centerX
            << ",0.0,10.0," << centerY
            << ",0.0,0.0,1.0],\"distCoeffs\":[0.0,0.0,0.0,0.0],"
            << "\"h\":" << fisheyeHeight << ",\"w\":" << fisheyeWidth
            << ",\"useFisheye\":true}";
        const auto calibrationFile = directory.path
            / ("fisheye_offset_" + std::to_string(index) + ".json");
        writeText(calibrationFile, calibration.str());

        std::vector<std::uint8_t> actual = fisheyeSource;
        runOne(11, calibrationFile, fisheyeWidth, fisheyeHeight, 16, 3, actual);
        const auto expected = referenceFisheyeDistortion(
            fisheyeSource, fisheyeWidth, fisheyeHeight, centerX, centerY);
        if (actual != expected) {
            throw std::runtime_error("Fisheye integer-offset folding changed pixels");
        }
    }

    std::vector<std::uint16_t> anglePixels(5 * 5 * 3, 100);
    std::vector<std::uint8_t> angleRaw(anglePixels.size() * sizeof(std::uint16_t));
    std::memcpy(angleRaw.data(), anglePixels.data(), angleRaw.size());
    const std::vector<std::uint8_t> angleSource = angleRaw;
    runOne(15, angleShift, 5, 5, 16, 3, angleRaw);
    verifyReadOnlySingle(15, angleShift, 5, 5, 16, 3, angleSource);
    std::memcpy(anglePixels.data(), angleRaw.data(), angleRaw.size());
    if (anglePixels[(2 * 5 + 2) * 3] != 100
        || !std::all_of(anglePixels.begin(), anglePixels.end(), [](auto value) { return value == 0 || value == 100; })) {
        throw std::runtime_error("AngleShift synthetic output is invalid");
    }
}

void runPoiV2SyntheticCoverage()
{
    constexpr int width = 5;
    constexpr int height = 5;
    constexpr std::size_t pixels = width * height;
    std::array<float, pixels * 3> cie{};
    for (std::size_t index = 0; index < pixels; ++index) {
        cie[index] = static_cast<float>(index + 1);
        cie[pixels + index] = static_cast<float>(101 + index);
        cie[pixels * 2 + index] = static_cast<float>(201 + index);
    }
    std::array<MPoiRequestV1, 2> requests{{
        { 1, 2, 2, 2, 2 },
        { 2, 2, 2, 2, 2 },
    }};
    std::array<MPoiResultV1, 2> v1{};
    std::array<MPoiResultV1, 2> v2{};
    if (M_CalculatePoiBatchV1(width, height, 32, 3, cie.data(), cie.size(),
            requests.data(), static_cast<std::uint32_t>(requests.size()), v1.data()) != M_POI_OK) {
        throw std::runtime_error("POI V1 synthetic setup failed");
    }
    MPoiOptionsV2 options{};
    options.structSize = sizeof(options);
    options.maxPercent = 0.2F;
    options.scaleX = 1;
    options.scaleY = 1;
    options.scaleZ = 1;
    if (M_CalculatePoiBatchV2(width, height, 32, 3, cie.data(), cie.size(),
            requests.data(), static_cast<std::uint32_t>(requests.size()), &options, v2.data()) != M_POI_OK
        || std::memcmp(v1.data(), v2.data(), sizeof(v1)) != 0) {
        throw std::runtime_error("POI V2 default path differs from V1");
    }

    options.filterMode = 2;
    options.xyzChannel = 1;
    options.threshold = 113;
    if (M_CalculatePoiBatchV2(width, height, 32, 3, cie.data(), cie.size(),
            requests.data(), static_cast<std::uint32_t>(requests.size()), &options, v2.data()) != M_POI_OK) {
        throw std::runtime_error("POI V2 XYZ-mask path failed");
    }
    if (v2[0].X != 15.0F || v2[0].Y != 115.0F || v2[0].Z != 215.0F) {
        throw std::runtime_error("POI V2 XYZ-mask common selection is incorrect");
    }

    std::array<MPoiRequestV1, 2> reversedRequests{{ requests[1], requests[0] }};
    std::array<MPoiResultV1, 2> reversedResults{};
    if (M_CalculatePoiBatchV2(width, height, 32, 3, cie.data(), cie.size(),
            reversedRequests.data(), static_cast<std::uint32_t>(reversedRequests.size()),
            &options, reversedResults.data()) != M_POI_OK
        || std::memcmp(&v2[0], &reversedResults[1], sizeof(MPoiResultV1)) != 0
        || std::memcmp(&v2[1], &reversedResults[0], sizeof(MPoiResultV1)) != 0) {
        throw std::runtime_error("POI V2 XYZ-mask result depends on request order");
    }

    options.flags = M_POI_OPTION_PERCENT_THRESHOLD;
    options.threshold = 1.0F;
    options.maxPercent = 0.5F;
    if (M_CalculatePoiBatchV2(width, height, 32, 3, cie.data(), cie.size(),
            requests.data(), static_cast<std::uint32_t>(requests.size()), &options, v2.data()) != M_POI_OK
        || v2[0].X != 15.0F || v2[0].Y != 115.0F || v2[0].Z != 215.0F
        || v2[1].X != 18.0F || v2[1].Y != 118.0F || v2[1].Z != 218.0F) {
        throw std::runtime_error("POI V2 percent XYZ-mask selected-plane threshold is incorrect");
    }
    if (M_CalculatePoiBatchV2(width, height, 32, 3, cie.data(), cie.size(),
            reversedRequests.data(), static_cast<std::uint32_t>(reversedRequests.size()),
            &options, reversedResults.data()) != M_POI_OK
        || std::memcmp(&v2[0], &reversedResults[1], sizeof(MPoiResultV1)) != 0
        || std::memcmp(&v2[1], &reversedResults[0], sizeof(MPoiResultV1)) != 0) {
        throw std::runtime_error("POI V2 percent XYZ-mask result depends on request order");
    }

    MPoiOptionsV2 invalid = options;
    invalid.flags = 0x80000000U;
    if (M_CalculatePoiBatchV2(width, height, 32, 3, cie.data(), cie.size(),
            requests.data(), static_cast<std::uint32_t>(requests.size()), &invalid, v2.data()) != M_POI_INVALID_ARGUMENT) {
        throw std::runtime_error("POI V2 accepted an unknown options flag");
    }

    std::array<float, pixels> luminance{};
    for (std::size_t index = 0; index < pixels; ++index) luminance[index] = static_cast<float>(index + 1);
    MPoiRequestV1 point{ 0, 2, 2, 1, 1 };
    MPoiResultV1 luminanceResult{};
    for (const auto [filterMode, expected] : std::array<std::pair<int, float>, 3>{ {
        { 1, 15.0F }, { 2, 15.0F }, { 3, 9.0F }
    } }) {
        options = {};
        options.structSize = sizeof(options);
        options.filterMode = filterMode;
        options.threshold = 13.0F;
        options.maxPercent = 0.2F;
        if (M_CalculatePoiBatchV2(width, height, 32, 1, luminance.data(), luminance.size(),
                &point, 1, &options, &luminanceResult) != M_POI_OK
            || luminanceResult.Y != expected) {
            throw std::runtime_error("POI V2 one-channel filter result is incorrect");
        }
    }

    options = {};
    options.structSize = sizeof(options);
    options.flags = M_POI_OPTION_PERCENT_THRESHOLD;
    options.filterMode = 1;
    options.threshold = 1.0F;
    options.maxPercent = 0.5F;
    if (M_CalculatePoiBatchV2(width, height, 32, 1, luminance.data(), luminance.size(),
            &point, 1, &options, &luminanceResult) != M_POI_OK
        || luminanceResult.Y != 15.0F) {
        throw std::runtime_error("POI V2 one-channel percent filter result is incorrect");
    }

    options = {};
    options.structSize = sizeof(options);
    options.flags = M_POI_OPTION_APPLY_MNP;
    options.filterMode = 1;
    options.threshold = 13.0F;
    options.scaleX = 2.0F;
    options.scaleY = 3.0F;
    options.scaleZ = 4.0F;
    MPoiResultV1 filteredMnp{};
    if (M_CalculatePoiBatchV2(width, height, 32, 3, cie.data(), cie.size(),
            &point, 1, &options, &filteredMnp) != M_POI_OK
        || filteredMnp.X != 30.0F || filteredMnp.Y != 339.0F || filteredMnp.Z != 852.0F) {
        throw std::runtime_error("POI V2 filter plus MNP result is incorrect");
    }

    options = {};
    options.structSize = sizeof(options);
    options.flags = M_POI_OPTION_APPLY_MNP;
    options.scaleX = 10;
    options.scaleY = 20;
    options.scaleZ = 30;
    if (M_CalculatePoiBatchV2(width, height, 32, 1, luminance.data(), luminance.size(),
            &point, 1, &options, &luminanceResult) != M_POI_OK
        || luminanceResult.Y != 13.0F) {
        throw std::runtime_error("POI V2 incorrectly applies MNP to one-channel CIE");
    }
}

} // namespace

bool RunCalibrationApiSmokeTests()
{
    try {
        Context context = createContext();
        if (M_CalibrationGetItemCount(context.get()) != 0) {
            throw std::runtime_error("New calibration context is not empty");
        }

        std::array<std::uint16_t, 12> raw{};
        MCalibrationExecutionOptionsV1 options{};
        options.structSize = sizeof(options);
        options.interleavedBgr = 1;
        requireResult(M_CalibrationExecute(context.get(), 2, 2, 16, 3,
            reinterpret_cast<std::uint8_t*>(raw.data()), sizeof(raw), nullptr, 0, &options),
            context.get(), "empty calibration execute");

        const int shortBufferResult = M_CalibrationExecute(context.get(), 2, 2, 16, 3,
            reinterpret_cast<std::uint8_t*>(raw.data()), sizeof(raw) - 1, nullptr, 0, &options);
        if (shortBufferResult != M_CALIBRATION_EXECUTE_FAILED
            || nativeError(context.get()).find("smaller") == std::string::npos) {
            throw std::runtime_error("Undersized RAW buffer was not rejected safely");
        }

        const int reservedResult = M_CalibrationLoadFileW(context.get(), 10, L"reserved.dat");
        if (reservedResult != M_CALIBRATION_UNSUPPORTED) {
            throw std::runtime_error("Reserved LumColor value was not rejected");
        }
        requireResult(M_CalibrationClear(context.get()), context.get(), "M_CalibrationClear");
        runSyntheticCoverage();
        runPoiV2SyntheticCoverage();
        return true;
    }
    catch (const std::exception& ex) {
        std::cerr << "Calibration API smoke test failed: " << ex.what() << std::endl;
        return false;
    }
}

bool RunCalibrationCacheSmallBudgetTests()
{
    try {
        runSmallBudgetCacheCoverage();
        return true;
    }
    catch (const std::exception& ex) {
        std::cerr << "Calibration small-budget cache test failed: " << ex.what() << std::endl;
        return false;
    }
}

bool RunCalibrationRealDataTests(const std::filesystem::path& testRoot)
{
    try {
        std::array<char, 16> threadText{};
        const DWORD threadTextLength = GetEnvironmentVariableA(
            "COLORVISION_CALIBRATION_TEST_THREADS", threadText.data(), static_cast<DWORD>(threadText.size()));
        if (threadTextLength > 0 && threadTextLength < threadText.size()) {
            cv::setNumThreads(std::stoi(threadText.data()));
        }
        std::cout << "calibration_runtime,opencv_threads," << cv::getNumThreads() << std::endl;

        const std::filesystem::path calibrationRoot = testRoot / "Calibration";
        const std::array<CalibrationFile, 4> basics = {{
            { 4, "DSNU", calibrationRoot / "DSNU" / "SV6100HK26026_Gain1_DSNU.dat", "A15E08778617B174D1475FAC640732EDA994246BEEAA5B1CF555F0AA2B8633B8" },
            { 5, "Uniformity", calibrationRoot / "Uniformity" / "SV6100HK26026_70mm_F3.6_ND0_0.7m_Gain1_White_Uniformity.dat", "B55061EDDD10D0494670CED40418F23DFB8A7C138A6D3D5130388458262FA313" },
            { 11, "Distortion", calibrationRoot / "Distortion" / "SV6100HK26026_Distortion.dat", "2826111A5D8BEEC8AB197D81C47B570E700FB8107671632AAC71AD9C69BC0D4C" },
            { 14, "ColorDiff", calibrationRoot / "ColorDiff" / "SV6100HK26026_ColorDiff.dat", "903E7BAFF0348793128BDE661C6FA3BAB441FD4CF668BC4FA33742494BC78211" },
        }};
        const CalibrationFile color{
            8,
            "FourColor",
            calibrationRoot / "LumFourColor" / "SV6100HK26026_70mm_F3.6_ND0_0.7m_Gain1_LED_FourColor.dat",
            "9BBA1CFA783E764511989A0B33726C9297AC315CB6A954909FAB139C06EE8B7D"
        };

        RawImage raw = readCvRaw(testRoot / "Local_20260802_034219_801.cvraw");
        std::vector<std::uint8_t> working = raw.data;
        const auto cieElements = static_cast<std::size_t>(raw.width) * raw.height * raw.channels;
        std::vector<float> cie(cieElements);

        for (const auto& file : basics) {
            Context context = createContext();
            requireResult(M_CalibrationLoadFileW(context.get(), file.type, file.path.c_str()), context.get(), std::string("load ") + file.name);
            const auto stageStart = std::chrono::steady_clock::now();
            execute(context.get(), raw, working);
            const auto stageEnd = std::chrono::steady_clock::now();
            std::cout << "calibration_time," << file.name << "_ms,"
                      << std::chrono::duration<double, std::milli>(stageEnd - stageStart).count() << std::endl;
            requireHash(file.name, sha256(working.data(), working.size()), file.expectedHash);
        }

        Context colorContext = createContext();
        requireResult(M_CalibrationLoadFileW(colorContext.get(), color.type, color.path.c_str()), colorContext.get(), "load FourColor");
        const auto colorStart = std::chrono::steady_clock::now();
        execute(colorContext.get(), raw, working, &cie);
        const auto colorEnd = std::chrono::steady_clock::now();
        std::cout << "calibration_time,FourColor_ms,"
                  << std::chrono::duration<double, std::milli>(colorEnd - colorStart).count() << std::endl;
        requireHash(color.name, sha256(cie.data(), cie.size() * sizeof(float)), color.expectedHash);

        Context combined = createContext();
        const auto loadStart = std::chrono::steady_clock::now();
        for (const auto& file : basics) {
            requireResult(M_CalibrationLoadFileW(combined.get(), file.type, file.path.c_str()), combined.get(), std::string("combined load ") + file.name);
        }
        requireResult(M_CalibrationLoadFileW(combined.get(), color.type, color.path.c_str()), combined.get(), "combined load FourColor");
        const auto loadEnd = std::chrono::steady_clock::now();
        const MCalibrationCacheStatsV1 cacheStats = readCalibrationCacheStats();
        std::cout << "calibration_cache,entry_count," << cacheStats.entryCount << std::endl;
        std::cout << "calibration_cache,estimated_mib,"
                  << cacheStats.estimatedMemoryBytes / (1024.0 * 1024.0) << std::endl;
        std::cout << "calibration_cache,budget_mib,"
                  << cacheStats.budgetBytes / (1024.0 * 1024.0) << std::endl;
        std::cout << "calibration_cache,hits," << cacheStats.hitCount << std::endl;
        std::cout << "calibration_cache,misses," << cacheStats.missCount << std::endl;
        if (cacheStats.entryCount < basics.size() + 1) {
            throw std::runtime_error("Calibration cache budget evicted part of one real-data group");
        }

        std::array<double, 4> executionTimes{};
        for (std::size_t iteration = 0; iteration < executionTimes.size(); ++iteration) {
            working = raw.data;
            const auto executeStart = std::chrono::steady_clock::now();
            execute(combined.get(), raw, working, &cie);
            const auto executeEnd = std::chrono::steady_clock::now();
            executionTimes[iteration] = std::chrono::duration<double, std::milli>(executeEnd - executeStart).count();
            std::cout << "calibration_time,execute_" << (iteration + 1) << "_ms," << executionTimes[iteration] << std::endl;
        }
        requireHash("CombinedRaw", sha256(working.data(), working.size()), basics.back().expectedHash);
        requireHash("CombinedCie", sha256(cie.data(), cie.size() * sizeof(float)), color.expectedHash);

        const std::string sourceHashBefore = sha256(raw.data.data(), raw.data.size());
        const auto readOnlyRawStart = std::chrono::steady_clock::now();
        executeTo(combined.get(), raw, raw.data, &working, &cie);
        const auto readOnlyRawEnd = std::chrono::steady_clock::now();
        std::cout << "calibration_time,execute_readonly_with_raw_ms,"
                  << std::chrono::duration<double, std::milli>(readOnlyRawEnd - readOnlyRawStart).count() << std::endl;
        requireHash("ReadOnlyCombinedRaw", sha256(working.data(), working.size()), basics.back().expectedHash);
        requireHash("ReadOnlyCombinedCieWithRaw", sha256(cie.data(), cie.size() * sizeof(float)), color.expectedHash);

        std::array<double, 4> readOnlyExecutionTimes{};
        for (std::size_t iteration = 0; iteration < readOnlyExecutionTimes.size(); ++iteration) {
            const auto executeStart = std::chrono::steady_clock::now();
            executeTo(combined.get(), raw, raw.data, nullptr, &cie);
            const auto executeEnd = std::chrono::steady_clock::now();
            readOnlyExecutionTimes[iteration] = std::chrono::duration<double, std::milli>(executeEnd - executeStart).count();
            std::cout << "calibration_time,execute_readonly_cie_" << (iteration + 1) << "_ms,"
                      << readOnlyExecutionTimes[iteration] << std::endl;
        }
        requireHash("ReadOnlyCombinedCie", sha256(cie.data(), cie.size() * sizeof(float)), color.expectedHash);
        if (sha256(raw.data.data(), raw.data.size()) != sourceHashBefore) {
            throw std::runtime_error("Real-data read-only-source calibration modified the source RAW");
        }

        const auto loadMilliseconds = std::chrono::duration<double, std::milli>(loadEnd - loadStart).count();
        std::cout << "calibration_time,load_ms," << loadMilliseconds << std::endl;
        return true;
    }
    catch (const std::exception& ex) {
        std::cerr << "Calibration real-data test failed: " << ex.what() << std::endl;
        return false;
    }
}

bool RunCalibrationLegacyColorComparison(
    const std::filesystem::path& rawPath,
    const std::filesystem::path& colorFile,
    const std::filesystem::path& legacyDll)
{
    using CreateCalibrationManage = HANDLE(WINAPI*)();
    using ReleaseCalibrationManage = BOOL(WINAPI*)(HANDLE);
    using SetCalibParam = BOOL(WINAPI*)(HANDLE, int, bool, const char*);
    using RunFourColor = BOOL(WINAPI*)(
        HANDLE, unsigned int, unsigned int, int, unsigned int, BYTE*, BYTE*, float*);
    using CalculatePoiBatch = int(WINAPI*)(
        int, int, int, int, const float*, const MPoiRequestV1*, int, MPoiResultV1*);
    using InitXyz = int(WINAPI*)(HANDLE);
    using SetBufferXyz = int(WINAPI*)(HANDLE, unsigned int, unsigned int, unsigned int, unsigned int, const float*);
    using ReleaseBuffer = int(WINAPI*)(HANDLE);
    using UnInitXyz = int(WINAPI*)(HANDLE);
    using SetPercentFilter = int(WINAPI*)(HANDLE, BOOL, float);
    using SetFilter = int(WINAPI*)(HANDLE, BOOL, float);
    using SetFilterXyz = int(WINAPI*)(HANDLE, BOOL, int, float);
    using SetByMnp = int(WINAPI*)(HANDLE, BOOL, float, float, float);
    using GetXyzCircle = int(WINAPI*)(
        HANDLE, int, int, float*, float*, float*, float*, float*, float*, float*, double);
    using GetXyzRect = int(WINAPI*)(
        HANDLE, int, int, float*, float*, float*, float*, float*, float*, float*, int, int);
    using GetCctCircle = int(WINAPI*)(
        HANDLE, int, int, float*, float*, float*, float*, float*, float*, double);
    using GetCctRect = int(WINAPI*)(
        HANDLE, int, int, float*, float*, float*, float*, float*, float*, int, int);

    HMODULE module = nullptr;
    HANDLE legacyContext = nullptr;
    try {
        RawImage raw = readCvRaw(rawPath);
        if (raw.channels != 3) throw std::runtime_error("Legacy FourColor comparison requires a three-channel RAW");

        module = LoadLibraryExW(legacyDll.c_str(), nullptr, LOAD_WITH_ALTERED_SEARCH_PATH);
        if (module == nullptr) {
            throw std::runtime_error("Unable to load legacy cvCamera.dll: " + std::to_string(GetLastError()));
        }
        auto getExport = [module](const char* name) {
            const auto address = GetProcAddress(module, name);
            if (address == nullptr) throw std::runtime_error(std::string("Missing legacy export: ") + name);
            return address;
        };
        const auto create = reinterpret_cast<CreateCalibrationManage>(getExport("CreatCalibrationManage"));
        const auto release = reinterpret_cast<ReleaseCalibrationManage>(getExport("ReleaseCalibrationManage"));
        const auto setParam = reinterpret_cast<SetCalibParam>(getExport("CM_SetCalibParam"));
        const auto runFourColor = reinterpret_cast<RunFourColor>(getExport("CM_SCGD_SDP_ColorFour"));
        const auto calculateLegacyPoi = reinterpret_cast<CalculatePoiBatch>(getExport("CM_CalculatePoiBatchV1"));
        const auto initXyz = reinterpret_cast<InitXyz>(getExport("CM_InitXYZ"));
        const auto setBufferXyz = reinterpret_cast<SetBufferXyz>(getExport("CM_SetBufferXYZ"));
        const auto releaseBuffer = reinterpret_cast<ReleaseBuffer>(getExport("CM_ReleaseBuffer"));
        const auto unInitXyz = reinterpret_cast<UnInitXyz>(getExport("CM_UnInitXYZ"));
        const auto setPercentFilter = reinterpret_cast<SetPercentFilter>(getExport("CM_SetPercentFilter"));
        const auto setFilter = reinterpret_cast<SetFilter>(getExport("CM_SetFilter"));
        const auto setFilterNoArea = reinterpret_cast<SetFilter>(getExport("CM_SetFilterNoArea"));
        const auto setFilterXyz = reinterpret_cast<SetFilterXyz>(getExport("CM_SetFilterXYZ"));
        const auto setByMnp = reinterpret_cast<SetByMnp>(getExport("CM_SetBymnp"));
        const auto getXyzCircle = reinterpret_cast<GetXyzCircle>(getExport("CM_GetXYZxyuvCircle"));
        const auto getXyzRect = reinterpret_cast<GetXyzRect>(getExport("CM_GetXYZxyuvRect"));
        const auto getCctCircle = reinterpret_cast<GetCctCircle>(getExport("CM_GetxyuvCCTWaveCircle"));
        const auto getCctRect = reinterpret_cast<GetCctRect>(getExport("CM_GetxyuvCCTWaveRect"));

        legacyContext = create();
        if (legacyContext == nullptr) throw std::runtime_error("Legacy calibration context creation failed");
        const std::string colorPath = colorFile.string();
        if (!setParam(legacyContext, 8, true, colorPath.c_str())) {
            throw std::runtime_error("Legacy FourColor calibration load failed");
        }

        const auto pixels = static_cast<std::size_t>(raw.width) * raw.height;
        std::vector<float> cie(pixels * 3);
        const std::string rawBefore = sha256(raw.data.data(), raw.data.size());
        std::array<double, 4> legacyTimes{};
        for (std::size_t iteration = 0; iteration < legacyTimes.size(); ++iteration) {
            const auto start = std::chrono::steady_clock::now();
            if (!runFourColor(
                    legacyContext,
                    raw.width,
                    raw.height,
                    static_cast<int>(raw.bitsPerChannel),
                    raw.channels,
                    raw.data.data(),
                    reinterpret_cast<BYTE*>(cie.data()),
                    raw.exposure.data())) {
                throw std::runtime_error("Legacy FourColor execution failed");
            }
            const auto end = std::chrono::steady_clock::now();
            legacyTimes[iteration] = std::chrono::duration<double, std::milli>(end - start).count();
        }
        const std::string legacyHash = sha256(cie.data(), cie.size() * sizeof(float));

        Context context = createContext();
        requireResult(M_CalibrationLoadFileW(context.get(), 8, colorFile.c_str()), context.get(), "load migrated FourColor");
        auto options = optionsFor(raw);
        std::array<double, 4> migratedTimes{};
        for (std::size_t iteration = 0; iteration < migratedTimes.size(); ++iteration) {
            const auto start = std::chrono::steady_clock::now();
            requireResult(M_CalibrationExecute(
                context.get(), raw.width, raw.height, raw.bitsPerChannel, raw.channels,
                raw.data.data(), raw.data.size(), cie.data(), cie.size(), &options),
                context.get(), "execute migrated FourColor");
            const auto end = std::chrono::steady_clock::now();
            migratedTimes[iteration] = std::chrono::duration<double, std::milli>(end - start).count();
        }
        const std::string migratedHash = sha256(cie.data(), cie.size() * sizeof(float));
        const std::string rawAfter = sha256(raw.data.data(), raw.data.size());

        std::array<MPoiRequestV1, 9> poiRequests{};
        for (std::size_t index = 0; index < poiRequests.size(); ++index) {
            const int column = static_cast<int>(index % 3) + 1;
            const int row = static_cast<int>(index / 3) + 1;
            MPoiRequestV1& request = poiRequests[index];
            request.type = static_cast<int>(index % 3);
            request.x = static_cast<int>((static_cast<std::uint64_t>(raw.width) * column) / 4);
            request.y = static_cast<int>((static_cast<std::uint64_t>(raw.height) * row) / 4);
            request.width = request.type == 0 ? 1 : (request.type == 1 ? 201 : 200);
            request.height = request.type == 2 ? 160 : request.width;
        }
        std::array<MPoiResultV1, 9> legacyPoiResults{};
        std::array<MPoiResultV1, 9> migratedPoiResults{};
        if (calculateLegacyPoi(
                static_cast<int>(raw.width), static_cast<int>(raw.height), 32,
                static_cast<int>(raw.channels), cie.data(), poiRequests.data(),
                static_cast<int>(poiRequests.size()), legacyPoiResults.data()) == 0) {
            throw std::runtime_error("Legacy batch POI comparison failed");
        }
        if (M_CalculatePoiBatchV1(
                static_cast<int>(raw.width), static_cast<int>(raw.height), 32,
                static_cast<int>(raw.channels), cie.data(), cie.size(), poiRequests.data(),
                static_cast<std::uint32_t>(poiRequests.size()), migratedPoiResults.data()) != M_POI_OK) {
            throw std::runtime_error("Migrated batch POI comparison failed");
        }
        if (std::memcmp(legacyPoiResults.data(), migratedPoiResults.data(), sizeof(legacyPoiResults)) != 0) {
            throw std::runtime_error("Migrated POI output differs byte-for-byte from legacy batch output");
        }

        auto runLegacyScalarPoi = [&](const MPoiOptionsV2& poiOptions,
                                      std::array<MPoiResultV1, 9>& output,
                                      std::uintptr_t handleValue) {
            const HANDLE handle = reinterpret_cast<HANDLE>(handleValue);
            bool initialized = false;
            auto cleanup = [&]() noexcept {
                if (!initialized) return true;
                const int releaseResult = releaseBuffer(handle);
                const int unInitResult = unInitXyz(handle);
                initialized = false;
                return releaseResult != 0 && unInitResult != 0;
            };
            try {
                if (initXyz(handle) == 0) throw std::runtime_error("Legacy scalar POI initialization failed");
                initialized = true;
                if (setBufferXyz(handle, raw.width, raw.height, 32, raw.channels, cie.data()) == 0) {
                    throw std::runtime_error("Legacy scalar POI buffer setup failed");
                }
                const BOOL usePercent = (poiOptions.flags & M_POI_OPTION_PERCENT_THRESHOLD) != 0;
                if (setPercentFilter(handle, usePercent, poiOptions.maxPercent) == 0) {
                    throw std::runtime_error("Legacy scalar POI percent setup failed");
                }
                int filterResult = 0;
                switch (poiOptions.filterMode) {
                case 0:
                    filterResult = setFilter(handle, FALSE, poiOptions.threshold);
                    break;
                case 1:
                    filterResult = setFilter(handle, TRUE, poiOptions.threshold);
                    break;
                case 2:
                    filterResult = setFilterXyz(handle, TRUE, poiOptions.xyzChannel, poiOptions.threshold);
                    break;
                case 3:
                    filterResult = setFilterNoArea(handle, TRUE, poiOptions.threshold);
                    break;
                default:
                    throw std::runtime_error("Invalid scalar POI test filter mode");
                }
                if (filterResult == 0) throw std::runtime_error("Legacy scalar POI filter setup failed");
                const BOOL applyMnp = (poiOptions.flags & M_POI_OPTION_APPLY_MNP) != 0;
                if (setByMnp(handle, applyMnp, poiOptions.scaleX, poiOptions.scaleY, poiOptions.scaleZ) == 0) {
                    throw std::runtime_error("Legacy scalar POI MNP setup failed");
                }

                for (std::size_t index = 0; index < poiRequests.size(); ++index) {
                    const MPoiRequestV1& request = poiRequests[index];
                    MPoiResultV1 result{};
                    int xyzResult = 0;
                    int cctResult = 0;
                    if (request.type == 2) {
                        xyzResult = getXyzRect(handle, request.x, request.y,
                            &result.X, &result.Y, &result.Z, &result.x, &result.y, &result.u, &result.v,
                            request.width, request.height);
                        if (xyzResult != 0) {
                            cctResult = getCctRect(handle, request.x, request.y,
                                &result.x, &result.y, &result.u, &result.v, &result.cct, &result.wave,
                                request.width, request.height);
                        }
                    }
                    else {
                        const double radius = request.type == 0 ? 1.0 : request.width / 2.0;
                        xyzResult = getXyzCircle(handle, request.x, request.y,
                            &result.X, &result.Y, &result.Z, &result.x, &result.y, &result.u, &result.v, radius);
                        if (xyzResult != 0) {
                            cctResult = getCctCircle(handle, request.x, request.y,
                                &result.x, &result.y, &result.u, &result.v, &result.cct, &result.wave, radius);
                        }
                    }
                    if (xyzResult == 0 || cctResult == 0) {
                        throw std::runtime_error("Legacy scalar POI calculation failed");
                    }
                    output[index] = result;
                }
                if (!cleanup()) throw std::runtime_error("Legacy scalar POI cleanup failed");
            }
            catch (...) {
                cleanup();
                throw;
            }
        };

        auto compareFilteredPoi = [&](const char* label, const MPoiOptionsV2& poiOptions, std::uintptr_t handleValue) {
            std::array<MPoiResultV1, 9> legacyFiltered{};
            std::array<MPoiResultV1, 9> migratedFiltered{};
            const auto legacyFilteredStart = std::chrono::steady_clock::now();
            runLegacyScalarPoi(poiOptions, legacyFiltered, handleValue);
            const auto legacyFilteredEnd = std::chrono::steady_clock::now();
            const auto migratedFilteredStart = std::chrono::steady_clock::now();
            if (M_CalculatePoiBatchV2(
                    static_cast<int>(raw.width), static_cast<int>(raw.height), 32,
                    static_cast<int>(raw.channels), cie.data(), cie.size(), poiRequests.data(),
                    static_cast<std::uint32_t>(poiRequests.size()), &poiOptions, migratedFiltered.data()) != M_POI_OK) {
                throw std::runtime_error(std::string("Migrated filtered POI failed: ") + label);
            }
            const auto migratedFilteredEnd = std::chrono::steady_clock::now();
            if (std::memcmp(legacyFiltered.data(), migratedFiltered.data(), sizeof(legacyFiltered)) != 0) {
                throw std::runtime_error(std::string("Migrated filtered POI differs from legacy: ") + label);
            }
            std::cout << "poi_filter_compare," << label << ",byte_equal,1" << std::endl;
            std::cout << "poi_filter_compare," << label << ",legacy_end_to_end_ms,"
                      << std::chrono::duration<double, std::milli>(legacyFilteredEnd - legacyFilteredStart).count() << std::endl;
            std::cout << "poi_filter_compare," << label << ",migrated_ms,"
                      << std::chrono::duration<double, std::milli>(migratedFilteredEnd - migratedFilteredStart).count() << std::endl;
        };

        auto makePoiOptions = []() {
            MPoiOptionsV2 value{};
            value.structSize = sizeof(value);
            value.maxPercent = 0.2F;
            value.scaleX = 1.0F;
            value.scaleY = 1.0F;
            value.scaleZ = 1.0F;
            return value;
        };
        const std::size_t centerIndex = static_cast<std::size_t>(raw.height / 2) * raw.width + raw.width / 2;
        const float absoluteThreshold = (std::max)(0.0F, (std::min)({
            cie[centerIndex], cie[pixels + centerIndex], cie[pixels * 2 + centerIndex] }) * 0.7F);

        MPoiOptionsV2 valueAbsolute = makePoiOptions();
        valueAbsolute.filterMode = 1;
        valueAbsolute.threshold = absoluteThreshold;
        compareFilteredPoi("value_absolute", valueAbsolute, 0x4356501001ULL);

        MPoiOptionsV2 valuePercent = makePoiOptions();
        valuePercent.flags = M_POI_OPTION_PERCENT_THRESHOLD;
        valuePercent.filterMode = 1;
        valuePercent.threshold = 0.65F;
        compareFilteredPoi("value_percent", valuePercent, 0x4356501002ULL);

        MPoiOptionsV2 noAreaAbsolute = makePoiOptions();
        noAreaAbsolute.filterMode = 3;
        noAreaAbsolute.threshold = absoluteThreshold;
        compareFilteredPoi("no_area_absolute", noAreaAbsolute, 0x4356501003ULL);

        MPoiOptionsV2 noAreaPercent = makePoiOptions();
        noAreaPercent.flags = M_POI_OPTION_PERCENT_THRESHOLD;
        noAreaPercent.filterMode = 3;
        noAreaPercent.threshold = 0.65F;
        compareFilteredPoi("no_area_percent", noAreaPercent, 0x4356501004ULL);

        MPoiOptionsV2 mnp = makePoiOptions();
        mnp.flags = M_POI_OPTION_APPLY_MNP;
        mnp.scaleX = 0.91F;
        mnp.scaleY = 1.07F;
        mnp.scaleZ = 1.13F;
        compareFilteredPoi("mnp", mnp, 0x4356501005ULL);

        constexpr std::size_t poiIterations = 100;
        const auto legacyPoiStart = std::chrono::steady_clock::now();
        for (std::size_t iteration = 0; iteration < poiIterations; ++iteration) {
            if (calculateLegacyPoi(
                    static_cast<int>(raw.width), static_cast<int>(raw.height), 32,
                    static_cast<int>(raw.channels), cie.data(), poiRequests.data(),
                    static_cast<int>(poiRequests.size()), legacyPoiResults.data()) == 0) {
                throw std::runtime_error("Legacy batch POI benchmark failed");
            }
        }
        const auto legacyPoiEnd = std::chrono::steady_clock::now();
        const auto migratedPoiStart = std::chrono::steady_clock::now();
        for (std::size_t iteration = 0; iteration < poiIterations; ++iteration) {
            if (M_CalculatePoiBatchV1(
                    static_cast<int>(raw.width), static_cast<int>(raw.height), 32,
                    static_cast<int>(raw.channels), cie.data(), cie.size(), poiRequests.data(),
                    static_cast<std::uint32_t>(poiRequests.size()), migratedPoiResults.data()) != M_POI_OK) {
                throw std::runtime_error("Migrated batch POI benchmark failed");
            }
        }
        const auto migratedPoiEnd = std::chrono::steady_clock::now();

        const HANDLE poiContext = reinterpret_cast<HANDLE>(static_cast<std::uintptr_t>(0x4356504F49));
        const auto legacyCopiedStart = std::chrono::steady_clock::now();
        if (initXyz(poiContext) == 0
            || setBufferXyz(poiContext, raw.width, raw.height, 32, raw.channels, cie.data()) == 0) {
            throw std::runtime_error("Legacy copied POI setup failed");
        }
        const int copiedReleaseResult = releaseBuffer(poiContext);
        const int copiedUnInitResult = unInitXyz(poiContext);
        if (copiedReleaseResult == 0 || copiedUnInitResult == 0) {
            throw std::runtime_error("Legacy copied POI cleanup failed");
        }
        const auto legacyCopiedEnd = std::chrono::steady_clock::now();

        std::cout << "calibration_compare,raw," << rawPath.string() << std::endl;
        std::cout << "calibration_compare,legacy_cie_sha256," << legacyHash << std::endl;
        std::cout << "calibration_compare,migrated_cie_sha256," << migratedHash << std::endl;
        for (std::size_t iteration = 0; iteration < legacyTimes.size(); ++iteration) {
            std::cout << "calibration_compare,legacy_" << (iteration + 1) << "_ms," << legacyTimes[iteration] << std::endl;
            std::cout << "calibration_compare,migrated_" << (iteration + 1) << "_ms," << migratedTimes[iteration] << std::endl;
        }
        std::cout << "poi_compare,result_bytes," << sizeof(legacyPoiResults) << std::endl;
        std::cout << "poi_compare,byte_equal,1" << std::endl;
        std::cout << "poi_compare,legacy_batch_average_ms,"
                  << std::chrono::duration<double, std::milli>(legacyPoiEnd - legacyPoiStart).count() / poiIterations << std::endl;
        std::cout << "poi_compare,migrated_batch_average_ms,"
                  << std::chrono::duration<double, std::milli>(migratedPoiEnd - migratedPoiStart).count() / poiIterations << std::endl;
        std::cout << "poi_compare,legacy_full_copy_setup_ms,"
                  << std::chrono::duration<double, std::milli>(legacyCopiedEnd - legacyCopiedStart).count() << std::endl;

        if (rawBefore != rawAfter) throw std::runtime_error("FourColor unexpectedly modified the RAW input");
        if (legacyHash != migratedHash) throw std::runtime_error("Migrated FourColor output differs from legacy output");
        if (!release(legacyContext)) throw std::runtime_error("Legacy calibration context release failed");
        legacyContext = nullptr;
        FreeLibrary(module);
        return true;
    }
    catch (const std::exception& ex) {
        if (legacyContext != nullptr && module != nullptr) {
            const auto release = reinterpret_cast<ReleaseCalibrationManage>(
                GetProcAddress(module, "ReleaseCalibrationManage"));
            if (release != nullptr) release(legacyContext);
        }
        if (module != nullptr) FreeLibrary(module);
        std::cerr << "Calibration legacy color comparison failed: " << ex.what() << std::endl;
        return false;
    }
}
