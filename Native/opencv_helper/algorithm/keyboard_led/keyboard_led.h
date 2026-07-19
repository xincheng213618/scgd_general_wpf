#pragma once

#include <nlohmann/json_fwd.hpp>
#include <opencv2/opencv.hpp>

#include <string>
#include <vector>

namespace cvcore {
namespace keyboard_led {

// Three-channel input is interpreted as BGR. All reported brightness values
// are normalized to [0, 1], independently of whether the source is 8 or 16 bit.
enum class GrayMode
{
    MaxChannel,
    AverageChannels,
    Luminance,
    Channel
};

struct GrayConfig
{
    GrayMode mode = GrayMode::MaxChannel;
    int channel = 0;
    double validMin = 0.0;
    double validMax = 1.0;
};

struct KeyHaloConfig
{
    GrayConfig gray;
    double innerInsetRatio = 0.15;
    double haloGapRatio = 0.10;
    double haloWidthRatio = 0.25;
    bool excludeKeyRectsFromHalo = true;
    int minimumValidPixels = 1;
};

struct KeyHaloMeasurement
{
    int id = 0;
    cv::Rect inputRect;
    cv::Rect clippedKeyRect;
    cv::Rect innerRect;
    cv::Rect haloBounds;
    int keyPixelCount = 0;
    int keyValidPixelCount = 0;
    int haloPixelCount = 0;
    int haloValidPixelCount = 0;
    double keyMean = 0.0;
    double haloMean = 0.0;
    double haloToKeyRatio = 0.0;
    bool ratioValid = false;
    std::string status = "not_run";
    std::vector<std::string> warnings;
};

struct KeyHaloResult
{
    bool success = false;
    std::string statusCode = "not_run";
    std::string message;
    cv::Size imageSize;
    std::vector<std::string> warnings;
    std::vector<KeyHaloMeasurement> keys;
};

KeyHaloResult measureKeyHalo(
    const cv::Mat& image,
    const std::vector<cv::Rect>& keyRects,
    const KeyHaloConfig& config = {});

nlohmann::json ToJson(const KeyHaloMeasurement& measurement);
nlohmann::json ToJson(const KeyHaloResult& result);

struct LedDetection
{
    int id = 0;
    cv::Point2d center;
    cv::Rect boundingRect;
    double area = 0.0;
};

struct LedArrayConfig
{
    GrayConfig gray;
    int rows = 0;
    int cols = 0;
    int sampleRadius = 3;
    double assignmentGateRatio = 0.45;
    double assignmentGatePixels = 0.0;
    double autoClusterTolerancePixels = 0.0;
    double autoClusterSeparationRatio = 3.0;
    double minimumBrightness = -1.0;
    double minimumArea = -1.0;
    double maximumArea = -1.0;
    int maximumDetections = 4096;
    int maximumExpectedPoints = 100000;
};

struct LedArrayPoint
{
    int id = 0;
    int sourceId = 0;
    int sourceIndex = -1;
    int row = -1; // Zero-based; -1 denotes an extra detection.
    int col = -1; // Zero-based; -1 denotes an extra detection.
    bool detected = false;
    cv::Point2d expectedCenter;
    cv::Point2d detectedCenter;
    cv::Point2d offset;
    double assignmentDistance = 0.0;
    double brightness = 0.0;
    bool brightnessValid = false;
    int samplePixelCount = 0;
    int validPixelCount = 0;
    double area = 0.0;
    std::string status = "not_run";
    std::vector<std::string> warnings;
};

struct LedArrayResult
{
    bool success = false;
    std::string statusCode = "not_run";
    std::string message;
    cv::Size imageSize;
    std::vector<std::string> warnings;
    int rows = 0;
    int cols = 0;
    int expectedCount = 0;
    int detectedCount = 0;
    int matchedCount = 0;
    int missingCount = 0;
    int extraCount = 0;
    int normalCount = 0;
    int abnormalCount = 0;
    double rowSpacing = 0.0;
    double colSpacing = 0.0;
    double angleDegrees = 0.0;
    std::vector<LedArrayPoint> points;
};

LedArrayResult analyzeLedArray(
    const cv::Mat& image,
    const std::vector<LedDetection>& detections,
    const LedArrayConfig& config = {});

nlohmann::json ToJson(const LedArrayPoint& point);
nlohmann::json ToJson(const LedArrayResult& result);

} // namespace keyboard_led
} // namespace cvcore
