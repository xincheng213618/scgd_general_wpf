#include "calibration_core.h"

#include <nlohmann/json.hpp>
#include <opencv2/core.hpp>

#include <algorithm>
#include <array>
#include <cmath>
#include <cstddef>
#include <cstdint>
#include <fstream>
#include <limits>
#include <memory>
#include <optional>
#include <stdexcept>
#include <string>
#include <type_traits>
#include <utility>

#if defined(_M_X64) && !defined(_M_ARM64EC)
#include <immintrin.h>
#endif

#ifdef _MSC_VER
#pragma float_control(precise, on, push)
#pragma fp_contract(off)
#endif

#include "calibration_color_eval.h"

namespace cvcore::calibration {
namespace {

using Json = nlohmann::json;

const char* calibrationName(CalibrationType type) noexcept
{
    switch (type) {
    case CalibrationType::Luminance:
        return "Luminance";
    case CalibrationType::LumOneColor:
        return "LumOneColor";
    case CalibrationType::LumFourColor:
        return "LumFourColor";
    case CalibrationType::LumMultiColor:
        return "LumMultiColor";
    default:
        return "Color";
    }
}

Json readJson(const std::filesystem::path& file)
{
    std::ifstream input(file, std::ios::binary);
    if (!input) {
        throw std::runtime_error("Unable to open calibration file");
    }

    Json root = Json::parse(input, nullptr, true, true);
    if (!root.is_object()) {
        throw std::runtime_error("Calibration JSON root must be an object");
    }
    return root;
}

// JsonCpp's legacy asDouble/asFloat behavior maps absent and null members to
// zero. Preserve that for old files while rejecting types that the old reader
// could not convert either.
double legacyDouble(const Json& root, const char* name)
{
    const auto value = root.find(name);
    if (value == root.end() || value->is_null()) {
        return 0.0;
    }
    if (value->is_boolean()) {
        return value->get<bool>() ? 1.0 : 0.0;
    }
    if (!value->is_number()) {
        throw std::runtime_error(std::string("Calibration member '") + name + "' must be numeric");
    }
    return value->get<double>();
}

double legacyFloatAsDouble(const Json& value, const char* name)
{
    if (value.is_null()) {
        return 0.0;
    }
    if (value.is_boolean()) {
        return value.get<bool>() ? 1.0 : 0.0;
    }
    if (!value.is_number()) {
        throw std::runtime_error(std::string("Calibration member '") + name + "' must be numeric");
    }

    // LumMultiColor historically calls JsonCpp::asFloat before assigning to a
    // double array. The float round-trip is observable in the final CIE bits.
    return static_cast<double>(static_cast<float>(value.get<double>()));
}

std::optional<std::int32_t> legacyBitsPerChannel(const Json& root)
{
    const auto value = root.find("bpp");
    if (value == root.end() || !value->is_number_integer()) {
        return std::nullopt;
    }

    if (value->is_number_unsigned()) {
        const auto number = value->get<std::uint64_t>();
        if (number > static_cast<std::uint64_t>((std::numeric_limits<std::int32_t>::max)())) {
            return std::nullopt;
        }
        return static_cast<std::int32_t>(number);
    }

    const auto number = value->get<std::int64_t>();
    if (number < (std::numeric_limits<std::int32_t>::min)()
        || number > (std::numeric_limits<std::int32_t>::max)()) {
        return std::nullopt;
    }
    return static_cast<std::int32_t>(number);
}

void readLegacyLumFields(const Json& root, std::array<double, 10>& values)
{
    values = {
        legacyDouble(root, "Texp_x"),
        legacyDouble(root, "Texp_y"),
        legacyDouble(root, "Texp_z"),
        legacyDouble(root, "Gain_x"),
        legacyDouble(root, "Gain_y"),
        legacyDouble(root, "Gain_z"),
        legacyDouble(root, "a"),
        legacyDouble(root, "b"),
        legacyDouble(root, "c"),
        legacyDouble(root, "d")
    };
}

bool validateColorImage(
    CalibrationType type,
    const std::optional<std::int32_t>& configuredBpp,
    const ImageView& raw,
    std::string& error)
{
    const std::uint32_t expectedChannels = type == CalibrationType::Luminance ? 1 : 3;
    if (raw.channels != expectedChannels) {
        error = std::string(calibrationName(type)) + " requires "
            + std::to_string(expectedChannels) + " source channel(s)";
        return false;
    }
    if (configuredBpp.has_value()
        && raw.bitsPerChannel != static_cast<std::uint32_t>(*configuredBpp)) {
        error = std::string(calibrationName(type)) + " calibration expects "
            + std::to_string(*configuredBpp) + "-bit RAW data";
        return false;
    }
    if (raw.height > static_cast<std::uint32_t>((std::numeric_limits<int>::max)())) {
        error = "RAW height exceeds OpenCV's parallel range";
        return false;
    }
    return true;
}

template <typename Function>
void parallelRows(std::uint32_t height, Function&& function)
{
    cv::parallel_for_(cv::Range(0, static_cast<int>(height)),
        [&function](const cv::Range& range) {
            for (int row = range.start; row < range.end; ++row) {
                function(static_cast<std::uint32_t>(row));
            }
        });
}

template <typename Source>
void transformInterleavedPlane(
    const Source* source,
    float* destination,
    std::size_t rowOffset,
    std::size_t width,
    double redCoefficient,
    double greenCoefficient,
    double blueCoefficient)
{
    const Source* current = source + rowOffset * 3;
    const Source* const end = current + width * 3;
    float* output = destination + rowOffset;
    for (; current < end; current += 3) {
        *output++ = color::matrixPlane(redCoefficient, greenCoefficient, blueCoefficient,
            current[2], current[1], current[0]);
    }
}

template <typename Source>
void transformPlanarPlane(
    const Source* red,
    const Source* green,
    const Source* blue,
    float* destination,
    std::size_t rowOffset,
    std::size_t width,
    double redCoefficient,
    double greenCoefficient,
    double blueCoefficient)
{
    const std::size_t end = rowOffset + width;
    for (std::size_t index = rowOffset; index < end; ++index) {
        destination[index] = color::matrixPlane(redCoefficient, greenCoefficient, blueCoefficient,
            red[index], green[index], blue[index]);
    }
}

class ColorCalibrationItem : public CalibrationItem {
public:
    ColorCalibrationItem(CalibrationType type, std::optional<std::int32_t> configuredBpp)
        : type_(type), configuredBpp_(configuredBpp)
    {
    }

    [[nodiscard]] CalibrationType type() const noexcept override { return type_; }
    [[nodiscard]] bool isColorTransform() const noexcept override { return true; }
    [[nodiscard]] std::optional<std::int32_t> requiredInputDepth() const noexcept override { return configuredBpp_; }
    [[nodiscard]] bool shareInstanceAcrossContexts() const noexcept override { return true; }

    bool prepareColorTransform(
        const ImageView& raw,
        const ExecutionOptions& options,
        ColorTransform& transform,
        std::string& error) const override
    {
        if (!validateColorImage(type_, configuredBpp_, raw, error)) return false;
        if (!std::all_of(options.exposure.begin(), options.exposure.end(),
            [](float exposure) { return std::isfinite(exposure) && exposure > 0; })) {
            error = "Color calibration exposure must be finite and positive";
            return false;
        }
        ColorTransform prepared;
        if (!colorTransform(options, prepared)) {
            error = "Unable to prepare color calibration factors";
            return false;
        }
        if (!std::all_of(prepared.coefficients.begin(), prepared.coefficients.end(),
            [](double factor) { return std::isfinite(factor); })) {
            error = "Non-finite color calibration factor";
            return false;
        }
        transform = prepared;
        return true;
    }

    bool apply(
        const ImageView& raw,
        float* cieData,
        const ExecutionOptions& options,
        std::string& error) override
    {
        if (cieData == nullptr) {
            error = std::string(calibrationName(type_)) + " CIE output pointer is null";
            return false;
        }
        if (!validateColorImage(type_, configuredBpp_, raw, error)) return false;

        // V1 writers preserve exceptional legacy arithmetic. V2 calls
        // prepareColorTransform before any frame writes to reject invalid factors.
        ColorTransform transform;
        if (!colorTransform(options, transform)) {
            error = "Unable to prepare color calibration factors";
            return false;
        }
        writeColorTransform(raw, options.interleavedBgr, transform, cieData);
        return true;
    }

private:
    CalibrationType type_;
    std::optional<std::int32_t> configuredBpp_;
};

class LuminanceCalibration final : public ColorCalibrationItem {
public:
    LuminanceCalibration(std::optional<std::int32_t> configuredBpp, double coefficient)
        : ColorCalibrationItem(CalibrationType::Luminance, configuredBpp), coefficient_(coefficient)
    {
    }

    bool colorTransform(const ExecutionOptions& options, ColorTransform& transform) const override
    {
        transform = { type(), 2, 1, {} };
        transform.coefficients[0] = coefficient_ / options.exposure[0];
        return true;
    }

private:
    double coefficient_;
};

template <typename Source>
void writeLuminance(const Source* source, float* destination, const ImageView& raw, double factor)
{
    const std::size_t width = raw.width;
    parallelRows(raw.height, [=](std::uint32_t row) {
        const std::size_t begin = static_cast<std::size_t>(row) * width;
        const std::size_t end = begin + width;
        for (std::size_t index = begin; index < end; ++index) {
            destination[index] = color::scale(factor, source[index]);
        }
    });
}

class OneColorCalibration final : public ColorCalibrationItem {
public:
    OneColorCalibration(
        std::optional<std::int32_t> configuredBpp,
        const std::array<double, 10>& values)
        : ColorCalibrationItem(CalibrationType::LumOneColor, configuredBpp),
          gain_{ values[3], values[4], values[5] },
          coefficient_{ values[6], values[7], values[8], values[9] }
    {
    }

    bool colorTransform(const ExecutionOptions& options, ColorTransform& transform) const override
    {
        transform = { type(), 1, 3, {} };
        transform.coefficients[0] = coefficient_[0] / options.exposure[0] / gain_[0];
        transform.coefficients[1] = coefficient_[1] / options.exposure[1] / gain_[1];
        transform.coefficients[2] = coefficient_[2] / options.exposure[2] / gain_[2];
        transform.coefficients[3] = coefficient_[3] / options.exposure[2] / gain_[2];
        return true;
    }

private:
    std::array<double, 3> gain_;
    std::array<double, 4> coefficient_;
};

template <typename Source>
void writeOneColor(const Source* source, float* destination, const ImageView& raw, bool interleavedBgr,
    const std::array<double, 9>& factor)
{
    const std::size_t width = raw.width;
    const std::size_t pixels = width * raw.height;
    float* const outX = destination;
    float* const outY = destination + pixels;
    float* const outZ = destination + pixels * 2;

    parallelRows(raw.height, [=, &factor](std::uint32_t row) {
        const std::size_t begin = static_cast<std::size_t>(row) * width;
        const std::size_t end = begin + width;
        if (interleavedBgr) {
            const Source* rowStart = source + begin * 3;
            float* x = outX + begin;
            float* y = outY + begin;
            float* z = outZ + begin;
            for (std::size_t i = 0; i < width; ++i) {
                x[i] = color::oneColorX(factor.data(), rowStart[i * 3 + 2], rowStart[i * 3]);
            }
            for (std::size_t i = 0; i < width; ++i) {
                y[i] = color::scale(factor[1], rowStart[i * 3 + 1]);
            }
            for (std::size_t i = 0; i < width; ++i) {
                z[i] = color::scale(factor[2], rowStart[i * 3]);
            }
            return;
        }

        const Source* const red = source;
        const Source* const green = source + pixels;
        const Source* const blue = source + pixels * 2;
        for (std::size_t index = begin; index < end; ++index) {
            outX[index] = color::oneColorX(factor.data(), red[index], blue[index]);
        }
        for (std::size_t index = begin; index < end; ++index) {
            outY[index] = color::scale(factor[1], green[index]);
        }
        for (std::size_t index = begin; index < end; ++index) {
            outZ[index] = color::scale(factor[2], blue[index]);
        }
    });
}

#if defined(_M_X64) && !defined(_M_ARM64EC)
__declspec(noinline) void streamMatrixBlocks(
    const std::uint16_t* source, float* outX, float* outY, float* outZ, std::size_t pixels,
    const std::array<double, 9>& factor)
{
    // Four BGR48 pixels occupy exactly 24 bytes. Shuffle their channels
    // into separate 16-bit lanes after a 16-byte and an 8-byte load.
    const auto blueLow = _mm_setr_epi8(0, 1, 6, 7, 12, 13, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1);
    const auto blueHigh = _mm_setr_epi8(-1, -1, -1, -1, -1, -1, 2, 3, -1, -1, -1, -1, -1, -1, -1, -1);
    const auto greenLow = _mm_setr_epi8(2, 3, 8, 9, 14, 15, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1);
    const auto greenHigh = _mm_setr_epi8(-1, -1, -1, -1, -1, -1, 4, 5, -1, -1, -1, -1, -1, -1, -1, -1);
    const auto redLow = _mm_setr_epi8(4, 5, 10, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1);
    const auto redHigh = _mm_setr_epi8(-1, -1, -1, -1, 0, 1, 6, 7, -1, -1, -1, -1, -1, -1, -1, -1);
    const auto zero = _mm_setzero_si128();
    const auto xr = _mm256_set1_pd(factor[0]);
    const auto xg = _mm256_set1_pd(factor[1]);
    const auto xb = _mm256_set1_pd(factor[2]);
    const auto yr = _mm256_set1_pd(factor[3]);
    const auto yg = _mm256_set1_pd(factor[4]);
    const auto yb = _mm256_set1_pd(factor[5]);
    const auto zr = _mm256_set1_pd(factor[6]);
    const auto zg = _mm256_set1_pd(factor[7]);
    const auto zb = _mm256_set1_pd(factor[8]);
    for (std::size_t index = 0; index < pixels; index += 4, source += 12) {
        const auto low = _mm_loadu_si128(reinterpret_cast<const __m128i*>(source));
        const auto high = _mm_loadl_epi64(reinterpret_cast<const __m128i*>(source + 8));
        const auto decode = [=](__m128i lowMask, __m128i highMask) {
            const auto samples = _mm_or_si128(_mm_shuffle_epi8(low, lowMask), _mm_shuffle_epi8(high, highMask));
            return _mm256_cvtepi32_pd(_mm_unpacklo_epi16(samples, zero));
        };
        const auto red = decode(redLow, redHigh);
        const auto green = decode(greenLow, greenHigh);
        const auto blue = decode(blueLow, blueHigh);
        const auto dotProduct = [=](__m256d r, __m256d g, __m256d b) {
            // Match the scalar double-precision multiply and left-to-right add order.
            const auto redGreen = _mm256_add_pd(_mm256_mul_pd(r, red), _mm256_mul_pd(g, green));
            return _mm256_cvtpd_ps(_mm256_add_pd(redGreen, _mm256_mul_pd(b, blue)));
        };
        _mm_stream_ps(outX + index, dotProduct(xr, xg, xb));
        _mm_stream_ps(outY + index, dotProduct(yr, yg, yb));
        _mm_stream_ps(outZ + index, dotProduct(zr, zg, zb));
    }
    _mm_sfence();
    _mm256_zeroupper();
}

bool tryStreamInterleavedMatrix(
    const std::uint16_t* source, float* destination, std::size_t pixels,
    const std::array<double, 9>& factor)
{
    constexpr std::size_t cacheLineBytes = 64;
    constexpr std::size_t pixelsPerBlock = cacheLineBytes / sizeof(float);
    constexpr std::size_t minimumPixels = 5'000'000;
    const auto address = reinterpret_cast<std::uintptr_t>(destination);
    // Keep exceptional legacy arithmetic on the scalar path.
    constexpr double maxFactor = (std::numeric_limits<double>::max)() / 262144.0;
    if (pixels < minimumPixels || pixels % pixelsPerBlock != 0
        || pixels / pixelsPerBlock > static_cast<std::size_t>((std::numeric_limits<int>::max)())
        || address % alignof(float) != 0
        || reinterpret_cast<std::uintptr_t>(source) % alignof(std::uint16_t) != 0
        || !cv::checkHardwareSupport(CV_CPU_AVX2) || !cv::checkHardwareSupport(CV_CPU_SSSE3)
        || !std::all_of(factor.begin(), factor.end(), [](double value) { return std::abs(value) <= maxFactor; })) {
        return false;
    }

    auto* outX = destination;
    auto* outY = destination + pixels;
    auto* outZ = destination + pixels * 2;
    // All three planes have the same alignment. The workers write whole cache
    // lines, and the scalar prefix and tail never share one of those lines.
    const std::size_t prefix = (cacheLineBytes - address % cacheLineBytes) % cacheLineBytes / sizeof(float);
    const auto blockCount = static_cast<int>((pixels - prefix) / pixelsPerBlock);
    const auto scalar = [&](std::size_t begin, std::size_t count) {
        transformInterleavedPlane(source, outX, begin, count, factor[0], factor[1], factor[2]);
        transformInterleavedPlane(source, outY, begin, count, factor[3], factor[4], factor[5]);
        transformInterleavedPlane(source, outZ, begin, count, factor[6], factor[7], factor[8]);
    };
    if (prefix != 0) scalar(0, prefix);
    cv::parallel_for_(cv::Range(0, blockCount), [&](const cv::Range& range) {
        const auto begin = prefix + static_cast<std::size_t>(range.start) * pixelsPerBlock;
        const auto count = static_cast<std::size_t>(range.size()) * pixelsPerBlock;
        streamMatrixBlocks(source + begin * 3, outX + begin, outY + begin, outZ + begin, count, factor);
    }, 4.0 * cv::getNumThreads());
    const auto tail = prefix + static_cast<std::size_t>(blockCount) * pixelsPerBlock;
    if (tail != pixels) scalar(tail, pixels - tail);
    return true;
}
#endif

class MatrixColorCalibration final : public ColorCalibrationItem {
public:
    MatrixColorCalibration(
        CalibrationType type,
        std::optional<std::int32_t> configuredBpp,
        std::array<double, 9> coefficient,
        std::array<double, 3> gain,
        bool usesGain)
        : ColorCalibrationItem(type, configuredBpp),
          coefficient_(std::move(coefficient)),
          gain_(std::move(gain)),
          usesGain_(usesGain)
    {
    }

    bool colorTransform(const ExecutionOptions& options, ColorTransform& transform) const override
    {
        transform = { type(), 0, 3, {} };
        for (std::size_t i = 0; i < transform.coefficients.size(); ++i) {
            transform.coefficients[i] = coefficient_[i] / options.exposure[i % 3];
            if (usesGain_) transform.coefficients[i] /= gain_[i % 3];
        }
        return true;
    }

private:
    std::array<double, 9> coefficient_;
    std::array<double, 3> gain_;
    bool usesGain_;
};

template <typename Source>
void writeMatrixColor(const Source* source, float* destination, const ImageView& raw, bool interleavedBgr,
    const std::array<double, 9>& factor)
{
    const std::size_t width = raw.width;
    const std::size_t pixels = width * raw.height;
#if defined(_M_X64) && !defined(_M_ARM64EC)
    if constexpr (std::is_same_v<Source, std::uint16_t>) {
        if (interleavedBgr && tryStreamInterleavedMatrix(source, destination, pixels, factor)) return;
    }
#endif
    float* const outX = destination;
    float* const outY = destination + pixels;
    float* const outZ = destination + pixels * 2;

    parallelRows(raw.height, [=, &factor](std::uint32_t row) {
        const std::size_t begin = static_cast<std::size_t>(row) * width;
        if (interleavedBgr) {
            transformInterleavedPlane(source, outX, begin, width, factor[0], factor[1], factor[2]);
            transformInterleavedPlane(source, outY, begin, width, factor[3], factor[4], factor[5]);
            transformInterleavedPlane(source, outZ, begin, width, factor[6], factor[7], factor[8]);
            return;
        }

        const Source* const red = source;
        const Source* const green = source + pixels;
        const Source* const blue = source + pixels * 2;
        transformPlanarPlane(red, green, blue, outX, begin, width, factor[0], factor[1], factor[2]);
        transformPlanarPlane(red, green, blue, outY, begin, width, factor[3], factor[4], factor[5]);
        transformPlanarPlane(red, green, blue, outZ, begin, width, factor[6], factor[7], factor[8]);
    });
}

template <typename Source>
void writeColor(const ImageView& raw, bool interleavedBgr, const ColorTransform& transform, float* cieData)
{
    const auto* source = reinterpret_cast<const Source*>(raw.data);
    switch (transform.kind) {
    case 2:
        writeLuminance(source, cieData, raw, transform.coefficients[0]);
        break;
    case 1:
        writeOneColor(source, cieData, raw, interleavedBgr, transform.coefficients);
        break;
    default:
        writeMatrixColor(source, cieData, raw, interleavedBgr, transform.coefficients);
    }
}

std::unique_ptr<CalibrationItem> loadLuminance(const Json& root)
{
    std::array<double, 10> values{};
    readLegacyLumFields(root, values);
    return std::make_unique<LuminanceCalibration>(legacyBitsPerChannel(root), values[6]);
}

std::unique_ptr<CalibrationItem> loadOneColor(const Json& root)
{
    std::array<double, 10> values{};
    readLegacyLumFields(root, values);
    return std::make_unique<OneColorCalibration>(legacyBitsPerChannel(root), values);
}

std::unique_ptr<CalibrationItem> loadFourColor(const Json& root)
{
    // Read all fields, including the historically unused Texp/Gain members,
    // so malformed legacy files fail at the same stage as before.
    std::array<double, 10> common{};
    readLegacyLumFields(root, common);
    std::array<double, 9> coefficient = {
        common[6], common[7], common[8], common[9],
        legacyDouble(root, "e"), legacyDouble(root, "f"),
        legacyDouble(root, "g"), legacyDouble(root, "h"), legacyDouble(root, "i")
    };
    const std::array<double, 3> gain = { common[3], common[4], common[5] };
    return std::make_unique<MatrixColorCalibration>(
        CalibrationType::LumFourColor,
        legacyBitsPerChannel(root),
        std::move(coefficient),
        gain,
        false);
}

std::unique_ptr<CalibrationItem> loadMultiColor(const Json& root)
{
    const auto pa = root.find("pa");
    if (pa == root.end() || !pa->is_array() || pa->empty()) {
        throw std::runtime_error("LumMultiColor member 'pa' must be a non-empty array");
    }
    if (pa->size() < 9) {
        throw std::runtime_error("LumMultiColor member 'pa' must contain at least 9 coefficients");
    }

    std::array<double, 9> coefficient{};
    for (std::size_t index = 0; index < coefficient.size(); ++index) {
        coefficient[index] = legacyFloatAsDouble((*pa)[index], "pa");
    }

    std::array<double, 3> gain{};
    const auto gainValue = root.find("Gain");
    if (gainValue != root.end() && !gainValue->is_null() && !gainValue->is_array()) {
        throw std::runtime_error("LumMultiColor member 'Gain' must be an array");
    }
    for (std::size_t index = 0; index < gain.size(); ++index) {
        if (gainValue != root.end() && gainValue->is_array() && index < gainValue->size()) {
            gain[index] = legacyFloatAsDouble((*gainValue)[index], "Gain");
        }
        // Missing legacy Gain values become zero through JsonCpp::asFloat.
    }

    return std::make_unique<MatrixColorCalibration>(
        CalibrationType::LumMultiColor,
        legacyBitsPerChannel(root),
        std::move(coefficient),
        gain,
        true);
}

} // namespace

// Color consumes the complete frame. ROI and optical-black bounds apply to
// the spatial stages in the calibration plan.
void writeColorTransform(const ImageView& raw, bool interleavedBgr, const ColorTransform& transform, float* cieData)
{
    if (raw.bitsPerChannel == 8) writeColor<std::uint8_t>(raw, interleavedBgr, transform, cieData);
    else writeColor<std::uint16_t>(raw, interleavedBgr, transform, cieData);
}

std::unique_ptr<CalibrationItem> loadColorCalibration(
    CalibrationType type,
    const std::filesystem::path& file,
    std::string& error)
{
    if (type != CalibrationType::Luminance
        && type != CalibrationType::LumOneColor
        && type != CalibrationType::LumFourColor
        && type != CalibrationType::LumMultiColor) {
        return nullptr;
    }

    error.clear();
    try {
        const Json root = readJson(file);
        switch (type) {
        case CalibrationType::Luminance:
            return loadLuminance(root);
        case CalibrationType::LumOneColor:
            return loadOneColor(root);
        case CalibrationType::LumFourColor:
            return loadFourColor(root);
        case CalibrationType::LumMultiColor:
            return loadMultiColor(root);
        default:
            return nullptr;
        }
    }
    catch (const std::exception& exception) {
        error = std::string("Unable to load ") + calibrationName(type)
            + " calibration: " + exception.what();
        return nullptr;
    }
    catch (...) {
        error = std::string("Unable to load ") + calibrationName(type)
            + " calibration: unknown error";
        return nullptr;
    }
}

} // namespace cvcore::calibration

#ifdef _MSC_VER
#pragma float_control(pop)
#endif
