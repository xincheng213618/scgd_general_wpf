#pragma once

#include <opencv2/opencv.hpp>
#include <string>
#include <vector>

namespace cvcore {
namespace surface_defect {

struct SurfaceDefectConfig
{
    int channel = -1;
    std::vector<int> scales = { 31, 61, 121 };
    double darkThreshold = 0.015;
    double brightThreshold = 0.015;
    int minArea = 8;
    int maxArea = 200000;
    int muraMinArea = 1000;
    int openKernel = 1;
    int closeKernel = 3;
    int mergeDistance = 3;
    int maxDefects = 1000;
    bool enableDark = true;
    bool enableBright = true;
    bool enableLineDetect = true;
    double lineAspectRatio = 8.0;
    double minSeverity = 0.0;
    double minorSeverity = 0.25;
    double majorSeverity = 1.0;
    double criticalSeverity = 3.0;
};

struct SurfaceDefectItem
{
    int id = 0;
    int scale = 0;
    std::string type;
    std::string polarity;
    cv::Rect boundingRect;
    cv::Point2d center;
    int area = 0;
    double meanDelta = 0.0;
    double minDelta = 0.0;
    double maxDelta = 0.0;
    double maxDeltaAbs = 0.0;
    double severity = 0.0;
    double aspectRatio = 0.0;
    double fillRatio = 0.0;
};

struct SurfaceDefectSummary
{
    int defectCount = 0;
    int darkCount = 0;
    int brightCount = 0;
    double maxSeverity = 0.0;
    double meanSeverity = 0.0;
    std::string grade = "ok";
};

struct SurfaceDefectResult
{
    bool success = false;
    std::string statusCode = "not_run";
    std::string message;
    cv::Size imageSize;
    std::vector<SurfaceDefectItem> defects;
    SurfaceDefectSummary summary;
};

SurfaceDefectResult detectSurfaceDefects(const cv::Mat& image, const SurfaceDefectConfig& config);

} // namespace surface_defect
} // namespace cvcore
