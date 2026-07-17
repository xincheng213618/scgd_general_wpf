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

// Stereo calibration follows OpenCV's convention:
// X_right = rotation * X_left + translation. Translation and reconstructed
// points use millimetres. Distortion vectors accept OpenCV's 4/5/8/12/14
// coefficient layouts.
struct StereoCalibration
{
    cv::Matx33d leftCameraMatrix = cv::Matx33d::eye();
    cv::Matx33d rightCameraMatrix = cv::Matx33d::eye();
    std::vector<double> leftDistCoeffs;
    std::vector<double> rightDistCoeffs;
    cv::Matx33d rotation = cv::Matx33d::eye();
    cv::Vec3d translation = cv::Vec3d(0.0, 0.0, 0.0);
};

struct StereoBinocularConfig
{
    BinocularFusionConfig leftDetection;
    BinocularFusionConfig rightDetection;
    StereoCalibration calibration;
    double minimumParallaxPixels = 0.25;
    double maximumReprojectionErrorPixels = 2.0;
    bool requirePositiveDepth = true;
};

struct StereoCrossPoint
{
    std::string role;
    cv::Point2d leftPoint;
    cv::Point2d rightPoint;
    cv::Point3d point;
    double parallaxPixels = 0.0;
    double leftReprojectionErrorPixels = 0.0;
    double rightReprojectionErrorPixels = 0.0;
    double confidence = 0.0;
    bool valid = false;
    std::string status = "not_run";
};

struct StereoBinocularResult
{
    bool success = false;
    std::string statusCode = "not_run";
    std::string message;
    BinocularFusionResult left;
    BinocularFusionResult right;
    double baselineMm = 0.0;
    int validPointCount = 0;
    double meanDepthMm = 0.0;
    double meanReprojectionErrorPixels = 0.0;
    double confidence = 0.0;
    std::vector<StereoCrossPoint> points;
    std::vector<std::string> warnings;
};

BinocularFusionResult calculateBinocularFusion(
    const cv::Mat& image,
    const cv::Rect& roi = {},
    const BinocularFusionConfig& config = {});

StereoBinocularResult calculateStereoBinocularFusion(
    const cv::Mat& leftImage,
    const cv::Mat& rightImage,
    const cv::Rect& leftRoi,
    const cv::Rect& rightRoi,
    const StereoBinocularConfig& config);

nlohmann::json ToJson(const BinocularCrossCandidate& candidate);
nlohmann::json ToJson(const BinocularCrossPoint& point);
nlohmann::json ToJson(const BinocularFusionResult& result);
nlohmann::json ToJson(const StereoCrossPoint& point);
nlohmann::json ToJson(const StereoBinocularResult& result);

} // namespace binocular
} // namespace cvcore
