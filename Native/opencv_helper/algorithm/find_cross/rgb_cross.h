#pragma once
#include <cstddef>
#include <cstdint>
#include <nlohmann/json.hpp>
#include <string>

namespace cvnative::rgb_cross
{
struct Rectangle
{
    int x = 0, y = 0, width = 0, height = 0;
};
struct Image
{
    const std::uint8_t *data = nullptr;
    std::size_t size = 0, stride = 0;
    int width = 0, height = 0, bits = 16, channels = 3; // BGR/BGRA; 8, 16 LE, or normalized float32 LE
};
struct Options
{
    double minimumContrast = .02, targetThreshold = .5, minimumArmSpanFraction = .35;
    double axisBandThreshold = .5, minimumArmCoverage = .5, decodeExponent = 1;
    int rows = 3, columns = 3;
};
// Neutral measurement only. No database, WPF, product limits or permission decisions.
// Throws invalid_argument/runtime_error for execution failures; undetectable points remain INVALID.
nlohmann::json Measure(const Image &image, Rectangle search, const Options &options, const std::string &measurementId,
                       const std::string &imageId);
} // namespace cvnative::rgb_cross
