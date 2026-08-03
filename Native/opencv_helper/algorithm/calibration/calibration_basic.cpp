#include "calibration_core.h"

#include <opencv2/core/utility.hpp>

#include <algorithm>
#include <cctype>
#include <cerrno>
#include <cmath>
#include <cstdlib>
#include <cstring>
#include <fstream>
#include <limits>
#include <stdexcept>
#include <string_view>
#include <type_traits>
#include <utility>
#include <vector>

namespace cvcore::calibration {
namespace {

constexpr std::uint64_t kLegacyMapHeaderBytes = 8;
constexpr std::uint64_t kCurrentMapHeaderBytes = 20;

template <typename T>
bool checkedMultiply(T left, T right, T& result)
{
    if (left != 0 && right > (std::numeric_limits<T>::max)() / left) {
        return false;
    }
    result = left * right;
    return true;
}

bool validDistinctRawOutput(
    const ImageView& source,
    const ImageView& destination,
    std::size_t& sampleCount,
    std::size_t& byteCount,
    std::string& error)
{
    if (source.data == nullptr || destination.data == nullptr
        || source.data == destination.data
        || source.width != destination.width
        || source.height != destination.height
        || source.bitsPerChannel != destination.bitsPerChannel
        || source.channels != destination.channels) {
        error = "Calibration source and destination layouts do not match";
        return false;
    }

    if (!checkedMultiply<std::size_t>(source.width, source.height, sampleCount)
        || !checkedMultiply<std::size_t>(sampleCount, source.channels, sampleCount)
        || !checkedMultiply<std::size_t>(
            sampleCount, source.bitsPerChannel / 8, byteCount)
        || source.dataLength < byteCount
        || destination.dataLength < byteCount) {
        error = "Calibration source or destination buffer is too small";
        return false;
    }
    return true;
}

bool checkedAdd(std::uint64_t left, std::uint64_t right, std::uint64_t& result)
{
    if (right > (std::numeric_limits<std::uint64_t>::max)() - left) {
        return false;
    }
    result = left + right;
    return true;
}

bool fileSize(const std::filesystem::path& file, std::uint64_t& size, std::string& error)
{
    std::error_code ec;
    const auto nativeSize = std::filesystem::file_size(file, ec);
    if (ec) {
        error = "Unable to read calibration file size: " + ec.message();
        return false;
    }
    size = static_cast<std::uint64_t>(nativeSize);
    return true;
}

bool seekAndRead(
    std::ifstream& stream,
    std::uint64_t offset,
    void* destination,
    std::uint64_t byteCount,
    std::string& error)
{
    if (offset > static_cast<std::uint64_t>((std::numeric_limits<std::streamoff>::max)())
        || byteCount > static_cast<std::uint64_t>((std::numeric_limits<std::streamsize>::max)())) {
        error = "Calibration file is too large for the native stream API";
        return false;
    }

    stream.clear();
    stream.seekg(static_cast<std::streamoff>(offset), std::ios::beg);
    if (!stream) {
        error = "Unable to seek in calibration file";
        return false;
    }

    if (byteCount == 0) {
        return true;
    }

    stream.read(static_cast<char*>(destination), static_cast<std::streamsize>(byteCount));
    if (!stream || static_cast<std::uint64_t>(stream.gcount()) != byteCount) {
        error = "Calibration file ended before all data was read";
        return false;
    }
    return true;
}

template <typename T>
bool readPod(std::ifstream& stream, T& value, std::string& error)
{
    stream.read(reinterpret_cast<char*>(&value), sizeof(value));
    if (!stream || stream.gcount() != static_cast<std::streamsize>(sizeof(value))) {
        error = "Calibration file header is incomplete";
        return false;
    }
    return true;
}

bool openBinary(
    const std::filesystem::path& file,
    std::ifstream& stream,
    std::uint64_t& size,
    std::string& error)
{
    if (!fileSize(file, size, error)) {
        return false;
    }
    stream.open(file, std::ios::binary);
    if (!stream) {
        error = "Unable to open calibration file";
        return false;
    }
    return true;
}

bool readTextFile(const std::filesystem::path& file, std::string& text, std::string& error)
{
    std::ifstream stream;
    std::uint64_t size = 0;
    if (!openBinary(file, stream, size, error)) {
        return false;
    }
    if (size > static_cast<std::uint64_t>((std::numeric_limits<std::size_t>::max)())) {
        error = "Calibration JSON is too large";
        return false;
    }

    text.resize(static_cast<std::size_t>(size));
    return seekAndRead(stream, 0, text.data(), size, error);
}

std::size_t skipWhitespace(std::string_view text, std::size_t position)
{
    while (position < text.size()
        && std::isspace(static_cast<unsigned char>(text[position])) != 0) {
        ++position;
    }
    return position;
}

bool looksLikeJsonObject(std::string_view text)
{
    const auto first = skipWhitespace(text, 0);
    if (first == text.size() || text[first] != '{') {
        return false;
    }

    auto last = text.size();
    while (last > first + 1
        && std::isspace(static_cast<unsigned char>(text[last - 1])) != 0) {
        --last;
    }
    return last > first + 1 && text[last - 1] == '}';
}

bool findJsonNumber(
    const std::string& text,
    std::string_view key,
    double& value,
    bool& representedAsReal)
{
    const std::string quotedKey = "\"" + std::string(key) + "\"";
    auto position = text.find(quotedKey);
    while (position != std::string::npos) {
        position = skipWhitespace(text, position + quotedKey.size());
        if (position >= text.size() || text[position] != ':') {
            position = text.find(quotedKey, position);
            continue;
        }
        position = skipWhitespace(text, position + 1);
        if (position >= text.size()) {
            return false;
        }

        const char* begin = text.c_str() + position;
        char* end = nullptr;
        errno = 0;
        const double parsed = std::strtod(begin, &end);
        if (end == begin || errno == ERANGE || !std::isfinite(parsed)) {
            return false;
        }

        const std::string_view token(begin, static_cast<std::size_t>(end - begin));
        representedAsReal = token.find_first_of(".eE") != std::string_view::npos;
        value = parsed;
        return true;
    }
    return false;
}

template <typename TAction>
void parallelRows(std::uint32_t rowCount, const TAction& action)
{
    if (rowCount == 0) {
        return;
    }
    if (rowCount < 8
        || rowCount > static_cast<std::uint32_t>((std::numeric_limits<int>::max)())) {
        action(0, rowCount);
        return;
    }

    cv::parallel_for_(cv::Range(0, static_cast<int>(rowCount)),
        [&action](const cv::Range& range) {
            action(static_cast<std::uint32_t>(range.start), static_cast<std::uint32_t>(range.end));
        });
}

bool hasLegacyRoi(
    const ExecutionOptions& options,
    std::uint32_t mapWidth,
    std::uint32_t mapHeight)
{
    const auto x = options.roi[0];
    const auto y = options.roi[1];
    const auto width = options.roi[2];
    const auto height = options.roi[3];

    // Keep the legacy strict comparison. An ROI ending exactly on the map's
    // right or bottom edge was not treated as an ROI by cvCamera.
    return width != 0 && height != 0
        && static_cast<std::uint64_t>(x) + width < mapWidth
        && static_cast<std::uint64_t>(y) + height < mapHeight;
}

template <typename T>
bool validateSpan(
    std::size_t totalCount,
    std::size_t base,
    std::size_t rowStride,
    std::size_t rowCount,
    std::size_t rowWidth,
    std::size_t elementStride,
    std::string_view name,
    std::string& error)
{
    static_assert(!std::is_void_v<T>);
    if (rowCount == 0 || rowWidth == 0) {
        return true;
    }
    if (base >= totalCount) {
        error = std::string(name) + " starts outside its buffer";
        return false;
    }

    std::size_t rowOffset = 0;
    std::size_t elementOffset = 0;
    if (!checkedMultiply(rowCount - 1, rowStride, rowOffset)
        || !checkedMultiply(rowWidth - 1, elementStride, elementOffset)) {
        error = std::string(name) + " span overflows address arithmetic";
        return false;
    }
    if (rowOffset > (std::numeric_limits<std::size_t>::max)() - base
        || elementOffset > (std::numeric_limits<std::size_t>::max)() - base - rowOffset) {
        error = std::string(name) + " span overflows address arithmetic";
        return false;
    }
    const auto last = base + rowOffset + elementOffset;
    if (last >= totalCount) {
        error = std::string(name) + " is smaller than the requested image/ROI";
        return false;
    }
    return true;
}

struct DsnuOperation {
    template <typename TPixel>
    static void apply(
        const TPixel* sourcePixels,
        TPixel* destinationPixels,
        std::size_t count,
        const std::uint16_t* __restrict correction,
        std::size_t correctionStride)
    {
        if (correctionStride == 1) {
            for (std::size_t index = 0; index < count; ++index) {
                const auto source = sourcePixels[index];
                const auto offset = correction[index];
                destinationPixels[index] = source < offset ? TPixel{} : static_cast<TPixel>(source - offset);
            }
            return;
        }
        for (std::size_t index = 0; index < count; ++index) {
            const auto source = sourcePixels[index];
            const auto offset = correction[index * correctionStride];
            destinationPixels[index] = source < offset ? TPixel{} : static_cast<TPixel>(source - offset);
        }
    }
};

struct UniformityOperation {
    template <typename TPixel>
    static void apply(
        const TPixel* sourcePixels,
        TPixel* destinationPixels,
        std::size_t count,
        const float* __restrict correction,
        std::size_t correctionStride)
    {
        constexpr auto maximum = (std::numeric_limits<TPixel>::max)();
        if (correctionStride == 1) {
            for (std::size_t index = 0; index < count; ++index) {
                const float corrected = correction[index] * sourcePixels[index];
                destinationPixels[index] = corrected > static_cast<float>(maximum)
                    ? maximum
                    : static_cast<TPixel>(corrected);
            }
            return;
        }
        for (std::size_t index = 0; index < count; ++index) {
            const float corrected = correction[index * correctionStride] * sourcePixels[index];
            destinationPixels[index] = corrected > static_cast<float>(maximum)
                ? maximum
                : static_cast<TPixel>(corrected);
        }
    }
};

template <typename TCorrection, typename TOperation, typename TPixel>
bool applyRows(
    const TPixel* sourcePixels,
    TPixel* destinationPixels,
    std::size_t pixelCount,
    std::size_t pixelBase,
    std::size_t pixelRowStride,
    std::uint32_t rows,
    std::size_t rowWidth,
    const std::vector<TCorrection>& correction,
    std::size_t correctionBase,
    std::size_t correctionRowStride,
    std::size_t correctionElementStride,
    std::string& error)
{
    if (!validateSpan<TPixel>(
            pixelCount, pixelBase, pixelRowStride, rows, rowWidth, 1, "RAW source buffer", error)
        || !validateSpan<TPixel>(
            pixelCount, pixelBase, pixelRowStride, rows, rowWidth, 1, "RAW destination buffer", error)
        || !validateSpan<TCorrection>(
            correction.size(), correctionBase, correctionRowStride, rows, rowWidth,
            correctionElementStride, "Calibration map", error)) {
        return false;
    }

    parallelRows(rows, [&](std::uint32_t startRow, std::uint32_t endRow) {
        for (auto row = startRow; row < endRow; ++row) {
            const auto* sourceRow = sourcePixels + pixelBase + static_cast<std::size_t>(row) * pixelRowStride;
            auto* destinationRow = destinationPixels + pixelBase + static_cast<std::size_t>(row) * pixelRowStride;
            const auto* correctionRow = correction.data() + correctionBase
                + static_cast<std::size_t>(row) * correctionRowStride;
            TOperation::apply(sourceRow, destinationRow, rowWidth, correctionRow, correctionElementStride);
        }
    });
    return true;
}

struct MapHeader {
    int version = -1;
    std::uint32_t height = 0;
    std::uint32_t width = 0;
    std::uint32_t sourceBitsPerChannel = 0;
    std::uint32_t channels = 0;
};

template <typename TCorrection>
bool loadMap(
    const std::filesystem::path& file,
    std::uint32_t requiredDataBits,
    MapHeader& header,
    std::vector<TCorrection>& values,
    std::string& error)
{
    std::ifstream stream;
    std::uint64_t length = 0;
    if (!openBinary(file, stream, length, error)) {
        return false;
    }
    if (length <= 16) {
        error = "Calibration map is too short";
        return false;
    }

    std::uint32_t height = 0;
    std::uint32_t width = 0;
    std::uint32_t dataBits = 0;
    std::uint32_t channels = 0;
    std::uint32_t sourceBits = 0;
    if (!readPod(stream, height, error) || !readPod(stream, width, error)
        || !readPod(stream, dataBits, error) || !readPod(stream, channels, error)
        || !readPod(stream, sourceBits, error)) {
        return false;
    }
    if (height == 0 || width == 0) {
        error = "Calibration map dimensions must be positive";
        return false;
    }

    std::uint64_t pixelCount = 0;
    std::uint64_t legacyBytes = 0;
    if (!checkedMultiply<std::uint64_t>(height, width, pixelCount)
        || !checkedMultiply<std::uint64_t>(pixelCount, sizeof(TCorrection), legacyBytes)) {
        error = "Calibration map dimensions overflow";
        return false;
    }

    std::uint64_t legacyLength = 0;
    if (!checkedAdd(legacyBytes, kLegacyMapHeaderBytes, legacyLength)) {
        error = "Calibration map size overflows";
        return false;
    }

    std::uint64_t currentValueCount = 0;
    std::uint64_t currentBytes = 0;
    std::uint64_t currentLength = 0;
    const bool currentSizeValid = checkedMultiply<std::uint64_t>(pixelCount, channels, currentValueCount)
        && checkedMultiply<std::uint64_t>(currentValueCount, sizeof(TCorrection), currentBytes)
        && checkedAdd(currentBytes, kCurrentMapHeaderBytes, currentLength);

    std::uint64_t dataOffset = 0;
    std::uint64_t valueCount = 0;
    if (length == legacyLength) {
        header.version = 0;
        header.height = height;
        header.width = width;
        header.channels = 1;
        header.sourceBitsPerChannel = 0;
        dataOffset = kLegacyMapHeaderBytes;
        valueCount = pixelCount;
    }
    else if (currentSizeValid && length == currentLength && dataBits == requiredDataBits) {
        if (channels == 0 || (sourceBits != 8 && sourceBits != 16)) {
            error = "Calibration map has invalid channel count or source bit depth";
            return false;
        }
        header.version = 1;
        header.height = height;
        header.width = width;
        header.channels = channels;
        header.sourceBitsPerChannel = sourceBits;
        dataOffset = kCurrentMapHeaderBytes;
        valueCount = currentValueCount;
    }
    else {
        error = "Calibration map length/header does not match V0 or V1 format";
        return false;
    }

    if (valueCount > static_cast<std::uint64_t>((std::numeric_limits<std::size_t>::max)())) {
        error = "Calibration map is too large for this process";
        return false;
    }
    values.resize(static_cast<std::size_t>(valueCount));

    std::uint64_t byteCount = 0;
    if (!checkedMultiply<std::uint64_t>(valueCount, sizeof(TCorrection), byteCount)) {
        error = "Calibration map byte count overflows";
        return false;
    }
    return seekAndRead(stream, dataOffset, values.data(), byteCount, error);
}

template <typename TCorrection, typename TOperation>
class MapCalibration final : public CalibrationItem {
public:
    MapCalibration(CalibrationType type, MapHeader header, std::vector<TCorrection> correction)
        : type_(type)
        , header_(header)
        , correction_(std::move(correction))
    {
    }

    [[nodiscard]] CalibrationType type() const noexcept override { return type_; }
    [[nodiscard]] bool supportsDistinctOutput() const noexcept override { return true; }
    [[nodiscard]] bool shareInstanceAcrossContexts() const noexcept override { return true; }
    [[nodiscard]] std::uint64_t cacheFootprintBytes() const noexcept override
    {
        return sizeof(*this)
            + static_cast<std::uint64_t>(correction_.capacity()) * sizeof(TCorrection);
    }

    bool apply(
        const ImageView& raw,
        float*,
        const ExecutionOptions& options,
        std::string& error) override
    {
        if (options.interleavedBgr
            && header_.version == 1
            && header_.sourceBitsPerChannel != raw.bitsPerChannel) {
            error = type_ == CalibrationType::Dsnu
                ? "DSNU source bit depth does not match its map"
                : "Uniformity source bit depth does not match its map";
            return false;
        }
        if (raw.bitsPerChannel == 8) {
            auto* pixels = reinterpret_cast<std::uint8_t*>(raw.data);
            return applyTyped(pixels, pixels, raw, options, error);
        }
        auto* pixels = reinterpret_cast<std::uint16_t*>(raw.data);
        return applyTyped(pixels, pixels, raw, options, error);
    }

    bool applyOutOfPlace(
        const ImageView& source,
        const ImageView& destination,
        const ExecutionOptions& options,
        std::string& error) override
    {
        if (source.data == nullptr || destination.data == nullptr
            || source.width != destination.width
            || source.height != destination.height
            || source.bitsPerChannel != destination.bitsPerChannel
            || source.channels != destination.channels) {
            error = "Map calibration source and destination layouts do not match";
            return false;
        }
        if (options.interleavedBgr
            && header_.version == 1
            && header_.sourceBitsPerChannel != source.bitsPerChannel) {
            error = type_ == CalibrationType::Dsnu
                ? "DSNU source bit depth does not match its map"
                : "Uniformity source bit depth does not match its map";
            return false;
        }

        std::size_t sampleCount = 0;
        std::size_t requiredBytes = 0;
        if (!checkedMultiply<std::size_t>(source.width, source.height, sampleCount)
            || !checkedMultiply<std::size_t>(sampleCount, source.channels, sampleCount)
            || !checkedMultiply<std::size_t>(sampleCount, source.bitsPerChannel / 8, requiredBytes)
            || source.dataLength < requiredBytes
            || destination.dataLength < requiredBytes) {
            error = "Map calibration source or destination buffer is too small";
            return false;
        }

        // Legacy ROI modes intentionally touch only a subset of the RAW image.
        // Preserve all untouched bytes before applying that subset. The normal
        // full-frame V1 path writes every sample directly and performs no copy.
        if (hasLegacyRoi(options, header_.width, header_.height)) {
            std::memcpy(destination.data, source.data, requiredBytes);
        }
        if (source.bitsPerChannel == 8) {
            return applyTyped(
                reinterpret_cast<const std::uint8_t*>(source.data),
                reinterpret_cast<std::uint8_t*>(destination.data), source, options, error);
        }
        return applyTyped(
            reinterpret_cast<const std::uint16_t*>(source.data),
            reinterpret_cast<std::uint16_t*>(destination.data), source, options, error);
    }

private:
    template <typename TPixel>
    bool applyTyped(
        const TPixel* sourcePixels,
        TPixel* destinationPixels,
        const ImageView& raw,
        const ExecutionOptions& options,
        std::string& error)
    {
        std::size_t pixelCount = 0;
        std::size_t pixelsPerPlane = 0;
        if (!checkedMultiply<std::size_t>(raw.width, raw.height, pixelsPerPlane)
            || !checkedMultiply<std::size_t>(pixelsPerPlane, raw.channels, pixelCount)) {
            error = "RAW dimensions overflow";
            return false;
        }

        const bool roi = hasLegacyRoi(options, header_.width, header_.height);
        if (options.interleavedBgr) {
            if (header_.version == 0) {
                if (raw.channels != 1) {
                    error = "V0 scalar calibration maps require one-channel RAW data";
                    return false;
                }
                return applyScalar(sourcePixels, destinationPixels, pixelCount, raw, options, roi, error);
            }

            switch (options.rgbType) {
            case 0:
                if (raw.channels == 1
                    && (type_ == CalibrationType::Dsnu || header_.channels == 1)) {
                    return applyScalar(sourcePixels, destinationPixels, pixelCount, raw, options, roi, error);
                }
                return applyInterleaved(sourcePixels, destinationPixels, pixelCount, raw, options, roi, error);
            case 1:
            case 2:
            case 3:
                if (raw.channels != 1) {
                    error = "Single R/G/B calibration mode requires one-channel RAW data";
                    return false;
                }
                return applySingleColor(
                    sourcePixels, destinationPixels, pixelCount, raw, options, roi, options.rgbType, error);
            default:
                error = "RGB calibration type must be 0, 1, 2, or 3";
                return false;
            }
        }

        if (raw.channels != 3) {
            error = "Planar calibration requires three RAW planes";
            return false;
        }
        return applyPlanar(sourcePixels, destinationPixels, pixelCount, pixelsPerPlane, raw, options, roi, error);
    }

    template <typename TPixel>
    bool applyScalar(
        const TPixel* sourcePixels,
        TPixel* destinationPixels,
        std::size_t pixelCount,
        const ImageView& raw,
        const ExecutionOptions& options,
        bool roi,
        std::string& error)
    {
        if (!roi && (raw.width != header_.width || raw.height != header_.height)) {
            error = "RAW dimensions do not match the scalar calibration map";
            return false;
        }

        // DSNU V0/V1 used roi_w/roi_h here; Uniformity historically used the
        // actual input width/height. Preserve that difference for byte parity.
        const bool dsnu = type_ == CalibrationType::Dsnu;
        const auto rows = roi && dsnu ? options.roi[3] : raw.height;
        const auto rowWidth = static_cast<std::size_t>(roi && dsnu ? options.roi[2] : raw.width);
        const auto pixelRowStride = rowWidth;
        const auto correctionBase = roi
            ? static_cast<std::size_t>(options.roi[1]) * header_.width + options.roi[0]
            : 0;

        return applyRows<TCorrection, TOperation>(
            sourcePixels, destinationPixels, pixelCount, 0, pixelRowStride, rows, rowWidth, correction_,
            correctionBase, header_.width, 1, error);
    }

    template <typename TPixel>
    bool applyInterleaved(
        const TPixel* sourcePixels,
        TPixel* destinationPixels,
        std::size_t pixelCount,
        const ImageView& raw,
        const ExecutionOptions& options,
        bool roi,
        std::string& error)
    {
        if (!roi && (raw.width != header_.width || raw.height != header_.height)) {
            error = "RAW dimensions do not match the calibration map";
            return false;
        }

        const auto rowWidth = static_cast<std::size_t>(raw.width) * raw.channels;
        const auto correctionRowStride = static_cast<std::size_t>(header_.width) * raw.channels;
        const auto correctionBase = roi
            ? static_cast<std::size_t>(options.roi[1]) * correctionRowStride
                + static_cast<std::size_t>(options.roi[0]) * raw.channels
            : 0;

        return applyRows<TCorrection, TOperation>(
            sourcePixels, destinationPixels, pixelCount, 0, rowWidth, raw.height, rowWidth, correction_,
            correctionBase, correctionRowStride, 1, error);
    }

    template <typename TPixel>
    bool applySingleColor(
        const TPixel* sourcePixels,
        TPixel* destinationPixels,
        std::size_t pixelCount,
        const ImageView& raw,
        const ExecutionOptions& options,
        bool roi,
        std::int32_t rgbType,
        std::string& error)
    {
        if (!roi && (raw.width != header_.width || raw.height != header_.height)) {
            error = "RAW dimensions do not match the calibration map";
            return false;
        }

        const auto rows = roi ? options.roi[3] : raw.height;
        const auto rowWidth = static_cast<std::size_t>(roi ? options.roi[2] : raw.width);
        const auto correctionRowStride = static_cast<std::size_t>(header_.width) * 3;
        const auto channelOffset = static_cast<std::size_t>(3 - rgbType); // map is B, G, R
        const auto correctionBase = (roi
            ? static_cast<std::size_t>(options.roi[1]) * header_.width + options.roi[0]
            : 0) * 3 + channelOffset;

        return applyRows<TCorrection, TOperation>(
            sourcePixels, destinationPixels, pixelCount, 0, rowWidth, rows, rowWidth, correction_,
            correctionBase, correctionRowStride, 3, error);
    }

    template <typename TPixel>
    bool applyPlanar(
        const TPixel* sourcePixels,
        TPixel* destinationPixels,
        std::size_t pixelCount,
        std::size_t pixelsPerPlane,
        const ImageView& raw,
        const ExecutionOptions& options,
        bool roi,
        std::string& error)
    {
        if (!roi && (raw.width != header_.width || raw.height != header_.height)) {
            error = "RAW dimensions do not match the calibration map";
            return false;
        }

        const auto rows = roi ? options.roi[3] : raw.height;
        const auto rowWidth = static_cast<std::size_t>(roi ? options.roi[2] : raw.width);
        const auto correctionRowStride = static_cast<std::size_t>(header_.width) * 3;
        const auto correctionPixelBase = (roi
            ? static_cast<std::size_t>(options.roi[1]) * header_.width + options.roi[0]
            : 0) * 3;

        // Legacy planar memory is R, G, B while the calibration map is B, G, R.
        for (std::size_t plane = 0; plane < 3; ++plane) {
            const auto correctionChannel = 2 - plane;
            if (!applyRows<TCorrection, TOperation>(
                    sourcePixels, destinationPixels, pixelCount, plane * pixelsPerPlane, rowWidth, rows, rowWidth,
                    correction_, correctionPixelBase + correctionChannel,
                    correctionRowStride, 3, error)) {
                return false;
            }
        }
        return true;
    }

    CalibrationType type_;
    MapHeader header_;
    std::vector<TCorrection> correction_;
};

class DarkNoiseCalibration final : public CalibrationItem {
public:
    explicit DarkNoiseCalibration(double ratio)
        : ratio_(static_cast<float>(ratio))
    {
    }

    [[nodiscard]] CalibrationType type() const noexcept override
    {
        return CalibrationType::DarkNoise;
    }

    [[nodiscard]] bool shareInstanceAcrossContexts() const noexcept override { return true; }
    [[nodiscard]] bool supportsDistinctOutput() const noexcept override { return true; }

    bool apply(
        const ImageView& raw,
        float*,
        const ExecutionOptions& options,
        std::string&) override
    {
        // This intentionally retains cvCamera's production behavior: the loop
        // is indexed only by height (not width/channels), and both 8/16-bit
        // paths clamp to 255. Changing it would break byte-for-byte parity.
        const auto start = (std::min)(options.roi[2], raw.height);
        const auto bottom = (std::min)(options.roi[3], raw.height);
        const auto end = raw.height - bottom;
        if (start >= end) {
            return true;
        }

        if (raw.bitsPerChannel == 8) {
            auto* data = reinterpret_cast<std::uint8_t*>(raw.data);
            for (auto index = start; index < end; ++index) {
                const float corrected = data[index] * ratio_;
                data[index] = corrected > 255.0F
                    ? std::uint8_t{ 255 }
                    : static_cast<std::uint8_t>(corrected);
            }
        }
        else {
            auto* data = reinterpret_cast<std::uint16_t*>(raw.data);
            for (auto index = start; index < end; ++index) {
                const float corrected = data[index] * ratio_;
                data[index] = corrected > 255.0F
                    ? std::uint16_t{ 255 }
                    : static_cast<std::uint16_t>(corrected);
            }
        }
        return true;
    }

    bool applyOutOfPlace(
        const ImageView& source,
        const ImageView& destination,
        const ExecutionOptions& options,
        std::string& error) override
    {
        std::size_t sampleCount = 0;
        std::size_t byteCount = 0;
        if (!validDistinctRawOutput(
                source, destination, sampleCount, byteCount, error)) {
            return false;
        }
        if (sampleCount < source.height) {
            error = "DarkNoise RAW layout is invalid";
            return false;
        }

        if (source.bitsPerChannel == 8) {
            correctTo(
                reinterpret_cast<const std::uint8_t*>(source.data),
                reinterpret_cast<std::uint8_t*>(destination.data),
                sampleCount,
                source.height,
                options);
        }
        else {
            correctTo(
                reinterpret_cast<const std::uint16_t*>(source.data),
                reinterpret_cast<std::uint16_t*>(destination.data),
                sampleCount,
                source.height,
                options);
        }
        return true;
    }

private:
    template <typename TPixel>
    void correctTo(
        const TPixel* source,
        TPixel* destination,
        std::size_t sampleCount,
        std::uint32_t height,
        const ExecutionOptions& options) const
    {
        const auto start = (std::min)(options.roi[2], height);
        const auto bottom = (std::min)(options.roi[3], height);
        const auto end = height - bottom;

        // DarkNoise's historical loop changes only a tiny height-indexed
        // prefix. The rest still has to be materialized for the next stage,
        // but copying the two untouched spans and producing the corrected
        // span directly avoids a separate copy-then-mutate pass.
        std::memcpy(destination, source, static_cast<std::size_t>(start) * sizeof(TPixel));
        for (auto index = start; index < end; ++index) {
            const float corrected = source[index] * ratio_;
            destination[index] = corrected > 255.0F
                ? static_cast<TPixel>(255)
                : static_cast<TPixel>(corrected);
        }
        const auto suffix = static_cast<std::size_t>((std::max)(start, end));
        std::memcpy(
            destination + suffix,
            source + suffix,
            (sampleCount - suffix) * sizeof(TPixel));
    }

    float ratio_ = 0.0F;
};

struct DefectPoint {
    std::uint32_t row = 0;
    std::uint32_t column = 0;
};

class DefectPointCalibration final : public CalibrationItem {
public:
    DefectPointCalibration(CalibrationType type, std::vector<DefectPoint> points)
        : type_(type)
        , points_(std::move(points))
    {
    }

    [[nodiscard]] CalibrationType type() const noexcept override { return type_; }

    [[nodiscard]] bool shareInstanceAcrossContexts() const noexcept override { return true; }
    [[nodiscard]] bool supportsDistinctOutput() const noexcept override { return true; }

    [[nodiscard]] std::uint64_t cacheFootprintBytes() const noexcept override
    {
        return sizeof(*this)
            + static_cast<std::uint64_t>(points_.capacity()) * sizeof(DefectPoint);
    }

    bool apply(
        const ImageView& raw,
        float*,
        const ExecutionOptions& options,
        std::string&) override
    {
        if (raw.bitsPerChannel == 8) {
            correct(reinterpret_cast<std::uint8_t*>(raw.data), raw, options);
        }
        else {
            correct(reinterpret_cast<std::uint16_t*>(raw.data), raw, options);
        }
        return true;
    }

    bool applyOutOfPlace(
        const ImageView& source,
        const ImageView& destination,
        const ExecutionOptions& options,
        std::string& error) override
    {
        std::size_t sampleCount = 0;
        std::size_t byteCount = 0;
        if (!validDistinctRawOutput(
                source, destination, sampleCount, byteCount, error)) {
            return false;
        }

        // Defect points are sparse, while their neighborhoods may overlap and
        // therefore intentionally observe earlier corrections. A complete
        // source copy is required before running the unchanged in-place logic.
        std::memcpy(destination.data, source.data, byteCount);
        return apply(destination, nullptr, options, error);
    }

private:
    template <typename TPixel>
    void correct(TPixel* data, const ImageView& raw, const ExecutionOptions& options)
    {
        std::vector<TPixel> neighborhood;
        neighborhood.reserve(49);

        for (const auto& point : points_) {
            // Unlike the legacy object, use a local coordinate. cvCamera
            // subtracted ROI offsets from its stored point on every call,
            // causing repeated non-zero-ROI executions to drift.
            const auto row = static_cast<std::int64_t>(point.row) - options.roi[1];
            const auto column = static_cast<std::int64_t>(point.column) - options.roi[0];
            if (row < 0 || column < 0
                || row >= raw.height || column >= raw.width) {
                continue;
            }

            const auto y1 = static_cast<std::uint32_t>((std::max<std::int64_t>)(0, row - 3));
            const auto x1 = static_cast<std::uint32_t>((std::max<std::int64_t>)(0, column - 3));
            const auto y2 = static_cast<std::uint32_t>((std::min<std::int64_t>)(raw.height - 1, row + 3));
            const auto x2 = static_cast<std::uint32_t>((std::min<std::int64_t>)(raw.width - 1, column + 3));

            const auto destinationPixel =
                (static_cast<std::size_t>(row) * raw.width + static_cast<std::size_t>(column))
                * raw.channels;
            for (std::uint32_t channel = 0; channel < raw.channels; ++channel) {
                neighborhood.clear();
                for (auto y = y1; y <= y2; ++y) {
                    for (auto x = x1; x <= x2; ++x) {
                        const auto index = (static_cast<std::size_t>(y) * raw.width + x)
                            * raw.channels + channel;
                        neighborhood.push_back(data[index]);
                    }
                }
                std::sort(neighborhood.begin(), neighborhood.end());
                const auto middle = neighborhood.size() / 2;
                data[destinationPixel + channel] = neighborhood.size() % 2 == 0
                    ? static_cast<TPixel>((static_cast<unsigned int>(neighborhood[middle - 1])
                        + neighborhood[middle]) / 2)
                    : neighborhood[middle];
            }
        }
    }

    CalibrationType type_;
    std::vector<DefectPoint> points_;
};

class LineArityCalibration final : public CalibrationItem {
public:
    explicit LineArityCalibration(std::vector<float> factors)
        : factors_(std::move(factors))
    {
    }

    [[nodiscard]] CalibrationType type() const noexcept override
    {
        return CalibrationType::LineArity;
    }

    [[nodiscard]] bool shareInstanceAcrossContexts() const noexcept override { return true; }
    [[nodiscard]] bool supportsDistinctOutput() const noexcept override { return true; }

    [[nodiscard]] std::uint64_t cacheFootprintBytes() const noexcept override
    {
        return sizeof(*this)
            + static_cast<std::uint64_t>(factors_.capacity()) * sizeof(float);
    }

    bool apply(
        const ImageView& raw,
        float*,
        const ExecutionOptions&,
        std::string& error) override
    {
        if (raw.bitsPerChannel != 16) {
            error = "LineArity supports only 16-bit RAW data";
            return false;
        }

        std::size_t count = 0;
        if (!checkedMultiply<std::size_t>(raw.width, raw.height, count)) {
            error = "LineArity RAW dimensions overflow";
            return false;
        }
        if (factors_.size() < count) {
            error = "LineArity table is smaller than the RAW image";
            return false;
        }

        // cvCamera intentionally processed width*height values and ignored
        // channels. Keep that first-plane behavior, but parallelize by row.
        auto* data = reinterpret_cast<std::uint16_t*>(raw.data);
        parallelRows(raw.height, [&](std::uint32_t startRow, std::uint32_t endRow) {
            for (auto row = startRow; row < endRow; ++row) {
                const auto base = static_cast<std::size_t>(row) * raw.width;
                for (std::uint32_t column = 0; column < raw.width; ++column) {
                    const auto index = base + column;
                    data[index] = static_cast<std::uint16_t>(data[index] * factors_[index]);
                }
            }
        });
        return true;
    }

    bool applyOutOfPlace(
        const ImageView& source,
        const ImageView& destination,
        const ExecutionOptions&,
        std::string& error) override
    {
        if (source.bitsPerChannel != 16) {
            error = "LineArity supports only 16-bit RAW data";
            return false;
        }

        std::size_t sampleCount = 0;
        std::size_t byteCount = 0;
        if (!validDistinctRawOutput(
                source, destination, sampleCount, byteCount, error)) {
            return false;
        }

        std::size_t count = 0;
        if (!checkedMultiply<std::size_t>(source.width, source.height, count)) {
            error = "LineArity RAW dimensions overflow";
            return false;
        }
        if (factors_.size() < count || sampleCount < count) {
            error = "LineArity table is smaller than the RAW image";
            return false;
        }

        const auto* sourceData = reinterpret_cast<const std::uint16_t*>(source.data);
        auto* destinationData = reinterpret_cast<std::uint16_t*>(destination.data);
        parallelRows(source.height, [&](std::uint32_t startRow, std::uint32_t endRow) {
            for (auto row = startRow; row < endRow; ++row) {
                const auto base = static_cast<std::size_t>(row) * source.width;
                for (std::uint32_t column = 0; column < source.width; ++column) {
                    const auto index = base + column;
                    destinationData[index] = static_cast<std::uint16_t>(
                        sourceData[index] * factors_[index]);
                }
            }
        });

        // The legacy algorithm changes only width*height values even for a
        // multi-channel buffer. Preserve the untouched tail without first
        // copying values that the correction overwrites.
        std::memcpy(
            destinationData + count,
            sourceData + count,
            (sampleCount - count) * sizeof(std::uint16_t));
        return true;
    }

private:
    std::vector<float> factors_;
};

std::unique_ptr<CalibrationItem> loadDarkNoise(
    const std::filesystem::path& file,
    std::string& error)
{
    std::string json;
    if (!readTextFile(file, json, error)) {
        return nullptr;
    }
    if (!looksLikeJsonObject(json)) {
        error = "DarkNoise calibration is not a JSON object";
        return nullptr;
    }

    double ratio = 0.0;
    double exposure = 0.0;
    bool exposureIsReal = false;
    bool ratioIsReal = false;
    // JsonCpp's old loader assigned DarkNoiseRatio only when Texp_x was a
    // JSON real. Missing/integer Texp_x therefore leaves the zero default.
    if (findJsonNumber(json, "Texp_x", exposure, exposureIsReal) && exposureIsReal) {
        findJsonNumber(json, "DarkNoiseRatio", ratio, ratioIsReal);
    }
    (void)ratioIsReal;
    return std::make_unique<DarkNoiseCalibration>(ratio);
}

std::unique_ptr<CalibrationItem> loadDefectPoints(
    CalibrationType type,
    const std::filesystem::path& file,
    std::string& error)
{
    std::ifstream stream;
    std::uint64_t length = 0;
    if (!openBinary(file, stream, length, error)) {
        return nullptr;
    }
    if (length < sizeof(std::uint32_t)) {
        error = "Defect-point file is too short";
        return nullptr;
    }

    std::uint32_t count = 0;
    if (!readPod(stream, count, error)) {
        return nullptr;
    }
    std::uint64_t pointBytes = 0;
    std::uint64_t requiredLength = 0;
    if (!checkedMultiply<std::uint64_t>(count, sizeof(std::uint32_t) * 2, pointBytes)
        || !checkedAdd(sizeof(std::uint32_t), pointBytes, requiredLength)
        || requiredLength > length) {
        error = "Defect-point count exceeds the calibration file length";
        return nullptr;
    }

    std::vector<DefectPoint> points;
    points.reserve(count);
    for (std::uint32_t index = 0; index < count; ++index) {
        DefectPoint point;
        if (!readPod(stream, point.row, error) || !readPod(stream, point.column, error)) {
            return nullptr;
        }
        points.push_back(point);
    }
    // The file loader historically tolerated trailing bytes; retain that V0
    // compatibility rather than requiring exact length equality.
    return std::make_unique<DefectPointCalibration>(type, std::move(points));
}

std::unique_ptr<CalibrationItem> loadDsnu(
    const std::filesystem::path& file,
    std::string& error)
{
    MapHeader header;
    std::vector<std::uint16_t> values;
    if (!loadMap(file, 16, header, values, error)) {
        return nullptr;
    }
    return std::make_unique<MapCalibration<std::uint16_t, DsnuOperation>>(
        CalibrationType::Dsnu, header, std::move(values));
}

std::unique_ptr<CalibrationItem> loadUniformity(
    const std::filesystem::path& file,
    std::string& error)
{
    MapHeader header;
    std::vector<float> values;
    if (!loadMap(file, 32, header, values, error)) {
        return nullptr;
    }
    return std::make_unique<MapCalibration<float, UniformityOperation>>(
        CalibrationType::Uniformity, header, std::move(values));
}

std::unique_ptr<CalibrationItem> loadLineArity(
    const std::filesystem::path& file,
    std::string& error)
{
    std::ifstream stream;
    std::uint64_t length = 0;
    if (!openBinary(file, stream, length, error)) {
        return nullptr;
    }
    if (length <= 16) {
        error = "LineArity calibration is too short";
        return nullptr;
    }

    std::uint32_t count = 0;
    if (!readPod(stream, count, error)) {
        return nullptr;
    }
    std::uint64_t dataBytes = 0;
    std::uint64_t expectedLength = 0;
    if (!checkedMultiply<std::uint64_t>(count, sizeof(float), dataBytes)
        || !checkedAdd(sizeof(std::uint32_t), dataBytes, expectedLength)
        || expectedLength != length) {
        error = "LineArity file length does not match its element count";
        return nullptr;
    }

    std::vector<float> factors(count);
    if (!seekAndRead(stream, sizeof(std::uint32_t), factors.data(), dataBytes, error)) {
        return nullptr;
    }
    return std::make_unique<LineArityCalibration>(std::move(factors));
}

} // namespace

std::unique_ptr<CalibrationItem> loadBasicCalibration(
    CalibrationType type,
    const std::filesystem::path& file,
    std::string& error)
{
    error.clear();
    try {
        switch (type) {
        case CalibrationType::DarkNoise:
            return loadDarkNoise(file, error);
        case CalibrationType::DefectWPoint:
        case CalibrationType::DefectBPoint:
        case CalibrationType::DefectPoint:
            return loadDefectPoints(type, file, error);
        case CalibrationType::Dsnu:
            return loadDsnu(file, error);
        case CalibrationType::Uniformity:
            return loadUniformity(file, error);
        case CalibrationType::LineArity:
            return loadLineArity(file, error);
        default:
            return nullptr;
        }
    }
    catch (const std::bad_alloc&) {
        error = "Not enough memory to load calibration data";
    }
    catch (const std::exception& exception) {
        error = "Unable to load calibration: " + std::string(exception.what());
    }
    return nullptr;
}

} // namespace cvcore::calibration
