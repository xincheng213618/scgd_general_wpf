#include "keyboard_led.h"

#include <nlohmann/json.hpp>

#include <algorithm>
#include <cmath>
#include <cstdint>
#include <limits>
#include <numeric>
#include <utility>

namespace cvcore {
namespace keyboard_led {

namespace {

constexpr double Pi = 3.14159265358979323846;
constexpr double Epsilon = 1e-9;
constexpr size_t ExactClusterReductionLimit = 64;

struct PixelStats
{
    int samplePixels = 0;
    int validPixels = 0;
    double mean = 0.0;
};

struct CoordinateCluster
{
    double center = 0.0;
    int support = 0;
};

struct WorkingDetection
{
    int sourceIndex = -1;
    const LedDetection* detection = nullptr;
    bool geometryValid = false;
    double projectedCol = 0.0;
    double projectedRow = 0.0;
    PixelStats brightness;
    double area = 0.0;
    std::vector<std::string> warnings;
};

struct MatchCandidate
{
    int detectionIndex = -1;
    int row = -1;
    int col = -1;
    double distance = 0.0;
};

bool isFinitePoint(const cv::Point2d& point)
{
    return std::isfinite(point.x) && std::isfinite(point.y);
}

double median(std::vector<double> values)
{
    if (values.empty()) {
        return 0.0;
    }

    const size_t middle = values.size() / 2;
    std::nth_element(values.begin(), values.begin() + middle, values.end());
    double value = values[middle];
    if (values.size() % 2 == 0) {
        std::nth_element(values.begin(), values.begin() + middle - 1, values.begin() + middle);
        value = (value + values[middle - 1]) * 0.5;
    }
    return value;
}

int median(std::vector<int> values)
{
    if (values.empty()) {
        return 0;
    }

    const size_t middle = values.size() / 2;
    std::nth_element(values.begin(), values.begin() + middle, values.end());
    return values[middle];
}

cv::Rect clipRect(const cv::Rect& rect, const cv::Size& size)
{
    if (rect.width <= 0 || rect.height <= 0 || size.width <= 0 || size.height <= 0) {
        return {};
    }

    const int64_t left = std::max<int64_t>(0, rect.x);
    const int64_t top = std::max<int64_t>(0, rect.y);
    const int64_t right = std::min<int64_t>(size.width, static_cast<int64_t>(rect.x) + rect.width);
    const int64_t bottom = std::min<int64_t>(size.height, static_cast<int64_t>(rect.y) + rect.height);
    if (right <= left || bottom <= top) {
        return {};
    }

    return cv::Rect(
        static_cast<int>(left),
        static_cast<int>(top),
        static_cast<int>(right - left),
        static_cast<int>(bottom - top));
}

cv::Rect expandRect(const cv::Rect& rect, int amount, const cv::Size& size)
{
    const int64_t left = static_cast<int64_t>(rect.x) - amount;
    const int64_t top = static_cast<int64_t>(rect.y) - amount;
    const int64_t width = static_cast<int64_t>(rect.width) + amount * 2LL;
    const int64_t height = static_cast<int64_t>(rect.height) + amount * 2LL;
    if (width <= 0 || height <= 0) {
        return {};
    }

    const int safeLeft = static_cast<int>(std::clamp<int64_t>(left, std::numeric_limits<int>::min(), std::numeric_limits<int>::max()));
    const int safeTop = static_cast<int>(std::clamp<int64_t>(top, std::numeric_limits<int>::min(), std::numeric_limits<int>::max()));
    const int safeWidth = static_cast<int>(std::min<int64_t>(width, std::numeric_limits<int>::max()));
    const int safeHeight = static_cast<int>(std::min<int64_t>(height, std::numeric_limits<int>::max()));
    return clipRect(cv::Rect(safeLeft, safeTop, safeWidth, safeHeight), size);
}

cv::Rect insetRect(const cv::Rect& rect, int amount)
{
    if (amount < 0 || rect.width <= amount * 2 || rect.height <= amount * 2) {
        return {};
    }
    return cv::Rect(rect.x + amount, rect.y + amount, rect.width - amount * 2, rect.height - amount * 2);
}

int scaledPixels(int referenceSize, double ratio, int minimum, int maximum)
{
    const double scaled = static_cast<double>(referenceSize) * ratio;
    if (scaled >= maximum) {
        return maximum;
    }
    return std::max(minimum, static_cast<int>(std::llround(scaled)));
}

bool validateGrayConfig(const GrayConfig& config, std::string& error)
{
    if (!std::isfinite(config.validMin) || !std::isfinite(config.validMax) ||
        config.validMin < 0.0 || config.validMax > 1.0 || config.validMin > config.validMax) {
        error = "Gray valid range must satisfy 0 <= validMin <= validMax <= 1.";
        return false;
    }
    return true;
}

bool convertToGray32(const cv::Mat& image, const GrayConfig& config, cv::Mat& gray32, std::string& error)
{
    if (image.empty() || image.dims != 2) {
        error = "Image is empty or is not two-dimensional.";
        return false;
    }
    if ((image.depth() != CV_8U && image.depth() != CV_16U) ||
        (image.channels() != 1 && image.channels() != 3)) {
        error = "Only 8/16-bit one- or three-channel images are supported.";
        return false;
    }
    if (!validateGrayConfig(config, error)) {
        return false;
    }

    const double scale = image.depth() == CV_8U ? 1.0 / 255.0 : 1.0 / 65535.0;
    cv::Mat source32;
    image.convertTo(source32, CV_MAKETYPE(CV_32F, image.channels()), scale);
    if (image.channels() == 1) {
        if (config.mode == GrayMode::Channel && config.channel != 0) {
            error = "The selected channel does not exist in a one-channel image.";
            return false;
        }
        gray32 = source32;
        return true;
    }

    std::vector<cv::Mat> channels;
    cv::split(source32, channels);
    switch (config.mode)
    {
    case GrayMode::MaxChannel:
        cv::max(channels[0], channels[1], gray32);
        cv::max(gray32, channels[2], gray32);
        break;
    case GrayMode::AverageChannels:
        gray32 = (channels[0] + channels[1] + channels[2]) / 3.0;
        break;
    case GrayMode::Luminance:
        gray32 = channels[0] * 0.114 + channels[1] * 0.587 + channels[2] * 0.299;
        break;
    case GrayMode::Channel:
        if (config.channel < 0 || config.channel >= 3) {
            error = "The selected channel is outside the three-channel image.";
            return false;
        }
        gray32 = channels[static_cast<size_t>(config.channel)];
        break;
    default:
        error = "Unknown gray conversion mode.";
        return false;
    }
    return !gray32.empty();
}

PixelStats calculateStats(
    const cv::Mat& gray32,
    const cv::Rect& rect,
    const cv::Mat& geometryMask,
    const GrayConfig& config)
{
    PixelStats stats;
    if (rect.area() <= 0) {
        return stats;
    }

    cv::Mat sampleMask;
    if (geometryMask.empty()) {
        sampleMask = cv::Mat(rect.size(), CV_8U, cv::Scalar(255));
    }
    else {
        sampleMask = geometryMask;
    }
    stats.samplePixels = cv::countNonZero(sampleMask);
    if (stats.samplePixels == 0) {
        return stats;
    }

    cv::Mat minimumMask;
    cv::Mat maximumMask;
    cv::compare(gray32(rect), config.validMin, minimumMask, cv::CMP_GE);
    cv::compare(gray32(rect), config.validMax, maximumMask, cv::CMP_LE);
    cv::Mat validMask;
    cv::bitwise_and(minimumMask, maximumMask, validMask);
    cv::bitwise_and(validMask, sampleMask, validMask);
    stats.validPixels = cv::countNonZero(validMask);
    if (stats.validPixels > 0) {
        stats.mean = cv::mean(gray32(rect), validMask)[0];
    }
    return stats;
}

void clearMaskRect(cv::Mat& mask, const cv::Rect& maskBounds, const cv::Rect& imageRect)
{
    const cv::Rect intersection = maskBounds & imageRect;
    if (intersection.area() <= 0) {
        return;
    }
    const cv::Rect local(intersection.x - maskBounds.x, intersection.y - maskBounds.y, intersection.width, intersection.height);
    mask(local).setTo(0);
}

bool validateKeyHaloConfig(const KeyHaloConfig& config, std::string& error)
{
    if (!validateGrayConfig(config.gray, error)) {
        return false;
    }
    if (!std::isfinite(config.innerInsetRatio) || config.innerInsetRatio < 0.0 || config.innerInsetRatio >= 0.5) {
        error = "innerInsetRatio must be in [0, 0.5).";
        return false;
    }
    if (!std::isfinite(config.haloGapRatio) || config.haloGapRatio < 0.0 ||
        !std::isfinite(config.haloWidthRatio) || config.haloWidthRatio <= 0.0) {
        error = "Halo gap must be non-negative and halo width must be positive.";
        return false;
    }
    if (config.minimumValidPixels <= 0) {
        error = "minimumValidPixels must be positive.";
        return false;
    }
    return true;
}

double normalizeQuarterTurn(double angle)
{
    while (angle >= Pi * 0.25) {
        angle -= Pi * 0.5;
    }
    while (angle < -Pi * 0.25) {
        angle += Pi * 0.5;
    }
    return angle;
}

double quarterTurnDistance(double first, double second)
{
    return std::abs(normalizeQuarterTurn(first - second));
}

void estimateGridAxes(
    const std::vector<cv::Point2d>& points,
    cv::Point2d& colAxis,
    cv::Point2d& rowAxis,
    double& angleDegrees,
    std::vector<std::string>& warnings)
{
    colAxis = cv::Point2d(1.0, 0.0);
    rowAxis = cv::Point2d(0.0, 1.0);
    angleDegrees = 0.0;
    if (points.size() < 2) {
        warnings.push_back("A single detection cannot determine grid rotation; image axes were used.");
        return;
    }

    std::vector<double> angles;
    angles.reserve(points.size());
    for (size_t i = 0; i < points.size(); ++i) {
        double nearestDistance = std::numeric_limits<double>::max();
        cv::Point2d nearestVector;
        for (size_t j = 0; j < points.size(); ++j) {
            if (i == j) {
                continue;
            }
            const cv::Point2d delta = points[j] - points[i];
            const double distance = delta.dot(delta);
            if (distance > Epsilon && distance < nearestDistance) {
                nearestDistance = distance;
                nearestVector = delta;
            }
        }
        if (nearestDistance < std::numeric_limits<double>::max()) {
            angles.push_back(normalizeQuarterTurn(std::atan2(nearestVector.y, nearestVector.x)));
        }
    }
    if (angles.empty()) {
        warnings.push_back("Grid rotation could not be estimated because detections overlap.");
        return;
    }

    constexpr double inlierTolerance = 12.0 * Pi / 180.0;
    size_t bestInlierCount = 0;
    double bestAngle = angles.front();
    for (double candidate : angles) {
        size_t count = 0;
        for (double angle : angles) {
            if (quarterTurnDistance(candidate, angle) <= inlierTolerance) {
                ++count;
            }
        }
        if (count > bestInlierCount) {
            bestInlierCount = count;
            bestAngle = candidate;
        }
    }

    double cosine = 0.0;
    double sine = 0.0;
    for (double angle : angles) {
        if (quarterTurnDistance(bestAngle, angle) <= inlierTolerance) {
            cosine += std::cos(angle * 4.0);
            sine += std::sin(angle * 4.0);
        }
    }
    const double gridAngle = normalizeQuarterTurn(std::atan2(sine, cosine) * 0.25);
    colAxis = cv::Point2d(std::cos(gridAngle), std::sin(gridAngle));
    rowAxis = cv::Point2d(-colAxis.y, colAxis.x);
    angleDegrees = gridAngle * 180.0 / Pi;
    if (bestInlierCount * 5 < angles.size() * 3) {
        warnings.push_back("Grid rotation has low consensus; verify extra detections and grid ordering.");
    }
}

double autoClusterTolerance(const std::vector<double>& values, const LedArrayConfig& config)
{
    if (config.autoClusterTolerancePixels > 0.0) {
        return config.autoClusterTolerancePixels;
    }
    if (values.size() < 2) {
        return 1.0;
    }

    std::vector<double> sorted = values;
    std::sort(sorted.begin(), sorted.end());
    if (sorted.back() - sorted.front() <= 1e-4) {
        return 1.0;
    }

    std::vector<double> gaps;
    gaps.reserve(sorted.size() - 1);
    for (size_t i = 1; i < sorted.size(); ++i) {
        const double gap = sorted[i] - sorted[i - 1];
        if (gap > 1e-6) {
            gaps.push_back(gap);
        }
    }
    if (gaps.empty()) {
        return 1.0;
    }

    std::sort(gaps.begin(), gaps.end());
    double bestRatio = 0.0;
    size_t split = 0;
    for (size_t i = 0; i + 1 < gaps.size(); ++i) {
        const double ratio = gaps[i + 1] / std::max(gaps[i], 1e-6);
        if (ratio > bestRatio) {
            bestRatio = ratio;
            split = i;
        }
    }
    if (bestRatio >= std::max(1.5, config.autoClusterSeparationRatio)) {
        return std::sqrt(gaps[split] * gaps[split + 1]);
    }
    return std::max(1e-4, median(gaps) * 0.35);
}

std::vector<CoordinateCluster> clusterCoordinates(const std::vector<double>& values, const LedArrayConfig& config)
{
    if (values.empty()) {
        return {};
    }

    std::vector<double> sorted = values;
    std::sort(sorted.begin(), sorted.end());
    const double tolerance = autoClusterTolerance(sorted, config);
    std::vector<std::vector<double>> groups(1);
    groups.front().push_back(sorted.front());
    for (size_t i = 1; i < sorted.size(); ++i) {
        if (sorted[i] - sorted[i - 1] > tolerance) {
            groups.emplace_back();
        }
        groups.back().push_back(sorted[i]);
    }

    std::vector<CoordinateCluster> clusters;
    clusters.reserve(groups.size());
    for (std::vector<double>& group : groups) {
        clusters.push_back({ median(group), static_cast<int>(group.size()) });
    }
    return clusters;
}

void discardSparseAutoClusters(
    std::vector<CoordinateCluster>& clusters,
    const char* dimension,
    std::vector<std::string>& warnings)
{
    if (clusters.size() <= 2) {
        return;
    }

    std::vector<int> supports;
    supports.reserve(clusters.size());
    for (const CoordinateCluster& cluster : clusters) {
        supports.push_back(cluster.support);
    }
    if (median(supports) < 3) {
        return;
    }

    const size_t originalSize = clusters.size();
    clusters.erase(std::remove_if(clusters.begin(), clusters.end(), [](const CoordinateCluster& cluster) {
        return cluster.support == 1;
    }), clusters.end());
    if (clusters.empty()) {
        return;
    }
    if (clusters.size() != originalSize) {
        warnings.push_back(std::string("Sparse ") + dimension + " clusters were treated as extra detections.");
    }
}

double clusterRegularity(const std::vector<CoordinateCluster>& clusters)
{
    if (clusters.size() <= 2) {
        return 0.0;
    }

    std::vector<double> gaps;
    gaps.reserve(clusters.size() - 1);
    for (size_t i = 1; i < clusters.size(); ++i) {
        gaps.push_back(clusters[i].center - clusters[i - 1].center);
    }
    const double spacing = std::max(Epsilon, median(gaps));
    double deviation = 0.0;
    for (double gap : gaps) {
        deviation += std::abs(gap - spacing) / spacing;
    }
    return deviation / static_cast<double>(gaps.size());
}

double localSpacingRegularity(
    const std::vector<CoordinateCluster>& clusters,
    size_t index,
    double nominalSpacing)
{
    if (clusters.size() <= 1) {
        return 1.0;
    }

    const bool hasPrevious = index > 0;
    const bool hasNext = index + 1 < clusters.size();
    const double previousGap = hasPrevious ? clusters[index].center - clusters[index - 1].center : 0.0;
    const double nextGap = hasNext ? clusters[index + 1].center - clusters[index].center : 0.0;
    if (nominalSpacing <= Epsilon) {
        if (!hasPrevious || !hasNext) {
            return 0.5;
        }
        return 1.0 - std::min(1.0, std::abs(previousGap - nextGap) /
            std::max(Epsilon, std::max(previousGap, nextGap)));
    }

    auto gapScore = [nominalSpacing](double gap) {
        return 1.0 - std::min(1.0, std::abs(gap - nominalSpacing) / nominalSpacing);
    };
    if (!hasPrevious) {
        return gapScore(nextGap);
    }
    if (!hasNext) {
        return gapScore(previousGap);
    }
    return 0.5 * (gapScore(previousGap) + gapScore(nextGap));
}

void reduceLargeClusterSet(
    std::vector<CoordinateCluster>& clusters,
    int expected,
    const char* dimension,
    std::vector<std::string>& warnings)
{
    std::vector<double> gaps;
    gaps.reserve(clusters.size() - 1);
    for (size_t index = 1; index < clusters.size(); ++index) {
        const double gap = clusters[index].center - clusters[index - 1].center;
        if (gap > Epsilon) {
            gaps.push_back(gap);
        }
    }
    const double nominalSpacing = median(std::move(gaps));

    int maximumSupport = 1;
    for (const CoordinateCluster& cluster : clusters) {
        maximumSupport = std::max(maximumSupport, cluster.support);
    }

    struct RankedCluster
    {
        CoordinateCluster cluster;
        double score = 0.0;
        double spacingRegularity = 0.0;
    };

    std::vector<RankedCluster> ranked;
    ranked.reserve(clusters.size());
    for (size_t index = 0; index < clusters.size(); ++index) {
        const double supportScore = static_cast<double>(clusters[index].support) / maximumSupport;
        const double spacingScore = localSpacingRegularity(clusters, index, nominalSpacing);
        ranked.push_back({ clusters[index], 0.75 * supportScore + 0.25 * spacingScore, spacingScore });
    }

    std::stable_sort(ranked.begin(), ranked.end(), [](const RankedCluster& first, const RankedCluster& second) {
        if (first.score != second.score) {
            return first.score > second.score;
        }
        if (first.cluster.support != second.cluster.support) {
            return first.cluster.support > second.cluster.support;
        }
        if (first.spacingRegularity != second.spacingRegularity) {
            return first.spacingRegularity > second.spacingRegularity;
        }
        return first.cluster.center < second.cluster.center;
    });
    ranked.resize(static_cast<size_t>(expected));

    clusters.clear();
    clusters.reserve(static_cast<size_t>(expected));
    for (const RankedCluster& item : ranked) {
        clusters.push_back(item.cluster);
    }
    std::sort(clusters.begin(), clusters.end(), [](const CoordinateCluster& first, const CoordinateCluster& second) {
        return first.center < second.center;
    });
    warnings.push_back(std::string("Large ") + dimension +
        " cluster set used bounded support/spacing approximation; verify highly irregular detections.");
}

void reduceClustersToExpected(
    std::vector<CoordinateCluster>& clusters,
    int expected,
    const char* dimension,
    std::vector<std::string>& warnings)
{
    if (clusters.size() > ExactClusterReductionLimit) {
        reduceLargeClusterSet(clusters, expected, dimension, warnings);
        return;
    }

    while (static_cast<int>(clusters.size()) > expected) {
        int maximumSupport = 1;
        for (const CoordinateCluster& cluster : clusters) {
            maximumSupport = std::max(maximumSupport, cluster.support);
        }

        size_t bestRemoval = 0;
        double bestScore = std::numeric_limits<double>::max();
        for (size_t remove = 0; remove < clusters.size(); ++remove) {
            std::vector<CoordinateCluster> candidate = clusters;
            const int removedSupport = candidate[remove].support;
            candidate.erase(candidate.begin() + remove);
            const double supportPenalty = 0.35 * static_cast<double>(removedSupport) / maximumSupport;
            const double score = clusterRegularity(candidate) + supportPenalty;
            if (score < bestScore) {
                bestScore = score;
                bestRemoval = remove;
            }
        }
        clusters.erase(clusters.begin() + bestRemoval);
    }
    warnings.push_back(std::string("More ") + dimension + " clusters than expected were found; low-support or irregular clusters were excluded.");
}

double estimateLatticeSpacing(const std::vector<CoordinateCluster>& clusters, int expected)
{
    if (clusters.size() < 2 || expected < 2) {
        return 0.0;
    }

    std::vector<double> values;
    values.reserve(clusters.size());
    for (const CoordinateCluster& cluster : clusters) {
        values.push_back(cluster.center);
    }

    std::vector<double> candidates;
    const int maximumDivisor = std::min(expected - 1, 32);
    for (size_t i = 1; i < values.size(); ++i) {
        const double gap = values[i] - values[i - 1];
        for (int divisor = 1; divisor <= maximumDivisor; ++divisor) {
            candidates.push_back(gap / divisor);
        }
    }

    double bestSpacing = 0.0;
    double bestScore = std::numeric_limits<double>::max();
    const double range = values.back() - values.front();
    for (double spacing : candidates) {
        if (spacing <= Epsilon) {
            continue;
        }
        const int span = static_cast<int>(std::llround(range / spacing));
        if (span <= 0 || span > expected - 1) {
            continue;
        }

        double score = static_cast<double>(expected - 1 - span) * 0.01;
        for (double value : values) {
            const double position = (value - values.front()) / spacing;
            score += std::abs(position - std::round(position));
        }
        score /= static_cast<double>(values.size());
        if (score < bestScore - Epsilon || (std::abs(score - bestScore) <= Epsilon && spacing > bestSpacing)) {
            bestScore = score;
            bestSpacing = spacing;
        }
    }
    return bestSpacing;
}

bool expandClustersToExpected(
    std::vector<CoordinateCluster>& clusters,
    int expected,
    const char* dimension,
    std::vector<std::string>& warnings)
{
    const double spacing = estimateLatticeSpacing(clusters, expected);
    if (spacing <= Epsilon) {
        return false;
    }

    const double observedSpan = clusters.back().center - clusters.front().center;
    const int occupiedSpan = std::clamp(static_cast<int>(std::llround(observedSpan / spacing)), 0, expected - 1);
    const int missingSlots = expected - 1 - occupiedSpan;
    const int leadingSlots = missingSlots / 2;
    const double start = clusters.front().center - leadingSlots * spacing;
    clusters.clear();
    clusters.reserve(static_cast<size_t>(expected));
    for (int index = 0; index < expected; ++index) {
        clusters.push_back({ start + index * spacing, 0 });
    }
    warnings.push_back(std::string("Fewer ") + dimension + " clusters than expected were found; missing boundary placement is geometrically ambiguous.");
    return true;
}

bool buildGridCoordinates(
    const std::vector<double>& values,
    int expected,
    const char* dimension,
    const LedArrayConfig& config,
    std::vector<double>& coordinates,
    std::vector<std::string>& warnings)
{
    std::vector<CoordinateCluster> clusters = clusterCoordinates(values, config);
    if (expected == 0) {
        discardSparseAutoClusters(clusters, dimension, warnings);
    }
    else if (static_cast<int>(clusters.size()) > expected) {
        reduceClustersToExpected(clusters, expected, dimension, warnings);
    }
    else if (static_cast<int>(clusters.size()) < expected && !expandClustersToExpected(clusters, expected, dimension, warnings)) {
        return false;
    }

    coordinates.clear();
    coordinates.reserve(clusters.size());
    for (const CoordinateCluster& cluster : clusters) {
        coordinates.push_back(cluster.center);
    }
    return !coordinates.empty();
}

double coordinateSpacing(const std::vector<double>& coordinates)
{
    if (coordinates.size() < 2) {
        return 0.0;
    }

    std::vector<double> gaps;
    gaps.reserve(coordinates.size() - 1);
    for (size_t i = 1; i < coordinates.size(); ++i) {
        gaps.push_back(coordinates[i] - coordinates[i - 1]);
    }
    return median(gaps);
}

int nearestCoordinate(const std::vector<double>& coordinates, double value)
{
    int best = 0;
    double bestDistance = std::numeric_limits<double>::max();
    for (int index = 0; index < static_cast<int>(coordinates.size()); ++index) {
        const double distance = std::abs(value - coordinates[static_cast<size_t>(index)]);
        if (distance < bestDistance) {
            best = index;
            bestDistance = distance;
        }
    }
    return best;
}

PixelStats sampleDetectionBrightness(
    const cv::Mat& gray32,
    const LedDetection& detection,
    int sampleRadius,
    const GrayConfig& grayConfig)
{
    cv::Rect sampleRect = clipRect(detection.boundingRect, gray32.size());
    cv::Mat mask;
    if (sampleRect.area() <= 0 && isFinitePoint(detection.center) &&
        detection.center.x >= 0.0 && detection.center.x < gray32.cols &&
        detection.center.y >= 0.0 && detection.center.y < gray32.rows) {
        const int radius = std::min(sampleRadius, std::max(gray32.cols, gray32.rows));
        const int centerX = std::clamp(cvRound(detection.center.x), 0, gray32.cols - 1);
        const int centerY = std::clamp(cvRound(detection.center.y), 0, gray32.rows - 1);
        sampleRect = expandRect(cv::Rect(centerX, centerY, 1, 1), radius, gray32.size());
        if (sampleRect.area() > 0) {
            mask = cv::Mat::zeros(sampleRect.size(), CV_8U);
            cv::circle(
                mask,
                cv::Point(centerX - sampleRect.x, centerY - sampleRect.y),
                radius,
                cv::Scalar(255),
                cv::FILLED,
                cv::LINE_8);
        }
    }
    return calculateStats(gray32, sampleRect, mask, grayConfig);
}

bool validateLedConfig(const LedArrayConfig& config, std::string& error)
{
    if (!validateGrayConfig(config.gray, error)) {
        return false;
    }
    if (config.rows < 0 || config.cols < 0) {
        error = "rows and cols must be zero (automatic) or positive.";
        return false;
    }
    if (config.sampleRadius < 0 || config.maximumDetections <= 0 || config.maximumExpectedPoints <= 0) {
        error = "Sampling radius and configured limits are invalid.";
        return false;
    }
    if (config.rows > config.maximumExpectedPoints || config.cols > config.maximumExpectedPoints ||
        (config.rows > 0 && config.cols > 0 &&
            static_cast<int64_t>(config.rows) * config.cols > config.maximumExpectedPoints)) {
        error = "Configured grid dimensions exceed maximumExpectedPoints.";
        return false;
    }
    if (!std::isfinite(config.assignmentGatePixels) || !std::isfinite(config.assignmentGateRatio) ||
        config.assignmentGatePixels < 0.0 || (config.assignmentGatePixels == 0.0 && config.assignmentGateRatio <= 0.0)) {
        error = "An absolute or positive relative assignment gate is required.";
        return false;
    }
    if (!std::isfinite(config.autoClusterTolerancePixels) || config.autoClusterTolerancePixels < 0.0 ||
        !std::isfinite(config.autoClusterSeparationRatio) || config.autoClusterSeparationRatio <= 1.0) {
        error = "Automatic clustering parameters are invalid.";
        return false;
    }
    if (!std::isfinite(config.minimumBrightness) || config.minimumBrightness > 1.0 ||
        !std::isfinite(config.minimumArea) || !std::isfinite(config.maximumArea) ||
        (config.minimumArea >= 0.0 && config.maximumArea >= 0.0 && config.minimumArea > config.maximumArea)) {
        error = "Brightness or area acceptance limits are invalid.";
        return false;
    }
    return true;
}

cv::Point2d gridPoint(
    double colCoordinate,
    double rowCoordinate,
    const cv::Point2d& colAxis,
    const cv::Point2d& rowAxis)
{
    return colAxis * colCoordinate + rowAxis * rowCoordinate;
}

std::string classifyMatchedPoint(const WorkingDetection& work, const LedArrayConfig& config)
{
    if (work.brightness.validPixels == 0) {
        return "invalid_measurement";
    }

    const bool brightnessRejected = config.minimumBrightness >= 0.0 && work.brightness.mean < config.minimumBrightness;
    const bool areaRejected =
        (config.minimumArea >= 0.0 && work.area < config.minimumArea) ||
        (config.maximumArea >= 0.0 && work.area > config.maximumArea);
    if (brightnessRejected && areaRejected) {
        return "brightness_and_area_out_of_range";
    }
    if (brightnessRejected) {
        return "dim";
    }
    if (areaRejected) {
        return "area_out_of_range";
    }
    return "normal";
}

LedArrayPoint makeDetectedPoint(
    const WorkingDetection& work,
    int row,
    int col,
    const cv::Point2d& expectedCenter,
    double assignmentDistance,
    const std::string& status)
{
    LedArrayPoint point;
    point.sourceId = work.detection->id;
    point.sourceIndex = work.sourceIndex;
    point.row = row;
    point.col = col;
    point.detected = true;
    point.expectedCenter = expectedCenter;
    point.detectedCenter = work.detection->center;
    point.offset = work.detection->center - expectedCenter;
    point.assignmentDistance = assignmentDistance;
    point.brightness = work.brightness.mean;
    point.brightnessValid = work.brightness.validPixels > 0;
    point.samplePixelCount = work.brightness.samplePixels;
    point.validPixelCount = work.brightness.validPixels;
    point.area = work.area;
    point.status = status;
    point.warnings = work.warnings;
    return point;
}

nlohmann::json pointJson(const cv::Point2d& point)
{
    return {
        { "x", point.x },
        { "y", point.y }
    };
}

nlohmann::json rectJson(const cv::Rect& rect)
{
    return {
        { "x", rect.x },
        { "y", rect.y },
        { "width", rect.width },
        { "height", rect.height }
    };
}

nlohmann::json sizeJson(const cv::Size& size)
{
    return {
        { "width", size.width },
        { "height", size.height }
    };
}

} // namespace

nlohmann::json ToJson(const KeyHaloMeasurement& measurement)
{
    return {
        { "id", measurement.id },
        { "inputRect", rectJson(measurement.inputRect) },
        { "clippedKeyRect", rectJson(measurement.clippedKeyRect) },
        { "innerRect", rectJson(measurement.innerRect) },
        { "haloBounds", rectJson(measurement.haloBounds) },
        { "keyPixelCount", measurement.keyPixelCount },
        { "keyValidPixelCount", measurement.keyValidPixelCount },
        { "haloPixelCount", measurement.haloPixelCount },
        { "haloValidPixelCount", measurement.haloValidPixelCount },
        { "keyMean", measurement.keyMean },
        { "haloMean", measurement.haloMean },
        { "haloToKeyRatio", measurement.haloToKeyRatio },
        { "ratioValid", measurement.ratioValid },
        { "status", measurement.status },
        { "warnings", measurement.warnings }
    };
}

nlohmann::json ToJson(const KeyHaloResult& result)
{
    nlohmann::json output = {
        { "success", result.success },
        { "statusCode", result.statusCode },
        { "message", result.message },
        { "imageSize", sizeJson(result.imageSize) },
        { "warnings", result.warnings },
        { "keys", nlohmann::json::array() }
    };
    for (const KeyHaloMeasurement& measurement : result.keys) {
        output["keys"].push_back(ToJson(measurement));
    }
    return output;
}

nlohmann::json ToJson(const LedArrayPoint& point)
{
    return {
        { "id", point.id },
        { "sourceId", point.sourceId },
        { "sourceIndex", point.sourceIndex },
        { "row", point.row },
        { "col", point.col },
        { "detected", point.detected },
        { "expectedCenter", pointJson(point.expectedCenter) },
        { "detectedCenter", pointJson(point.detectedCenter) },
        { "offset", pointJson(point.offset) },
        { "assignmentDistance", point.assignmentDistance },
        { "brightness", point.brightness },
        { "brightnessValid", point.brightnessValid },
        { "samplePixelCount", point.samplePixelCount },
        { "validPixelCount", point.validPixelCount },
        { "area", point.area },
        { "status", point.status },
        { "warnings", point.warnings }
    };
}

nlohmann::json ToJson(const LedArrayResult& result)
{
    nlohmann::json output = {
        { "success", result.success },
        { "statusCode", result.statusCode },
        { "message", result.message },
        { "imageSize", sizeJson(result.imageSize) },
        { "warnings", result.warnings },
        { "rows", result.rows },
        { "cols", result.cols },
        { "expectedCount", result.expectedCount },
        { "detectedCount", result.detectedCount },
        { "matchedCount", result.matchedCount },
        { "missingCount", result.missingCount },
        { "extraCount", result.extraCount },
        { "normalCount", result.normalCount },
        { "abnormalCount", result.abnormalCount },
        { "rowSpacing", result.rowSpacing },
        { "colSpacing", result.colSpacing },
        { "angleDegrees", result.angleDegrees },
        { "points", nlohmann::json::array() }
    };
    for (const LedArrayPoint& point : result.points) {
        output["points"].push_back(ToJson(point));
    }
    return output;
}

KeyHaloResult measureKeyHalo(
    const cv::Mat& image,
    const std::vector<cv::Rect>& keyRects,
    const KeyHaloConfig& config)
{
    KeyHaloResult result;
    result.imageSize = image.empty() ? cv::Size() : image.size();

    std::string error;
    cv::Mat gray32;
    if (!validateKeyHaloConfig(config, error) || !convertToGray32(image, config.gray, gray32, error)) {
        result.statusCode = "invalid_input";
        result.message = error;
        return result;
    }
    if (keyRects.empty()) {
        result.statusCode = "invalid_input";
        result.message = "At least one key rectangle is required.";
        return result;
    }

    std::vector<cv::Rect> clippedRects;
    clippedRects.reserve(keyRects.size());
    for (const cv::Rect& rect : keyRects) {
        clippedRects.push_back(clipRect(rect, gray32.size()));
    }

    int validMeasurements = 0;
    result.keys.reserve(keyRects.size());
    for (size_t index = 0; index < keyRects.size(); ++index) {
        KeyHaloMeasurement item;
        item.id = static_cast<int>(index) + 1;
        item.inputRect = keyRects[index];
        item.clippedKeyRect = clippedRects[index];
        if (item.clippedKeyRect.area() <= 0) {
            item.status = "invalid_rect";
            item.warnings.push_back("Key rectangle does not intersect the image.");
            result.keys.push_back(std::move(item));
            continue;
        }

        const int referenceSize = std::min(item.clippedKeyRect.width, item.clippedKeyRect.height);
        const int inset = static_cast<int>(std::llround(referenceSize * config.innerInsetRatio));
        const int maximumExpansion = std::max(gray32.cols, gray32.rows);
        const int gap = scaledPixels(referenceSize, config.haloGapRatio, 0, maximumExpansion);
        const int haloWidth = scaledPixels(referenceSize, config.haloWidthRatio, 1, maximumExpansion);
        const int haloExpansion = static_cast<int>(
            std::min<int64_t>(maximumExpansion, static_cast<int64_t>(gap) + haloWidth));
        item.innerRect = insetRect(item.clippedKeyRect, inset);
        item.haloBounds = expandRect(item.clippedKeyRect, haloExpansion, gray32.size());
        if (item.innerRect.area() <= 0 || item.haloBounds.area() <= 0) {
            item.status = "invalid_geometry";
            item.warnings.push_back("Inset or halo geometry is empty after clipping.");
            result.keys.push_back(std::move(item));
            continue;
        }

        const PixelStats keyStats = calculateStats(gray32, item.innerRect, {}, config.gray);
        cv::Mat haloMask(item.haloBounds.size(), CV_8U, cv::Scalar(255));
        clearMaskRect(haloMask, item.haloBounds, expandRect(item.clippedKeyRect, gap, gray32.size()));
        if (config.excludeKeyRectsFromHalo) {
            for (const cv::Rect& keyRect : clippedRects) {
                clearMaskRect(haloMask, item.haloBounds, keyRect);
            }
        }
        const PixelStats haloStats = calculateStats(gray32, item.haloBounds, haloMask, config.gray);

        item.keyPixelCount = keyStats.samplePixels;
        item.keyValidPixelCount = keyStats.validPixels;
        item.haloPixelCount = haloStats.samplePixels;
        item.haloValidPixelCount = haloStats.validPixels;
        item.keyMean = keyStats.mean;
        item.haloMean = haloStats.mean;
        if (keyStats.validPixels < config.minimumValidPixels || haloStats.validPixels < config.minimumValidPixels) {
            item.status = "insufficient_pixels";
            item.warnings.push_back("Key or halo has fewer valid pixels than required.");
        }
        else if (keyStats.mean <= Epsilon) {
            item.status = "zero_key_mean";
            item.warnings.push_back("Halo-to-key ratio is undefined because key mean is zero.");
        }
        else {
            item.haloToKeyRatio = haloStats.mean / keyStats.mean;
            item.ratioValid = true;
            item.status = "ok";
            ++validMeasurements;
        }
        result.keys.push_back(std::move(item));
    }

    result.success = validMeasurements > 0;
    if (validMeasurements == static_cast<int>(result.keys.size())) {
        result.statusCode = "ok";
        result.message = "ok";
    }
    else if (validMeasurements > 0) {
        result.statusCode = "partial";
        result.message = "Some key halo measurements are invalid.";
        result.warnings.push_back("Inspect per-key status and warnings.");
    }
    else {
        result.statusCode = "no_valid_measurement";
        result.message = "No key produced a valid halo-to-key ratio.";
    }
    return result;
}

LedArrayResult analyzeLedArray(
    const cv::Mat& image,
    const std::vector<LedDetection>& detections,
    const LedArrayConfig& config)
{
    LedArrayResult result;
    result.imageSize = image.empty() ? cv::Size() : image.size();
    result.detectedCount = static_cast<int>(detections.size());

    std::string error;
    cv::Mat gray32;
    if (!validateLedConfig(config, error) || !convertToGray32(image, config.gray, gray32, error)) {
        result.statusCode = "invalid_input";
        result.message = error;
        return result;
    }
    if (detections.empty()) {
        result.statusCode = "no_detections";
        result.message = "At least one LED detection is required to establish grid geometry.";
        return result;
    }
    if (static_cast<int>(detections.size()) > config.maximumDetections) {
        result.statusCode = "too_many_detections";
        result.message = "Detection count exceeds maximumDetections.";
        return result;
    }

    std::vector<WorkingDetection> work;
    std::vector<cv::Point2d> geometryPoints;
    work.reserve(detections.size());
    geometryPoints.reserve(detections.size());
    for (int index = 0; index < static_cast<int>(detections.size()); ++index) {
        const LedDetection& detection = detections[static_cast<size_t>(index)];
        WorkingDetection item;
        item.sourceIndex = index;
        item.detection = &detection;
        item.brightness = sampleDetectionBrightness(gray32, detection, config.sampleRadius, config.gray);
        const cv::Rect clippedBounds = clipRect(detection.boundingRect, gray32.size());
        if (std::isfinite(detection.area) && detection.area > 0.0) {
            item.area = detection.area;
        }
        else if (clippedBounds.area() > 0) {
            item.area = static_cast<double>(clippedBounds.area());
        }
        else {
            item.area = static_cast<double>(item.brightness.samplePixels);
        }

        item.geometryValid = isFinitePoint(detection.center) &&
            detection.center.x >= 0.0 && detection.center.x < gray32.cols &&
            detection.center.y >= 0.0 && detection.center.y < gray32.rows;
        if (item.geometryValid) {
            geometryPoints.push_back(detection.center);
        }
        else {
            item.warnings.push_back("Detection center is non-finite or outside the image.");
        }
        if (item.brightness.validPixels == 0) {
            item.warnings.push_back("No valid brightness pixels were available.");
        }
        work.push_back(std::move(item));
    }
    if (geometryPoints.empty()) {
        result.statusCode = "invalid_geometry";
        result.message = "No detection center is usable for grid geometry.";
        return result;
    }

    cv::Point2d colAxis;
    cv::Point2d rowAxis;
    estimateGridAxes(geometryPoints, colAxis, rowAxis, result.angleDegrees, result.warnings);

    std::vector<double> colValues;
    std::vector<double> rowValues;
    colValues.reserve(geometryPoints.size());
    rowValues.reserve(geometryPoints.size());
    for (WorkingDetection& item : work) {
        if (!item.geometryValid) {
            continue;
        }
        item.projectedCol = item.detection->center.dot(colAxis);
        item.projectedRow = item.detection->center.dot(rowAxis);
        colValues.push_back(item.projectedCol);
        rowValues.push_back(item.projectedRow);
    }

    std::vector<double> colCoordinates;
    std::vector<double> rowCoordinates;
    if (!buildGridCoordinates(colValues, config.cols, "column", config, colCoordinates, result.warnings) ||
        !buildGridCoordinates(rowValues, config.rows, "row", config, rowCoordinates, result.warnings)) {
        result.statusCode = "insufficient_geometry";
        result.message = "The requested grid dimensions cannot be inferred from available detections.";
        return result;
    }
    result.cols = static_cast<int>(colCoordinates.size());
    result.rows = static_cast<int>(rowCoordinates.size());
    const int64_t expectedCount = static_cast<int64_t>(result.rows) * result.cols;
    if (expectedCount <= 0 || expectedCount > config.maximumExpectedPoints) {
        result.statusCode = "invalid_grid_size";
        result.message = "Estimated grid size is empty or exceeds maximumExpectedPoints.";
        return result;
    }
    result.expectedCount = static_cast<int>(expectedCount);
    result.rowSpacing = coordinateSpacing(rowCoordinates);
    result.colSpacing = coordinateSpacing(colCoordinates);
    if (config.rows == 0 || config.cols == 0) {
        result.warnings.push_back("One or more grid dimensions were estimated automatically.");
    }

    const double availableSpacing = result.rowSpacing > Epsilon && result.colSpacing > Epsilon
        ? std::min(result.rowSpacing, result.colSpacing)
        : std::max(result.rowSpacing, result.colSpacing);
    const double fallbackGate = availableSpacing > Epsilon
        ? availableSpacing * config.assignmentGateRatio
        : std::max(2.0, config.sampleRadius * 2.0 + 1.0);
    const double colGate = config.assignmentGatePixels > 0.0
        ? config.assignmentGatePixels
        : (result.colSpacing > Epsilon ? result.colSpacing * config.assignmentGateRatio : fallbackGate);
    const double rowGate = config.assignmentGatePixels > 0.0
        ? config.assignmentGatePixels
        : (result.rowSpacing > Epsilon ? result.rowSpacing * config.assignmentGateRatio : fallbackGate);

    std::vector<MatchCandidate> matchCandidates;
    matchCandidates.reserve(work.size());
    for (int index = 0; index < static_cast<int>(work.size()); ++index) {
        const WorkingDetection& item = work[static_cast<size_t>(index)];
        if (!item.geometryValid) {
            continue;
        }
        const int col = nearestCoordinate(colCoordinates, item.projectedCol);
        const int row = nearestCoordinate(rowCoordinates, item.projectedRow);
        const double colDelta = std::abs(item.projectedCol - colCoordinates[static_cast<size_t>(col)]);
        const double rowDelta = std::abs(item.projectedRow - rowCoordinates[static_cast<size_t>(row)]);
        if (colDelta <= colGate && rowDelta <= rowGate) {
            const cv::Point2d expectedCenter = gridPoint(
                colCoordinates[static_cast<size_t>(col)],
                rowCoordinates[static_cast<size_t>(row)],
                colAxis,
                rowAxis);
            matchCandidates.push_back({ index, row, col, cv::norm(item.detection->center - expectedCenter) });
        }
    }
    std::sort(matchCandidates.begin(), matchCandidates.end(), [](const MatchCandidate& first, const MatchCandidate& second) {
        return first.distance < second.distance;
    });

    std::vector<int> cellAssignments(static_cast<size_t>(result.expectedCount), -1);
    std::vector<double> assignmentDistances(static_cast<size_t>(result.expectedCount), 0.0);
    std::vector<bool> detectionAssigned(work.size(), false);
    for (const MatchCandidate& candidate : matchCandidates) {
        const int cell = candidate.row * result.cols + candidate.col;
        if (cellAssignments[static_cast<size_t>(cell)] >= 0 || detectionAssigned[static_cast<size_t>(candidate.detectionIndex)]) {
            continue;
        }
        cellAssignments[static_cast<size_t>(cell)] = candidate.detectionIndex;
        assignmentDistances[static_cast<size_t>(cell)] = candidate.distance;
        detectionAssigned[static_cast<size_t>(candidate.detectionIndex)] = true;
    }

    result.points.reserve(static_cast<size_t>(result.expectedCount) + work.size());
    for (int row = 0; row < result.rows; ++row) {
        for (int col = 0; col < result.cols; ++col) {
            const int cell = row * result.cols + col;
            const cv::Point2d expectedCenter = gridPoint(
                colCoordinates[static_cast<size_t>(col)],
                rowCoordinates[static_cast<size_t>(row)],
                colAxis,
                rowAxis);
            const int workIndex = cellAssignments[static_cast<size_t>(cell)];
            if (workIndex < 0) {
                LedArrayPoint point;
                point.row = row;
                point.col = col;
                point.expectedCenter = expectedCenter;
                point.status = "missing";
                result.points.push_back(std::move(point));
                ++result.missingCount;
                continue;
            }

            const WorkingDetection& item = work[static_cast<size_t>(workIndex)];
            const std::string status = classifyMatchedPoint(item, config);
            result.points.push_back(makeDetectedPoint(
                item,
                row,
                col,
                expectedCenter,
                assignmentDistances[static_cast<size_t>(cell)],
                status));
            ++result.matchedCount;
            if (status == "normal") {
                ++result.normalCount;
            }
            else {
                ++result.abnormalCount;
            }
        }
    }

    for (int index = 0; index < static_cast<int>(work.size()); ++index) {
        if (detectionAssigned[static_cast<size_t>(index)]) {
            continue;
        }
        const WorkingDetection& item = work[static_cast<size_t>(index)];
        result.points.push_back(makeDetectedPoint(item, -1, -1, item.detection->center, 0.0, "extra"));
        ++result.extraCount;
    }
    for (int index = 0; index < static_cast<int>(result.points.size()); ++index) {
        result.points[static_cast<size_t>(index)].id = index + 1;
    }

    result.success = true;
    if (result.missingCount > 0 || result.extraCount > 0) {
        result.statusCode = "grid_mismatch";
        result.message = "LED grid contains missing or extra detections.";
    }
    else if (result.abnormalCount > 0) {
        result.statusCode = "measurement_warning";
        result.message = "LED grid is complete but one or more measurements are outside configured limits.";
    }
    else {
        result.statusCode = "ok";
        result.message = "ok";
    }
    return result;
}

} // namespace keyboard_led
} // namespace cvcore
