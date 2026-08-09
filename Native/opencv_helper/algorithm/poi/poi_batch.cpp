#include "poi_batch.h"

#include <algorithm>
#include <array>
#include <cmath>
#include <cstddef>
#include <cstdint>
#include <functional>
#include <limits>
#include <utility>
#include <vector>

namespace cvcore::poi {
namespace {

constexpr double kReferenceX = 0.3333;
constexpr double kReferenceY = 0.3333;
constexpr int kWaveStep = 5;

constexpr std::array<double, 81> kWaveX{
    0.1741, 0.1740, 0.1738, 0.1736, 0.1733, 0.1730, 0.1726, 0.1721, 0.1714, 0.1703,
    0.1689, 0.1669, 0.1644, 0.1611, 0.1566, 0.1510, 0.1440, 0.1355, 0.1241, 0.1096,
    0.0913, 0.0687, 0.0454, 0.0235, 0.0082, 0.0039, 0.0139, 0.0389, 0.0743, 0.1142,
    0.1547, 0.1929, 0.2296, 0.2658, 0.3016, 0.3373, 0.3731, 0.4087, 0.4441, 0.4788,
    0.5125, 0.5448, 0.5752, 0.6029, 0.6270, 0.6482, 0.6658, 0.6801, 0.6915, 0.7006,
    0.7079, 0.7140, 0.7219, 0.7230, 0.7260, 0.7283, 0.7300, 0.7311, 0.7320, 0.7327,
    0.7334, 0.7340, 0.7344, 0.7346, 0.7347, 0.7347, 0.7347, 0.7347, 0.7347, 0.7347,
    0.7347, 0.7347, 0.7347, 0.7347, 0.7347, 0.7347, 0.7347, 0.7347, 0.7347, 0.7347,
    0.7347
};

constexpr std::array<double, 81> kWaveY{
    0.0050, 0.0050, 0.0049, 0.0049, 0.0048, 0.0048, 0.0048, 0.0048, 0.0051, 0.0058,
    0.0069, 0.0086, 0.0109, 0.0138, 0.0177, 0.0227, 0.0297, 0.0399, 0.0578, 0.0868,
    0.1327, 0.2007, 0.2950, 0.4127, 0.5384, 0.6548, 0.7502, 0.8120, 0.8338, 0.8262,
    0.8059, 0.7816, 0.7543, 0.7243, 0.6923, 0.6589, 0.6245, 0.5896, 0.5547, 0.5202,
    0.4866, 0.4544, 0.4242, 0.3965, 0.3725, 0.3514, 0.3340, 0.3197, 0.3083, 0.2993,
    0.2920, 0.2859, 0.2809, 0.2770, 0.2740, 0.2717, 0.2700, 0.2689, 0.2680, 0.2673,
    0.2666, 0.2660, 0.2656, 0.2654, 0.2653, 0.2653, 0.2653, 0.2653, 0.2653, 0.2653,
    0.2653, 0.2653, 0.2653, 0.2653, 0.2653, 0.2653, 0.2653, 0.2653, 0.2653, 0.2653,
    0.2653
};

struct ChannelAverage {
    double x = 0.0;
    double y = 0.0;
    double z = 0.0;
    std::int32_t count = 0;
};

void addPixel(
    ChannelAverage& average,
    const float* channelX,
    const float* channelY,
    const float* channelZ,
    std::size_t index,
    std::int32_t channels) noexcept
{
    if (channels == 1) {
        average.y += static_cast<float>(channelX[index]);
    }
    else {
        average.x += static_cast<float>(channelX[index]);
        average.y += static_cast<float>(channelY[index]);
        average.z += static_cast<float>(channelZ[index]);
    }
    ++average.count;
}

ChannelAverage calculateCircle(
    const float* channelX,
    const float* channelY,
    const float* channelZ,
    std::int32_t imageWidth,
    std::int32_t imageHeight,
    std::int32_t channels,
    std::int32_t centerX,
    std::int32_t centerY,
    double radius) noexcept
{
    ChannelAverage average;
    if (radius > 0.0) {
        for (std::int32_t row = static_cast<std::int32_t>(centerY - radius); row <= centerY + radius; ++row) {
            if (row < 0 || row >= imageHeight) continue;
            for (std::int32_t column = static_cast<std::int32_t>(centerX - radius); column <= centerX + radius; ++column) {
                if (column < 0 || column >= imageWidth) continue;
                const double distance = (row - centerY) * (row - centerY)
                    + (column - centerX) * (column - centerX);
                if (distance < radius * radius) {
                    addPixel(average, channelX, channelY, channelZ,
                        static_cast<std::size_t>(row) * imageWidth + column, channels);
                }
            }
        }
    }
    if (average.count == 0) {
        addPixel(average, channelX, channelY, channelZ,
            static_cast<std::size_t>(centerY) * imageWidth + centerX, channels);
    }
    return average;
}

ChannelAverage calculateRectangle(
    const float* channelX,
    const float* channelY,
    const float* channelZ,
    std::int32_t imageWidth,
    std::int32_t imageHeight,
    std::int32_t channels,
    std::int32_t centerX,
    std::int32_t centerY,
    std::int32_t roiWidth,
    std::int32_t roiHeight) noexcept
{
    ChannelAverage average;
    if (roiWidth > 0 && roiHeight > 0) {
        const std::int32_t firstRow = static_cast<std::int32_t>(centerY - roiHeight / 2 + 0.5);
        const std::int32_t lastRow = centerY + roiHeight / 2;
        const std::int32_t firstColumn = static_cast<std::int32_t>(centerX - roiWidth / 2 + 0.5);
        const std::int32_t lastColumn = centerX + roiWidth / 2;
        for (std::int32_t row = firstRow; row <= lastRow; ++row) {
            if (row < 0 || row >= imageHeight) continue;
            for (std::int32_t column = firstColumn; column <= lastColumn; ++column) {
                if (column < 0 || column >= imageWidth) continue;
                addPixel(average, channelX, channelY, channelZ,
                    static_cast<std::size_t>(row) * imageWidth + column, channels);
            }
        }
    }
    if (average.count == 0) {
        addPixel(average, channelX, channelY, channelZ,
            static_cast<std::size_t>(centerY) * imageWidth + centerX, channels);
    }
    return average;
}

double calculateCct(double x, double y) noexcept
{
    const double n = (x - 0.3320) / (0.1858 - y);
    return 437 * std::pow(n, 3) + 3601 * std::pow(n, 2) + 6831 * n + 5517;
}

double calculateMainWave(double x, double y) noexcept
{
    constexpr double minWaveX = 0.1741;
    constexpr double minWaveY = 0.0050;
    constexpr double maxWaveX = 0.7347;
    constexpr double maxWaveY = 0.2653;
    if (y < kReferenceY
        && ((y - kReferenceY) / (x - kReferenceX) > (minWaveY - kReferenceY) / (minWaveX - kReferenceX)
            || (y - kReferenceY) / (x - kReferenceX) < (maxWaveY - kReferenceY) / (maxWaveX - kReferenceX))) {
        return -1;
    }

    const double A = (y - kReferenceY) / (x - kReferenceX);
    const double B = -1;
    const double C = y - x * (y - kReferenceY) / (x - kReferenceX);
    std::array<double, 80> intersections{};
    std::size_t count = 0;
    for (std::size_t index = 0; index + 1 < kWaveX.size(); ++index) {
        const double divisor = std::sqrt(A * A + B * B);
        const double d1 = (A * kWaveX[index] + B * kWaveY[index] + C) / divisor;
        const double d2 = (A * kWaveX[index + 1] + B * kWaveY[index + 1] + C) / divisor;
        if (d1 * d2 <= 0 && count < intersections.size()) {
            intersections[count++] = 380 + static_cast<double>(index * kWaveStep)
                + kWaveStep * std::fabs(d1) / (std::fabs(d1) + std::fabs(d2));
        }
    }

    double mainWave = -99;
    if (count == 1) {
        mainWave = intersections[0];
    }
    else if (count == 2) {
        mainWave = x < kReferenceX ? intersections[0] : intersections[1];
    }
    return mainWave;
}

} // namespace

bool calculateBatchV1(
    std::int32_t width,
    std::int32_t height,
    std::int32_t bitsPerChannel,
    std::int32_t channels,
    const float* cieData,
    std::size_t cieFloatCount,
    const RequestV1* requests,
    std::uint32_t requestCount,
    ResultV1* results) noexcept
{
    if (width <= 0 || height <= 0 || bitsPerChannel != 32
        || (channels != 1 && channels != 3)
        || cieData == nullptr || requests == nullptr || requestCount == 0 || results == nullptr) {
        return false;
    }
    const auto widthSize = static_cast<std::size_t>(width);
    const auto heightSize = static_cast<std::size_t>(height);
    if (widthSize > (std::numeric_limits<std::size_t>::max)() / heightSize) return false;
    const std::size_t pixelCount = widthSize * heightSize;
    if (pixelCount > (std::numeric_limits<std::size_t>::max)() / static_cast<std::size_t>(channels)
        || cieFloatCount < pixelCount * static_cast<std::size_t>(channels)) {
        return false;
    }

    const float* channelX = cieData;
    const float* channelY = channels == 3 ? channelX + pixelCount : nullptr;
    const float* channelZ = channels == 3 ? channelY + pixelCount : nullptr;
    for (std::uint32_t index = 0; index < requestCount; ++index) {
        const RequestV1& request = requests[index];
        if (request.x < 0 || request.x >= width || request.y < 0 || request.y >= height
            || request.width <= 0 || request.height <= 0 || request.type < 0 || request.type > 2) {
            return false;
        }

        const ChannelAverage average = request.type == 2
            ? calculateRectangle(channelX, channelY, channelZ, width, height, channels,
                request.x, request.y, request.width, request.height)
            : calculateCircle(channelX, channelY, channelZ, width, height, channels,
                request.x, request.y, request.type == 0 ? 1.0 : request.width / 2.0);

        ResultV1 result{};
        if (channels == 1) {
            result.Y = static_cast<float>(average.y / average.count);
        }
        else {
            result.X = static_cast<float>(average.x / average.count);
            result.Y = static_cast<float>(average.y / average.count);
            result.Z = static_cast<float>(average.z / average.count);
            if (result.X <= 0.0F) result.X = 0.000001F;
            if (result.Y <= 0.0F) result.Y = 0.000001F;
            if (result.Z <= 0.0F) result.Z = 0.000001F;

            const float sum = result.X + result.Y + result.Z;
            result.x = result.X / sum;
            result.y = result.Y / sum;
            const float uvDenominator = result.X + result.Y * 15.0F + result.Z * 3.0F;
            if (uvDenominator != 0.0F) {
                result.u = 4.0F * result.X / uvDenominator;
                result.v = 9.0F * result.Y / uvDenominator;
            }
            result.cct = static_cast<float>(calculateCct(result.x, result.y));
            result.wave = static_cast<float>(calculateMainWave(result.x, result.y));
        }
        results[index] = result;
    }
    return true;
}

namespace {

constexpr std::uint32_t kKnownOptionsFlags = PercentThreshold | ApplyMnp;

bool isValidRequest(const RequestV1& request, std::int32_t width, std::int32_t height) noexcept
{
    return request.x >= 0 && request.x < width && request.y >= 0 && request.y < height
        && request.width > 0 && request.height > 0 && request.type >= 0 && request.type <= 2;
}

bool validateOptions(const OptionsV2& options, std::int32_t channels) noexcept
{
    if (options.structSize != sizeof(OptionsV2)
        || (options.flags & ~kKnownOptionsFlags) != 0
        || options.filterMode < 0 || options.filterMode > 3
        || options.xyzChannel < 0 || options.xyzChannel > 2
        || !std::isfinite(options.threshold) || !std::isfinite(options.maxPercent)
        || !std::isfinite(options.scaleX) || !std::isfinite(options.scaleY) || !std::isfinite(options.scaleZ)
        || options.reserved[0] != 0 || options.reserved[1] != 0 || options.reserved[2] != 0) {
        return false;
    }
    if ((options.flags & PercentThreshold) != 0) {
        if (options.filterMode == 0 || options.maxPercent < 0.0F || options.maxPercent > 1.0F) return false;
    }
    return options.filterMode != 2 || options.xyzChannel < channels;
}

template <typename Consumer>
void forEachCirclePixel(
    const RequestV1& request,
    std::int32_t imageWidth,
    std::int32_t imageHeight,
    bool includeBoundary,
    Consumer&& consume)
{
    const double radius = request.type == 0 ? 1.0 : request.width / 2.0;
    const double firstRowValue = std::max(0.0, static_cast<double>(request.y) - radius);
    const double lastRowValue = std::min(static_cast<double>(imageHeight - 1), static_cast<double>(request.y) + radius);
    const double firstColumnValue = std::max(0.0, static_cast<double>(request.x) - radius);
    const double lastColumnValue = std::min(static_cast<double>(imageWidth - 1), static_cast<double>(request.x) + radius);
    const auto firstRow = static_cast<std::int32_t>(firstRowValue);
    const auto lastRow = static_cast<std::int32_t>(lastRowValue);
    const auto firstColumn = static_cast<std::int32_t>(firstColumnValue);
    const auto lastColumn = static_cast<std::int32_t>(lastColumnValue);
    const double radiusSquared = radius * radius;
    for (std::int32_t row = firstRow; row <= lastRow; ++row) {
        const double deltaY = static_cast<double>(row) - request.y;
        for (std::int32_t column = firstColumn; column <= lastColumn; ++column) {
            const double deltaX = static_cast<double>(column) - request.x;
            const double distanceSquared = deltaY * deltaY + deltaX * deltaX;
            if ((includeBoundary && distanceSquared <= radiusSquared)
                || (!includeBoundary && distanceSquared < radiusSquared)) {
                consume(static_cast<std::size_t>(row) * static_cast<std::size_t>(imageWidth)
                    + static_cast<std::size_t>(column));
            }
        }
    }
}

template <typename Consumer>
void forEachRectanglePixel(
    const RequestV1& request,
    std::int32_t imageWidth,
    std::int32_t imageHeight,
    Consumer&& consume)
{
    const double halfHeight = request.height / 2;
    const double halfWidth = request.width / 2;
    const double firstRowValue = std::max(0.0, static_cast<double>(request.y) - halfHeight + 0.5);
    const double lastRowValue = std::min(static_cast<double>(imageHeight - 1), static_cast<double>(request.y) + halfHeight);
    const double firstColumnValue = std::max(0.0, static_cast<double>(request.x) - halfWidth + 0.5);
    const double lastColumnValue = std::min(static_cast<double>(imageWidth - 1), static_cast<double>(request.x) + halfWidth);
    const auto firstRow = static_cast<std::int32_t>(firstRowValue);
    const auto lastRow = static_cast<std::int32_t>(lastRowValue);
    const auto firstColumn = static_cast<std::int32_t>(firstColumnValue);
    const auto lastColumn = static_cast<std::int32_t>(lastColumnValue);
    for (std::int32_t row = firstRow; row <= lastRow; ++row) {
        for (std::int32_t column = firstColumn; column <= lastColumn; ++column) {
            consume(static_cast<std::size_t>(row) * static_cast<std::size_t>(imageWidth)
                + static_cast<std::size_t>(column));
        }
    }
}

template <typename Consumer>
void forEachFilterPixel(
    const RequestV1& request,
    std::int32_t imageWidth,
    std::int32_t imageHeight,
    Consumer&& consume)
{
    if (request.type == 2) {
        forEachRectanglePixel(request, imageWidth, imageHeight, std::forward<Consumer>(consume));
    }
    else {
        forEachCirclePixel(request, imageWidth, imageHeight, true, std::forward<Consumer>(consume));
    }
}

template <typename Consumer>
void forEachTopPercentPixel(
    const RequestV1& request,
    std::int32_t imageWidth,
    std::int32_t imageHeight,
    Consumer&& consume)
{
    if (request.type == 2) {
        forEachRectanglePixel(request, imageWidth, imageHeight, std::forward<Consumer>(consume));
    }
    else {
        forEachCirclePixel(request, imageWidth, imageHeight, false, std::forward<Consumer>(consume));
    }
}

float calculateTopMean(
    const float* plane,
    const RequestV1& request,
    std::int32_t width,
    std::int32_t height,
    float maxPercent,
    std::vector<float>& values)
{
    values.clear();
    forEachTopPercentPixel(request, width, height,
        [&](std::size_t pixelIndex) { values.push_back(plane[pixelIndex]); });
    if (values.empty()) return 0.0F;

    std::size_t topCount = static_cast<std::size_t>(std::floor(
        static_cast<double>(values.size()) * static_cast<double>(maxPercent)));
    topCount = std::max<std::size_t>(1, std::min(topCount, values.size()));
    const std::greater<float> descending;
    if (topCount < values.size()) {
        std::nth_element(values.begin(), values.begin() + topCount, values.end(), descending);
    }
    std::sort(values.begin(), values.begin() + topCount, descending);
    double sum = 0.0;
    for (std::size_t index = 0; index < topCount; ++index) sum += values[index];
    return static_cast<float>(sum / static_cast<double>(topCount));
}

std::array<float, 3> calculateFilteredAverages(
    const std::array<const float*, 3>& planes,
    std::int32_t channels,
    const RequestV1& request,
    std::int32_t width,
    std::int32_t height,
    const OptionsV2& options,
    std::vector<float>& topValues)
{
    std::array<float, 3> thresholds{ options.threshold, options.threshold, options.threshold };
    if ((options.flags & PercentThreshold) != 0) {
        if (options.filterMode == 2) {
            const auto selected = static_cast<std::size_t>(options.xyzChannel);
            thresholds[selected] *= calculateTopMean(
                planes[selected], request, width, height, options.maxPercent, topValues);
        }
        else {
            for (std::int32_t channel = 0; channel < channels; ++channel) {
                thresholds[channel] *= calculateTopMean(
                    planes[channel], request, width, height, options.maxPercent, topValues);
            }
        }
    }

    std::array<double, 3> sums{};
    std::array<std::size_t, 3> acceptedCounts{};
    std::size_t geometricCount = 0;
    if (options.filterMode == 2) {
        const auto selected = static_cast<std::size_t>(options.xyzChannel);
        forEachFilterPixel(request, width, height, [&](std::size_t pixelIndex) {
            if (planes[selected][pixelIndex] < thresholds[selected]) return;
            for (std::int32_t channel = 0; channel < channels; ++channel) {
                sums[channel] += static_cast<float>(planes[channel][pixelIndex]);
            }
            ++geometricCount;
        });
        acceptedCounts.fill(geometricCount);
    }
    else {
        forEachFilterPixel(request, width, height, [&](std::size_t pixelIndex) {
            ++geometricCount;
            for (std::int32_t channel = 0; channel < channels; ++channel) {
                const float value = planes[channel][pixelIndex];
                if (value >= thresholds[channel]) {
                    sums[channel] += static_cast<float>(value);
                    ++acceptedCounts[channel];
                }
            }
        });
        if (options.filterMode == 3) acceptedCounts.fill(geometricCount);
    }

    std::array<float, 3> averages{};
    for (std::int32_t channel = 0; channel < channels; ++channel) {
        if (acceptedCounts[channel] != 0) {
            averages[channel] = static_cast<float>(sums[channel] / acceptedCounts[channel]);
        }
    }
    return averages;
}

std::array<float, 3> calculateUnfilteredAverages(
    const float* channelX,
    const float* channelY,
    const float* channelZ,
    std::int32_t width,
    std::int32_t height,
    std::int32_t channels,
    const RequestV1& request) noexcept
{
    const ChannelAverage average = request.type == 2
        ? calculateRectangle(channelX, channelY, channelZ, width, height, channels,
            request.x, request.y, request.width, request.height)
        : calculateCircle(channelX, channelY, channelZ, width, height, channels,
            request.x, request.y, request.type == 0 ? 1.0 : request.width / 2.0);
    if (channels == 1) return { 0.0F, static_cast<float>(average.y / average.count), 0.0F };
    return {
        static_cast<float>(average.x / average.count),
        static_cast<float>(average.y / average.count),
        static_cast<float>(average.z / average.count)
    };
}

ResultV1 makeResult(std::array<float, 3> averages, std::int32_t channels, const OptionsV2& options) noexcept
{
    ResultV1 result{};
    if (channels == 1) {
        result.Y = averages[1];
        return result;
    }

    result.X = averages[0];
    result.Y = averages[1];
    result.Z = averages[2];
    if ((options.flags & ApplyMnp) != 0) {
        result.X = static_cast<float>(result.X * options.scaleX);
        result.Y = static_cast<float>(result.Y * options.scaleY);
        result.Z = static_cast<float>(result.Z * options.scaleZ);
    }
    if (result.X <= 0.0F) result.X = 0.000001F;
    if (result.Y <= 0.0F) result.Y = 0.000001F;
    if (result.Z <= 0.0F) result.Z = 0.000001F;

    const float sum = result.X + result.Y + result.Z;
    result.x = result.X / sum;
    result.y = result.Y / sum;
    const float uvDenominator = result.X + result.Y * 15.0F + result.Z * 3.0F;
    if (uvDenominator != 0.0F) {
        result.u = 4.0F * result.X / uvDenominator;
        result.v = 9.0F * result.Y / uvDenominator;
    }
    result.cct = static_cast<float>(calculateCct(result.x, result.y));
    result.wave = static_cast<float>(calculateMainWave(result.x, result.y));
    return result;
}

} // namespace

bool calculateBatchV2(
    std::int32_t width,
    std::int32_t height,
    std::int32_t bitsPerChannel,
    std::int32_t channels,
    const float* cieData,
    std::size_t cieFloatCount,
    const RequestV1* requests,
    std::uint32_t requestCount,
    const OptionsV2* options,
    ResultV1* results)
{
    if (width <= 0 || height <= 0 || bitsPerChannel != 32
        || (channels != 1 && channels != 3)
        || cieData == nullptr || requests == nullptr || requestCount == 0
        || options == nullptr || results == nullptr || !validateOptions(*options, channels)) {
        return false;
    }
    const auto widthSize = static_cast<std::size_t>(width);
    const auto heightSize = static_cast<std::size_t>(height);
    if (widthSize > (std::numeric_limits<std::size_t>::max)() / heightSize) return false;
    const std::size_t pixelCount = widthSize * heightSize;
    if (pixelCount > (std::numeric_limits<std::size_t>::max)() / static_cast<std::size_t>(channels)
        || cieFloatCount < pixelCount * static_cast<std::size_t>(channels)) {
        return false;
    }
    for (std::uint32_t index = 0; index < requestCount; ++index) {
        if (!isValidRequest(requests[index], width, height)) return false;
    }

    if (options->filterMode == 0 && options->flags == 0) {
        return calculateBatchV1(width, height, bitsPerChannel, channels, cieData, cieFloatCount,
            requests, requestCount, results);
    }

    const float* channelX = cieData;
    const float* channelY = channels == 3 ? channelX + pixelCount : nullptr;
    const float* channelZ = channels == 3 ? channelY + pixelCount : nullptr;
    const std::array<const float*, 3> planes = channels == 3
        ? std::array<const float*, 3>{ channelX, channelY, channelZ }
        : std::array<const float*, 3>{ channelX, nullptr, nullptr };
    std::vector<float> topValues;
    for (std::uint32_t index = 0; index < requestCount; ++index) {
        std::array<float, 3> averages;
        if (options->filterMode == 0) {
            averages = calculateUnfilteredAverages(
                channelX, channelY, channelZ, width, height, channels, requests[index]);
        }
        else {
            averages = calculateFilteredAverages(
                planes, channels, requests[index], width, height, *options, topValues);
            if (channels == 1) averages[1] = averages[0];
        }
        results[index] = makeResult(averages, channels, *options);
    }
    return true;
}

} // namespace cvcore::poi
