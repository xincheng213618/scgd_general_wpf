#pragma once

#include <array>
#include <string>
#include <vector>

#include <opencv2/core.hpp>

namespace cvnative::find_cross
{

enum class PatternPolarity
{
    Auto,
    Bright,
    Dark
};

struct PatternCrossConfig
{
    PatternPolarity polarity = PatternPolarity::Auto;
    double expectedAngleDegrees = 0.0;
    double angleToleranceDegrees = 10.0;
    double minContrast = 0.01;
    double minArmLengthPixels = 40.0;
    double minArmCoverage = 0.50;
    double minConfidence = 0.35;
    int maxProcessingSize = 1600;
};

struct PatternArmQuality
{
    double coverage = 0.0;
    double contrast = 0.0;
    double span = 0.0;
    double fitRms = 0.0;
    int sampleCount = 0;
    int inlierCount = 0;
};

struct PatternCrossResult
{
    bool success = false;
    cv::Point2d center{};
    double primaryAngleDegrees = 0.0;
    double secondaryAngleDegrees = 90.0;
    double combinedAngleDegrees = 0.0;
    double orthogonalityErrorDegrees = 0.0;
    double confidence = 0.0;
    double patternContrast = 0.0;
    std::string polarityUsed;
    std::array<cv::Point2d, 4> armEndpoints{};
    std::array<PatternArmQuality, 4> armQuality{};
    std::string failureReason;
    std::vector<std::string> warnings;
};

PatternCrossResult FindPatternCross(
    const cv::Mat& image,
    const PatternCrossConfig& config);

} // namespace cvnative::find_cross
