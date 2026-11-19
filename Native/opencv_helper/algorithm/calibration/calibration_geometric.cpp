#include "calibration_geometric.h"

#include <opencv2/calib3d.hpp>
#include <opencv2/core.hpp>
#include <opencv2/imgproc.hpp>

#include <nlohmann/json.hpp>

#include <algorithm>
#include <array>
#include <cmath>
#include <cstddef>
#include <cstdint>
#include <cstring>
#include <fstream>
#include <limits>
#include <memory>
#include <mutex>
#include <string>
#include <type_traits>
#include <utility>
#include <vector>

#if defined(_M_X64)
#include <emmintrin.h>
#endif

namespace cvcore::calibration {
namespace {

using json = nlohmann::json;

bool readJson(const std::filesystem::path& file, json& root, std::string& error)
{
    std::ifstream stream(file, std::ios::binary);
    if (!stream) {
        error = "Unable to open calibration file";
        return false;
    }

    try {
        stream >> root;
    }
    catch (const json::exception& exception) {
        error = "Invalid calibration JSON: " + std::string(exception.what());
        return false;
    }

    if (!root.is_object()) {
        error = "Calibration JSON root must be an object";
        return false;
    }
    return true;
}

bool readInt(const json& root, const char* key, int& value, std::string& error)
{
    const auto iterator = root.find(key);
    if (iterator == root.end() || !iterator->is_number()) {
        error = std::string("Missing numeric calibration field: ") + key;
        return false;
    }

    const double candidate = iterator->get<double>();
    if (!std::isfinite(candidate)
        || candidate < static_cast<double>((std::numeric_limits<int>::min)())
        || candidate > static_cast<double>((std::numeric_limits<int>::max)())) {
        error = std::string("Calibration field is outside the integer range: ") + key;
        return false;
    }
    value = static_cast<int>(candidate);
    return true;
}

bool readDouble(const json& root, const char* key, double& value, std::string& error)
{
    const auto iterator = root.find(key);
    if (iterator == root.end() || !iterator->is_number()) {
        error = std::string("Missing numeric calibration field: ") + key;
        return false;
    }
    value = iterator->get<double>();
    if (!std::isfinite(value)) {
        error = std::string("Calibration field must be finite: ") + key;
        return false;
    }
    return true;
}

bool readDoubleArray(
    const json& root,
    const char* key,
    std::vector<double>& values,
    std::string& error)
{
    const auto iterator = root.find(key);
    if (iterator == root.end() || !iterator->is_array()) {
        error = std::string("Missing calibration array: ") + key;
        return false;
    }

    values.clear();
    values.reserve(iterator->size());
    for (const auto& entry : *iterator) {
        if (!entry.is_number()) {
            error = std::string("Calibration array contains a non-number: ") + key;
            return false;
        }
        const double value = entry.get<double>();
        if (!std::isfinite(value)) {
            error = std::string("Calibration array contains a non-finite value: ") + key;
            return false;
        }
        values.push_back(value);
    }
    return true;
}

int imageType(const ImageView& image)
{
    const int depth = image.bitsPerChannel == 8 ? CV_8U : CV_16U;
    return CV_MAKETYPE(depth, static_cast<int>(image.channels));
}

std::size_t imageByteCount(const ImageView& image)
{
    return static_cast<std::size_t>(image.width)
        * static_cast<std::size_t>(image.height)
        * static_cast<std::size_t>(image.channels)
        * static_cast<std::size_t>(image.bitsPerChannel / 8);
}

bool validCvDimensions(const ImageView& image, std::string& error)
{
    if (image.width > static_cast<std::uint32_t>((std::numeric_limits<int>::max)())
        || image.height > static_cast<std::uint32_t>((std::numeric_limits<int>::max)())) {
        error = "Image dimensions exceed OpenCV's integer range";
        return false;
    }
    return true;
}

bool copyResultToRaw(const cv::Mat& result, const ImageView& raw, std::string& error)
{
    if (result.cols != static_cast<int>(raw.width)
        || result.rows != static_cast<int>(raw.height)
        || result.type() != imageType(raw)) {
        error = "Calibration result layout does not match the in-place RAW buffer";
        return false;
    }
    if (!result.isContinuous()) {
        error = "Calibration result is not contiguous";
        return false;
    }
    std::memcpy(raw.data, result.data, imageByteCount(raw));
    return true;
}

bool validDistinctOutput(
    const ImageView& source,
    const ImageView& destination,
    std::string& error)
{
    if (source.data == destination.data) {
        error = "Geometric calibration requires distinct source and destination buffers";
        return false;
    }
    if (destination.data == nullptr
        || source.width != destination.width
        || source.height != destination.height
        || source.bitsPerChannel != destination.bitsPerChannel
        || source.channels != destination.channels
        || destination.dataLength < imageByteCount(source)) {
        error = "Geometric calibration output layout does not match its source";
        return false;
    }
    return true;
}

bool validLegacyRoi(
    const ExecutionOptions& options,
    int fullWidth,
    int fullHeight)
{
    const auto x = static_cast<std::uint64_t>(options.roi[0]);
    const auto y = static_cast<std::uint64_t>(options.roi[1]);
    const auto width = static_cast<std::uint64_t>(options.roi[2]);
    const auto height = static_cast<std::uint64_t>(options.roi[3]);
    return width != 0 && height != 0
        && x + width < static_cast<std::uint64_t>(fullWidth)
        && y + height < static_cast<std::uint64_t>(fullHeight);
}

#if defined(_M_X64)
bool canRemapFixedBgr16(cv::Size source, cv::Size result,
    const cv::Mat& coordinates, const cv::Mat& interpolation)
{
    // Keep the fixed-map interpolation and float rounding verified against OpenCV 5.0.0.
    static const bool supportedVersion = cv::getVersionString() == "5.0.0";
    constexpr auto maximumDimension = (std::numeric_limits<short>::max)();
    constexpr std::size_t minimumPixels = 5'000'000;
    return supportedVersion && cv::useOptimized() && source.width >= 3 && source.height >= 2
        && source.width < maximumDimension && source.height < maximumDimension
        && result.width < maximumDimension && result.height < maximumDimension
        && static_cast<std::size_t>(result.area()) >= minimumPixels
        && coordinates.type() == CV_16SC2 && interpolation.type() == CV_16UC1
        && coordinates.size() == result && interpolation.size() == result;
}

const auto& fixedRemapWeights()
{
    static const auto weights = [] {
        constexpr auto scale = static_cast<float>(cv::INTER_TAB_SIZE);
        std::array<cv::Vec4f, cv::INTER_TAB_SIZE2> table;
        for (int i = 0; i != cv::INTER_TAB_SIZE2; ++i) {
            const float u = static_cast<float>(i % cv::INTER_TAB_SIZE) / scale;
            const float v = static_cast<float>(i / cv::INTER_TAB_SIZE) / scale;
            table[i] = cv::Vec4f((1.0F - u) * (1.0F - v), u * (1.0F - v), (1.0F - u) * v, u * v);
        }
        return table;
    }();
    return weights;
}

template<bool flipRows, bool flipColumns>
void remapFixedBgr16(const cv::Mat& source, cv::Mat& result,
    const cv::Mat& coordinates, const cv::Mat& interpolation)
{
    const auto& weights = fixedRemapWeights();
    const auto zero = _mm_setzero_si128();
    const auto bias = _mm_set1_epi32(0x8000);
    const auto sign = _mm_set1_epi16(-32768);
    const auto stride = source.step1();
    const auto* sourcePixels = source.ptr<std::uint16_t>();
    const auto loadFourSamples = [&](const std::uint16_t* samples) {
        return _mm_cvtepi32_ps(_mm_unpacklo_epi16(
            _mm_loadl_epi64(reinterpret_cast<const __m128i*>(samples)), zero));
    };
    const auto lookupPixel = [&](int column, int row) -> const std::uint16_t* {
        static constexpr std::uint16_t border[3]{};
        return static_cast<unsigned>(column) < static_cast<unsigned>(source.cols)
                && static_cast<unsigned>(row) < static_cast<unsigned>(source.rows)
            ? sourcePixels + row * stride + 3 * column : border;
    };
    cv::parallel_for_(cv::Range(0, result.rows), [&](const cv::Range& rows) {
        for (int y = rows.start; y < rows.end; ++y) {
            const auto* positions = coordinates.ptr<cv::Vec2s>(y);
            const auto* fractions = interpolation.ptr<std::uint16_t>(y);
            auto* out = result.ptr<std::uint16_t>(flipRows ? result.rows - 1 - y : y);
            // For a reversed row, decrement before each store so the pointer
            // stays within the output row after writing its first pixel.
            if constexpr (flipColumns) out += 3 * result.cols;
            for (int x = 0; x < result.cols; ++x, out += flipColumns ? 0 : 3) {
                if constexpr (flipColumns) out -= 3;
                const int sourceX = positions[x][0];
                const int sourceY = positions[x][1];
                const auto& weight = weights[fractions[x] & (cv::INTER_TAB_SIZE2 - 1)];
                if (static_cast<unsigned>(sourceX) < static_cast<unsigned>(source.cols - 2)
                    && static_cast<unsigned>(sourceY) < static_cast<unsigned>(source.rows - 1)) {
                    // The fourth loaded component belongs to the next pixel.
                    // The column check above keeps both loads inside this row.
                    const auto* top = sourcePixels + sourceY * stride + 3 * sourceX;
                    const auto* bottom = top + stride;
                    const auto factors = _mm_loadu_ps(weight.val);
                    const auto a = _mm_mul_ps(loadFourSamples(top), _mm_shuffle_ps(factors, factors, _MM_SHUFFLE(0, 0, 0, 0)));
                    const auto b = _mm_mul_ps(loadFourSamples(top + 3), _mm_shuffle_ps(factors, factors, _MM_SHUFFLE(1, 1, 1, 1)));
                    const auto c = _mm_mul_ps(loadFourSamples(bottom), _mm_shuffle_ps(factors, factors, _MM_SHUFFLE(2, 2, 2, 2)));
                    const auto d = _mm_mul_ps(loadFourSamples(bottom + 3), _mm_shuffle_ps(factors, factors, _MM_SHUFFLE(3, 3, 3, 3)));
                    // Match cv::remap's float addition order before rounding to 16 bits.
                    const auto weighted = _mm_add_ps(_mm_add_ps(_mm_add_ps(a, b), c), d);
                    // SSE2 packs signed values, so shift the unsigned range first.
                    const auto ints = _mm_sub_epi32(_mm_cvtps_epi32(weighted), bias);
                    const auto packed = _mm_xor_si128(_mm_packs_epi32(ints, ints), sign);
                    const auto bg = static_cast<std::uint32_t>(_mm_cvtsi128_si32(packed));
                    std::memcpy(out, &bg, sizeof(bg));
                    out[2] = static_cast<std::uint16_t>(_mm_extract_epi16(packed, 2));
                    continue;
                }
                if (sourceX < -1 || sourceX >= source.cols || sourceY < -1 || sourceY >= source.rows) {
                    std::fill_n(out, 3, std::uint16_t{0});
                    continue;
                }
                const auto* topLeft = lookupPixel(sourceX, sourceY);
                const auto* topRight = lookupPixel(sourceX + 1, sourceY);
                const auto* bottomLeft = lookupPixel(sourceX, sourceY + 1);
                const auto* bottomRight = lookupPixel(sourceX + 1, sourceY + 1);
                for (int c = 0; c != 3; ++c) {
                    out[c] = cv::saturate_cast<std::uint16_t>(topLeft[c] * weight[0]
                        + topRight[c] * weight[1] + bottomLeft[c] * weight[2] + bottomRight[c] * weight[3]);
                }
            }
        }
    });
}

bool tryRemapFixedBgr16(const cv::Mat& source, cv::Mat& result,
    const cv::Mat& coordinates, const cv::Mat& interpolation, int flip)
{
    if (source.type() != CV_16UC3
        || reinterpret_cast<std::uintptr_t>(source.data) % alignof(std::uint16_t) != 0
        || reinterpret_cast<std::uintptr_t>(result.data) % alignof(std::uint16_t) != 0
        || !canRemapFixedBgr16(source.size(), result.size(), coordinates, interpolation)) {
        return false;
    }
    switch (flip) {
    case 0: remapFixedBgr16<true, false>(source, result, coordinates, interpolation); break;
    case 1: remapFixedBgr16<false, true>(source, result, coordinates, interpolation); break;
    case -1: remapFixedBgr16<true, true>(source, result, coordinates, interpolation); break;
    default: remapFixedBgr16<false, false>(source, result, coordinates, interpolation); break;
    }
    return true;
}
#endif

void buildFisheyeFixedMaps(const std::array<float, 9>& camera, const std::array<float, 5>& distortion,
    const cv::Mat& newCamera, cv::Size size, int offsetX, int offsetY, cv::Mat& coordinates, cv::Mat& interpolation)
{
    cv::Matx33d projection;
    newCamera.convertTo(projection, CV_64F);
    const auto inverse = (projection * cv::Matx33d::eye()).inv(cv::DECOMP_SVD);
    coordinates.create(size, CV_16SC2);
    interpolation.create(size, CV_16UC1);

    // Keep OpenCV's row recurrence and float rounding before fixed-point conversion.
    // A worker needs only one band of temporary float coordinates.
    constexpr int bandRows = 16;
    cv::parallel_for_(cv::Range(0, 1 + (size.height - 1) / bandRows), [&](const cv::Range& range) {
        const int scratchHeight = (std::min)(bandRows, size.height);
        cv::Mat mapX(scratchHeight, size.width, CV_32FC1), mapY(scratchHeight, size.width, CV_32FC1);
        for (int band = range.start; band < range.end; ++band) {
            const int first = band * bandRows;
            const int rows = (std::min)(bandRows, size.height - first);
            for (int index = 0; index < rows; ++index) {
                const int row = first + index;
                auto* xOut = mapX.ptr<float>(index);
                auto* yOut = mapY.ptr<float>(index);
                double xNumerator = row * inverse(0, 1) + inverse(0, 2);
                double yNumerator = row * inverse(1, 1) + inverse(1, 2);
                double denominator = row * inverse(2, 1) + inverse(2, 2);
                for (int column = 0; column < size.width; ++column) {
                    double u, v;
                    if (denominator <= 0) {
                        constexpr auto infinity = std::numeric_limits<double>::infinity();
                        u = xNumerator > 0 ? -infinity : infinity;
                        v = yNumerator > 0 ? -infinity : infinity;
                    }
                    else {
                        const auto x = xNumerator / denominator, y = yNumerator / denominator;
                        const auto radius = std::sqrt(x * x + y * y);
                        const auto theta = std::atan(radius);
                        const auto theta2 = theta * theta, theta4 = theta2 * theta2;
                        const auto theta6 = theta4 * theta2, theta8 = theta4 * theta4;
                        const auto distorted = theta * (1 + distortion[0] * theta2 + distortion[1] * theta4
                            + distortion[2] * theta6 + distortion[3] * theta8);
                        const auto scale = radius == 0 ? 1.0 : distorted / radius;
                        u = camera[0] * x * scale + camera[2];
                        v = camera[4] * y * scale + camera[5];
                    }
                    xOut[column] = static_cast<float>(u);
                    yOut[column] = static_cast<float>(v);
                    xNumerator += inverse(0, 0);
                    yNumerator += inverse(1, 0);
                    denominator += inverse(2, 0);
                }
            }
            auto xBand = mapX.rowRange(0, rows), yBand = mapY.rowRange(0, rows);
            if (offsetX > 0) xBand -= static_cast<float>(offsetX);
            if (offsetY > 0) yBand -= static_cast<float>(offsetY);
            auto coordinateBand = coordinates.rowRange(first, first + rows);
            auto interpolationBand = interpolation.rowRange(first, first + rows);
            cv::convertMaps(xBand, yBand, coordinateBand, interpolationBand, CV_16SC2);
        }
    });
}

class DistortionCalibration final : public CalibrationItem {
public:
    DistortionCalibration() = default;

    [[nodiscard]] CalibrationType type() const noexcept override
    {
        return CalibrationType::Distortion;
    }

    [[nodiscard]] bool requiresDistinctOutput() const noexcept override
    {
        return true;
    }

#if defined(_M_X64)
    bool canFuseRawFlip(const ImageView& source, const ImageView& destination,
        const ExecutionOptions& options) const override
    {
        if (source.bitsPerChannel != 16 || source.channels != 3 || !options.interleavedBgr || options.rgbType != 0
            || destination.width != source.width || destination.height != source.height
            || destination.bitsPerChannel != source.bitsPerChannel || destination.channels != source.channels
            || (options.rawOutputFlip != 0 && options.rawOutputFlip != 1 && options.rawOutputFlip != -1)
            || reinterpret_cast<std::uintptr_t>(source.data) % alignof(std::uint16_t) != 0
            || reinterpret_cast<std::uintptr_t>(destination.data) % alignof(std::uint16_t) != 0) {
            return false;
        }

        const bool useRoi = validLegacyRoi(options, width_, height_);
        const std::int64_t roiX = useRoi ? options.roi[0] : 0;
        const std::int64_t roiY = useRoi ? options.roi[1] : 0;
        const auto width = useRoi ? options.roi[2] : static_cast<std::uint32_t>(width_);
        const auto height = useRoi ? options.roi[3] : static_cast<std::uint32_t>(height_);
        constexpr auto maximumDimension = static_cast<std::uint32_t>((std::numeric_limits<short>::max)());
        if (width == 0 || height == 0 || width >= maximumDimension || height >= maximumDimension
            || source.width != width || source.height != height) {
            return false;
        }
        const cv::Size result(static_cast<int>(width), static_cast<int>(height));
        // Let ordinary execution build maps for a new ROI before fusing its flip.
        if (useRoi && (roiX_ != roiX || roiY_ != roiY || roiWidth_ != result.width || roiHeight_ != result.height)) {
            return false;
        }

        const std::int64_t offsetX = static_cast<std::int64_t>(width) / 2 - cameraCenterX_ + roiX;
        const std::int64_t offsetY = static_cast<std::int64_t>(height) / 2 - cameraCenterY_ + roiY;
        const std::int64_t sourceWidth = static_cast<std::int64_t>(width) - (useFisheye_ ? std::abs(offsetX) : 0);
        const std::int64_t sourceHeight = static_cast<std::int64_t>(height) - (useFisheye_ ? std::abs(offsetY) : 0);
        if (sourceWidth < 3 || sourceHeight < 2 || sourceWidth >= maximumDimension || sourceHeight >= maximumDimension) {
            return false;
        }
        const cv::Size remapSource(static_cast<int>(sourceWidth), static_cast<int>(sourceHeight));
        return canRemapFixedBgr16(remapSource, result,
            useRoi ? roiCoordinates_ : coordinates_, useRoi ? roiInterpolation_ : interpolation_);
    }
#endif

    [[nodiscard]] std::unique_ptr<CalibrationItem> cloneForContext() const override
    {
        return std::unique_ptr<CalibrationItem>(new DistortionCalibration(*this));
    }

    [[nodiscard]] std::uint64_t cacheFootprintBytes() const noexcept override
    {
        return sizeof(*this)
            + static_cast<std::uint64_t>(coordinates_.total()) * coordinates_.elemSize()
            + static_cast<std::uint64_t>(interpolation_.total()) * interpolation_.elemSize();
    }

    bool load(const json& root, std::string& error)
    {
        if (!readInt(root, "w", width_, error)
            || !readInt(root, "h", height_, error)) {
            return false;
        }
        if (width_ <= 0 || height_ <= 0) {
            error = "Distortion calibration dimensions must be positive";
            return false;
        }

        showWidth_ = width_;
        showHeight_ = height_;
        if (root.contains("s_w") && !readInt(root, "s_w", showWidth_, error)) {
            return false;
        }
        if (root.contains("s_h") && !readInt(root, "s_h", showHeight_, error)) {
            return false;
        }
        if (showWidth_ <= 0 || showHeight_ <= 0) {
            error = "Distortion output dimensions must be positive";
            return false;
        }

        useFisheye_ = root.value("useFisheye", false);
        if (root.contains("alpha")) {
            if (!readDouble(root, "alpha", alpha_, error)) {
                return false;
            }
        }

        std::vector<double> camera;
        std::vector<double> distortion;
        if (!readDoubleArray(root, "cameraMatrix", camera, error)
            || !readDoubleArray(root, "distCoeffs", distortion, error)) {
            return false;
        }
        if (camera.size() < cameraMatrix_.size()) {
            error = "cameraMatrix must contain nine values";
            return false;
        }
        if (distortion.size() < 4) {
            error = "distCoeffs must contain at least four values";
            return false;
        }

        for (std::size_t index = 0; index < cameraMatrix_.size(); ++index) {
            cameraMatrix_[index] = static_cast<float>(camera[index]);
        }
        for (std::size_t index = 0;
             index < distortionCoefficients_.size() && index < distortion.size();
             ++index) {
            distortionCoefficients_[index] = static_cast<float>(distortion[index]);
        }

        if (useFisheye_) {
            cameraCenterX_ = static_cast<int>(cameraMatrix_[2]);
            cameraCenterY_ = static_cast<int>(cameraMatrix_[5]);
            cameraMatrix_[2] = static_cast<float>(width_ / 2);
            cameraMatrix_[5] = static_cast<float>(height_ / 2);
        }

        try {
            buildMaps(width_, height_, 0, 0, false, coordinates_, interpolation_);
        }
        catch (const cv::Exception& exception) {
            error = "Unable to create distortion maps: " + std::string(exception.what());
            return false;
        }
        return true;
    }

    bool apply(
        const ImageView& raw,
        float*,
        const ExecutionOptions& options,
        std::string& error) override
    {
        fallbackScratch_.create(
            static_cast<int>(raw.height),
            static_cast<int>(raw.width),
            imageType(raw));
        ImageView destination = raw;
        destination.data = fallbackScratch_.data;
        destination.dataLength = fallbackScratch_.total() * fallbackScratch_.elemSize();
        return applyOutOfPlace(raw, destination, options, error)
            && copyResultToRaw(fallbackScratch_, raw, error);
    }

    bool applyOutOfPlace(
        const ImageView& raw,
        const ImageView& destination,
        const ExecutionOptions& options,
        std::string& error) override
    {
        if (!validCvDimensions(raw, error)) {
            return false;
        }
        if (!validDistinctOutput(raw, destination, error)) {
            return false;
        }

        try {
            const bool useRoi = validLegacyRoi(options, width_, height_);
            const cv::Mat* selectedCoordinates = &coordinates_;
            const cv::Mat* selectedInterpolation = &interpolation_;
            int offsetX = width_ / 2 - cameraCenterX_;
            int offsetY = height_ / 2 - cameraCenterY_;

            if (useRoi) {
                const int roiX = static_cast<int>(options.roi[0]);
                const int roiY = static_cast<int>(options.roi[1]);
                const int roiWidth = static_cast<int>(options.roi[2]);
                const int roiHeight = static_cast<int>(options.roi[3]);
                if (raw.width != static_cast<std::uint32_t>(roiWidth)
                    || raw.height != static_cast<std::uint32_t>(roiHeight)) {
                    error = "Distortion ROI dimensions do not match the RAW buffer";
                    return false;
                }

                if (roiX_ != roiX || roiY_ != roiY
                    || roiWidth_ != roiWidth || roiHeight_ != roiHeight
                    || roiCoordinates_.empty() || roiInterpolation_.empty()) {
                    buildMaps(roiWidth, roiHeight, roiX, roiY, true, roiCoordinates_, roiInterpolation_);
                    roiX_ = roiX;
                    roiY_ = roiY;
                    roiWidth_ = roiWidth;
                    roiHeight_ = roiHeight;
                }
                selectedCoordinates = &roiCoordinates_;
                selectedInterpolation = &roiInterpolation_;
                offsetX = roiWidth / 2 - cameraCenterX_ + roiX;
                offsetY = roiHeight / 2 - cameraCenterY_ + roiY;
            }
            else {
                if (raw.width != static_cast<std::uint32_t>(width_)
                    || raw.height != static_cast<std::uint32_t>(height_)) {
                    error = "RAW dimensions do not match the distortion calibration";
                    return false;
                }
                if (selectedCoordinates->cols != static_cast<int>(raw.width)
                    || selectedCoordinates->rows != static_cast<int>(raw.height)) {
                    error = "Distortion show size cannot be written to an in-place RAW buffer";
                    return false;
                }
            }

            cv::Mat source(
                static_cast<int>(raw.height),
                static_cast<int>(raw.width),
                imageType(raw),
                raw.data);
            cv::Mat output(
                static_cast<int>(destination.height),
                static_cast<int>(destination.width),
                imageType(destination),
                destination.data);
            cv::Mat remapSource = source;

            if (useFisheye_ && (offsetX != 0 || offsetY != 0)) {
                // The legacy integer warp is exactly a zero-bordered view of
                // this cropped source rectangle. Maps are rebased to the
                // rectangle in buildMaps, avoiding a full-frame translation.
                const int copyWidth = source.cols - std::abs(offsetX);
                const int copyHeight = source.rows - std::abs(offsetY);
                if (copyWidth <= 0 || copyHeight <= 0) {
                    output.setTo(cv::Scalar::all(0));
                    return true;
                }
                remapSource = source(cv::Rect(
                    (std::max)(0, -offsetX),
                    (std::max)(0, -offsetY),
                    copyWidth,
                    copyHeight));
            }

#if defined(_M_X64)
            if (tryRemapFixedBgr16(remapSource, output, *selectedCoordinates, *selectedInterpolation, options.rawOutputFlip)) {
                return true;
            }
#endif
            cv::remap(
                remapSource,
                output,
                *selectedCoordinates,
                *selectedInterpolation,
                cv::INTER_LINEAR,
                cv::BORDER_CONSTANT);
            // The backend can still decline after the context's preflight.
            if (options.rawOutputFlip != -99) cv::flip(output, output, options.rawOutputFlip);
            return true;
        }
        catch (const cv::Exception& exception) {
            error = "Distortion calibration failed: " + std::string(exception.what());
            return false;
        }
    }

private:
    DistortionCalibration(const DistortionCalibration& source)
        : width_(source.width_)
        , height_(source.height_)
        , showWidth_(source.showWidth_)
        , showHeight_(source.showHeight_)
        , cameraCenterX_(source.cameraCenterX_)
        , cameraCenterY_(source.cameraCenterY_)
        , alpha_(source.alpha_)
        , useFisheye_(source.useFisheye_)
        , cameraMatrix_(source.cameraMatrix_)
        , distortionCoefficients_(source.distortionCoefficients_)
        , coordinates_(source.coordinates_)
        , interpolation_(source.interpolation_)
    {
    }

    void buildMaps(
        int outputWidth,
        int outputHeight,
        int roiX,
        int roiY,
        bool roi,
        cv::Mat& coordinates,
        cv::Mat& interpolation) const
    {
        cv::Mat mapX;
        cv::Mat mapY;
        std::array<float, 9> adjusted = cameraMatrix_;
        if (roi) {
            if (useFisheye_) {
                adjusted[2] = static_cast<float>(outputWidth / 2);
                adjusted[5] = static_cast<float>(outputHeight / 2);
            }
            else {
                adjusted[2] -= static_cast<float>(roiX);
                adjusted[5] -= static_cast<float>(roiY);
            }
        }

        cv::Mat camera(3, 3, CV_32FC1, adjusted.data());
        const cv::Size imageSize(outputWidth, outputHeight);
        if (useFisheye_) {
            cv::Mat distortion(4, 1, CV_32FC1,
                const_cast<float*>(distortionCoefficients_.data()));
            cv::Mat newCamera;
            const cv::Size newSize = roi
                ? imageSize
                : cv::Size(showWidth_, showHeight_);
            cv::fisheye::estimateNewCameraMatrixForUndistortRectify(
                camera,
                distortion,
                imageSize,
                cv::Matx33d::eye(),
                newCamera,
                alpha_,
                newSize);

            const int offsetX = roi
                ? outputWidth / 2 - cameraCenterX_ + roiX
                : width_ / 2 - cameraCenterX_;
            const int offsetY = roi
                ? outputHeight / 2 - cameraCenterY_ + roiY
                : height_ / 2 - cameraCenterY_;
            if (!newSize.empty() && cv::checkRange(camera) && cv::checkRange(distortion) && cv::checkRange(newCamera)) {
                buildFisheyeFixedMaps(adjusted, distortionCoefficients_, newCamera, newSize,
                    offsetX, offsetY, coordinates, interpolation);
                return;
            }

            cv::fisheye::initUndistortRectifyMap(
                camera,
                distortion,
                cv::Matx33d::eye(),
                newCamera,
                newSize,
                CV_32FC1,
                mapX,
                mapY);

            // Positive shifts move the valid rectangle's origin in the
            // translated image; rebase float maps to the cropped source view.
            mapX -= static_cast<float>((std::max)(0, offsetX));
            mapY -= static_cast<float>((std::max)(0, offsetY));
        }
        else {
            cv::Mat distortion(5, 1, CV_32FC1,
                const_cast<float*>(distortionCoefficients_.data()));
            const cv::Mat newCamera = cv::getOptimalNewCameraMatrix(
                camera,
                distortion,
                imageSize,
                alpha_,
                imageSize,
                nullptr);
            cv::initUndistortRectifyMap(
                camera,
                distortion,
                cv::Mat(),
                newCamera,
                imageSize,
                CV_32FC1,
                mapX,
                mapY);
        }

        // Cache remap's fixed-point coordinates and interpolation indices once.
        cv::convertMaps(mapX, mapY, coordinates, interpolation, CV_16SC2);
    }

    int width_ = 0;
    int height_ = 0;
    int showWidth_ = 0;
    int showHeight_ = 0;
    int cameraCenterX_ = 0;
    int cameraCenterY_ = 0;
    double alpha_ = 0.0;
    bool useFisheye_ = false;
    std::array<float, 9> cameraMatrix_{};
    std::array<float, 5> distortionCoefficients_{};

    cv::Mat coordinates_;
    cv::Mat interpolation_;
    cv::Mat roiCoordinates_;
    cv::Mat roiInterpolation_;
    int roiX_ = -1;
    int roiY_ = -1;
    int roiWidth_ = -1;
    int roiHeight_ = -1;

    cv::Mat fallbackScratch_;
};

struct PixelOffset {
    int x = 0;
    int y = 0;
};

bool readOffset(const json& value, PixelOffset& offset)
{
    if (!value.is_object() || !value.contains("X") || !value.contains("Y")
        || !value["X"].is_number() || !value["Y"].is_number()) {
        return false;
    }
    offset.x = static_cast<int>(value["X"].get<double>());
    offset.y = static_cast<int>(value["Y"].get<double>());
    return true;
}

class ColorShiftCalibration final : public CalibrationItem {
public:
    ColorShiftCalibration() = default;

    [[nodiscard]] CalibrationType type() const noexcept override
    {
        return CalibrationType::ColorShift;
    }

    [[nodiscard]] bool requiresDistinctOutput() const noexcept override
    {
        return true;
    }

    [[nodiscard]] std::unique_ptr<CalibrationItem> cloneForContext() const override
    {
        return std::unique_ptr<CalibrationItem>(new ColorShiftCalibration(*this));
    }

    bool load(const json& root, std::string& error)
    {
        fillOffset_ = root.value("fillOffset", false);
        const auto iterator = root.find("offset");
        if (iterator == root.end()) {
            error = "ColorShift is missing offset";
            return false;
        }

        if (iterator->is_array()) {
            if (iterator->size() != channelOffsets_.size()) {
                error = "ColorShift offset array must contain three entries";
                return false;
            }
            for (std::size_t index = 0; index < channelOffsets_.size(); ++index) {
                if (!readOffset((*iterator)[index], channelOffsets_[index])) {
                    error = "ColorShift contains an invalid channel offset";
                    return false;
                }
            }
            hasChannelOffsets_ = true;
            return true;
        }

        if (!readOffset(*iterator, commonOffset_)) {
            error = "ColorShift contains an invalid offset";
            return false;
        }
        return true;
    }

    bool apply(
        const ImageView& raw,
        float*,
        const ExecutionOptions& options,
        std::string& error) override
    {
        fallbackScratch_.create(
            static_cast<int>(raw.height),
            static_cast<int>(raw.width),
            imageType(raw));
        ImageView destination = raw;
        destination.data = fallbackScratch_.data;
        destination.dataLength = fallbackScratch_.total() * fallbackScratch_.elemSize();
        return applyOutOfPlace(raw, destination, options, error)
            && copyResultToRaw(fallbackScratch_, raw, error);
    }

    bool applyOutOfPlace(
        const ImageView& raw,
        const ImageView& destination,
        const ExecutionOptions& options,
        std::string& error) override
    {
        if (raw.channels != 3) {
            error = "ColorShift requires three channels";
            return false;
        }
        if (!validCvDimensions(raw, error)) {
            return false;
        }
        if (!validDistinctOutput(raw, destination, error)) {
            return false;
        }

        try {
            cv::Mat source(
                static_cast<int>(raw.height),
                static_cast<int>(raw.width),
                imageType(raw),
                raw.data);
            cv::Mat output(
                static_cast<int>(destination.height),
                static_cast<int>(destination.width),
                imageType(destination),
                destination.data);

            if (!hasChannelOffsets_) {
                applyCommonOffset(source, output, commonOffset_);
                return true;
            }

            if (options.interleavedBgr && canGatherInterleaved(source, output)) {
                const PixelOffset first = channelOffsets_.front();
                if (std::all_of(channelOffsets_.begin() + 1, channelOffsets_.end(), [first](const PixelOffset& offset) {
                        return first.x == offset.x && first.y == offset.y;
                    })) {
                    applyCommonOffset(source, output, first);
                }
                else if (raw.bitsPerChannel == 8) {
                    gatherInterleaved<std::uint8_t>(source, output);
                }
                else {
                    gatherInterleaved<std::uint16_t>(source, output);
                }
                return true;
            }

            std::array<cv::Mat, 3> planarViews;
            const cv::Mat* sourcePlanes = planarViews.data();
            if (options.interleavedBgr) {
                cv::split(source, planes_);
                sourcePlanes = planes_.data();
            }
            else {
                // Borrow the caller's planar R/G/B only for this execution.
                // Keep the retained split outputs independent of the RAW buffer.
                const std::size_t planeBytes = static_cast<std::size_t>(raw.width)
                    * raw.height * (raw.bitsPerChannel / 8);
                const int planeType = CV_MAKETYPE(
                    raw.bitsPerChannel == 8 ? CV_8U : CV_16U,
                    1);
                planarViews[0] = cv::Mat(
                    static_cast<int>(raw.height),
                    static_cast<int>(raw.width),
                    planeType,
                    raw.data + planeBytes * 2);
                planarViews[1] = cv::Mat(
                    static_cast<int>(raw.height),
                    static_cast<int>(raw.width),
                    planeType,
                    raw.data + planeBytes);
                planarViews[2] = cv::Mat(
                    static_cast<int>(raw.height),
                    static_cast<int>(raw.width),
                    planeType,
                    raw.data);
            }

            std::array<cv::Mat, 3> mergePlanes;
            for (std::size_t index = 0; index < mergePlanes.size(); ++index) {
                const auto& offset = channelOffsets_[index];
                if (offset.x == 0 && offset.y == 0) {
                    mergePlanes[index] = sourcePlanes[index];
                }
                else {
                    warp(sourcePlanes[index], warpedPlanes_[index], offset);
                    preserveLegacyFillBehavior(warpedPlanes_[index], offset);
                    mergePlanes[index] = warpedPlanes_[index];
                }
            }

            cv::merge(mergePlanes.data(), mergePlanes.size(), output);
            return true;
        }
        catch (const cv::Exception& exception) {
            error = "ColorShift calibration failed: " + std::string(exception.what());
            return false;
        }
    }

private:
    ColorShiftCalibration(const ColorShiftCalibration& source)
        : fillOffset_(source.fillOffset_)
        , hasChannelOffsets_(source.hasChannelOffsets_)
        , commonOffset_(source.commonOffset_)
        , channelOffsets_(source.channelOffsets_)
    {
    }

    void applyCommonOffset(const cv::Mat& source, cv::Mat& output, const PixelOffset& offset) const
    {
        if (offset.x == 0 && offset.y == 0) {
            source.copyTo(output);
            return;
        }
        warp(source, output, offset);
        preserveLegacyFillBehavior(output, offset);
    }

    bool canGatherInterleaved(const cv::Mat& source, const cv::Mat& output) const
    {
        constexpr int maximumDimension = (std::numeric_limits<short>::max)();
        if (source.cols >= maximumDimension || source.rows >= maximumDimension) {
            return false;
        }
        if (source.depth() == CV_16U
            && (reinterpret_cast<std::uintptr_t>(source.data) % alignof(std::uint16_t) != 0
                || reinterpret_cast<std::uintptr_t>(output.data) % alignof(std::uint16_t) != 0)) {
            return false;
        }
        return std::all_of(channelOffsets_.begin(), channelOffsets_.end(), [&](const PixelOffset& offset) {
            return offset.x > -source.cols && offset.x < source.cols
                && offset.y > -source.rows && offset.y < source.rows
                // A wider filled strip fails the legacy OpenCV ROI operation.
                && (!fillOffset_ || std::abs(offset.x) <= source.cols / 2);
        });
    }

    template<typename Sample>
    void gatherInterleaved(const cv::Mat& source, cv::Mat& output) const
    {
        const int firstInterior = (std::max)({0, channelOffsets_[0].x, channelOffsets_[1].x, channelOffsets_[2].x});
        const int endInterior = source.cols + (std::min)({0, channelOffsets_[0].x, channelOffsets_[1].x, channelOffsets_[2].x});
        cv::parallel_for_(cv::Range(0, source.rows), [&](const cv::Range& rows) {
            for (int row = rows.start; row < rows.end; ++row) {
                std::array<const Sample*, 3> shiftedRows{};
                for (std::size_t channel = 0; channel < shiftedRows.size(); ++channel) {
                    const int inputRow = row - channelOffsets_[channel].y;
                    if (inputRow >= 0 && inputRow < source.rows) {
                        shiftedRows[channel] = source.ptr<Sample>(inputRow);
                    }
                }
                auto* outputRow = output.ptr<Sample>(row);
                const auto writeEdge = [&](int column) {
                    auto* pixel = outputRow + column * 3;
                    for (std::size_t channel = 0; channel < shiftedRows.size(); ++channel) {
                        const auto* input = shiftedRows[channel];
                        int inputColumn = column - channelOffsets_[channel].x;
                        if (inputColumn < 0 || inputColumn >= source.cols) {
                            // Legacy filling copies the adjacent shifted strip, which samples
                            // the same original X. It leaves the vertical zero border intact.
                            inputColumn = fillOffset_ ? column : -1;
                        }
                        pixel[channel] = input && inputColumn >= 0 ? input[inputColumn * 3 + channel] : Sample{};
                    }
                };
                int column = 0;
                if (firstInterior < endInterior && shiftedRows[0] && shiftedRows[1] && shiftedRows[2]) {
                    for (; column < firstInterior; ++column) writeEdge(column);
                    const Sample* blue = shiftedRows[0] + (column - channelOffsets_[0].x) * 3;
                    const Sample* green = shiftedRows[1] + (column - channelOffsets_[1].x) * 3;
                    const Sample* red = shiftedRows[2] + (column - channelOffsets_[2].x) * 3;
                    Sample* destination = outputRow + column * 3;
                    const int samples = (endInterior - firstInterior) * 3;
                    for (int index = 0; index < samples; index += 3) {
                        destination[index] = blue[index];
                        destination[index + 1] = green[index + 1];
                        destination[index + 2] = red[index + 2];
                    }
                    column = endInterior;
                }
                for (; column < source.cols; ++column) writeEdge(column);
            }
        });
    }

    static void warp(const cv::Mat& source, cv::Mat& destination, const PixelOffset& offset)
    {
        const cv::Matx23f translation(
            1.0F, 0.0F, static_cast<float>(offset.x),
            0.0F, 1.0F, static_cast<float>(offset.y));
        cv::warpAffine(
            source,
            destination,
            translation,
            source.size(),
            cv::INTER_LINEAR,
            cv::BORDER_CONSTANT);
    }

    void preserveLegacyFillBehavior(cv::Mat& image, const PixelOffset& offset) const
    {
        if (!fillOffset_) {
            return;
        }

        // Preserve the historical implementation exactly: the horizontal
        // uncovered strip is filled from the adjacent shifted strip, while the
        // vertical strip is copied onto itself and therefore remains unchanged.
        const int width = std::abs(offset.x);
        if (width == 0) {
            return;
        }
        const int sourceX = offset.x < 0
            ? image.cols + offset.x * 2
            : offset.x;
        const int destinationX = offset.x < 0
            ? image.cols + offset.x
            : 0;
        const cv::Mat sourceStrip(image, cv::Rect(sourceX, 0, width, image.rows));
        sourceStrip.copyTo(image(cv::Rect(destinationX, 0, width, image.rows)));
    }

    bool fillOffset_ = false;
    bool hasChannelOffsets_ = false;
    PixelOffset commonOffset_{};
    std::array<PixelOffset, 3> channelOffsets_{};
    std::vector<cv::Mat> planes_;
    std::array<cv::Mat, 3> warpedPlanes_{};
    cv::Mat fallbackScratch_;
};

class ColorDiffCalibration final : public CalibrationItem {
public:
    ColorDiffCalibration() = default;

    [[nodiscard]] CalibrationType type() const noexcept override
    {
        return CalibrationType::ColorDiff;
    }

    [[nodiscard]] bool requiresDistinctOutput() const noexcept override
    {
        return true;
    }

    [[nodiscard]] std::unique_ptr<CalibrationItem> cloneForContext() const override
    {
        return std::unique_ptr<CalibrationItem>(new ColorDiffCalibration(*this));
    }

    [[nodiscard]] std::uint64_t cacheFootprintBytes() const noexcept override
    {
        return sizeof(*this)
            + static_cast<std::uint64_t>(coefficientsGr_.capacity()) * sizeof(double)
            + static_cast<std::uint64_t>(coefficientsGb_.capacity()) * sizeof(double)
            + static_cast<std::uint64_t>(mapGr_.total()) * mapGr_.elemSize()
            + static_cast<std::uint64_t>(mapGb_.total()) * mapGb_.elemSize();
    }

    bool load(const json& root, std::string& error)
    {
        if (!readInt(root, "w", width_, error)
            || !readInt(root, "h", height_, error)
            || !readInt(root, "CenterCol", centerColumn_, error)
            || !readInt(root, "CenterRow", centerRow_, error)
            || !readDouble(root, "MeasDis", measurementDistance_, error)
            || !readDouble(root, "CalibDis", calibrationDistance_, error)
            || !readDoubleArray(root, "ColorDiffCoeffs_GR", coefficientsGr_, error)
            || !readDoubleArray(root, "ColorDiffCoeffs_GB", coefficientsGb_, error)) {
            return false;
        }

        std::vector<double> shiftGr;
        std::vector<double> shiftGb;
        if (!readDoubleArray(root, "ColRowCoeffs_GR", shiftGr, error)
            || !readDoubleArray(root, "ColRowCoeffs_GB", shiftGb, error)) {
            return false;
        }
        if (shiftGr.size() != 2 || shiftGb.size() != 2) {
            error = "ColorDiff ColRowCoeffs arrays must contain two values";
            return false;
        }
        shiftGr_ = { shiftGr[0], shiftGr[1] };
        shiftGb_ = { shiftGb[0], shiftGb[1] };

        if (width_ < 3 || height_ < 3
            || width_ > (std::numeric_limits<std::uint16_t>::max)()
            || height_ > (std::numeric_limits<std::uint16_t>::max)()) {
            error = "ColorDiff dimensions must be between 3 and 65535 pixels";
            return false;
        }
        if (coefficientsGr_.empty() || coefficientsGb_.empty()) {
            error = "ColorDiff coefficient arrays must not be empty";
            return false;
        }
        if (measurementDistance_ == 0.0) {
            error = "ColorDiff MeasDis must not be zero";
            return false;
        }

        try {
            buildMaps(
                width_, height_, centerColumn_, centerRow_,
                mapGr_, mapGb_);
        }
        catch (const cv::Exception& exception) {
            error = "Unable to create ColorDiff maps: " + std::string(exception.what());
            return false;
        }
        return true;
    }

    bool apply(
        const ImageView& raw,
        float*,
        const ExecutionOptions& options,
        std::string& error) override
    {
        fallbackScratch_.create(
            static_cast<int>(raw.height),
            static_cast<int>(raw.width),
            imageType(raw));
        ImageView destination = raw;
        destination.data = fallbackScratch_.data;
        destination.dataLength = fallbackScratch_.total() * fallbackScratch_.elemSize();
        return applyOutOfPlace(raw, destination, options, error)
            && copyResultToRaw(fallbackScratch_, raw, error);
    }

    bool applyOutOfPlace(
        const ImageView& raw,
        const ImageView& destination,
        const ExecutionOptions& options,
        std::string& error) override
    {
        if (!validCvDimensions(raw, error)) {
            return false;
        }
        if (!validDistinctOutput(raw, destination, error)) {
            return false;
        }

        try {
            const cv::Mat* selectedGr = nullptr;
            const cv::Mat* selectedGb = nullptr;
            if (!selectMaps(raw, options, selectedGr, selectedGb, error)) {
                return false;
            }
            return selectedGr->type() == CV_8UC1
                ? applyTyped<std::uint8_t>(raw, destination, options, *selectedGr, *selectedGb, error)
                : applyTyped<MapEntry>(raw, destination, options, *selectedGr, *selectedGb, error);
        }
        catch (const cv::Exception& exception) {
            error = "ColorDiff calibration failed: " + std::string(exception.what());
            return false;
        }
    }

private:
    ColorDiffCalibration(const ColorDiffCalibration& source)
        : width_(source.width_)
        , height_(source.height_)
        , centerColumn_(source.centerColumn_)
        , centerRow_(source.centerRow_)
        , measurementDistance_(source.measurementDistance_)
        , calibrationDistance_(source.calibrationDistance_)
        , coefficientsGr_(source.coefficientsGr_)
        , coefficientsGb_(source.coefficientsGb_)
        , shiftGr_(source.shiftGr_)
        , shiftGb_(source.shiftGb_)
        , mapGr_(source.mapGr_)
        , mapGb_(source.mapGb_)
    {
    }

    using MapEntry = cv::Vec<std::uint16_t, 2>;

    template<typename Entry>
    static std::size_t sourcePixel(const Entry& entry, std::size_t pixel, int width)
    {
        if constexpr (std::is_same_v<Entry, std::uint8_t>) {
            const auto dx = static_cast<int>(entry & 15) - 8;
            const auto dy = static_cast<int>(entry >> 4) - 8;
            return static_cast<std::size_t>(static_cast<std::ptrdiff_t>(pixel)
                + static_cast<std::ptrdiff_t>(dy) * width + dx);
        }
        else {
            return static_cast<std::size_t>(entry[1]) * width + entry[0];
        }
    }

    template<typename Entry>
    bool applyTyped(
        const ImageView& raw,
        const ImageView& destination,
        const ExecutionOptions& options,
        const cv::Mat& mapGr,
        const cv::Mat& mapGb,
        std::string& error)
    {
        const bool flipRows = options.rawOutputFlip == 0;

        if (!options.interleavedBgr) {
            if (raw.channels != 3) {
                error = "Planar ColorDiff requires ImageView.channels == 3";
                return false;
            }
            return raw.bitsPerChannel == 8
                ? applyPlanar<std::uint8_t, Entry>(raw, destination, mapGr, mapGb, flipRows)
                : applyPlanar<std::uint16_t, Entry>(raw, destination, mapGr, mapGb, flipRows);
        }

        switch (options.rgbType) {
        case 0:
            if (raw.channels != 3) {
                error = "Combined ColorDiff requires a three-channel BGR image";
                return false;
            }
            if (options.rawOutputFlip == 1 || options.rawOutputFlip == -1) {
                const bool flipBothAxes = options.rawOutputFlip == -1;
                return raw.bitsPerChannel == 8
                    ? applyInterleavedHorizontal<std::uint8_t, Entry>(raw, destination, mapGr, mapGb, flipBothAxes)
                    : applyInterleavedHorizontal<std::uint16_t, Entry>(raw, destination, mapGr, mapGb, flipBothAxes);
            }
            return raw.bitsPerChannel == 8
                ? applyInterleaved<std::uint8_t, Entry>(raw, destination, mapGr, mapGb, flipRows)
                : applyInterleaved<std::uint16_t, Entry>(raw, destination, mapGr, mapGb, flipRows);
        case 1:
        case 2:
        case 3:
            if (raw.channels != 1) {
                error = "Single-channel ColorDiff requires ImageView.channels == 1";
                return false;
            }
            if (options.rgbType == 2) {
                if (!flipRows) {
                    std::memcpy(destination.data, raw.data, imageByteCount(raw));
                    return true;
                }
                const std::size_t rowBytes = imageByteCount(raw) / raw.height;
                cv::parallel_for_(cv::Range(0, raw.height), [&](const cv::Range& range) {
                    for (int row = range.start; row < range.end; ++row) {
                        const auto targetRow = raw.height - 1 - row;
                        std::memcpy(destination.data + targetRow * rowBytes, raw.data + row * rowBytes, rowBytes);
                    }
                });
                return true;
            }
            return raw.bitsPerChannel == 8
                ? applySingle<std::uint8_t, Entry>(
                    raw,
                    destination,
                    options.rgbType == 1 ? mapGr : mapGb, flipRows)
                : applySingle<std::uint16_t, Entry>(
                    raw,
                    destination,
                    options.rgbType == 1 ? mapGr : mapGb, flipRows);
        default:
            error = "ColorDiff rgbType must be 0 (BGR), 1 (R), 2 (G), or 3 (B)";
            return false;
        }
    }


    void buildMaps(
        int width,
        int height,
        int centerColumn,
        int centerRow,
        cv::Mat& destinationGr,
        cv::Mat& destinationGb) const
    {
        cv::Mat compactGr(height, width, CV_8UC1);
        cv::Mat compactGb(height, width, CV_8UC1);
        cv::Mat wideGr;
        cv::Mat wideGb;
        std::once_flag allocateWideMaps;
        std::vector<std::uint8_t> wideRows(height);
        const auto expandPrefix = [&](int row, int columns, MapEntry* red, MapEntry* blue) {
            const auto* compactRed = compactGr.ptr<std::uint8_t>(row);
            const auto* compactBlue = compactGb.ptr<std::uint8_t>(row);
            const auto expand = [row](std::uint8_t packed, int column) {
                return MapEntry(static_cast<std::uint16_t>(column + (packed & 15) - 8),
                    static_cast<std::uint16_t>(row + (packed >> 4) - 8));
            };
            for (int column = 0; column < columns; ++column) {
                red[column] = expand(compactRed[column], column);
                blue[column] = expand(compactBlue[column], column);
            }
        };
        const auto sharedTerms = (std::min)(coefficientsGr_.size(), coefficientsGb_.size());
        cv::parallel_for_(cv::Range(0, height), [&](const cv::Range& range) {
            for (int row = range.start; row < range.end; ++row) {
                auto* compactRed = compactGr.ptr<std::uint8_t>(row);
                auto* compactBlue = compactGb.ptr<std::uint8_t>(row);
                MapEntry* wideRed = nullptr;
                MapEntry* wideBlue = nullptr;
                for (int column = 0; column < width; ++column) {
                    const double deltaColumn = static_cast<double>(column - centerColumn);
                    const double deltaRow = static_cast<double>(row - centerRow);
                    const double radius = std::sqrt(
                        std::pow(deltaColumn, 2.0) + std::pow(deltaRow, 2.0));
                    const double angle = std::atan2(deltaColumn, deltaRow);
                    const double angleSin = std::sin(angle);
                    const double angleCos = std::cos(angle);

                    // Both channels use the same powers. Keep the original
                    // accumulation order in each channel before rounding coordinates.
                    double targetRadiusGr = 0.0;
                    double targetRadiusGb = 0.0;
                    for (std::size_t index = 0; index < sharedTerms; ++index) {
                        const double radiusPower = std::pow(radius, static_cast<int>(index));
                        targetRadiusGr += coefficientsGr_[index] * radiusPower;
                        targetRadiusGb += coefficientsGb_[index] * radiusPower;
                    }

                    const auto computeEntry = [&](double targetRadius, const std::vector<double>& coefficients,
                                                const std::array<double, 2>& columnRowShift) {
                        for (std::size_t index = sharedTerms; index < coefficients.size(); ++index) {
                            targetRadius += coefficients[index]
                                * std::pow(radius, static_cast<int>(index));
                        }
                        targetRadius = targetRadius
                            / measurementDistance_ * calibrationDistance_;

                        const double mappedRow = (radius - targetRadius) * angleCos
                            + centerRow - columnRowShift[1];
                        const double mappedColumn = (radius - targetRadius) * angleSin
                            + centerColumn - columnRowShift[0];
                        int sourceRow = static_cast<int>(mappedRow + 0.5);
                        int sourceColumn = static_cast<int>(mappedColumn + 0.5);
                        sourceRow = (std::max)(1, (std::min)(sourceRow, height - 2));
                        sourceColumn = (std::max)(1, (std::min)(sourceColumn, width - 2));

                        return MapEntry(static_cast<std::uint16_t>(sourceColumn),
                            static_cast<std::uint16_t>(sourceRow));
                    };
                    const MapEntry red = computeEntry(targetRadiusGr, coefficientsGr_, shiftGr_);
                    const MapEntry blue = computeEntry(targetRadiusGb, coefficientsGb_, shiftGb_);
                    if (wideRed == nullptr) {
                        // Each nibble represents a signed offset in [-8, 7], biased by 8.
                        const auto redX = static_cast<unsigned>(red[0] - column + 8);
                        const auto redY = static_cast<unsigned>(red[1] - row + 8);
                        const auto blueX = static_cast<unsigned>(blue[0] - column + 8);
                        const auto blueY = static_cast<unsigned>(blue[1] - row + 8);
                        if ((redX | redY | blueX | blueY) < 16) {
                            compactRed[column] = static_cast<std::uint8_t>(redX | (redY << 4));
                            compactBlue[column] = static_cast<std::uint8_t>(blueX | (blueY << 4));
                            continue;
                        }
                        // Expand the prefix and finish this row with absolute coordinates.
                        std::call_once(allocateWideMaps, [&] {
                            wideGr.create(height, width, CV_16UC2);
                            wideGb.create(height, width, CV_16UC2);
                        });
                        wideRed = wideGr.ptr<MapEntry>(row);
                        wideBlue = wideGb.ptr<MapEntry>(row);
                        expandPrefix(row, column, wideRed, wideBlue);
                        wideRows[row] = 1;
                    }
                    wideRed[column] = red;
                    wideBlue[column] = blue;
                }
            }
        });
        if (wideGr.empty()) {
            destinationGr = std::move(compactGr);
            destinationGb = std::move(compactGb);
            return;
        }

        // One out-of-range offset selects the wide layout for both maps.
        cv::parallel_for_(cv::Range(0, height), [&](const cv::Range& range) {
            for (int row = range.start; row < range.end; ++row) {
                if (wideRows[row] == 0) {
                    expandPrefix(row, width, wideGr.ptr<MapEntry>(row), wideGb.ptr<MapEntry>(row));
                }
            }
        });
        destinationGr = std::move(wideGr);
        destinationGb = std::move(wideGb);
    }

    bool selectMaps(
        const ImageView& raw,
        const ExecutionOptions& options,
        const cv::Mat*& selectedGr,
        const cv::Mat*& selectedGb,
        std::string& error)
    {
        if (!validLegacyRoi(options, width_, height_)) {
            if (raw.width != static_cast<std::uint32_t>(width_)
                || raw.height != static_cast<std::uint32_t>(height_)) {
                error = "RAW dimensions do not match the ColorDiff calibration";
                return false;
            }
            selectedGr = &mapGr_;
            selectedGb = &mapGb_;
            return true;
        }

        const int roiX = static_cast<int>(options.roi[0]);
        const int roiY = static_cast<int>(options.roi[1]);
        const int roiWidth = static_cast<int>(options.roi[2]);
        const int roiHeight = static_cast<int>(options.roi[3]);
        if (roiWidth < 3 || roiHeight < 3) {
            error = "ColorDiff ROI dimensions must be at least 3 by 3";
            return false;
        }
        if (raw.width != static_cast<std::uint32_t>(roiWidth)
            || raw.height != static_cast<std::uint32_t>(roiHeight)) {
            error = "ColorDiff ROI dimensions do not match the RAW buffer";
            return false;
        }

        if (roiX_ != roiX || roiY_ != roiY
            || roiWidth_ != roiWidth || roiHeight_ != roiHeight
            || roiMapGr_.empty() || roiMapGb_.empty()) {
            buildMaps(
                roiWidth, roiHeight, centerColumn_ - roiX, centerRow_ - roiY,
                roiMapGr_, roiMapGb_);
            roiX_ = roiX;
            roiY_ = roiY;
            roiWidth_ = roiWidth;
            roiHeight_ = roiHeight;
        }

        selectedGr = &roiMapGr_;
        selectedGb = &roiMapGb_;
        return true;
    }

    template<typename Sample, typename Entry>
    bool applyInterleaved(
        const ImageView& raw,
        const ImageView& output,
        const cv::Mat& mapGr,
        const cv::Mat& mapGb,
        bool flipRows)
    {
#if defined(_M_X64)
        if constexpr (std::is_same_v<Sample, std::uint16_t> && std::is_same_v<Entry, std::uint8_t>) {
            // Large outputs exceed the useful cache working set. Stream complete
            // blocks without fetching the old destination pixels into the cache.
            if (imageByteCount(raw) >= 32ULL * 1024 * 1024
                && (reinterpret_cast<std::uintptr_t>(output.data) & 1U) == 0) {
                return applyInterleavedStream16(raw, output, mapGr, mapGb, flipRows);
            }
        }
#endif
        const int width = static_cast<int>(raw.width);
        const int height = static_cast<int>(raw.height);
        const auto* source = reinterpret_cast<const Sample*>(raw.data);
        auto* destination = reinterpret_cast<Sample*>(output.data);

        cv::parallel_for_(cv::Range(0, height), [&](const cv::Range& range) {
            for (int row = range.start; row < range.end; ++row) {
                const auto* gr = mapGr.ptr<Entry>(row);
                const auto* gb = mapGb.ptr<Entry>(row);
                const bool borderRow = row == 0 || row == height - 1;
                const int targetRow = flipRows ? height - 1 - row : row;
                for (int column = 0; column < width; ++column) {
                    const std::size_t pixel = static_cast<std::size_t>(row) * width + column;
                    const std::size_t destinationIndex = (static_cast<std::size_t>(targetRow) * width + column) * 3;
                    destination[destinationIndex + 1] = source[pixel * 3 + 1];
                    if (borderRow || column == 0 || column == width - 1) {
                        destination[destinationIndex] = 0;
                        destination[destinationIndex + 2] = 0;
                        continue;
                    }

                    const std::size_t sourceRed =
                        sourcePixel(gr[column], pixel, width) * 3 + 2;
                    const std::size_t sourceBlue =
                        sourcePixel(gb[column], pixel, width) * 3;
                    destination[destinationIndex + 2] = source[sourceRed];
                    destination[destinationIndex] = source[sourceBlue];
                }
            }
        });
        return true;
    }

#if defined(_M_X64)
    bool applyInterleavedStream16(
        const ImageView& raw,
        const ImageView& output,
        const cv::Mat& mapGr,
        const cv::Mat& mapGb,
        bool flipRows)
    {
        constexpr int blockPixels = 32;
        const int width = static_cast<int>(raw.width);
        const int height = static_cast<int>(raw.height);
        const auto* source = reinterpret_cast<const std::uint16_t*>(raw.data);
        auto* destination = reinterpret_cast<std::uint16_t*>(output.data);
        cv::parallel_for_(cv::Range(0, height), [&](const cv::Range& range) {
            constexpr std::uint8_t identity = 0x88;
            const auto evenBlue = _mm_setr_epi16(-1, 0, 0, -1, 0, 0, -1, 0);
            const auto evenGreen = _mm_setr_epi16(0, -1, 0, 0, -1, 0, 0, -1);
            const auto evenRed = _mm_setr_epi16(0, 0, -1, 0, 0, -1, 0, 0);
            const auto isUniform = [](const std::uint8_t* map, std::uint8_t entry) {
                static_assert(blockPixels == 32);
                const auto code = _mm_set1_epi8(static_cast<char>(entry));
                const auto first = _mm_loadu_si128(reinterpret_cast<const __m128i*>(map));
                const auto second = _mm_loadu_si128(reinterpret_cast<const __m128i*>(map + 16));
                return _mm_movemask_epi8(_mm_and_si128(_mm_cmpeq_epi8(first, code), _mm_cmpeq_epi8(second, code))) == 0xFFFF;
            };
            alignas(16) std::uint16_t block[blockPixels * 3];
            for (int row = range.start; row < range.end; ++row) {
                const auto* gr = mapGr.ptr<std::uint8_t>(row);
                const auto* gb = mapGb.ptr<std::uint8_t>(row);
                const auto rowBase = static_cast<std::size_t>(row) * width;
                const int targetRow = flipRows ? height - 1 - row : row;
                auto* outputRow = destination + static_cast<std::size_t>(targetRow) * width * 3;
                const auto writePixel = [&](int column, std::uint16_t* pixel) {
                    const auto index = rowBase + column;
                    pixel[1] = source[index * 3 + 1];
                    if (row == 0 || row == height - 1 || column == 0 || column == width - 1) {
                        pixel[0] = pixel[2] = 0;
                        return;
                    }
                    pixel[0] = source[sourcePixel(gb[column], index, width) * 3];
                    pixel[2] = source[sourcePixel(gr[column], index, width) * 3 + 2];
                };
                int column = 0;
                // Keep streamed cache lines separate from scalar heads/tails,
                // including cache lines shared by two adjacent image rows.
                for (; column < width && (reinterpret_cast<std::uintptr_t>(outputRow + column * 3) & 63U) != 0; ++column)
                    writePixel(column, outputRow + column * 3);
                for (; column + blockPixels <= width; column += blockPixels) {
                    // With a constant map entry, the corrected channel comes
                    // from a contiguous run. Keep the sensor borders scalar.
                    if (!flipRows && row != 0 && row != height - 1 && column > 0 && column + blockPixels < width) {
                        const auto red = gr[column];
                        const auto blue = gb[column];
                        if ((red == identity || blue == identity) && isUniform(gb + column, blue)
                            && isUniform(gr + column, red)) {
                            const auto pixel = rowBase + column;
                            const auto* original = source + pixel * 3;
                            auto* out = outputRow + column * 3;
                            if (red == identity && blue == identity) {
                                for (int index = 0; index < blockPixels * 3; index += 8) {
                                    _mm_stream_si128(reinterpret_cast<__m128i*>(out + index),
                                        _mm_loadu_si128(reinterpret_cast<const __m128i*>(original + index)));
                                }
                            }
                            else {
                                const bool moveRed = red != identity;
                                const auto* shifted = source + sourcePixel(moveRed ? red : blue, pixel, width) * 3;
                                const auto firstMask = moveRed ? evenRed : evenBlue;
                                const auto secondMask = moveRed ? evenBlue : evenGreen;
                                const auto thirdMask = moveRed ? evenGreen : evenRed;
                                const auto writeBlend = [&](int index, __m128i mask) {
                                    const auto base = _mm_loadu_si128(reinterpret_cast<const __m128i*>(original + index));
                                    const auto moved = _mm_loadu_si128(reinterpret_cast<const __m128i*>(shifted + index));
                                    const auto corrected = _mm_or_si128(_mm_andnot_si128(mask, base), _mm_and_si128(mask, moved));
                                    _mm_stream_si128(reinterpret_cast<__m128i*>(out + index), corrected);
                                };
                                for (int index = 0; index < blockPixels * 3; index += 24) {
                                    writeBlend(index, firstMask);
                                    writeBlend(index + 8, secondMask);
                                    writeBlend(index + 16, thirdMask);
                                }
                            }
                            continue;
                        }
                    }
                    for (int index = 0; index < blockPixels; ++index)
                        writePixel(column + index, block + index * 3);
                    for (int index = 0; index < blockPixels * 3; index += 8) {
                        _mm_stream_si128(reinterpret_cast<__m128i*>(outputRow + column * 3 + index),
                            _mm_load_si128(reinterpret_cast<const __m128i*>(block + index)));
                    }
                }
                for (; column < width; ++column) writePixel(column, outputRow + column * 3);
            }
            // Fence on every worker before it publishes completion to parallel_for_.
            _mm_sfence();
        });
        return true;
    }
#endif

    template<typename Sample, typename Entry>
    bool applyInterleavedHorizontal(
        const ImageView& raw,
        const ImageView& output,
        const cv::Mat& mapGr,
        const cv::Mat& mapGb,
        bool flipRows)
    {
        const int width = static_cast<int>(raw.width);
        const int height = static_cast<int>(raw.height);
        const auto* source = reinterpret_cast<const Sample*>(raw.data);
        auto* destination = reinterpret_cast<Sample*>(output.data);
#if defined(_M_X64)
        constexpr bool canStream = std::is_same_v<Sample, std::uint16_t> && std::is_same_v<Entry, std::uint8_t>;
        [[maybe_unused]] const bool streamOutput = canStream && imageByteCount(raw) >= 32ULL * 1024 * 1024;
#endif
        cv::parallel_for_(cv::Range(0, height), [&](const cv::Range& range) {
            for (int row = range.start; row < range.end; ++row) {
                const auto* gr = mapGr.ptr<Entry>(row);
                const auto* gb = mapGb.ptr<Entry>(row);
                const auto rowBase = static_cast<std::size_t>(row) * width;
                const bool borderRow = row == 0 || row == height - 1;
                const int targetRow = flipRows ? height - 1 - row : row;
                auto* outputRow = destination + static_cast<std::size_t>(targetRow) * width * 3;
                const auto writePixel = [&](int targetColumn, Sample* pixel) {
                    // Resolve the map and border in sensor coordinates, then write the mirrored destination.
                    const int column = width - 1 - targetColumn;
                    const auto index = rowBase + column;
                    pixel[1] = source[index * 3 + 1];
                    if (borderRow || column == 0 || column == width - 1) {
                        pixel[0] = pixel[2] = 0;
                        return;
                    }
                    pixel[0] = source[sourcePixel(gb[column], index, width) * 3];
                    pixel[2] = source[sourcePixel(gr[column], index, width) * 3 + 2];
                };
                int column = 0;
#if defined(_M_X64)
                if constexpr (canStream) {
                    if (streamOutput) {
                        constexpr int blockPixels = 32;
                        alignas(16) std::uint16_t block[blockPixels * 3];
                        for (; column < width && (reinterpret_cast<std::uintptr_t>(outputRow + column * 3) & 63U) != 0; ++column)
                            writePixel(column, outputRow + column * 3);
                        for (; column + blockPixels <= width; column += blockPixels) {
                            // The first and last destination columns mirror the
                            // sensor edges. Interior blocks use only mapped taps.
                            if (!borderRow && column > 0 && column + blockPixels < width) {
                                const int firstSensorColumn = width - 1 - column;
                                for (int index = 0; index < blockPixels; ++index) {
                                    const int sensorColumn = firstSensorColumn - index;
                                    const auto sensorPixel = rowBase + sensorColumn;
                                    auto* pixel = block + index * 3;
                                    pixel[0] = source[sourcePixel(gb[sensorColumn], sensorPixel, width) * 3];
                                    pixel[1] = source[sensorPixel * 3 + 1];
                                    pixel[2] = source[sourcePixel(gr[sensorColumn], sensorPixel, width) * 3 + 2];
                                }
                            }
                            else {
                                for (int index = 0; index < blockPixels; ++index)
                                    writePixel(column + index, block + index * 3);
                            }
                            for (int index = 0; index < blockPixels * 3; index += 8) {
                                _mm_stream_si128(reinterpret_cast<__m128i*>(outputRow + column * 3 + index),
                                    _mm_load_si128(reinterpret_cast<const __m128i*>(block + index)));
                            }
                        }
                    }
                }
#endif
                for (; column < width; ++column) writePixel(column, outputRow + column * 3);
            }
#if defined(_M_X64)
            if constexpr (canStream) {
                if (streamOutput) _mm_sfence();
            }
#endif
        });
        return true;
    }

    template<typename Sample, typename Entry>
    bool applySingle(
        const ImageView& raw,
        const ImageView& output,
        const cv::Mat& map,
        bool flipRows)
    {
        const int width = static_cast<int>(raw.width);
        const int height = static_cast<int>(raw.height);
        const auto* source = reinterpret_cast<const Sample*>(raw.data);
        auto* destination = reinterpret_cast<Sample*>(output.data);

        cv::parallel_for_(cv::Range(0, height), [&](const cv::Range& range) {
            for (int row = range.start; row < range.end; ++row) {
                const auto* mapRow = map.ptr<Entry>(row);
                const bool borderRow = row == 0 || row == height - 1;
                const int targetRow = flipRows ? height - 1 - row : row;
                for (int column = 0; column < width; ++column) {
                    const std::size_t destinationIndex =
                        static_cast<std::size_t>(targetRow) * width + column;
                    if (borderRow || column == 0 || column == width - 1) {
                        destination[destinationIndex] = 0;
                    }
                    else {
                        const std::size_t sourceIndex =
                            sourcePixel(mapRow[column], static_cast<std::size_t>(row) * width + column, width);
                        destination[destinationIndex] = source[sourceIndex];
                    }
                }
            }
        });
        return true;
    }

    template<typename Sample, typename Entry>
    bool applyPlanar(
        const ImageView& raw,
        const ImageView& output,
        const cv::Mat& mapGr,
        const cv::Mat& mapGb,
        bool flipRows)
    {
        const int width = static_cast<int>(raw.width);
        const int height = static_cast<int>(raw.height);
        const std::size_t pixels = static_cast<std::size_t>(width) * height;
        const auto* source = reinterpret_cast<const Sample*>(raw.data);
        auto* destination = reinterpret_cast<Sample*>(output.data);

        cv::parallel_for_(cv::Range(0, height), [&](const cv::Range& range) {
            for (int row = range.start; row < range.end; ++row) {
                const auto* gr = mapGr.ptr<Entry>(row);
                const auto* gb = mapGb.ptr<Entry>(row);
                const bool borderRow = row == 0 || row == height - 1;
                const int targetRow = flipRows ? height - 1 - row : row;
                for (int column = 0; column < width; ++column) {
                    const std::size_t pixel = static_cast<std::size_t>(row) * width + column;
                    const std::size_t destinationPixel = static_cast<std::size_t>(targetRow) * width + column;
                    destination[pixels + destinationPixel] = source[pixels + pixel];
                    if (borderRow || column == 0 || column == width - 1) {
                        destination[destinationPixel] = 0;
                        destination[pixels * 2 + destinationPixel] = 0;
                        continue;
                    }

                    const std::size_t sourceRed =
                        sourcePixel(gr[column], pixel, width);
                    const std::size_t sourceBlue =
                        sourcePixel(gb[column], pixel, width);
                    destination[destinationPixel] = source[sourceRed];
                    destination[pixels * 2 + destinationPixel] = source[pixels * 2 + sourceBlue];
                }
            }
        });
        return true;
    }

    int width_ = 0;
    int height_ = 0;
    int centerColumn_ = 0;
    int centerRow_ = 0;
    double measurementDistance_ = 0.0;
    double calibrationDistance_ = 0.0;
    std::vector<double> coefficientsGr_;
    std::vector<double> coefficientsGb_;
    std::array<double, 2> shiftGr_{};
    std::array<double, 2> shiftGb_{};

    cv::Mat mapGr_;
    cv::Mat mapGb_;
    cv::Mat roiMapGr_;
    cv::Mat roiMapGb_;
    int roiX_ = -1;
    int roiY_ = -1;
    int roiWidth_ = -1;
    int roiHeight_ = -1;

    cv::Mat fallbackScratch_;
};

class AngleShiftCalibration final : public CalibrationItem {
public:
    AngleShiftCalibration() = default;

    [[nodiscard]] CalibrationType type() const noexcept override
    {
        return CalibrationType::AngleShift;
    }

    [[nodiscard]] bool requiresDistinctOutput() const noexcept override
    {
        return true;
    }

    [[nodiscard]] std::unique_ptr<CalibrationItem> cloneForContext() const override
    {
        return std::unique_ptr<CalibrationItem>(new AngleShiftCalibration(*this));
    }

    bool load(const json& root, std::string& error)
    {
        if (!readDouble(root, "optical_center_x", opticalCenterX_, error)
            || !readDouble(root, "optical_center_y", opticalCenterY_, error)
            || !readDouble(root, "interpolate_ratio", interpolationRatio_, error)
            || !readInt(root, "coefficient_order", coefficientOrder_, error)
            || !readInt(root, "target_row", targetRows_, error)
            || !readInt(root, "target_col", targetColumns_, error)
            || !readDoubleArray(root, "coeff_r", coefficientsRed_, error)
            || !readDoubleArray(root, "coeff_g", coefficientsGreen_, error)
            || !readDoubleArray(root, "coeff_b", coefficientsBlue_, error)
            || !readDoubleArray(root, "rowColShift", rowColumnShift_, error)) {
            return false;
        }

        if (coefficientOrder_ < 0) {
            error = "AngleShift coefficient_order must not be negative";
            return false;
        }
        const std::size_t expectedCount = static_cast<std::size_t>(coefficientOrder_) + 1;
        if (coefficientsRed_.size() != expectedCount
            || coefficientsGreen_.size() != expectedCount
            || coefficientsBlue_.size() != expectedCount) {
            error = "AngleShift coefficient array length does not match coefficient_order";
            return false;
        }
        if (rowColumnShift_.size() != 2) {
            error = "AngleShift rowColShift must contain two values";
            return false;
        }
        if (interpolationRatio_ <= 0.0) {
            error = "AngleShift interpolate_ratio must be positive";
            return false;
        }
        if (targetRows_ <= 0 || targetColumns_ <= 0) {
            error = "AngleShift target dimensions must be positive";
            return false;
        }
        return true;
    }

    bool apply(
        const ImageView& raw,
        float*,
        const ExecutionOptions& options,
        std::string& error) override
    {
        fallbackScratch_.create(
            static_cast<int>(raw.height),
            static_cast<int>(raw.width),
            imageType(raw));
        ImageView destination = raw;
        destination.data = fallbackScratch_.data;
        destination.dataLength = fallbackScratch_.total() * fallbackScratch_.elemSize();
        return applyOutOfPlace(raw, destination, options, error)
            && copyResultToRaw(fallbackScratch_, raw, error);
    }

    bool applyOutOfPlace(
        const ImageView& raw,
        const ImageView& destination,
        const ExecutionOptions&,
        std::string& error) override
    {
        if (raw.channels != 3) {
            error = "AngleShift requires a three-channel image";
            return false;
        }
        if (!validCvDimensions(raw, error)) {
            return false;
        }
        if (!validDistinctOutput(raw, destination, error)) {
            return false;
        }
        if (raw.width != static_cast<std::uint32_t>(targetColumns_)
            || raw.height != static_cast<std::uint32_t>(targetRows_)) {
            error = "AngleShift target dimensions do not match the RAW buffer";
            return false;
        }

        try {
            // Integer enlargement keeps the cubic phase unchanged when the
            // source origin moves by whole pixels. The vendored IPP 16-bit
            // resize has matching rounding for these scales; other backends,
            // bit depths and fractional scales retain the full-frame path.
            const bool allowTiles = raw.bitsPerChannel == 16 && cv::ipp::useIPP()
                && interpolationRatio_ >= 2.0 && interpolationRatio_ <= 4.0
                && interpolationRatio_ == std::floor(interpolationRatio_);
            if (!ensureMaps(
                    static_cast<int>(raw.width),
                    static_cast<int>(raw.height),
                    allowTiles,
                    error)) {
                return false;
            }

            cv::Mat source(
                static_cast<int>(raw.height),
                static_cast<int>(raw.width),
                imageType(raw),
                raw.data);

            if (!tiles_.empty()) {
                renderTiles(source, destination);
                return true;
            }

            // OpenCV copies the entire input when the rounded size is unchanged.
            // The gather can read those pixels directly from its distinct source.
            const cv::Mat* samples = &source;
            if (source.cols != resizedColumns_ || source.rows != resizedRows_) {
                cv::resize(source, resized_, cv::Size(resizedColumns_, resizedRows_), 0.0, 0.0, cv::INTER_CUBIC);
                samples = &resized_;
            }
            if (raw.bitsPerChannel == 8) {
                render<std::uint8_t>(*samples, destination);
            }
            else {
                render<std::uint16_t>(*samples, destination);
            }
            return true;
        }
        catch (const cv::Exception& exception) {
            error = "AngleShift calibration failed: " + std::string(exception.what());
            return false;
        }
    }

private:
    AngleShiftCalibration(const AngleShiftCalibration& source)
        : opticalCenterX_(source.opticalCenterX_)
        , opticalCenterY_(source.opticalCenterY_)
        , interpolationRatio_(source.interpolationRatio_)
        , coefficientOrder_(source.coefficientOrder_)
        , targetRows_(source.targetRows_)
        , targetColumns_(source.targetColumns_)
        , coefficientsRed_(source.coefficientsRed_)
        , coefficientsGreen_(source.coefficientsGreen_)
        , coefficientsBlue_(source.coefficientsBlue_)
        , rowColumnShift_(source.rowColumnShift_)
    {
    }

    using IndexEntry = cv::Vec<std::int32_t, 3>;

    struct ResizeTile {
        cv::Rect source;
        cv::Rect output;
    };

    bool ensureMaps(int inputColumns, int inputRows, bool allowTiles, std::string& error)
    {
        if (inputColumns_ == inputColumns && inputRows_ == inputRows
            && mapsAllowTiles_ == allowTiles
            && !sourceIndices_.empty()) {
            return true;
        }

        const double scaledColumns = inputColumns * interpolationRatio_;
        const double scaledRows = inputRows * interpolationRatio_;
        if (!std::isfinite(scaledColumns) || !std::isfinite(scaledRows)
            || scaledColumns < 1.0 || scaledRows < 1.0
            || scaledColumns > (std::numeric_limits<std::uint16_t>::max)()
            || scaledRows > (std::numeric_limits<std::uint16_t>::max)()) {
            error = "AngleShift resized dimensions exceed the legacy ushort range";
            return false;
        }

        // The old implementation explicitly casts these sizes to ushort.
        resizedColumns_ = static_cast<std::uint16_t>(scaledColumns);
        resizedRows_ = static_cast<std::uint16_t>(scaledRows);
        const std::uint64_t resizedPixels =
            static_cast<std::uint64_t>(resizedColumns_) * resizedRows_;
        if (resizedPixels > static_cast<std::uint64_t>((std::numeric_limits<std::int32_t>::max)())) {
            error = "AngleShift resized image is too large for cached source indices";
            return false;
        }

        const double opticalColumn = opticalCenterX_ * interpolationRatio_;
        const double opticalRow = opticalCenterY_ * interpolationRatio_;
        inputColumns_ = inputRows_ = -1;
        tiles_.clear();
        tileScratch_.clear();
        resized_.release();
        sourceIndices_.create(targetRows_, targetColumns_, CV_32SC3);

        cv::parallel_for_(cv::Range(0, targetRows_), [&](const cv::Range& range) {
            for (int row = range.start; row < range.end; ++row) {
                auto* indices = sourceIndices_.ptr<IndexEntry>(row);
                const double deltaRow = row * interpolationRatio_ - opticalRow;
                for (int column = 0; column < targetColumns_; ++column) {
                    const double deltaColumn = column * interpolationRatio_ - opticalColumn;
                    const double radius = std::sqrt(
                        deltaRow * deltaRow + deltaColumn * deltaColumn);
                    const double denominator = radius + 1.0e-12;
                    const double angleCos = deltaColumn / denominator;
                    const double angleSin = deltaRow / denominator;

                    indices[column][0] = correctedIndex(
                        coefficientsBlue_, radius, angleCos, angleSin,
                        opticalColumn, opticalRow);
                    indices[column][1] = correctedIndex(
                        coefficientsGreen_, radius, angleCos, angleSin,
                        opticalColumn, opticalRow);
                    indices[column][2] = correctedIndex(
                        coefficientsRed_, radius, angleCos, angleSin,
                        opticalColumn, opticalRow);
                }
            }
        });

        if (allowTiles) {
            prepareTiles(inputColumns, inputRows);
        }
        inputColumns_ = inputColumns;
        inputRows_ = inputRows;
        mapsAllowTiles_ = allowTiles;
        return true;
    }

    void prepareTiles(int inputColumns, int inputRows)
    {
        constexpr int tileSize = 128;
        const int scale = static_cast<int>(interpolationRatio_);
        const int columns = (targetColumns_ + tileSize - 1) / tileSize;
        const int rows = (targetRows_ + tileSize - 1) / tileSize;
        std::vector<ResizeTile> tiles(static_cast<std::size_t>(columns) * rows);
        cv::parallel_for_(cv::Range(0, static_cast<int>(tiles.size())), [&](const cv::Range& range) {
            for (int index = range.start; index < range.end; ++index) {
                auto& tile = tiles[index];
                const int x = index % columns * tileSize;
                const int y = index / columns * tileSize;
                tile.output = cv::Rect(x, y, (std::min)(tileSize, targetColumns_ - x), (std::min)(tileSize, targetRows_ - y));
                int left = resizedColumns_, top = resizedRows_, right = -1, bottom = -1;
                for (int row = y; row < y + tile.output.height; ++row) {
                    const auto* indices = sourceIndices_.ptr<IndexEntry>(row);
                    for (int column = x; column < x + tile.output.width; ++column) {
                        for (int channel = 0; channel < 3; ++channel) {
                            const int pixel = indices[column][channel];
                            if (pixel < 0) continue;
                            const int sourceColumn = pixel % resizedColumns_;
                            const int sourceRow = pixel / resizedColumns_;
                            left = (std::min)(left, sourceColumn);
                            right = (std::max)(right, sourceColumn);
                            top = (std::min)(top, sourceRow);
                            bottom = (std::max)(bottom, sourceRow);
                        }
                    }
                }
                if (right >= 0) {
                    // Include the entire four-tap cubic footprint. Only true
                    // image edges may use OpenCV's replicated border pixels.
                    const int sourceX = (std::max)(0, left / scale - 2);
                    const int sourceY = (std::max)(0, top / scale - 2);
                    tile.source = cv::Rect(sourceX, sourceY,
                        (std::min)(inputColumns, right / scale + 3) - sourceX,
                        (std::min)(inputRows, bottom / scale + 3) - sourceY);
                }
            }
        });

        cv::Size scratchSize;
        std::uint64_t totalPixels = 0;
        for (const auto& tile : tiles) {
            scratchSize.width = (std::max)(scratchSize.width, tile.source.width * scale);
            scratchSize.height = (std::max)(scratchSize.height, tile.source.height * scale);
            totalPixels += static_cast<std::uint64_t>(tile.source.width) * tile.source.height * scale * scale;
        }
        // Extreme/nonlocal maps can make a tile span nearly the whole input.
        // Keep the original path when tiling would increase work or require
        // large per-worker intermediates. Do not mutate global indices yet.
        const auto scratchPixels = static_cast<std::uint64_t>(scratchSize.width) * scratchSize.height;
        if (scratchPixels > 1024 * 1024
            || totalPixels >= static_cast<std::uint64_t>(resizedColumns_) * resizedRows_) {
            return;
        }

        cv::parallel_for_(cv::Range(0, static_cast<int>(tiles.size())), [&](const cv::Range& range) {
            for (int index = range.start; index < range.end; ++index) {
                const auto& tile = tiles[index];
                for (int row = tile.output.y; row < tile.output.y + tile.output.height; ++row) {
                    auto* indices = sourceIndices_.ptr<IndexEntry>(row);
                    for (int column = tile.output.x; column < tile.output.x + tile.output.width; ++column) {
                        for (int channel = 0; channel < 3; ++channel) {
                            int& pixel = indices[column][channel];
                            if (pixel >= 0) {
                                pixel = (pixel / resizedColumns_ - tile.source.y * scale) * scratchSize.width
                                    + pixel % resizedColumns_ - tile.source.x * scale;
                            }
                        }
                    }
                }
            }
        });
        tileScratchSize_ = scratchSize;
        tiles_ = std::move(tiles);
    }

    void renderTiles(const cv::Mat& source, const ImageView& output)
    {
        cv::Mat destination(targetRows_, targetColumns_, source.type(), output.data);
        if (tileScratchSize_.empty()) {
            destination.setTo(0);
            return;
        }
        constexpr std::size_t scratchBudgetBytes = 64 * 1024 * 1024;
        const std::size_t workerBytes = static_cast<std::size_t>(tileScratchSize_.area()) * source.elemSize();
        const int workers = (std::min)({ (std::max)(1, cv::getNumThreads()),
            static_cast<int>(tiles_.size()), static_cast<int>(scratchBudgetBytes / workerBytes) });
        tileScratch_.resize(workers);
        const int scale = static_cast<int>(interpolationRatio_);
        cv::parallel_for_(cv::Range(0, workers), [&](const cv::Range& range) {
            for (int worker = range.start; worker < range.end; ++worker) {
                auto& buffer = tileScratch_[worker];
                buffer.create(tileScratchSize_, source.type());
                const auto* samples = buffer.ptr<std::uint16_t>();
                for (std::size_t index = worker; index < tiles_.size(); index += workers) {
                    const auto& tile = tiles_[index];
                    if (tile.source.empty()) {
                        destination(tile.output).setTo(0);
                        continue;
                    }
                    cv::Mat resized = buffer(cv::Rect(0, 0, tile.source.width * scale, tile.source.height * scale));
                    cv::resize(source(tile.source), resized, resized.size(), 0.0, 0.0, cv::INTER_CUBIC);
                    for (int row = tile.output.y; row < tile.output.y + tile.output.height; ++row) {
                        const auto* indices = sourceIndices_.ptr<IndexEntry>(row);
                        auto* values = destination.ptr<std::uint16_t>(row);
                        for (int column = tile.output.x; column < tile.output.x + tile.output.width; ++column) {
                            for (int channel = 0; channel < 3; ++channel) {
                                const int pixel = indices[column][channel];
                                values[column * 3 + channel] = pixel < 0 ? 0
                                    : samples[static_cast<std::size_t>(pixel) * 3 + channel];
                            }
                        }
                    }
                }
            }
        });
    }

    std::int32_t correctedIndex(
        const std::vector<double>& coefficients,
        double radius,
        double angleCos,
        double angleSin,
        double opticalColumn,
        double opticalRow) const
    {
        double correctedRadius = 0.0;
        double radiusPower = 1.0;
        for (const double coefficient : coefficients) {
            correctedRadius += coefficient * radiusPower;
            radiusPower *= radius;
        }

        const double sourceColumnValue = (radius - correctedRadius) * angleCos
            + opticalColumn - rowColumnShift_[1];
        const double sourceRowValue = (radius - correctedRadius) * angleSin
            + opticalRow - rowColumnShift_[0];
        const int sourceColumn = static_cast<int>(sourceColumnValue);
        const int sourceRow = static_cast<int>(sourceRowValue);

        if (sourceColumn < 0 || sourceColumn >= resizedColumns_
            || sourceRow < 0 || sourceRow >= resizedRows_
            || radius > static_cast<double>(resizedRows_) / 2.0
            || radius > static_cast<double>(resizedColumns_) / 2.0) {
            return -1;
        }
        return sourceRow * resizedColumns_ + sourceColumn;
    }

    template<typename Sample>
    void render(const cv::Mat& samples, const ImageView& output)
    {
        const auto* source = samples.ptr<Sample>();
        auto* destination = reinterpret_cast<Sample*>(output.data);
        cv::parallel_for_(cv::Range(0, targetRows_), [&](const cv::Range& range) {
            for (int row = range.start; row < range.end; ++row) {
                const auto* indices = sourceIndices_.ptr<IndexEntry>(row);
                for (int column = 0; column < targetColumns_; ++column) {
                    const std::size_t outputPixel =
                        static_cast<std::size_t>(row) * targetColumns_ + column;
                    for (int channel = 0; channel < 3; ++channel) {
                        const auto sourcePixel = indices[column][channel];
                        destination[outputPixel * 3 + channel] = sourcePixel < 0
                            ? static_cast<Sample>(0)
                            : source[static_cast<std::size_t>(sourcePixel) * 3 + channel];
                    }
                }
            }
        });
    }

    double opticalCenterX_ = 0.0;
    double opticalCenterY_ = 0.0;
    double interpolationRatio_ = 0.0;
    int coefficientOrder_ = 0;
    int targetRows_ = 0;
    int targetColumns_ = 0;
    std::vector<double> coefficientsRed_;
    std::vector<double> coefficientsGreen_;
    std::vector<double> coefficientsBlue_;
    std::vector<double> rowColumnShift_;

    int inputColumns_ = -1;
    int inputRows_ = -1;
    int resizedColumns_ = 0;
    int resizedRows_ = 0;
    bool mapsAllowTiles_ = false;
    std::vector<ResizeTile> tiles_;
    cv::Size tileScratchSize_;
    std::vector<cv::Mat> tileScratch_;
    cv::Mat sourceIndices_;
    cv::Mat resized_;
    cv::Mat fallbackScratch_;
};

} // namespace

std::unique_ptr<CalibrationItem> loadGeometricCalibration(
    CalibrationType type,
    const std::filesystem::path& file,
    std::string& error)
{
    if (type != CalibrationType::Distortion
        && type != CalibrationType::ColorShift
        && type != CalibrationType::ColorDiff
        && type != CalibrationType::AngleShift) {
        return nullptr;
    }

    error.clear();
    json root;
    if (!readJson(file, root, error)) {
        return nullptr;
    }

    try {
        switch (type) {
        case CalibrationType::Distortion: {
            auto item = std::make_unique<DistortionCalibration>();
            if (!item->load(root, error)) {
                return nullptr;
            }
            return item;
        }
        case CalibrationType::ColorShift: {
            auto item = std::make_unique<ColorShiftCalibration>();
            if (!item->load(root, error)) {
                return nullptr;
            }
            return item;
        }
        case CalibrationType::ColorDiff: {
            auto item = std::make_unique<ColorDiffCalibration>();
            if (!item->load(root, error)) {
                return nullptr;
            }
            return item;
        }
        case CalibrationType::AngleShift: {
            auto item = std::make_unique<AngleShiftCalibration>();
            if (!item->load(root, error)) {
                return nullptr;
            }
            return item;
        }
        default:
            break;
        }
    }
    catch (const json::exception& exception) {
        error = "Invalid calibration JSON value: " + std::string(exception.what());
        return nullptr;
    }
    catch (const std::exception& exception) {
        error = "Unable to load geometric calibration: " + std::string(exception.what());
        return nullptr;
    }

    error = "Unsupported geometric calibration type";
    return nullptr;
}

} // namespace cvcore::calibration
