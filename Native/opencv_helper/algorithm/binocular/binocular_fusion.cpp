#include "binocular_fusion.h"

#include <algorithm>
#include <array>
#include <cmath>
#include <limits>
#include <numeric>
#include <tuple>
#include <utility>

namespace cvcore {
namespace binocular {

namespace {

constexpr double Epsilon = 1e-9;
constexpr double Pi = 3.14159265358979323846;
constexpr int MaximumKernelSize = 4095;
constexpr int MaximumCandidateCount = 512;
constexpr double CrossArmHalfWidthRatio = 0.12;
constexpr double MinimumCrossShapeScore = 0.60;

struct CandidateDetection
{
    std::vector<BinocularCrossCandidate> candidates;
    double threshold = 0.0;
    bool truncated = false;
};

struct OppositePair
{
    int first = -1;
    int second = -1;
    cv::Point2d axis;
    double score = 0.0;
};

struct CrossSelection
{
    bool valid = false;
    int center = -1;
    int top = -1;
    int bottom = -1;
    int left = -1;
    int right = -1;
    double quality = 0.0;
};

bool isFinite(double value)
{
    return std::isfinite(value) != 0;
}

double clamp01(double value)
{
    return std::clamp(value, 0.0, 1.0);
}

double distance(const cv::Point2d& lhs, const cv::Point2d& rhs)
{
    return std::hypot(lhs.x - rhs.x, lhs.y - rhs.y);
}

double dot(const cv::Point2d& lhs, const cv::Point2d& rhs)
{
    return lhs.x * rhs.x + lhs.y * rhs.y;
}

cv::Point2d normalized(const cv::Point2d& value)
{
    const double length = std::hypot(value.x, value.y);
    return length <= Epsilon ? cv::Point2d() : value * (1.0 / length);
}

bool isFullImageRoi(const cv::Rect& roi)
{
    return roi.x == 0 && roi.y == 0 && roi.width == 0 && roi.height == 0;
}

bool prepareRoi(const cv::Size& imageSize, const cv::Rect& requested, cv::Rect& effective, bool& clipped)
{
    clipped = false;
    if (isFullImageRoi(requested)) {
        effective = cv::Rect(0, 0, imageSize.width, imageSize.height);
        return true;
    }
    if (requested.width <= 0 || requested.height <= 0) {
        return false;
    }

    const cv::Rect bounds(0, 0, imageSize.width, imageSize.height);
    effective = requested & bounds;
    clipped = effective != requested;
    return effective.width > 0 && effective.height > 0;
}

bool toGray8(const cv::Mat& image, cv::Mat& gray8)
{
    if (image.empty() || (image.channels() != 1 && image.channels() != 3 && image.channels() != 4)) {
        return false;
    }

    if (image.depth() == CV_8U) {
        if (image.channels() == 1) {
            gray8 = image.clone();
        }
        else if (image.channels() == 3) {
            cv::cvtColor(image, gray8, cv::COLOR_BGR2GRAY);
        }
        else {
            cv::cvtColor(image, gray8, cv::COLOR_BGRA2GRAY);
        }
        return true;
    }

    cv::Mat floatImage;
    image.convertTo(floatImage, CV_MAKETYPE(CV_32F, image.channels()));
    cv::Mat gray32;
    if (floatImage.channels() == 1) {
        gray32 = floatImage;
    }
    else if (floatImage.channels() == 3) {
        cv::cvtColor(floatImage, gray32, cv::COLOR_BGR2GRAY);
    }
    else {
        cv::cvtColor(floatImage, gray32, cv::COLOR_BGRA2GRAY);
    }
    if (!cv::checkRange(gray32, true, nullptr)) {
        return false;
    }

    double minValue = 0.0;
    double maxValue = 0.0;
    cv::minMaxLoc(gray32, &minValue, &maxValue);
    if (maxValue - minValue <= Epsilon) {
        gray8 = cv::Mat(gray32.size(), CV_8U, cv::Scalar(0));
    }
    else {
        gray32.convertTo(gray8, CV_8U, 255.0 / (maxValue - minValue), -255.0 * minValue / (maxValue - minValue));
    }
    return true;
}

bool isDisabledOpticalCenter(const cv::Point2d& point)
{
    return point.x < 0.0 && point.y < 0.0;
}

bool validateConfig(const BinocularFusionConfig& config, std::string& message)
{
    if (!isFinite(config.threshold) || config.threshold > 255.0) {
        message = "threshold must be finite and no greater than 255; values <= 0 enable Otsu.";
        return false;
    }
    if (config.blurKernel < 0 || config.blurKernel > MaximumKernelSize ||
        (config.blurKernel > 1 && config.blurKernel % 2 == 0)) {
        message = "blurKernel must be zero/one or a positive odd value.";
        return false;
    }
    if (config.morphKernel < 0 || config.morphKernel > MaximumKernelSize ||
        (config.morphKernel > 1 && config.morphKernel % 2 == 0)) {
        message = "morphKernel must be zero/one or a positive odd value.";
        return false;
    }
    if (config.minArea <= 0 || config.maxArea < 0 || (config.maxArea > 0 && config.maxArea < config.minArea)) {
        message = "minArea must be positive and maxArea must be zero or no smaller than minArea.";
        return false;
    }
    if (!isFinite(config.pixelSize) || config.pixelSize <= 0.0 ||
        !isFinite(config.focalLength) || config.focalLength <= 0.0 ||
        !isFinite(config.virtualImageDistance) || config.virtualImageDistance < 0.0) {
        message = "pixelSize and focalLength must be positive; virtualImageDistance must be finite and non-negative.";
        return false;
    }
    if (config.maxCandidates < 5 || config.maxCandidates > MaximumCandidateCount) {
        message = "maxCandidates must be within [5, 512].";
        return false;
    }
    if (!isFinite(config.opticalCenter.x) || !isFinite(config.opticalCenter.y) ||
        ((config.opticalCenter.x < 0.0) != (config.opticalCenter.y < 0.0))) {
        message = "opticalCenter must contain two finite image coordinates or two negative values to use image center.";
        return false;
    }
    if (config.virtualImageDistance > 0.0) {
        const double denominator = 1.0 / config.focalLength - 1.0 / config.virtualImageDistance;
        if (denominator <= Epsilon) {
            message = "virtualImageDistance must produce a positive image distance under the thin-lens relation.";
            return false;
        }
    }
    return true;
}

double calculateImageDistance(const BinocularFusionConfig& config)
{
    if (config.virtualImageDistance <= 0.0) {
        return config.focalLength;
    }
    return 1.0 / (1.0 / config.focalLength - 1.0 / config.virtualImageDistance);
}

cv::Mat makeBrightMask(
    const cv::Mat& gray,
    const BinocularFusionConfig& config,
    double& usedThreshold)
{
    cv::Mat processed = gray;
    if (config.blurKernel > 1) {
        cv::GaussianBlur(gray, processed, cv::Size(config.blurKernel, config.blurKernel), 0.0);
    }

    cv::Mat mask;
    if (config.threshold <= 0.0) {
        usedThreshold = cv::threshold(processed, mask, 0.0, 255.0, cv::THRESH_BINARY | cv::THRESH_OTSU);
    }
    else {
        usedThreshold = config.threshold;
        cv::threshold(processed, mask, usedThreshold, 255.0, cv::THRESH_BINARY);
    }

    if (config.morphKernel > 1) {
        const cv::Mat kernel = cv::getStructuringElement(
            cv::MORPH_CROSS, cv::Size(config.morphKernel, config.morphKernel));
        cv::morphologyEx(mask, mask, cv::MORPH_CLOSE, kernel);
    }
    return mask;
}

double calculateCrossShapeScore(const cv::Mat& labels, int label, const cv::Rect& rect, int area)
{
    if (rect.width <= 0 || rect.height <= 0 || area <= 0) {
        return 0.0;
    }

    const double centerX = rect.x + (rect.width - 1) * 0.5;
    const double centerY = rect.y + (rect.height - 1) * 0.5;
    const int verticalHalfWidth = std::max(1, static_cast<int>(std::round(rect.width * CrossArmHalfWidthRatio)));
    const int horizontalHalfHeight = std::max(1, static_cast<int>(std::round(rect.height * CrossArmHalfWidthRatio)));
    int pixelsOnArms = 0;
    int armRegionPixels = 0;
    int cornerForegroundPixels = 0;
    int cornerRegionPixels = 0;

    for (int y = rect.y; y < rect.y + rect.height; ++y) {
        const int* labelRow = labels.ptr<int>(y);
        for (int x = rect.x; x < rect.x + rect.width; ++x) {
            const bool inArmRegion = std::abs(x - centerX) <= verticalHalfWidth ||
                std::abs(y - centerY) <= horizontalHalfHeight;
            const bool isForeground = labelRow[x] == label;
            if (inArmRegion) {
                ++armRegionPixels;
            }
            else {
                ++cornerRegionPixels;
            }
            if (isForeground && inArmRegion) {
                ++pixelsOnArms;
            }
            else if (isForeground) {
                ++cornerForegroundPixels;
            }
        }
    }

    const double foregroundPurity = static_cast<double>(pixelsOnArms) / area;
    const double armFill = armRegionPixels > 0
        ? static_cast<double>(pixelsOnArms) / armRegionPixels : 0.0;
    const double cornerBlankness = cornerRegionPixels > 0
        ? 1.0 - static_cast<double>(cornerForegroundPixels) / cornerRegionPixels : 0.0;
    const double purityScore = clamp01((foregroundPurity - 0.55) / 0.40);
    const double aspectScore = static_cast<double>(std::min(rect.width, rect.height)) /
        std::max(rect.width, rect.height);
    return clamp01(0.45 * purityScore + 0.30 * cornerBlankness +
        0.20 * armFill + 0.05 * aspectScore);
}

cv::Point2d calculateWeightedCenter(
    const cv::Mat& gray,
    const cv::Mat& labels,
    int label,
    const cv::Rect& rect,
    double threshold)
{
    double sumWeight = 0.0;
    double sumX = 0.0;
    double sumY = 0.0;
    for (int y = rect.y; y < rect.y + rect.height; ++y) {
        const int* labelRow = labels.ptr<int>(y);
        const uchar* grayRow = gray.ptr<uchar>(y);
        for (int x = rect.x; x < rect.x + rect.width; ++x) {
            if (labelRow[x] != label) {
                continue;
            }
            const double weight = std::max(1.0, grayRow[x] - threshold + 1.0);
            sumWeight += weight;
            sumX += x * weight;
            sumY += y * weight;
        }
    }

    if (sumWeight <= Epsilon) {
        return cv::Point2d(rect.x + rect.width * 0.5, rect.y + rect.height * 0.5);
    }
    return cv::Point2d(sumX / sumWeight, sumY / sumWeight);
}

CandidateDetection detectCandidates(
    const cv::Mat& grayRoi,
    const cv::Point& roiOffset,
    const BinocularFusionConfig& config)
{
    CandidateDetection detection;
    cv::Mat mask = makeBrightMask(grayRoi, config, detection.threshold);

    cv::Mat labels;
    cv::Mat stats;
    cv::Mat centroids;
    const int labelCount = cv::connectedComponentsWithStats(mask, labels, stats, centroids, 8, CV_32S);
    detection.candidates.reserve(static_cast<size_t>(std::min(
        std::max(0, labelCount - 1), config.maxCandidates * 2)));

    const auto strongerCandidate = [](const auto& lhs, const auto& rhs) {
        if (lhs.shapeScore != rhs.shapeScore) return lhs.shapeScore > rhs.shapeScore;
        if (lhs.area != rhs.area) return lhs.area > rhs.area;
        if (lhs.center.y != rhs.center.y) return lhs.center.y < rhs.center.y;
        return lhs.center.x < rhs.center.x;
    };

    for (int label = 1; label < labelCount; ++label) {
        const int area = stats.at<int>(label, cv::CC_STAT_AREA);
        if (area < config.minArea || (config.maxArea > 0 && area > config.maxArea)) {
            continue;
        }

        const cv::Rect localRect(
            stats.at<int>(label, cv::CC_STAT_LEFT),
            stats.at<int>(label, cv::CC_STAT_TOP),
            stats.at<int>(label, cv::CC_STAT_WIDTH),
            stats.at<int>(label, cv::CC_STAT_HEIGHT));
        const double aspectScore = static_cast<double>(std::min(localRect.width, localRect.height)) /
            std::max(localRect.width, localRect.height);
        if (aspectScore < 0.18) {
            continue;
        }

        const double shapeScore = calculateCrossShapeScore(labels, label, localRect, area);
        if (shapeScore < MinimumCrossShapeScore) {
            continue;
        }

        BinocularCrossCandidate candidate;
        candidate.center = calculateWeightedCenter(grayRoi, labels, label, localRect, detection.threshold) +
            cv::Point2d(roiOffset.x, roiOffset.y);
        candidate.boundingRect = localRect + roiOffset;
        candidate.area = area;
        candidate.shapeScore = shapeScore;
        detection.candidates.push_back(candidate);
        if (static_cast<int>(detection.candidates.size()) >= config.maxCandidates * 2) {
            detection.truncated = true;
            std::stable_sort(detection.candidates.begin(), detection.candidates.end(), strongerCandidate);
            detection.candidates.resize(static_cast<size_t>(config.maxCandidates));
        }
    }

    if (static_cast<int>(detection.candidates.size()) > config.maxCandidates) {
        detection.truncated = true;
        std::stable_sort(detection.candidates.begin(), detection.candidates.end(), strongerCandidate);
        detection.candidates.resize(static_cast<size_t>(config.maxCandidates));
    }

    std::stable_sort(detection.candidates.begin(), detection.candidates.end(), [](const auto& lhs, const auto& rhs) {
        if (lhs.center.y != rhs.center.y) {
            return lhs.center.y < rhs.center.y;
        }
        if (lhs.center.x != rhs.center.x) {
            return lhs.center.x < rhs.center.x;
        }
        return lhs.area > rhs.area;
    });
    for (int i = 0; i < static_cast<int>(detection.candidates.size()); ++i) {
        detection.candidates[static_cast<size_t>(i)].id = i;
    }
    return detection;
}

std::vector<OppositePair> makeOppositePairs(
    const std::vector<BinocularCrossCandidate>& candidates,
    int centerIndex,
    double minimumDistance)
{
    std::vector<OppositePair> pairs;
    const cv::Point2d center = candidates[static_cast<size_t>(centerIndex)].center;
    for (int first = 0; first < static_cast<int>(candidates.size()); ++first) {
        if (first == centerIndex) {
            continue;
        }
        for (int second = first + 1; second < static_cast<int>(candidates.size()); ++second) {
            if (second == centerIndex) {
                continue;
            }

            const cv::Point2d vectorFirst = candidates[static_cast<size_t>(first)].center - center;
            const cv::Point2d vectorSecond = candidates[static_cast<size_t>(second)].center - center;
            const double lengthFirst = std::hypot(vectorFirst.x, vectorFirst.y);
            const double lengthSecond = std::hypot(vectorSecond.x, vectorSecond.y);
            if (lengthFirst < minimumDistance || lengthSecond < minimumDistance) {
                continue;
            }

            const double cosine = dot(vectorFirst, vectorSecond) / (lengthFirst * lengthSecond);
            if (cosine > -0.65) {
                continue;
            }
            const double symmetry = std::min(lengthFirst, lengthSecond) / std::max(lengthFirst, lengthSecond);
            if (symmetry < 0.25) {
                continue;
            }

            const cv::Point2d midpoint =
                (candidates[static_cast<size_t>(first)].center + candidates[static_cast<size_t>(second)].center) * 0.5;
            const double midpointResidual = distance(midpoint, center) / ((lengthFirst + lengthSecond) * 0.5);
            if (midpointResidual > 0.65) {
                continue;
            }

            const double oppositionScore = clamp01((-cosine - 0.65) / 0.35);
            const double symmetryScore = clamp01((symmetry - 0.25) / 0.75);
            const double midpointScore = clamp01(1.0 - midpointResidual / 0.65);
            const double shapeScore = 0.5 * (
                candidates[static_cast<size_t>(first)].shapeScore +
                candidates[static_cast<size_t>(second)].shapeScore);

            OppositePair pair;
            pair.first = first;
            pair.second = second;
            pair.axis = normalized(candidates[static_cast<size_t>(second)].center -
                candidates[static_cast<size_t>(first)].center);
            pair.score = 0.30 * oppositionScore + 0.30 * midpointScore +
                0.25 * symmetryScore + 0.15 * shapeScore;
            pairs.push_back(pair);
        }
    }

    std::stable_sort(pairs.begin(), pairs.end(), [](const auto& lhs, const auto& rhs) {
        if (lhs.score != rhs.score) {
            return lhs.score > rhs.score;
        }
        return std::tie(lhs.first, lhs.second) < std::tie(rhs.first, rhs.second);
    });
    if (pairs.size() > 32) {
        pairs.resize(32);
    }
    return pairs;
}

bool pairSharesPoint(const OppositePair& lhs, const OppositePair& rhs)
{
    return lhs.first == rhs.first || lhs.first == rhs.second ||
        lhs.second == rhs.first || lhs.second == rhs.second;
}

std::array<int, 5> selectionKey(const CrossSelection& selection)
{
    return { selection.center, selection.top, selection.bottom, selection.left, selection.right };
}

CrossSelection selectCross(
    const std::vector<BinocularCrossCandidate>& candidates,
    const cv::Point2d& opticalCenter,
    const cv::Rect& searchRoi,
    int minArea)
{
    CrossSelection best;
    std::vector<int> centerChoices(candidates.size());
    std::iota(centerChoices.begin(), centerChoices.end(), 0);
    std::stable_sort(centerChoices.begin(), centerChoices.end(), [&](int lhs, int rhs) {
        const double lhsDistance = distance(candidates[static_cast<size_t>(lhs)].center, opticalCenter);
        const double rhsDistance = distance(candidates[static_cast<size_t>(rhs)].center, opticalCenter);
        if (lhsDistance != rhsDistance) {
            return lhsDistance < rhsDistance;
        }
        if (candidates[static_cast<size_t>(lhs)].shapeScore != candidates[static_cast<size_t>(rhs)].shapeScore) {
            return candidates[static_cast<size_t>(lhs)].shapeScore > candidates[static_cast<size_t>(rhs)].shapeScore;
        }
        return lhs < rhs;
    });
    if (centerChoices.size() > 16) {
        centerChoices.resize(16);
    }

    const double minimumDistance = std::max(2.0, std::sqrt(static_cast<double>(minArea)) * 0.5);
    const double roiDiagonal = std::max(1.0, std::hypot(searchRoi.width, searchRoi.height));
    for (int centerIndex : centerChoices) {
        const std::vector<OppositePair> pairs = makeOppositePairs(candidates, centerIndex, minimumDistance);
        for (size_t firstPairIndex = 0; firstPairIndex < pairs.size(); ++firstPairIndex) {
            for (size_t secondPairIndex = firstPairIndex + 1; secondPairIndex < pairs.size(); ++secondPairIndex) {
                const OppositePair& firstPair = pairs[firstPairIndex];
                const OppositePair& secondPair = pairs[secondPairIndex];
                if (pairSharesPoint(firstPair, secondPair)) {
                    continue;
                }

                const double perpendicularError = std::abs(dot(firstPair.axis, secondPair.axis));
                if (perpendicularError > 0.40) {
                    continue;
                }

                const OppositePair* horizontal = &firstPair;
                const OppositePair* vertical = &secondPair;
                if (std::abs(horizontal->axis.x) < std::abs(vertical->axis.x)) {
                    std::swap(horizontal, vertical);
                }

                cv::Point2d horizontalAxis = horizontal->axis;
                cv::Point2d verticalAxis = vertical->axis;
                if (horizontalAxis.x < 0.0) {
                    horizontalAxis *= -1.0;
                }
                if (verticalAxis.y < 0.0) {
                    verticalAxis *= -1.0;
                }

                const cv::Point2d center = candidates[static_cast<size_t>(centerIndex)].center;
                const double horizontalFirstProjection = dot(
                    candidates[static_cast<size_t>(horizontal->first)].center - center, horizontalAxis);
                const double horizontalSecondProjection = dot(
                    candidates[static_cast<size_t>(horizontal->second)].center - center, horizontalAxis);
                const int left = horizontalFirstProjection < horizontalSecondProjection
                    ? horizontal->first : horizontal->second;
                const int right = horizontalFirstProjection < horizontalSecondProjection
                    ? horizontal->second : horizontal->first;
                if (std::min(horizontalFirstProjection, horizontalSecondProjection) >= 0.0 ||
                    std::max(horizontalFirstProjection, horizontalSecondProjection) <= 0.0) {
                    continue;
                }

                const double verticalFirstProjection = dot(
                    candidates[static_cast<size_t>(vertical->first)].center - center, verticalAxis);
                const double verticalSecondProjection = dot(
                    candidates[static_cast<size_t>(vertical->second)].center - center, verticalAxis);
                const int top = verticalFirstProjection < verticalSecondProjection
                    ? vertical->first : vertical->second;
                const int bottom = verticalFirstProjection < verticalSecondProjection
                    ? vertical->second : vertical->first;
                if (std::min(verticalFirstProjection, verticalSecondProjection) >= 0.0 ||
                    std::max(verticalFirstProjection, verticalSecondProjection) <= 0.0) {
                    continue;
                }

                const double perpendicularScore = clamp01(1.0 - perpendicularError / 0.40);
                const double shapeScore = (
                    candidates[static_cast<size_t>(centerIndex)].shapeScore +
                    candidates[static_cast<size_t>(top)].shapeScore +
                    candidates[static_cast<size_t>(bottom)].shapeScore +
                    candidates[static_cast<size_t>(left)].shapeScore +
                    candidates[static_cast<size_t>(right)].shapeScore) / 5.0;
                const double opticalScore = std::exp(-distance(center, opticalCenter) / (0.5 * roiDiagonal));

                CrossSelection selection;
                selection.valid = true;
                selection.center = centerIndex;
                selection.top = top;
                selection.bottom = bottom;
                selection.left = left;
                selection.right = right;
                selection.quality = clamp01(
                    0.32 * firstPair.score + 0.32 * secondPair.score +
                    0.18 * perpendicularScore + 0.13 * shapeScore + 0.05 * opticalScore);

                if (!best.valid || selection.quality > best.quality + Epsilon ||
                    (std::abs(selection.quality - best.quality) <= Epsilon && selectionKey(selection) < selectionKey(best))) {
                    best = selection;
                }
            }
        }
    }
    return best;
}

BinocularCrossPoint makeCrossPoint(
    const char* role,
    const BinocularCrossCandidate& candidate)
{
    BinocularCrossPoint point;
    point.role = role;
    point.candidateId = candidate.id;
    point.center = candidate.center;
    point.boundingRect = candidate.boundingRect;
    point.area = candidate.area;
    point.shapeScore = candidate.shapeScore;
    return point;
}

double radiansToDegrees(double radians)
{
    return radians * 180.0 / Pi;
}

double normalizeLineAngle(double degrees)
{
    while (degrees > 90.0) {
        degrees -= 180.0;
    }
    while (degrees <= -90.0) {
        degrees += 180.0;
    }
    return degrees;
}

double averageLineAngles(double firstDegrees, double secondDegrees)
{
    const double first = firstDegrees * Pi / 180.0;
    const double second = secondDegrees * Pi / 180.0;
    return normalizeLineAngle(radiansToDegrees(0.5 * std::atan2(
        std::sin(2.0 * first) + std::sin(2.0 * second),
        std::cos(2.0 * first) + std::cos(2.0 * second))));
}

bool touchesBoundary(const cv::Rect& rect, const cv::Rect& bounds)
{
    return rect.x <= bounds.x || rect.y <= bounds.y ||
        rect.x + rect.width >= bounds.x + bounds.width ||
        rect.y + rect.height >= bounds.y + bounds.height;
}

nlohmann::json pointJson(const cv::Point2d& point)
{
    return { { "x", point.x }, { "y", point.y } };
}

nlohmann::json rectJson(const cv::Rect& rect)
{
    return { { "x", rect.x }, { "y", rect.y }, { "width", rect.width }, { "height", rect.height } };
}

} // namespace

BinocularFusionResult calculateBinocularFusion(
    const cv::Mat& image,
    const cv::Rect& roi,
    const BinocularFusionConfig& config)
{
    BinocularFusionResult result;
    result.imageSize = image.empty() ? cv::Size() : image.size();

    std::string validationMessage;
    if (!validateConfig(config, validationMessage)) {
        result.statusCode = "invalid_config";
        result.message = validationMessage;
        return result;
    }

    cv::Mat gray8;
    if (!toGray8(image, gray8)) {
        result.statusCode = "invalid_image";
        result.message = "Image is empty, contains non-finite data, or has an unsupported channel count.";
        return result;
    }

    bool roiClipped = false;
    if (!prepareRoi(gray8.size(), roi, result.searchRoi, roiClipped)) {
        result.statusCode = "invalid_roi";
        result.message = "ROI must have a positive intersection with the image.";
        return result;
    }
    if (roiClipped) {
        result.warnings.push_back("ROI extended outside the image and was clipped to valid image bounds.");
    }
    if ((config.blurKernel > result.searchRoi.width || config.blurKernel > result.searchRoi.height) ||
        (config.morphKernel > result.searchRoi.width || config.morphKernel > result.searchRoi.height)) {
        result.statusCode = "invalid_config";
        result.message = "blurKernel and morphKernel must not exceed the effective ROI dimensions.";
        return result;
    }

    if (isDisabledOpticalCenter(config.opticalCenter)) {
        result.effectiveOpticalCenter = cv::Point2d(image.cols * 0.5, image.rows * 0.5);
        result.warnings.push_back("opticalCenter was not supplied; complete-image center was used.");
    }
    else {
        if (config.opticalCenter.x < 0.0 || config.opticalCenter.x >= image.cols ||
            config.opticalCenter.y < 0.0 || config.opticalCenter.y >= image.rows) {
            result.statusCode = "invalid_config";
            result.message = "opticalCenter must be inside the image.";
            return result;
        }
        result.effectiveOpticalCenter = config.opticalCenter;
    }
    result.imageDistance = calculateImageDistance(config);

    try {
        const CandidateDetection detection = detectCandidates(
            gray8(result.searchRoi), result.searchRoi.tl(), config);
        result.candidates = detection.candidates;
        if (detection.truncated) {
            result.warnings.push_back("Candidate count exceeded maxCandidates; only the strongest shape/area candidates were retained.");
        }
    }
    catch (const cv::Exception& exception) {
        result.statusCode = "opencv_error";
        result.message = exception.what();
        return result;
    }

    if (result.candidates.size() < 5) {
        result.statusCode = "too_few_candidates";
        result.message = "Fewer than five valid bright cross candidates were detected.";
        return result;
    }
    if (result.candidates.size() > 5) {
        result.warnings.push_back("More than five candidates were detected; geometric scoring selected one center and two opposite arm pairs.");
    }

    const CrossSelection selection = selectCross(
        result.candidates, result.effectiveOpticalCenter, result.searchRoi, config.minArea);
    if (!selection.valid) {
        result.statusCode = "geometry_not_found";
        result.message = "Candidates could not satisfy the center/opposite-pair/perpendicular-axis constraints.";
        return result;
    }

    result.points.center = makeCrossPoint("center", result.candidates[static_cast<size_t>(selection.center)]);
    result.points.top = makeCrossPoint("top", result.candidates[static_cast<size_t>(selection.top)]);
    result.points.bottom = makeCrossPoint("bottom", result.candidates[static_cast<size_t>(selection.bottom)]);
    result.points.left = makeCrossPoint("left", result.candidates[static_cast<size_t>(selection.left)]);
    result.points.right = makeCrossPoint("right", result.candidates[static_cast<size_t>(selection.right)]);

    const cv::Point2d horizontal = result.points.right.center - result.points.left.center;
    const cv::Point2d vertical = result.points.bottom.center - result.points.top.center;
    const double horizontalRoll = normalizeLineAngle(radiansToDegrees(std::atan2(-horizontal.y, horizontal.x)));
    const double verticalRoll = normalizeLineAngle(radiansToDegrees(std::atan2(vertical.x, vertical.y)));
    result.rollDegrees = averageLineAngles(horizontalRoll, verticalRoll);

    const double rollDisagreement = std::abs(normalizeLineAngle(horizontalRoll - verticalRoll));
    const double agreementScore = clamp01(1.0 - rollDisagreement / 15.0);
    result.quality = clamp01(selection.quality * (0.8 + 0.2 * agreementScore));
    if (rollDisagreement > 3.0) {
        result.warnings.push_back("Horizontal and vertical roll estimates differ by more than 3 degrees.");
    }
    if (std::abs(result.rollDegrees) > 30.0) {
        result.warnings.push_back("Absolute roll exceeds 30 degrees; horizontal/vertical role assignment may be ambiguous.");
    }

    const cv::Point2d center = result.points.center.center;
    const double imageDistanceUm = result.imageDistance * 1000.0;
    result.horizontalOffsetDegrees = radiansToDegrees(std::atan2(
        config.pixelSize * (center.x - result.effectiveOpticalCenter.x), imageDistanceUm));
    result.verticalOffsetDegrees = radiansToDegrees(std::atan2(
        config.pixelSize * (result.effectiveOpticalCenter.y - center.y), imageDistanceUm));

    const std::array<const BinocularCrossPoint*, 5> selectedPoints = {
        &result.points.center, &result.points.top, &result.points.bottom,
        &result.points.left, &result.points.right
    };
    if (std::any_of(selectedPoints.begin(), selectedPoints.end(), [&](const BinocularCrossPoint* point) {
        return touchesBoundary(point->boundingRect, result.searchRoi);
    })) {
        result.warnings.push_back("At least one selected cross touches the ROI boundary; its centroid or area may be clipped.");
    }
    if (result.quality < 0.60) {
        result.warnings.push_back("Geometric quality is below 0.60; inspect threshold, ROI, reflections, and missing marks.");
    }

    result.success = true;
    result.statusCode = result.warnings.empty() ? "ok" : "ok_with_warnings";
    result.message = "ok";
    return result;
}

nlohmann::json ToJson(const BinocularCrossCandidate& candidate)
{
    return {
        { "id", candidate.id },
        { "center", pointJson(candidate.center) },
        { "boundingRect", rectJson(candidate.boundingRect) },
        { "area", candidate.area },
        { "shapeScore", candidate.shapeScore }
    };
}

nlohmann::json ToJson(const BinocularCrossPoint& point)
{
    return {
        { "role", point.role },
        { "candidateId", point.candidateId },
        { "center", pointJson(point.center) },
        { "boundingRect", rectJson(point.boundingRect) },
        { "area", point.area },
        { "shapeScore", point.shapeScore }
    };
}

nlohmann::json ToJson(const BinocularFusionResult& result)
{
    nlohmann::json candidates = nlohmann::json::array();
    for (const BinocularCrossCandidate& candidate : result.candidates) {
        candidates.push_back(ToJson(candidate));
    }
    return {
        { "success", result.success },
        { "statusCode", result.statusCode },
        { "message", result.message },
        { "imageSize", { { "width", result.imageSize.width }, { "height", result.imageSize.height } } },
        { "searchRoi", rectJson(result.searchRoi) },
        { "effectiveOpticalCenter", pointJson(result.effectiveOpticalCenter) },
        { "candidates", std::move(candidates) },
        { "points", {
            { "center", ToJson(result.points.center) },
            { "top", ToJson(result.points.top) },
            { "bottom", ToJson(result.points.bottom) },
            { "left", ToJson(result.points.left) },
            { "right", ToJson(result.points.right) }
        } },
        { "rollDegrees", result.rollDegrees },
        { "horizontalOffsetDegrees", result.horizontalOffsetDegrees },
        { "verticalOffsetDegrees", result.verticalOffsetDegrees },
        { "imageDistanceMm", result.imageDistance },
        { "quality", result.quality },
        { "warnings", result.warnings }
    };
}

} // namespace binocular
} // namespace cvcore
