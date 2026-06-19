#pragma once

#include "sfr_slanted.h"
#include <array>
#include <string>
#include <vector>

namespace cvcore {
namespace sfr {

struct BmwSfr4Config {
    double pixelPitch = 1.0;
    int polynomialDegree = 5;
    int binning = 4;
    int threshold = -1;
    int minTargetArea = 1000;
    int maxTargetArea = 0;
    int maxTargets = 64;
    int roiWidth = 60;
    int roiHeight = 60;
    int maxCurveLength = 0;
    double edgeOffsetRatio = 0.45;
    double minAspectRatio = 0.55;
    double maxAspectRatio = 1.85;
    int closeKernel = 21;
    int openKernel = 3;
    int borderMargin = 2;
    bool requireFullTarget = true;
    bool requireFourCurves = true;
    bool usePcaAngle = true;
    std::array<bool, 4> activeEdges{ true, true, true, true };
};

struct BmwSfr4Curve {
    int id = 0;
    std::string name;
    cv::Rect roi;
    SFRResult sfr;
};

struct BmwSfr4Point {
    std::string name;
    cv::Rect targetRect;
    cv::Point2d center;
    double angleRadians = 0.0;
    std::vector<BmwSfr4Curve> curves;
};

struct BmwSfr4Result {
    std::vector<BmwSfr4Point> points;
};

BmwSfr4Result calculateBmwSfr4In1(const cv::Mat& img, const BmwSfr4Config& config = {});

} // namespace sfr
} // namespace cvcore
