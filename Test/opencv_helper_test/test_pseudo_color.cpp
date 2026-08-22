#ifndef NOMINMAX
#define NOMINMAX
#endif

#include <opencv2/opencv.hpp>

#include "../../Native/include/opencv_media_export.h"

#include <algorithm>
#include <array>
#include <cstdint>
#include <cstring>
#include <iostream>
#include <string>
#include <utility>

namespace
{
constexpr std::array<cv::ColormapTypes, 22> Colormaps = {
    cv::COLORMAP_AUTUMN,
    cv::COLORMAP_BONE,
    cv::COLORMAP_JET,
    cv::COLORMAP_WINTER,
    cv::COLORMAP_RAINBOW,
    cv::COLORMAP_OCEAN,
    cv::COLORMAP_SUMMER,
    cv::COLORMAP_SPRING,
    cv::COLORMAP_COOL,
    cv::COLORMAP_HSV,
    cv::COLORMAP_PINK,
    cv::COLORMAP_HOT,
    cv::COLORMAP_PARULA,
    cv::COLORMAP_MAGMA,
    cv::COLORMAP_INFERNO,
    cv::COLORMAP_PLASMA,
    cv::COLORMAP_VIRIDIS,
    cv::COLORMAP_CIVIDIS,
    cv::COLORMAP_TWILIGHT,
    cv::COLORMAP_TWILIGHT_SHIFTED,
    cv::COLORMAP_TURBO,
    cv::COLORMAP_DEEPGREEN,
};

HImage MakeImageView(cv::Mat& image)
{
    HImage view{};
    view.rows = image.rows;
    view.cols = image.cols;
    view.channels = image.channels();
    view.depth = CvDepthToHImageDepth(image.depth());
    view.stride = static_cast<int>(image.step);
    view.isDispose = false;
    view.pData = image.data;
    return view;
}

cv::Mat BuildReferenceLut(cv::ColormapTypes mapType, int minValue, int maxValue, bool stretched)
{
    cv::Mat range(256, 1, CV_8U);
    for (int value = 0; value < 256; ++value) {
        range.at<std::uint8_t>(value) = static_cast<std::uint8_t>(value);
    }

    cv::Mat fullColormap;
    cv::applyColorMap(range, fullColormap, mapType);
    if (!stretched) {
        cv::Mat lut = fullColormap.clone();
        cv::Vec3b* values = lut.ptr<cv::Vec3b>();
        if (minValue > 0) {
            std::memset(values, 0, static_cast<std::size_t>((std::min)(minValue, 256)) * sizeof(cv::Vec3b));
        }
        for (int value = (std::max)(maxValue + 1, 0); value < 256; ++value) {
            values[value] = cv::Vec3b(255, 255, 255);
        }
        return lut;
    }

    cv::Mat lut(256, 1, CV_8UC3);
    const cv::Vec3b* colors = fullColormap.ptr<cv::Vec3b>();
    cv::Vec3b* values = lut.ptr<cv::Vec3b>();
    const int rangeSize = maxValue - minValue;
    for (int value = 0; value < 256; ++value) {
        if (value < minValue) {
            values[value] = cv::Vec3b(0, 0, 0);
        }
        else if (value > maxValue) {
            values[value] = cv::Vec3b(255, 255, 255);
        }
        else {
            int colorIndex = rangeSize > 0
                ? static_cast<int>(static_cast<double>(value - minValue) / rangeSize * 255.0)
                : 128;
            values[value] = colors[std::clamp(colorIndex, 0, 255)];
        }
    }
    return lut;
}

cv::Mat ReferencePseudoColor(
    const cv::Mat& image,
    std::uint32_t minValue,
    std::uint32_t maxValue,
    cv::ColormapTypes mapType,
    int channel,
    bool autoRange,
    std::uint32_t dataMin,
    std::uint32_t dataMax)
{
    cv::Mat selected;
    if (image.channels() == 1) {
        selected = image;
    }
    else if (channel >= 0 && channel < image.channels()) {
        cv::extractChannel(image, selected, channel);
    }
    else {
        cv::cvtColor(image, selected, cv::COLOR_BGR2GRAY);
    }

    double scale = 1.0;
    double offset = 0.0;
    cv::Mat normalized;
    if (selected.depth() == CV_32F || selected.depth() == CV_64F) {
        cv::normalize(selected, normalized, 0, 255, cv::NORM_MINMAX, CV_8U);
        selected = normalized;
        if (autoRange) {
            minValue = 0;
            maxValue = 255;
        }
    }
    if (selected.depth() == CV_16U) {
        scale = 1.0 / 257.0;
        if (autoRange) {
            scale = dataMax > dataMin ? 255.0 / (dataMax - dataMin) : 1.0;
            offset = -static_cast<double>(dataMin) * scale;
            if (dataMax > dataMin) {
                const double range = static_cast<double>(dataMax) - dataMin;
                minValue = static_cast<std::uint32_t>(std::clamp(
                    (static_cast<double>(minValue) - dataMin) / range * 255.0, 0.0, 255.0));
                maxValue = static_cast<std::uint32_t>(std::clamp(
                    (static_cast<double>(maxValue) - dataMin) / range * 255.0, 0.0, 255.0));
            }
        }
        else {
            minValue >>= 8;
            maxValue >>= 8;
        }
    }

    minValue = (std::min)(minValue, 255u);
    maxValue = (std::min)(maxValue, 255u);
    cv::Mat lut = BuildReferenceLut(
        mapType,
        static_cast<int>(minValue),
        static_cast<int>(maxValue),
        autoRange);
    const cv::Vec3b* lutValues = lut.ptr<cv::Vec3b>();
    cv::Mat expected(selected.rows, selected.cols, CV_8UC3);

    for (int y = 0; y < selected.rows; ++y) {
        cv::Vec3b* destination = expected.ptr<cv::Vec3b>(y);
        if (selected.depth() == CV_16U) {
            const std::uint16_t* source = selected.ptr<std::uint16_t>(y);
            for (int x = 0; x < selected.cols; ++x) {
                const std::uint8_t index = cv::saturate_cast<std::uint8_t>(source[x] * scale + offset);
                destination[x] = lutValues[index];
            }
        }
        else {
            const std::uint8_t* source = selected.ptr<std::uint8_t>(y);
            for (int x = 0; x < selected.cols; ++x) {
                destination[x] = lutValues[source[x]];
            }
        }
    }
    return expected;
}

bool RunIntoCase(
    cv::Mat& source,
    std::uint32_t minValue,
    std::uint32_t maxValue,
    cv::ColormapTypes mapType,
    int channel,
    bool autoRange = false,
    std::uint32_t dataMin = 0,
    std::uint32_t dataMax = 0)
{
    cv::Mat outputStorage(source.rows, source.cols + 2, CV_8UC3, cv::Scalar(17, 31, 47));
    cv::Mat originalStorage = outputStorage.clone();
    cv::Mat output = outputStorage(cv::Rect(1, 0, source.cols, source.rows));
    HImage sourceView = MakeImageView(source);
    HImage outputView = MakeImageView(output);
    const int result = autoRange
        ? M_PseudoColorAutoRangeInto(
            sourceView, outputView, minValue, maxValue, mapType, channel, dataMin, dataMax)
        : M_PseudoColorInto(sourceView, outputView, minValue, maxValue, mapType, channel);
    if (result != 0) {
        std::cerr << "Pseudo-color export returned " << result << " for map " << static_cast<int>(mapType) << std::endl;
        return false;
    }

    cv::Mat expected = ReferencePseudoColor(
        source, minValue, maxValue, mapType, channel, autoRange, dataMin, dataMax);
    if (cv::norm(expected, output, cv::NORM_INF) != 0.0) {
        std::cerr << "Pseudo-color pixels differ for map " << static_cast<int>(mapType)
            << ", depth " << source.depth() << ", channel " << channel
            << ", autoRange " << autoRange << std::endl;
        return false;
    }

    if (cv::norm(originalStorage.col(0), outputStorage.col(0), cv::NORM_INF) != 0.0
        || cv::norm(originalStorage.col(outputStorage.cols - 1), outputStorage.col(outputStorage.cols - 1), cv::NORM_INF) != 0.0) {
        std::cerr << "Pseudo-color export overwrote destination padding." << std::endl;
        return false;
    }
    return true;
}

bool RunOwnedCase(
    cv::Mat& source,
    std::uint32_t minValue,
    std::uint32_t maxValue,
    cv::ColormapTypes mapType,
    int channel,
    bool autoRange,
    std::uint32_t dataMin,
    std::uint32_t dataMax)
{
    HImage owned{};
    HImage sourceView = MakeImageView(source);
    const int result = autoRange
        ? M_PseudoColorAutoRange(
            sourceView, &owned, minValue, maxValue, mapType, channel, dataMin, dataMax)
        : M_PseudoColor(sourceView, &owned, minValue, maxValue, mapType, channel);
    if (result != 0 || owned.pData == nullptr) {
        std::cerr << "Owned pseudo-color export failed with " << result << std::endl;
        return false;
    }

    cv::Mat actual(owned.rows, owned.cols, CV_8UC3, owned.pData, owned.stride);
    cv::Mat expected = ReferencePseudoColor(
        source, minValue, maxValue, mapType, channel, autoRange, dataMin, dataMax);
    const bool success = cv::norm(expected, actual, cv::NORM_INF) == 0.0;
    M_FreeHImageData(static_cast<unsigned char*>(owned.pData));
    if (!success) {
        std::cerr << "Owned pseudo-color pixels differ." << std::endl;
    }
    return success;
}

cv::Mat Create8BitSource(cv::Mat& storage)
{
    storage.create(19, 265, CV_8UC4);
    cv::Mat source = storage(cv::Rect(3, 2, 257, 15));
    for (int y = 0; y < source.rows; ++y) {
        cv::Vec4b* row = source.ptr<cv::Vec4b>(y);
        for (int x = 0; x < source.cols; ++x) {
            row[x] = cv::Vec4b(
                static_cast<std::uint8_t>(x),
                static_cast<std::uint8_t>(255 - x),
                static_cast<std::uint8_t>(x * 73 + y * 19),
                static_cast<std::uint8_t>(x * 151 + y * 7));
        }
    }
    return source;
}

cv::Mat Create16BitSource(cv::Mat& storage)
{
    storage.create(258, 263, CV_16UC4);
    cv::Mat source = storage(cv::Rect(3, 1, 256, 256));
    for (int y = 0; y < source.rows; ++y) {
        cv::Vec<std::uint16_t, 4>* row = source.ptr<cv::Vec<std::uint16_t, 4>>(y);
        for (int x = 0; x < source.cols; ++x) {
            const std::uint32_t value = static_cast<std::uint32_t>(y * 256 + x);
            row[x] = cv::Vec<std::uint16_t, 4>(
                static_cast<std::uint16_t>(value),
                static_cast<std::uint16_t>(65535u - value),
                static_cast<std::uint16_t>(value * 25173u + 13849u),
                static_cast<std::uint16_t>(value * 40503u + 97u));
        }
    }
    return source;
}
}

bool RunPseudoColorTests()
{
    bool success = true;
    cv::Mat storage8;
    cv::Mat source8 = Create8BitSource(storage8);
    cv::Mat single8Storage(source8.rows, source8.cols + 2, CV_8U);
    cv::Mat single8 = single8Storage(cv::Rect(1, 0, source8.cols, source8.rows));
    cv::extractChannel(source8, single8, 1);
    for (cv::ColormapTypes mapType : Colormaps) {
        success &= RunIntoCase(source8, 23, 231, mapType, 2);
        success &= RunIntoCase(source8, 31, 207, mapType, 3, true, 0, 255);
        success &= RunIntoCase(single8, 23, 231, mapType, -1);
    }

    constexpr std::array<std::pair<std::uint32_t, std::uint32_t>, 9> Thresholds = {
        std::pair{ 0u, 0u },
        std::pair{ 0u, 1u },
        std::pair{ 0u, 255u },
        std::pair{ 1u, 254u },
        std::pair{ 127u, 128u },
        std::pair{ 128u, 127u },
        std::pair{ 254u, 255u },
        std::pair{ 255u, 255u },
        std::pair{ 300u, 10u },
    };
    for (const auto& [minValue, maxValue] : Thresholds) {
        success &= RunIntoCase(single8, minValue, maxValue, cv::COLORMAP_JET, -1);
        success &= RunIntoCase(single8, minValue, maxValue, cv::COLORMAP_TURBO, -1, true, 0, 255);
    }
    success &= RunIntoCase(source8, 17, 219, cv::COLORMAP_VIRIDIS, -1);

    cv::Mat storage16;
    cv::Mat source16 = Create16BitSource(storage16);
    for (cv::ColormapTypes mapType : Colormaps) {
        success &= RunIntoCase(source16, 4096, 61440, mapType, 2);
        success &= RunIntoCase(source16, 5000, 59000, mapType, 3, true, 1024, 63000);
    }
    cv::Mat single16;
    cv::extractChannel(source16, single16, 0);
    success &= RunIntoCase(single16, 4096, 61440, cv::COLORMAP_JET, -1);
    success &= RunIntoCase(single16, 5000, 59000, cv::COLORMAP_TURBO, -1, true, 1024, 63000);
    success &= RunIntoCase(single16, 123, 456, cv::COLORMAP_TURBO, -1, true, 4096, 4096);
    success &= RunIntoCase(source16, 4096, 61440, cv::COLORMAP_DEEPGREEN, -1);
    success &= RunOwnedCase(source16, 4096, 61440, cv::COLORMAP_JET, 1, false, 0, 0);
    success &= RunOwnedCase(source16, 5000, 59000, cv::COLORMAP_TURBO, 3, true, 1024, 63000);

    cv::Mat storage32(11, 19, CV_32FC4);
    cv::Mat source32 = storage32(cv::Rect(2, 1, 15, 9));
    for (int y = 0; y < source32.rows; ++y) {
        cv::Vec4f* row = source32.ptr<cv::Vec4f>(y);
        for (int x = 0; x < source32.cols; ++x) {
            row[x] = cv::Vec4f(
                static_cast<float>(x * 0.25 - y),
                static_cast<float>(y * 1.5 + x * x),
                static_cast<float>((x - 7) * (y + 0.5)),
                static_cast<float>(x * 3.25 - y * 2.5));
        }
    }
    success &= RunIntoCase(source32, 23, 231, cv::COLORMAP_VIRIDIS, 2);
    success &= RunIntoCase(source32, 23, 231, cv::COLORMAP_TURBO, 3, true, 0, 255);

    cv::Mat storage64(11, 19, CV_64FC4);
    cv::Mat source64 = storage64(cv::Rect(2, 1, 15, 9));
    source32.convertTo(source64, CV_64FC4, 1.75, -13.0);
    success &= RunIntoCase(source64, 31, 207, cv::COLORMAP_PLASMA, 1);
    success &= RunIntoCase(source64, 31, 207, cv::COLORMAP_DEEPGREEN, 0, true, 0, 255);

    if (success) {
        std::cout << "Pseudo-color regression tests passed." << std::endl;
    }
    return success;
}
