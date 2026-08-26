#pragma once

#include <array>
#include <string>
#include <vector>

#include <nlohmann/json.hpp>
#include <opencv2/core.hpp>

#include "../luminous_area/luminous_area_v2.h"
#include "pattern_cross.h"

namespace cvnative::find_cross
{

enum class CenterMethod
{
    DiagonalIntersection,
    CornerAverage
};

enum class RotationMethod
{
    TopEdge,
    AllEdges
};

enum class DetectionMode
{
    PatternCross,
    OuterPanelAssist
};

struct DistortionParams
{
    bool enabled = false;
    double k1 = 0.0;
    double k2 = 0.0;
    double p1 = 0.0;
    double p2 = 0.0;
    double k3 = 0.0;
    bool intrinsicsSpecified = false;
    double fx = 0.0;
    double fy = 0.0;
    double cx = 0.0;
    double cy = 0.0;
};

struct OpticsParams
{
    cv::Point2d standardCenter{ 0.0, 0.0 };
    double focusLengthMm = 25.4;
    double sensorPixelSizeUm = 3.76;
    bool standardCenterSpecified = false;
    DistortionParams distortion;
};

struct FindCrossConfig
{
    luminous::FindLuminousAreaV2Config luminous;
    PatternCrossConfig pattern;
    OpticsParams optics;
    DetectionMode detectionMode = DetectionMode::PatternCross;
    std::string requestedDetectionMode = "PatternCross";
    std::string name = "Point_1";
    CenterMethod centerMethod = CenterMethod::DiagonalIntersection;
    RotationMethod rotationMethod = RotationMethod::AllEdges;
    std::string requestedCenterMethod = "DiagonalIntersection";
    std::string requestedRotationMethod = "AllEdges";
    cv::Point2d calibrationOffset{};
    std::vector<std::string> ignoredParameters;
    std::vector<std::string> warnings;
};

struct FindCrossResult
{
    luminous::FindLuminousAreaV2Result detection;
    PatternCrossResult pattern;
    bool patternMode = true;
    std::array<cv::Point2d, 4> globalCorners{};
    std::array<cv::Point2d, 4> rawGlobalArmEndpoints{};
    std::array<cv::Point2d, 4> globalArmEndpoints{};
    bool distortionApplied = false;
    bool hasCenter = false;
    cv::Point2d rawGlobalCenter{};
    cv::Point2d globalCenter{};
    double topEdgeRotationDegrees = 0.0;
    double allEdgesRotationDegrees = 0.0;
    double selectedRotationDegrees = 0.0;
    double tiltXDegrees = 0.0;
    double tiltYDegrees = 0.0;
    std::string centerMethodUsed;
    std::string rotationMethodUsed;
};

// Unknown fields are intentionally ignored so the adapter accepts the legacy
// on-site FindCross payload while exposing a small set of robust-V2 options.
bool ParseFindCrossConfig(
    const nlohmann::json& json,
    FindCrossConfig& config,
    std::string& error);

// The image is ROI-local. globalOffset is added to every reported coordinate.
FindCrossResult FindCross(
    const cv::Mat& image,
    const cv::Point2d& globalOffset,
    const FindCrossConfig& config);

} // namespace cvnative::find_cross
