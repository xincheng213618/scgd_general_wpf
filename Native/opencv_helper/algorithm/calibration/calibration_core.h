#pragma once

#include <array>
#include <cstddef>
#include <cstdint>
#include <filesystem>
#include <memory>
#include <string>

namespace cvcore::calibration {

// Values intentionally match cvCamera's long-lived CalibrationType ABI.
enum class CalibrationType : std::int32_t {
    DarkNoise = 0,
    DefectWPoint = 1,
    DefectBPoint = 2,
    DefectPoint = 3,
    Dsnu = 4,
    Uniformity = 5,
    Luminance = 6,
    LumOneColor = 7,
    LumFourColor = 8,
    LumMultiColor = 9,
    LumColor = 10, // Reserved by the legacy ABI; there is no implementation.
    Distortion = 11,
    ColorShift = 12,
    LineArity = 13,
    ColorDiff = 14,
    AngleShift = 15,
};

struct ImageView {
    std::uint32_t width = 0;
    std::uint32_t height = 0;
    std::uint32_t bitsPerChannel = 0;
    std::uint32_t channels = 0;
    std::uint8_t* data = nullptr;
    std::size_t dataLength = 0;
};

struct ExecutionOptions {
    // Legacy local calibration uses interleaved BGR and rgbType == 0, but the
    // fields remain explicit so the new module does not narrow the old ABI.
    bool interleavedBgr = true;
    std::int32_t rgbType = 0;
    std::array<std::uint32_t, 4> roi{}; // x, y, width, height
    std::array<std::uint32_t, 4> ob{};  // left, right, top, bottom
    std::array<float, 3> exposure{};
};

class CalibrationItem {
public:
    virtual ~CalibrationItem() = default;

    [[nodiscard]] virtual CalibrationType type() const noexcept = 0;
    [[nodiscard]] virtual bool isColorTransform() const noexcept { return false; }
    [[nodiscard]] virtual bool requiresDistinctOutput() const noexcept { return false; }
    [[nodiscard]] virtual bool supportsDistinctOutput() const noexcept
    {
        return requiresDistinctOutput();
    }

    // Process-file-cache hooks. Stateless items may be used directly by
    // independent contexts. Stateful geometric items instead return a fresh
    // executor whose immutable OpenCV maps share the prototype's storage.
    [[nodiscard]] virtual bool shareInstanceAcrossContexts() const noexcept { return false; }
    [[nodiscard]] virtual std::unique_ptr<CalibrationItem> cloneForContext() const { return nullptr; }
    [[nodiscard]] virtual std::uint64_t cacheFootprintBytes() const noexcept { return 0; }

    // Basic items mutate RAW in place. Color items write planar float XYZ to
    // cieData (X plane, then Y, then Z); Luminance writes the first plane.
    virtual bool apply(
        const ImageView& raw,
        float* cieData,
        const ExecutionOptions& options,
        std::string& error) = 0;

    // Geometric transforms whose output depends on pixels that may be
    // overwritten require this path. Map corrections also implement it so a
    // read-only caller buffer can be corrected directly into a work/output
    // buffer without first materializing an unchanged full-frame copy.
    virtual bool applyOutOfPlace(
        const ImageView& source,
        const ImageView& destination,
        const ExecutionOptions& options,
        std::string& error)
    {
        error = "Calibration does not support a distinct output buffer";
        return false;
    }
};

[[nodiscard]] bool isColorCalibration(CalibrationType type) noexcept;
[[nodiscard]] bool isSupportedCalibration(CalibrationType type) noexcept;

// Each group owns only its algorithms. The context tries the matching group;
// nullptr with an empty error means "not handled by this group".
std::unique_ptr<CalibrationItem> loadBasicCalibration(
    CalibrationType type,
    const std::filesystem::path& file,
    std::string& error);

std::unique_ptr<CalibrationItem> loadColorCalibration(
    CalibrationType type,
    const std::filesystem::path& file,
    std::string& error);

std::unique_ptr<CalibrationItem> loadGeometricCalibration(
    CalibrationType type,
    const std::filesystem::path& file,
    std::string& error);

} // namespace cvcore::calibration
