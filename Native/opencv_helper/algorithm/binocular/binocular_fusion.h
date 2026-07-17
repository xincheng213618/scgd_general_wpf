#pragma once

#include <nlohmann/json.hpp>
#include <opencv2/opencv.hpp>

#include <string>
#include <vector>

namespace cvcore {
namespace binocular {

// threshold <= 0 enables Otsu. pixelSize is um; focal/image distances are mm.
// Negative opticalCenter coordinates select the complete-image center.
struct BinocularFusionConfig
{
    double threshold = -1.0;
    int blurKernel = 5;
    int morphKernel = 3;
    int minArea = 20;
    int maxArea = 0;
    double pixelSize = 3.76;
    double focalLength = 30.0;
    double virtualImageDistance = 0.0;
    cv::Point2d opticalCenter = cv::Point2d(-1.0, -1.0);
    int maxCandidates = 128;
};

struct BinocularCrossCandidate
{
    int id = -1;
    cv::Point2d center;
    cv::Rect boundingRect;
    int area = 0;
    double shapeScore = 0.0;
};

struct BinocularCrossPoint
{
    std::string role;
    int candidateId = -1;
    cv::Point2d center;
    cv::Rect boundingRect;
    int area = 0;
    double shapeScore = 0.0;
};

struct BinocularCrossSet
{
    BinocularCrossPoint center;
    BinocularCrossPoint top;
    BinocularCrossPoint bottom;
    BinocularCrossPoint left;
    BinocularCrossPoint right;
};

struct BinocularFusionResult
{
    bool success = false;
    std::string statusCode = "not_run";
    std::string message;
    cv::Size imageSize;
    cv::Rect searchRoi;
    cv::Point2d effectiveOpticalCenter;
    std::vector<BinocularCrossCandidate> candidates;
    BinocularCrossSet points;
    double rollDegrees = 0.0;
    double horizontalOffsetDegrees = 0.0;
    double verticalOffsetDegrees = 0.0;
    double imageDistance = 0.0;
    double quality = 0.0;
    std::vector<std::string> warnings;
};

BinocularFusionResult calculateBinocularFusion(
    const cv::Mat& image,
    const cv::Rect& roi = {},
    const BinocularFusionConfig& config = {});

nlohmann::json ToJson(const BinocularCrossCandidate& candidate);
nlohmann::json ToJson(const BinocularCrossPoint& point);
nlohmann::json ToJson(const BinocularFusionResult& result);

} // namespace binocular
} // namespace cvcore
