#pragma once

#include <array>
#include <string>
#include <vector>

#include <nlohmann/json.hpp>
#include <opencv2/core.hpp>

namespace cvnative::luminous
{

struct FindLuminousAreaV2Config
{
    double minConfidence = 0.25;
    double minAreaRatio = 0.001;
    double maxAreaRatio = 0.999;
    double searchWidthRatio = 0.18;
    double minEdgeContrast = 0.025;
    int caliperCount = 40;
    int maxProcessingSize = 1600;
    bool allowBorder = true;
};

struct LuminousSideQuality
{
    double coverage = 0.0;
    double inlierRatio = 0.0;
    double contrastP10 = 0.0;
    double fitRms = 0.0;
    double maxGap = 1.0;
    double confidence = 0.0;
    int sampleCount = 0;
    int inlierCount = 0;
};

struct FindLuminousAreaV2Result
{
    bool success = false;
    std::array<cv::Point2f, 4> corners{};
    bool hasCorners = false;
    double confidence = 0.0;
    std::array<LuminousSideQuality, 4> sideQuality{};
    std::string failureReason;
    std::vector<std::string> warnings;
};

// Returns false only when the JSON contains an invalid option. Unknown options
// are intentionally ignored so future versions remain configuration-compatible.
bool ParseFindLuminousAreaV2Config(
    const nlohmann::json& json,
    FindLuminousAreaV2Config& config,
    std::string& error);

// Algorithm-level rejection is represented by result.success == false. The
// function itself does not use error codes; the export layer owns ABI errors.
FindLuminousAreaV2Result FindLuminousAreaV2(
    const cv::Mat& image,
    const FindLuminousAreaV2Config& config);

} // namespace cvnative::luminous
