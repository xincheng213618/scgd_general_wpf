#pragma once

#include <opencv2/opencv.hpp>

#include <array>
#include <string>
#include <vector>

namespace cvcore {
namespace distortion {

struct DistortionP9Config {
    int expectedRows = 3;
    int expectedCols = 3;
    double threshold = -1.0;
    bool brightTarget = true;
    int minRectSize = 40;
    int maxRectSize = 400;
    int minArea = 0;
    int maxArea = 0;
    int erodeKernel = 3;
    int erodeIterations = 0;
    int dilateIterations = 0;
    int maxCandidates = 64;
    int tvCalcWay = 0;
    bool sortWithPca = true;
};

struct DistortionP9Point {
    int id = 0;
    int row = 0;
    int col = 0;
    std::string name;
    cv::Point2d center;
    cv::Rect boundingRect;
    int area = 0;
};

struct DistortionP9Metric {
    double horizontalTvPercent = 0.0;
    double verticalTvPercent = 0.0;
    double topPercent = 0.0;
    double bottomPercent = 0.0;
    double leftPercent = 0.0;
    double rightPercent = 0.0;
    double keystoneHorizontalPercent = 0.0;
    double keystoneVerticalPercent = 0.0;
    double topWidth = 0.0;
    double middleWidth = 0.0;
    double bottomWidth = 0.0;
    double leftHeight = 0.0;
    double centerHeight = 0.0;
    double rightHeight = 0.0;
    double gridWidth = 0.0;
    double gridHeight = 0.0;
};

struct DistortionP9Result {
    bool success = false;
    std::string statusCode;
    std::string message;
    cv::Size imageSize;
    int candidateCount = 0;
    std::vector<std::string> warnings;
    std::vector<DistortionP9Point> candidatePoints;
    std::vector<DistortionP9Point> points;
    DistortionP9Metric metrics;
};

DistortionP9Result calculateDistortionP9(const cv::Mat& img, const DistortionP9Config& config = {});

} // namespace distortion
} // namespace cvcore
