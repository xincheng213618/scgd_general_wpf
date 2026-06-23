#include "sfr_bmw4.h"

#include <algorithm>
#include <cmath>
#include <numeric>

namespace cvcore {
namespace sfr {

namespace {

constexpr double PI = 3.141592653589793238462643383279502884;

const char* edgeName(int id)
{
    static constexpr const char* names[] = { "Left", "Top", "Right", "Bottom" };
    return (id >= 0 && id < 4) ? names[id] : "";
}

cv::Mat toGray8(const cv::Mat& img)
{
    if (img.empty()) {
        return {};
    }

    cv::Mat gray;
    if (img.channels() == 1) {
        gray = img;
    }
    else if (img.channels() == 3) {
        cv::cvtColor(img, gray, cv::COLOR_BGR2GRAY);
    }
    else if (img.channels() == 4) {
        cv::cvtColor(img, gray, cv::COLOR_BGRA2GRAY);
    }
    else {
        return {};
    }

    if (gray.depth() == CV_8U) {
        return gray.clone();
    }

    cv::Mat out;
    cv::normalize(gray, out, 0, 255, cv::NORM_MINMAX, CV_8U);
    return out;
}

int normalizedOddKernel(int value)
{
    if (value <= 1) {
        return 0;
    }

    return (value % 2 == 0) ? value + 1 : value;
}

cv::Mat makeDarkMask(const cv::Mat& gray8, const BmwSfr4Config& config)
{
    cv::Mat mask;
    if (config.threshold >= 0) {
        cv::threshold(gray8, mask, config.threshold, 255, cv::THRESH_BINARY_INV);
    }
    else {
        cv::threshold(gray8, mask, 0, 255, cv::THRESH_BINARY_INV | cv::THRESH_OTSU);
    }

    int openKernel = normalizedOddKernel(config.openKernel);
    if (openKernel > 0) {
        cv::Mat kernel = cv::getStructuringElement(cv::MORPH_ELLIPSE, cv::Size(openKernel, openKernel));
        cv::morphologyEx(mask, mask, cv::MORPH_OPEN, kernel);
    }

    int closeKernel = normalizedOddKernel(config.closeKernel);
    if (closeKernel > 0) {
        cv::Mat kernel = cv::getStructuringElement(cv::MORPH_ELLIPSE, cv::Size(closeKernel, closeKernel));
        cv::morphologyEx(mask, mask, cv::MORPH_CLOSE, kernel);
    }

    return mask;
}

double normalizeHalfTurn(double angle)
{
    while (angle <= -PI / 2.0) {
        angle += PI;
    }
    while (angle > PI / 2.0) {
        angle -= PI;
    }
    return angle;
}

double estimateTargetAngle(const cv::Mat& labels, int labelId, const cv::Rect& rect)
{
    std::vector<cv::Point2d> points;
    points.reserve(static_cast<size_t>(rect.area()));

    for (int y = rect.y; y < rect.y + rect.height; ++y) {
        const int* row = labels.ptr<int>(y);
        for (int x = rect.x; x < rect.x + rect.width; ++x) {
            if (row[x] == labelId) {
                points.emplace_back(static_cast<double>(x), static_cast<double>(y));
            }
        }
    }

    if (points.size() < 8) {
        return 0.0;
    }

    cv::Mat data(static_cast<int>(points.size()), 2, CV_64F);
    for (int i = 0; i < static_cast<int>(points.size()); ++i) {
        data.at<double>(i, 0) = points[i].x;
        data.at<double>(i, 1) = points[i].y;
    }

    cv::PCA pca(data, cv::Mat(), cv::PCA::DATA_AS_ROW);
    if (pca.eigenvectors.empty()) {
        return 0.0;
    }

    const double vx = pca.eigenvectors.at<double>(0, 0);
    const double vy = pca.eigenvectors.at<double>(0, 1);
    const double principalAngle = std::atan2(vy, vx);

    return normalizeHalfTurn(principalAngle + PI / 4.0);
}

bool isTargetCandidate(const cv::Rect& rect, int area, const cv::Size& imageSize, const BmwSfr4Config& config)
{
    if (area < config.minTargetArea) {
        return false;
    }
    if (config.maxTargetArea > 0 && area > config.maxTargetArea) {
        return false;
    }
    if (rect.width <= 0 || rect.height <= 0) {
        return false;
    }

    const double aspect = static_cast<double>(rect.width) / static_cast<double>(rect.height);
    if (aspect < config.minAspectRatio || aspect > config.maxAspectRatio) {
        return false;
    }

    if (config.requireFullTarget) {
        const int margin = std::max(0, config.borderMargin);
        if (rect.x <= margin || rect.y <= margin ||
            rect.x + rect.width >= imageSize.width - margin ||
            rect.y + rect.height >= imageSize.height - margin) {
            return false;
        }
    }

    return true;
}

cv::Rect makeCenteredRoi(const cv::Point2d& center, int width, int height, const cv::Size& bounds)
{
    width = std::max(1, width);
    height = std::max(1, height);

    cv::Rect roi(
        static_cast<int>(std::round(center.x - width / 2.0)),
        static_cast<int>(std::round(center.y - height / 2.0)),
        width,
        height);

    cv::Rect imageRect(0, 0, bounds.width, bounds.height);
    return ((roi & imageRect) == roi) ? roi : cv::Rect();
}

std::array<cv::Point2d, 4> edgeCenters(const BmwSfr4Point& point, const BmwSfr4Config& config)
{
    const double halfW = std::max(1.0, point.targetRect.width * 0.5);
    const double halfH = std::max(1.0, point.targetRect.height * 0.5);
    const double ox = std::clamp(config.edgeOffsetRatio, 0.1, 0.85) * halfW;
    const double oy = std::clamp(config.edgeOffsetRatio, 0.1, 0.85) * halfH;

    const double c = std::cos(point.angleRadians);
    const double s = std::sin(point.angleRadians);
    const cv::Point2d axisX(c, s);
    const cv::Point2d axisY(-s, c);

    return {
        point.center - axisX * ox,
        point.center - axisY * oy,
        point.center + axisX * ox,
        point.center + axisY * oy
    };
}

std::vector<BmwSfr4Point> findTargets(const cv::Mat& img, const BmwSfr4Config& config)
{
    cv::Mat gray8 = toGray8(img);
    if (gray8.empty()) {
        return {};
    }

    cv::Mat mask = makeDarkMask(gray8, config);

    cv::Mat labels;
    cv::Mat stats;
    cv::Mat centroids;
    const int count = cv::connectedComponentsWithStats(mask, labels, stats, centroids, 8, CV_32S);

    std::vector<BmwSfr4Point> points;
    const cv::Size imageSize(gray8.cols, gray8.rows);
    for (int label = 1; label < count; ++label) {
        const int x = stats.at<int>(label, cv::CC_STAT_LEFT);
        const int y = stats.at<int>(label, cv::CC_STAT_TOP);
        const int w = stats.at<int>(label, cv::CC_STAT_WIDTH);
        const int h = stats.at<int>(label, cv::CC_STAT_HEIGHT);
        const int area = stats.at<int>(label, cv::CC_STAT_AREA);
        const cv::Rect rect(x, y, w, h);

        if (!isTargetCandidate(rect, area, imageSize, config)) {
            continue;
        }

        BmwSfr4Point point;
        point.targetRect = rect;
        point.center = cv::Point2d(centroids.at<double>(label, 0), centroids.at<double>(label, 1));
        point.angleRadians = config.usePcaAngle ? estimateTargetAngle(labels, label, rect) : 0.0;
        points.push_back(std::move(point));
    }

    std::sort(points.begin(), points.end(), [](const BmwSfr4Point& a, const BmwSfr4Point& b) {
        const double rowTol = std::max(a.targetRect.height, b.targetRect.height) * 0.5;
        if (std::abs(a.center.y - b.center.y) > rowTol) {
            return a.center.y < b.center.y;
        }
        return a.center.x < b.center.x;
    });

    if (config.maxTargets > 0 && static_cast<int>(points.size()) > config.maxTargets) {
        points.resize(static_cast<size_t>(config.maxTargets));
    }

    for (int i = 0; i < static_cast<int>(points.size()); ++i) {
        points[i].name = "Point_" + std::to_string(i + 1);
    }

    return points;
}

} // namespace

BmwSfr4Result calculateBmwSfr4In1(const cv::Mat& img, const BmwSfr4Config& config)
{
    BmwSfr4Result output;
    if (img.empty()) {
        return output;
    }

    std::vector<BmwSfr4Point> candidates = findTargets(img, config);
    const cv::Size bounds(img.cols, img.rows);

    for (auto& point : candidates) {
        const auto centers = edgeCenters(point, config);
        for (int id = 0; id < 4; ++id) {
            if (!config.activeEdges[static_cast<size_t>(id)]) {
                continue;
            }

            cv::Rect roi = makeCenteredRoi(centers[static_cast<size_t>(id)],
                config.roiWidth, config.roiHeight, bounds);
            if (roi.empty()) {
                continue;
            }

            cv::Mat crop = img(roi);
            SFRResult sfr = calculateSlantedEdgeSFR(
                crop,
                config.pixelPitch,
                config.polynomialDegree,
                config.binning);
            if (!sfr.isValid()) {
                continue;
            }

            BmwSfr4Curve curve;
            curve.id = id;
            curve.name = edgeName(id);
            curve.roi = roi;
            curve.sfr = std::move(sfr);
            point.curves.push_back(std::move(curve));
        }

        if (!config.requireFourCurves || point.curves.size() == 4) {
            output.points.push_back(std::move(point));
        }
    }

    for (int i = 0; i < static_cast<int>(output.points.size()); ++i) {
        output.points[i].name = "Point_" + std::to_string(i + 1);
    }

    return output;
}

} // namespace sfr
} // namespace cvcore
