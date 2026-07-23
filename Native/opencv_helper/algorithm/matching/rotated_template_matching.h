#pragma once

#include <nlohmann/json.hpp>
#include <opencv2/opencv.hpp>

#include <array>
#include <string>
#include <vector>

namespace cvcore {
namespace matching {

enum class TemplateFeatureMode
{
    Intensity,
    Gradient
};

// Angles are expressed in degrees. An empty ROI searches the complete source image.
struct RotatedTemplateMatchingConfig
{
    double angleMin = -15.0;
    double angleMax = 15.0;
    double angleStep = 1.0;
    double scoreThreshold = 0.80;
    int maxMatches = 10;
    double nmsRadius = 12.0;
    int pyramidLevels = 1;
    bool subpixel = false;
    double scaleMin = 1.0;
    double scaleMax = 1.0;
    double scaleStep = 0.05;
    TemplateFeatureMode featureMode = TemplateFeatureMode::Intensity;
    double occlusionTolerance = 0.0;
};

struct RotatedTemplateMatch
{
    cv::Point2d center;
    double angle = 0.0;
    double scale = 1.0;
    double score = 0.0;
    double rawScore = 0.0;
    // Fraction of active template samples retained by robust scoring.
    double visibleFraction = 1.0;
    cv::Rect2d boundingBox;
    std::array<cv::Point2d, 4> corners{};
};

struct RotatedTemplateMatchingResult
{
    bool success = false;
    std::string statusCode = "not_run";
    std::string message;
    cv::Size sourceSize;
    cv::Size templateSize;
    cv::Rect searchRoi;
    int evaluatedAngles = 0;
    int evaluatedScales = 0;
    int evaluatedModels = 0;
    int skippedAngles = 0;
    int candidateCount = 0;
    std::vector<std::string> warnings;
    std::vector<RotatedTemplateMatch> matches;
};

RotatedTemplateMatchingResult matchRotatedTemplate(
    const cv::Mat& source,
    const cv::Mat& templ,
    const cv::Rect& roi = {},
    const RotatedTemplateMatchingConfig& config = {});

nlohmann::json ToJson(const RotatedTemplateMatch& match);
nlohmann::json ToJson(const RotatedTemplateMatchingResult& result);

} // namespace matching
} // namespace cvcore
