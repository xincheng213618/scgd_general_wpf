#pragma once

#include <cstddef>
#include <cstdint>

struct MRawColorTransformV1;

namespace cvcore::calibration::color {

bool frameValuesAreFinite(const void* raw, std::size_t pixels, std::uint32_t bitsPerChannel,
    const MRawColorTransformV1& transform, const float* cie);

// Keep the products and sums in double, round once to float, and preserve
// operand order across full frames, RAW replay, and POI accumulation.
inline float scale(double factor, double sample) noexcept
{
    return static_cast<float>(factor * sample);
}

inline float oneColorX(const double* factors, double red, double blue) noexcept
{
    return static_cast<float>(factors[0] * red + factors[3] * blue);
}

inline float matrixPlane(
    double redFactor, double greenFactor, double blueFactor, double red, double green, double blue) noexcept
{
    return static_cast<float>(redFactor * red + greenFactor * green + blueFactor * blue);
}

inline float matrixPlane(const double* factors, double red, double green, double blue) noexcept
{
    return matrixPlane(factors[0], factors[1], factors[2], red, green, blue);
}

} // namespace cvcore::calibration::color
