#include "calibration_core.h"

#include <nlohmann/json.hpp>
#include <opencv2/core.hpp>

#include <array>
#include <cstddef>
#include <cstdint>
#include <fstream>
#include <limits>
#include <memory>
#include <optional>
#include <stdexcept>
#include <string>
#include <utility>

#ifdef _MSC_VER
#pragma float_control(precise, on, push)
#pragma fp_contract(off)
#endif

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
    float* cieData,
    std::uint32_t expectedChannels,
    std::string& error)
{
    if (cieData == nullptr) {
        error = std::string(calibrationName(type)) + " CIE output pointer is null";
        return false;
    }
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
        *output++ = static_cast<float>(redCoefficient * current[2]
            + greenCoefficient * current[1]
            + blueCoefficient * current[0]);
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
        destination[index] = static_cast<float>(redCoefficient * red[index]
            + greenCoefficient * green[index]
            + blueCoefficient * blue[index]);
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
    [[nodiscard]] bool shareInstanceAcrossContexts() const noexcept override { return true; }

protected:
    // options.roi/options.ob remain in the shared ABI for the basic correction
    // groups. Legacy luminance/color transforms ignored both and always wrote
    // the complete frame, so the color group deliberately preserves that
    // behavior for byte-for-byte replacement.
    bool validate(
        const ImageView& raw,
        float* cieData,
        std::uint32_t expectedChannels,
        std::string& error) const
    {
        return validateColorImage(type_, configuredBpp_, raw, cieData, expectedChannels, error);
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

    bool apply(
        const ImageView& raw,
        float* cieData,
        const ExecutionOptions& options,
        std::string& error) override
    {
        if (!validate(raw, cieData, 1, error)) {
            return false;
        }

        const double factor = coefficient_ / options.exposure[0];
        if (raw.bitsPerChannel == 8) {
            applyTyped(reinterpret_cast<const std::uint8_t*>(raw.data), cieData, raw, factor);
        }
        else {
            applyTyped(reinterpret_cast<const std::uint16_t*>(raw.data), cieData, raw, factor);
        }
        return true;
    }

private:
    template <typename Source>
    static void applyTyped(const Source* source, float* destination, const ImageView& raw, double factor)
    {
        const std::size_t width = raw.width;
        parallelRows(raw.height, [=](std::uint32_t row) {
            const std::size_t begin = static_cast<std::size_t>(row) * width;
            const std::size_t end = begin + width;
            for (std::size_t index = begin; index < end; ++index) {
                destination[index] = static_cast<float>(factor * source[index]);
            }
        });
    }

    double coefficient_;
};

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

    bool apply(
        const ImageView& raw,
        float* cieData,
        const ExecutionOptions& options,
        std::string& error) override
    {
        if (!validate(raw, cieData, 3, error)) {
            return false;
        }

        const std::array<double, 4> factor = {
            coefficient_[0] / options.exposure[0] / gain_[0],
            coefficient_[1] / options.exposure[1] / gain_[1],
            coefficient_[2] / options.exposure[2] / gain_[2],
            coefficient_[3] / options.exposure[2] / gain_[2]
        };

        if (raw.bitsPerChannel == 8) {
            applyTyped(reinterpret_cast<const std::uint8_t*>(raw.data), cieData, raw, options.interleavedBgr, factor);
        }
        else {
            applyTyped(reinterpret_cast<const std::uint16_t*>(raw.data), cieData, raw, options.interleavedBgr, factor);
        }
        return true;
    }

private:
    template <typename Source>
    static void applyTyped(
        const Source* source,
        float* destination,
        const ImageView& raw,
        bool interleavedBgr,
        const std::array<double, 4>& factor)
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
                const Source* current = source + begin * 3;
                const Source* const sourceEnd = current + width * 3;
                float* output = outX + begin;
                for (; current < sourceEnd; current += 3) {
                    *output++ = static_cast<float>(factor[0] * current[2] + factor[3] * current[0]);
                }

                current = source + begin * 3 + 1;
                output = outY + begin;
                for (; current < sourceEnd; current += 3) {
                    *output++ = static_cast<float>(factor[1] * *current);
                }

                current = source + begin * 3;
                output = outZ + begin;
                for (; current < sourceEnd; current += 3) {
                    *output++ = static_cast<float>(factor[2] * *current);
                }
                return;
            }

            const Source* const red = source;
            const Source* const green = source + pixels;
            const Source* const blue = source + pixels * 2;
            for (std::size_t index = begin; index < end; ++index) {
                outX[index] = static_cast<float>(factor[0] * red[index] + factor[3] * blue[index]);
            }
            for (std::size_t index = begin; index < end; ++index) {
                outY[index] = static_cast<float>(factor[1] * green[index]);
            }
            for (std::size_t index = begin; index < end; ++index) {
                outZ[index] = static_cast<float>(factor[2] * blue[index]);
            }
        });
    }

    std::array<double, 3> gain_;
    std::array<double, 4> coefficient_;
};

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

    bool apply(
        const ImageView& raw,
        float* cieData,
        const ExecutionOptions& options,
        std::string& error) override
    {
        if (!validate(raw, cieData, 3, error)) {
            return false;
        }

        std::array<double, 9> factor{};
        for (std::size_t index = 0; index < factor.size(); ++index) {
            const std::size_t channel = index % 3;
            factor[index] = coefficient_[index] / options.exposure[channel];
            if (usesGain_) {
                factor[index] = factor[index] / gain_[channel];
            }
        }

        if (raw.bitsPerChannel == 8) {
            applyTyped(reinterpret_cast<const std::uint8_t*>(raw.data), cieData, raw, options.interleavedBgr, factor);
        }
        else {
            applyTyped(reinterpret_cast<const std::uint16_t*>(raw.data), cieData, raw, options.interleavedBgr, factor);
        }
        return true;
    }

private:
    template <typename Source>
    static void applyTyped(
        const Source* source,
        float* destination,
        const ImageView& raw,
        bool interleavedBgr,
        const std::array<double, 9>& factor)
    {
        const std::size_t width = raw.width;
        const std::size_t pixels = width * raw.height;
        float* const outX = destination;
        float* const outY = destination + pixels;
        float* const outZ = destination + pixels * 2;

        parallelRows(raw.height, [=, &factor](std::uint32_t row) {
            const std::size_t begin = static_cast<std::size_t>(row) * width;
            if (interleavedBgr) {
                // Keep the legacy three-pass row ordering. Fusing these loops
                // changes the optimized DLL's access/timing behavior and makes
                // bit-level regression analysis unnecessarily ambiguous.
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

    std::array<double, 9> coefficient_;
    std::array<double, 3> gain_;
    bool usesGain_;
};

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
