#include "distortion_p9.h"

#include <algorithm>
#include <array>
#include <cmath>
#include <numeric>

namespace cvcore {
namespace distortion {

namespace {

constexpr double Epsilon = 1e-9;

const std::array<const char*, 9> PointNames = {
    "TL", "TC", "TR",
    "ML", "C", "MR",
    "BL", "BC", "BR"
};

int normalizedOddKernel(int value)
{
    if (value <= 1) {
        return 0;
    }

    return (value % 2 == 0) ? value + 1 : value;
}

cv::Mat toGrayPreserveDepth(const cv::Mat& img)
{
    if (img.empty()) {
        return {};
    }

    if (img.channels() == 1) {
        return img;
    }

    cv::Mat gray;
    if (img.channels() == 3) {
        cv::cvtColor(img, gray, cv::COLOR_BGR2GRAY);
    }
    else if (img.channels() == 4) {
        cv::cvtColor(img, gray, cv::COLOR_BGRA2GRAY);
    }

    return gray;
}

cv::Mat normalizeTo8U(const cv::Mat& gray)
{
    cv::Mat gray8;
    if (gray.depth() == CV_8U) {
        return gray.clone();
    }

    cv::normalize(gray, gray8, 0, 255, cv::NORM_MINMAX, CV_8U);
    return gray8;
}

cv::Mat makePointMask(const cv::Mat& gray, const DistortionP9Config& config)
{
    cv::Mat mask;
    if (config.threshold >= 0.0) {
        cv::compare(gray, cv::Scalar(config.threshold), mask,
            config.brightTarget ? cv::CMP_GE : cv::CMP_LE);
    }
    else {
        cv::Mat gray8 = normalizeTo8U(gray);
        const int thresholdType = config.brightTarget
            ? (cv::THRESH_BINARY | cv::THRESH_OTSU)
            : (cv::THRESH_BINARY_INV | cv::THRESH_OTSU);
        cv::threshold(gray8, mask, 0, 255, thresholdType);
    }

    int kernelSize = normalizedOddKernel(config.erodeKernel);
    if (kernelSize > 0) {
        cv::Mat kernel = cv::getStructuringElement(cv::MORPH_ELLIPSE, cv::Size(kernelSize, kernelSize));
        if (config.erodeIterations > 0) {
            cv::erode(mask, mask, kernel, cv::Point(-1, -1), config.erodeIterations);
        }
        if (config.dilateIterations > 0) {
            cv::dilate(mask, mask, kernel, cv::Point(-1, -1), config.dilateIterations);
        }
    }

    return mask;
}

bool isCandidateRect(const cv::Rect& rect, int area, const DistortionP9Config& config)
{
    if (rect.width <= 0 || rect.height <= 0 || area <= 0) {
        return false;
    }

    const int minSide = std::min(rect.width, rect.height);
    const int maxSide = std::max(rect.width, rect.height);
    if (config.minRectSize > 0 && minSide < config.minRectSize) {
        return false;
    }
    if (config.maxRectSize > 0 && maxSide > config.maxRectSize) {
        return false;
    }
    if (config.minArea > 0 && area < config.minArea) {
        return false;
    }
    if (config.maxArea > 0 && area > config.maxArea) {
        return false;
    }

    return true;
}

std::vector<DistortionP9Point> findCandidates(const cv::Mat& gray, const DistortionP9Config& config)
{
    cv::Mat mask = makePointMask(gray, config);
    if (mask.empty()) {
        return {};
    }

    cv::Mat labels;
    cv::Mat stats;
    cv::Mat centroids;
    const int labelCount = cv::connectedComponentsWithStats(mask, labels, stats, centroids, 8, CV_32S);

    std::vector<DistortionP9Point> candidates;
    candidates.reserve(static_cast<size_t>(std::max(0, labelCount - 1)));
    for (int label = 1; label < labelCount; ++label) {
        cv::Rect rect(
            stats.at<int>(label, cv::CC_STAT_LEFT),
            stats.at<int>(label, cv::CC_STAT_TOP),
            stats.at<int>(label, cv::CC_STAT_WIDTH),
            stats.at<int>(label, cv::CC_STAT_HEIGHT));
        const int area = stats.at<int>(label, cv::CC_STAT_AREA);
        if (!isCandidateRect(rect, area, config)) {
            continue;
        }

        DistortionP9Point point;
        point.id = static_cast<int>(candidates.size());
        point.boundingRect = rect;
        point.area = area;
        point.center = cv::Point2d(centroids.at<double>(label, 0), centroids.at<double>(label, 1));
        candidates.push_back(std::move(point));
    }

    if (config.maxCandidates > 0 && static_cast<int>(candidates.size()) > config.maxCandidates) {
        std::sort(candidates.begin(), candidates.end(), [](const auto& a, const auto& b) {
            return a.area > b.area;
        });
        candidates.resize(static_cast<size_t>(config.maxCandidates));
    }

    return candidates;
}

void assignCandidateNames(std::vector<DistortionP9Point>& candidates)
{
    std::sort(candidates.begin(), candidates.end(), [](const auto& a, const auto& b) {
        const double rowTolerance = std::max(a.boundingRect.height, b.boundingRect.height) * 0.8;
        if (std::abs(a.center.y - b.center.y) > rowTolerance) {
            return a.center.y < b.center.y;
        }
        return a.center.x < b.center.x;
    });

    for (int i = 0; i < static_cast<int>(candidates.size()); ++i) {
        candidates[static_cast<size_t>(i)].id = i;
        candidates[static_cast<size_t>(i)].name = "Candidate_" + std::to_string(i + 1);
    }
}

cv::Point2d normalizeAxis(cv::Point2d axis)
{
    const double length = std::hypot(axis.x, axis.y);
    if (length <= Epsilon) {
        return cv::Point2d(1.0, 0.0);
    }

    return axis * (1.0 / length);
}

std::pair<cv::Point2d, cv::Point2d> estimateSortAxes(const std::vector<DistortionP9Point>& candidates, bool usePca)
{
    if (!usePca || candidates.size() < 2) {
        return { cv::Point2d(1.0, 0.0), cv::Point2d(0.0, 1.0) };
    }

    cv::Mat data(static_cast<int>(candidates.size()), 2, CV_64F);
    for (int i = 0; i < static_cast<int>(candidates.size()); ++i) {
        data.at<double>(i, 0) = candidates[static_cast<size_t>(i)].center.x;
        data.at<double>(i, 1) = candidates[static_cast<size_t>(i)].center.y;
    }

    cv::PCA pca(data, cv::Mat(), cv::PCA::DATA_AS_ROW);
    if (pca.eigenvectors.rows < 2 || pca.eigenvectors.cols < 2) {
        return { cv::Point2d(1.0, 0.0), cv::Point2d(0.0, 1.0) };
    }

    cv::Point2d axis0(pca.eigenvectors.at<double>(0, 0), pca.eigenvectors.at<double>(0, 1));
    cv::Point2d axis1(pca.eigenvectors.at<double>(1, 0), pca.eigenvectors.at<double>(1, 1));

    cv::Point2d axisH = std::abs(axis0.x) >= std::abs(axis1.x) ? axis0 : axis1;
    cv::Point2d axisV = std::abs(axis0.x) >= std::abs(axis1.x) ? axis1 : axis0;

    axisH = normalizeAxis(axisH);
    axisV = normalizeAxis(axisV);
    if (axisH.x < 0.0) {
        axisH *= -1.0;
    }
    if (axisV.y < 0.0) {
        axisV *= -1.0;
    }

    return { axisH, axisV };
}

double projectPoint(const cv::Point2d& point, const cv::Point2d& axis)
{
    return point.x * axis.x + point.y * axis.y;
}

std::vector<DistortionP9Point> selectAndSortGrid(std::vector<DistortionP9Point> candidates, const DistortionP9Config& config)
{
    const int expectedCount = config.expectedRows * config.expectedCols;
    if (expectedCount <= 0 || static_cast<int>(candidates.size()) < expectedCount) {
        return {};
    }

    if (static_cast<int>(candidates.size()) > expectedCount) {
        std::sort(candidates.begin(), candidates.end(), [](const auto& a, const auto& b) {
            if (a.area != b.area) {
                return a.area > b.area;
            }
            return a.center.y < b.center.y;
        });
        candidates.resize(static_cast<size_t>(expectedCount));
    }

    const auto axes = estimateSortAxes(candidates, config.sortWithPca);
    const cv::Point2d axisH = axes.first;
    const cv::Point2d axisV = axes.second;

    std::sort(candidates.begin(), candidates.end(), [&](const auto& a, const auto& b) {
        return projectPoint(a.center, axisV) < projectPoint(b.center, axisV);
    });

    std::vector<DistortionP9Point> ordered;
    ordered.reserve(static_cast<size_t>(expectedCount));
    for (int row = 0; row < config.expectedRows; ++row) {
        const int start = row * config.expectedCols;
        const int end = start + config.expectedCols;
        std::vector<DistortionP9Point> rowPoints(candidates.begin() + start, candidates.begin() + end);
        std::sort(rowPoints.begin(), rowPoints.end(), [&](const auto& a, const auto& b) {
            return projectPoint(a.center, axisH) < projectPoint(b.center, axisH);
        });

        for (int col = 0; col < static_cast<int>(rowPoints.size()); ++col) {
            DistortionP9Point point = rowPoints[static_cast<size_t>(col)];
            const int id = row * config.expectedCols + col;
            point.id = id;
            point.row = row;
            point.col = col;
            point.name = id >= 0 && id < static_cast<int>(PointNames.size())
                ? PointNames[static_cast<size_t>(id)]
                : ("P" + std::to_string(id));
            ordered.push_back(std::move(point));
        }
    }

    return ordered;
}

double distance(const cv::Point2d& a, const cv::Point2d& b)
{
    return std::hypot(a.x - b.x, a.y - b.y);
}

double safePercent(double numerator, double denominator, double scale = 100.0)
{
    return std::abs(denominator) <= Epsilon ? 0.0 : numerator / denominator * scale;
}

double signedDistanceToLine(const cv::Point2d& point, const cv::Point2d& lineStart, const cv::Point2d& lineEnd)
{
    const cv::Point2d edge = lineEnd - lineStart;
    const double length = std::hypot(edge.x, edge.y);
    if (length <= Epsilon) {
        return 0.0;
    }

    const cv::Point2d delta = point - lineStart;
    return (edge.x * delta.y - edge.y * delta.x) / length;
}

DistortionP9Metric calculateMetrics(const std::vector<DistortionP9Point>& points, int tvCalcWay)
{
    DistortionP9Metric metrics;
    if (points.size() != 9) {
        return metrics;
    }

    std::array<std::array<cv::Point2d, 3>, 3> grid{};
    for (const DistortionP9Point& point : points) {
        if (point.row >= 0 && point.row < 3 && point.col >= 0 && point.col < 3) {
            grid[static_cast<size_t>(point.row)][static_cast<size_t>(point.col)] = point.center;
        }
    }

    metrics.topWidth = distance(grid[0][0], grid[0][2]);
    metrics.middleWidth = distance(grid[1][0], grid[1][2]);
    metrics.bottomWidth = distance(grid[2][0], grid[2][2]);
    metrics.leftHeight = distance(grid[0][0], grid[2][0]);
    metrics.centerHeight = distance(grid[0][1], grid[2][1]);
    metrics.rightHeight = distance(grid[0][2], grid[2][2]);
    metrics.gridWidth = (metrics.topWidth + metrics.middleWidth + metrics.bottomWidth) / 3.0;
    metrics.gridHeight = (metrics.leftHeight + metrics.centerHeight + metrics.rightHeight) / 3.0;

    const double tvScale = tvCalcWay == 1 ? 50.0 : 100.0;
    metrics.horizontalTvPercent = safePercent(((metrics.topWidth + metrics.bottomWidth) * 0.5) - metrics.middleWidth, metrics.middleWidth, tvScale);
    metrics.verticalTvPercent = safePercent(((metrics.leftHeight + metrics.rightHeight) * 0.5) - metrics.centerHeight, metrics.centerHeight, tvScale);

    metrics.topPercent = safePercent(signedDistanceToLine(grid[0][1], grid[0][0], grid[0][2]), metrics.gridHeight);
    metrics.bottomPercent = safePercent(-signedDistanceToLine(grid[2][1], grid[2][0], grid[2][2]), metrics.gridHeight);
    metrics.leftPercent = safePercent(-signedDistanceToLine(grid[1][0], grid[0][0], grid[2][0]), metrics.gridWidth);
    metrics.rightPercent = safePercent(signedDistanceToLine(grid[1][2], grid[0][2], grid[2][2]), metrics.gridWidth);

    metrics.keystoneHorizontalPercent = safePercent(metrics.topWidth - metrics.bottomWidth, metrics.gridWidth);
    metrics.keystoneVerticalPercent = safePercent(metrics.leftHeight - metrics.rightHeight, metrics.gridHeight);
    return metrics;
}

} // namespace

DistortionP9Result calculateDistortionP9(const cv::Mat& img, const DistortionP9Config& config)
{
    DistortionP9Result result;
    result.imageSize = img.empty() ? cv::Size() : cv::Size(img.cols, img.rows);

    cv::Mat gray = toGrayPreserveDepth(img);
    if (gray.empty()) {
        result.statusCode = "invalid_image";
        result.message = "Invalid image or unsupported channel count.";
        return result;
    }

    DistortionP9Config effectiveConfig = config;
    effectiveConfig.expectedRows = std::max(1, effectiveConfig.expectedRows);
    effectiveConfig.expectedCols = std::max(1, effectiveConfig.expectedCols);

    std::vector<DistortionP9Point> candidates = findCandidates(gray, effectiveConfig);
    result.candidateCount = static_cast<int>(candidates.size());
    result.candidatePoints = candidates;
    assignCandidateNames(result.candidatePoints);

    const int expectedCount = effectiveConfig.expectedRows * effectiveConfig.expectedCols;
    if (expectedCount <= 0) {
        result.statusCode = "invalid_grid_size";
        result.message = "Invalid grid size.";
        return result;
    }

    if (result.candidateCount == 0) {
        result.statusCode = "no_candidates";
        result.message = "No valid point candidates were detected. Check ROI, threshold, target polarity, or point size limits.";
        return result;
    }

    if (result.candidateCount < expectedCount) {
        result.statusCode = "too_few_candidates";
        result.message = "Detected fewer candidates than the expected point grid. Some points may be too dim, missing, outside ROI, or filtered by size.";
        return result;
    }

    if (result.candidateCount > expectedCount) {
        result.warnings.push_back("Detected more candidates than expected. The algorithm selected the largest grid-sized set; check light leakage, reflections, or false bright regions.");
    }

    result.points = selectAndSortGrid(std::move(candidates), effectiveConfig);
    if (static_cast<int>(result.points.size()) != expectedCount) {
        result.statusCode = "grid_sort_failed";
        result.message = "Unable to sort candidates into the expected point grid. Check false positives, grid rotation, or ROI coverage.";
        return result;
    }

    result.metrics = calculateMetrics(result.points, effectiveConfig.tvCalcWay);
    result.success = true;
    result.statusCode = result.warnings.empty() ? "ok" : "ok_with_warnings";
    result.message = "ok";
    return result;
}

} // namespace distortion
} // namespace cvcore
