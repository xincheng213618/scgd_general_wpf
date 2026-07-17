#pragma once

#include <nlohmann/json_fwd.hpp>
#include <opencv2/opencv.hpp>

#include <string>
#include <vector>

namespace cvcore {
namespace ghost {

enum class ThresholdMode
{
    Automatic,
    Fixed
};

struct GhostDetectionConfig
{
    cv::Rect roi;
    int channel = -1;

    ThresholdMode brightThresholdMode = ThresholdMode::Automatic;
    ThresholdMode ghostThresholdMode = ThresholdMode::Automatic;
    double brightThreshold = 0.85;
    double ghostThreshold = 0.10;

    int brightGridRows = 1;
    int brightGridCols = 1;
    int brightMinArea = 16;
    int brightMaxArea = 0;
    int brightMinSize = 3;
    int brightMaxSize = 0;

    int ghostMinArea = 4;
    int ghostMaxArea = 200000;
    int ghostMinSize = 2;
    int ghostMaxSize = 0;

    int brightOpenKernel = 1;
    int brightCloseKernel = 3;
    int ghostOpenKernel = 1;
    int ghostCloseKernel = 3;
    int sourceMaskPadding = 2;

    double minDistanceFromBright = 10.0;
    double minRelativeIntensity = 0.0;
    int maxCandidates = 128;

    double minorSeverity = 0.25;
    double majorSeverity = 1.0;
    double criticalSeverity = 3.0;
};

struct BrightSource
{
    int id = 0;
    int gridRow = 0;
    int gridCol = 0;
    cv::Point2d center;
    cv::Point2d centerInRoi;
    cv::Rect boundingRect;
    cv::Rect boundingRectInRoi;
    int area = 0;
    double meanIntensity = 0.0;
    double peakIntensity = 0.0;
};

struct GhostCandidate
{
    int id = 0;
    cv::Point2d center;
    cv::Point2d centerInRoi;
    cv::Rect boundingRect;
    cv::Rect boundingRectInRoi;
    int area = 0;
    double meanIntensity = 0.0;
    double peakIntensity = 0.0;
    double relativeIntensity = 0.0;
    double peakRelativeIntensity = 0.0;
    int nearestBrightSourceId = 0;
    double distanceToNearestBright = 0.0;
    double severity = 0.0;
    std::string severityGrade = "trace";
};

struct GhostDetectionSummary
{
    int brightSourceCount = 0;
    int candidateCount = 0;
    double maxSeverity = 0.0;
    double meanSeverity = 0.0;
    std::string grade = "ok";
};

struct GhostDetectionResult
{
    bool success = false;
    std::string statusCode = "not_run";
    std::string message;
    std::vector<std::string> warnings;
    cv::Size imageSize;
    cv::Rect roi;
    double brightThresholdUsed = 0.0;
    double ghostThresholdUsed = 0.0;
    double backgroundMeanIntensity = 0.0;
    double referenceBrightMeanIntensity = 0.0;
    std::vector<BrightSource> brightSources;
    std::vector<GhostCandidate> candidates;
    GhostDetectionSummary summary;
};

// Thresholds and reported intensities are normalized to [0, 1] for both
// 8-bit and 16-bit input. Coordinates without the InRoi suffix are relative
// to the complete input image. This function never writes files or retains
// state between calls.
GhostDetectionResult detectGhosts(const cv::Mat& image, const GhostDetectionConfig& config = {}) noexcept;

nlohmann::json ToJson(const BrightSource& source);
nlohmann::json ToJson(const GhostCandidate& candidate);
nlohmann::json ToJson(const GhostDetectionResult& result);

} // namespace ghost
} // namespace cvcore
