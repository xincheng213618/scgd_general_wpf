#include "ghost_detection.h"

#include <nlohmann/json.hpp>

#include <algorithm>
#include <array>
#include <cmath>
#include <cstdint>
#include <limits>
#include <numeric>
#include <utility>

namespace cvcore {
namespace ghost {

namespace {

constexpr double Epsilon = 1e-9;
constexpr int MaximumGridSources = 100000;
constexpr int MaximumMorphologyKernel = 4095;
constexpr int MaximumSourcePadding = (MaximumMorphologyKernel - 1) / 2;
constexpr int MaximumBackgroundKernel = 511;
constexpr int MaximumScaleLevels = 8;
constexpr int ExposureHistogramBins = 4096;
constexpr double Pi = 3.14159265358979323846;

struct Component
{
    int label = 0;
    cv::Rect boundingRectInRoi;
    cv::Point2d centerInRoi;
    int area = 0;
    double meanIntensity = 0.0;
    double peakIntensity = 0.0;
};

struct ComponentLimits
{
    int minArea = 0;
    int maxArea = 0;
    int minSize = 0;
    int maxSize = 0;
};

bool isNormalizedThreshold(double value)
{
    return std::isfinite(value) && value >= 0.0 && value <= 1.0;
}

bool validateConfig(const GhostDetectionConfig& config, std::string& message)
{
    if (config.brightGridRows <= 0 || config.brightGridCols <= 0) {
        message = "Bright grid rows and columns must be positive.";
        return false;
    }
    if (config.brightGridRows > MaximumGridSources || config.brightGridCols > MaximumGridSources ||
        static_cast<std::int64_t>(config.brightGridRows) * config.brightGridCols > MaximumGridSources) {
        message = "Configured bright-source grid is too large.";
        return false;
    }
    if (config.brightThresholdMode == ThresholdMode::Fixed && !isNormalizedThreshold(config.brightThreshold)) {
        message = "Fixed bright threshold must be in the normalized range [0, 1].";
        return false;
    }
    if (config.ghostThresholdMode == ThresholdMode::Fixed && !isNormalizedThreshold(config.ghostThreshold)) {
        message = "Fixed ghost threshold must be in the normalized range [0, 1].";
        return false;
    }
    if (config.brightMinArea < 0 || config.brightMaxArea < 0 || config.ghostMinArea < 0 || config.ghostMaxArea < 0 ||
        config.brightMinSize < 0 || config.brightMaxSize < 0 || config.ghostMinSize < 0 || config.ghostMaxSize < 0) {
        message = "Component area and size limits cannot be negative.";
        return false;
    }
    if ((config.brightMaxArea > 0 && config.brightMaxArea < config.brightMinArea) ||
        (config.ghostMaxArea > 0 && config.ghostMaxArea < config.ghostMinArea) ||
        (config.brightMaxSize > 0 && config.brightMaxSize < config.brightMinSize) ||
        (config.ghostMaxSize > 0 && config.ghostMaxSize < config.ghostMinSize)) {
        message = "Maximum component limits must be zero (unbounded) or greater than or equal to minimum limits.";
        return false;
    }
    if (config.brightOpenKernel < 0 || config.brightCloseKernel < 0 ||
        config.ghostOpenKernel < 0 || config.ghostCloseKernel < 0 ||
        config.brightOpenKernel > MaximumMorphologyKernel || config.brightCloseKernel > MaximumMorphologyKernel ||
        config.ghostOpenKernel > MaximumMorphologyKernel || config.ghostCloseKernel > MaximumMorphologyKernel ||
        config.sourceMaskPadding < 0 || config.sourceMaskPadding > MaximumSourcePadding ||
        !std::isfinite(config.minDistanceFromBright) || config.minDistanceFromBright < 0.0 ||
        !std::isfinite(config.minRelativeIntensity) || config.minRelativeIntensity < 0.0 || config.maxCandidates < 0) {
        message = "Morphology, padding, distance, relative intensity, or candidate limits are invalid.";
        return false;
    }
    if (!std::isfinite(config.exposureLowPercentile) || !std::isfinite(config.exposureHighPercentile) ||
        config.exposureLowPercentile < 0.0 || config.exposureHighPercentile > 1.0 ||
        config.exposureLowPercentile >= config.exposureHighPercentile) {
        message = "Exposure percentiles must be finite, ordered, and within [0, 1].";
        return false;
    }
    if (config.backgroundKernel < 0 || config.backgroundKernel > MaximumBackgroundKernel ||
        !std::isfinite(config.backgroundSigma) || config.backgroundSigma < 0.0) {
        message = "Background kernel and sigma are invalid.";
        return false;
    }
    if (config.multiScaleLevels < 1 || config.multiScaleLevels > MaximumScaleLevels ||
        !std::isfinite(config.multiScaleFactor) || config.multiScaleFactor <= 1.0 || config.multiScaleFactor > 4.0 ||
        !std::isfinite(config.multiScaleThresholdFactor) ||
        config.multiScaleThresholdFactor < 0.5 || config.multiScaleThresholdFactor > 1.0) {
        message = "Multi-scale levels, scale factor, or threshold factor are invalid.";
        return false;
    }
    const bool automaticOpticalCenter = config.opticalCenter.x < 0.0 && config.opticalCenter.y < 0.0;
    const bool explicitOpticalCenter = config.opticalCenter.x >= 0.0 && config.opticalCenter.y >= 0.0;
    if (!std::isfinite(config.opticalCenter.x) || !std::isfinite(config.opticalCenter.y) ||
        (!automaticOpticalCenter && !explicitOpticalCenter) || !std::isfinite(config.minDirectionConfidence) ||
        config.minDirectionConfidence < 0.0 || config.minDirectionConfidence > 1.0) {
        message = "Optical center or directional confidence threshold is invalid.";
        return false;
    }
    if (!std::isfinite(config.minorSeverity) || !std::isfinite(config.majorSeverity) || !std::isfinite(config.criticalSeverity) ||
        config.minorSeverity < 0.0 || config.majorSeverity < config.minorSeverity || config.criticalSeverity < config.majorSeverity) {
        message = "Severity thresholds must be finite, non-negative, and ordered minor <= major <= critical.";
        return false;
    }
    return true;
}

bool resolveRoi(const cv::Mat& image, const cv::Rect& requested, cv::Rect& roi)
{
    if (requested.width == 0 && requested.height == 0) {
        roi = cv::Rect(0, 0, image.cols, image.rows);
        return true;
    }

    if (requested.x < 0 || requested.y < 0 || requested.width <= 0 || requested.height <= 0 ||
        requested.x > image.cols - requested.width || requested.y > image.rows - requested.height) {
        return false;
    }

    roi = requested;
    return true;
}

bool convertToNormalizedGray(const cv::Mat& image, int channel, cv::Mat& gray32)
{
    if (image.empty() || (image.depth() != CV_8U && image.depth() != CV_16U) ||
        (image.channels() != 1 && image.channels() != 3)) {
        return false;
    }

    cv::Mat gray;
    if (image.channels() == 1) {
        if (channel > 0) {
            return false;
        }
        gray = image;
    }
    else if (channel >= 0) {
        if (channel >= image.channels()) {
            return false;
        }
        cv::extractChannel(image, gray, channel);
    }
    else {
        cv::cvtColor(image, gray, cv::COLOR_BGR2GRAY);
    }

    const double scale = gray.depth() == CV_8U ? (1.0 / 255.0) : (1.0 / 65535.0);
    gray.convertTo(gray32, CV_32F, scale);
    return !gray32.empty();
}

double clamp01(double value)
{
    return std::max(0.0, std::min(1.0, value));
}

double histogramPercentile(const std::array<std::uint64_t, ExposureHistogramBins>& histogram, std::uint64_t total, double percentile)
{
    if (total == 0) {
        return 0.0;
    }

    const std::uint64_t rank = static_cast<std::uint64_t>(std::floor(percentile * static_cast<double>(total - 1)));
    std::uint64_t cumulative = 0;
    for (int bin = 0; bin < ExposureHistogramBins; ++bin) {
        cumulative += histogram[static_cast<size_t>(bin)];
        if (cumulative > rank) {
            return static_cast<double>(bin) / static_cast<double>(ExposureHistogramBins - 1);
        }
    }
    return 1.0;
}

bool normalizeExposureByPercentile(
    cv::Mat& gray32,
    double lowPercentile,
    double highPercentile,
    double& lowUsed,
    double& highUsed)
{
    std::array<std::uint64_t, ExposureHistogramBins> histogram{};
    std::uint64_t total = 0;
    for (int y = 0; y < gray32.rows; ++y) {
        const float* row = gray32.ptr<float>(y);
        for (int x = 0; x < gray32.cols; ++x) {
            const int bin = cvRound(clamp01(row[x]) * static_cast<double>(ExposureHistogramBins - 1));
            histogram[static_cast<size_t>(std::max(0, std::min(ExposureHistogramBins - 1, bin)))]++;
            total++;
        }
    }

    lowUsed = histogramPercentile(histogram, total, lowPercentile);
    highUsed = histogramPercentile(histogram, total, highPercentile);
    if (highUsed - lowUsed <= Epsilon) {
        return false;
    }

    gray32.convertTo(gray32, CV_32F, 1.0 / (highUsed - lowUsed), -lowUsed / (highUsed - lowUsed));
    cv::max(gray32, 0.0, gray32);
    cv::min(gray32, 1.0, gray32);
    return true;
}

int scaledOddKernel(int baseKernel, double scale)
{
    int kernel = std::max(3, cvRound(static_cast<double>(baseKernel) * scale));
    if (kernel % 2 == 0) {
        ++kernel;
    }
    return std::min(MaximumBackgroundKernel, kernel);
}

cv::Mat makeAnalysisResponse(const cv::Mat& gray32, const GhostDetectionConfig& config, int level)
{
    const double levelScale = std::pow(config.multiScaleFactor, level);
    if (config.backgroundKernel > 1) {
        cv::Mat background;
        const int kernel = scaledOddKernel(config.backgroundKernel, levelScale);
        const double sigma = config.backgroundSigma > 0.0 ? config.backgroundSigma * levelScale : 0.0;
        cv::GaussianBlur(gray32, background, cv::Size(kernel, kernel), sigma, sigma, cv::BORDER_REPLICATE);
        cv::Mat response;
        cv::subtract(gray32, background, response);
        cv::max(response, 0.0, response);
        return response;
    }

    if (level == 0) {
        return gray32.clone();
    }

    cv::Mat response;
    const int kernel = scaledOddKernel(3, levelScale);
    cv::GaussianBlur(gray32, response, cv::Size(kernel, kernel), 0.0, 0.0, cv::BORDER_REPLICATE);
    return response;
}

int normalizedOddKernel(int value)
{
    if (value <= 1) {
        return 0;
    }
    return value % 2 == 0 ? value + 1 : value;
}

void applyMorphology(cv::Mat& mask, int openKernel, int closeKernel)
{
    const int openSize = normalizedOddKernel(openKernel);
    if (openSize > 1) {
        const cv::Mat kernel = cv::getStructuringElement(cv::MORPH_ELLIPSE, cv::Size(openSize, openSize));
        cv::morphologyEx(mask, mask, cv::MORPH_OPEN, kernel);
    }

    const int closeSize = normalizedOddKernel(closeKernel);
    if (closeSize > 1) {
        const cv::Mat kernel = cv::getStructuringElement(cv::MORPH_ELLIPSE, cv::Size(closeSize, closeSize));
        cv::morphologyEx(mask, mask, cv::MORPH_CLOSE, kernel);
    }
}

double automaticOtsuThreshold(const cv::Mat& gray32, const cv::Mat& validMask)
{
    std::array<std::uint64_t, 256> histogram{};
    std::uint64_t total = 0;
    double weightedSum = 0.0;

    for (int y = 0; y < gray32.rows; ++y) {
        const float* grayRow = gray32.ptr<float>(y);
        const uchar* maskRow = validMask.empty() ? nullptr : validMask.ptr<uchar>(y);
        for (int x = 0; x < gray32.cols; ++x) {
            if (maskRow != nullptr && maskRow[x] == 0) {
                continue;
            }

            const double value = std::max(0.0, std::min(1.0, static_cast<double>(grayRow[x])));
            const int bin = std::max(0, std::min(255, cvRound(value * 255.0)));
            histogram[static_cast<size_t>(bin)]++;
            total++;
            weightedSum += static_cast<double>(bin);
        }
    }

    if (total == 0) {
        return 1.0;
    }

    std::uint64_t backgroundWeight = 0;
    double backgroundSum = 0.0;
    double bestVariance = -1.0;
    int bestThreshold = 0;
    for (int threshold = 0; threshold < 256; ++threshold) {
        const std::uint64_t count = histogram[static_cast<size_t>(threshold)];
        backgroundWeight += count;
        backgroundSum += static_cast<double>(threshold) * static_cast<double>(count);
        if (backgroundWeight == 0) {
            continue;
        }

        const std::uint64_t foregroundWeight = total - backgroundWeight;
        if (foregroundWeight == 0) {
            break;
        }

        const double backgroundMean = backgroundSum / static_cast<double>(backgroundWeight);
        const double foregroundMean = (weightedSum - backgroundSum) / static_cast<double>(foregroundWeight);
        const double meanDelta = backgroundMean - foregroundMean;
        const double variance = static_cast<double>(backgroundWeight) * static_cast<double>(foregroundWeight) * meanDelta * meanDelta;
        if (variance > bestVariance) {
            bestVariance = variance;
            bestThreshold = threshold;
        }
    }

    return static_cast<double>(bestThreshold) / 255.0;
}

cv::Mat thresholdMask(const cv::Mat& gray32, double threshold, int openKernel, int closeKernel)
{
    cv::Mat mask;
    cv::compare(gray32, cv::Scalar::all(threshold), mask, cv::CMP_GT);
    applyMorphology(mask, openKernel, closeKernel);
    return mask;
}

bool isWithinLimits(const cv::Rect& rect, int area, const ComponentLimits& limits)
{
    if (rect.width <= 0 || rect.height <= 0 || area <= 0 || area < limits.minArea) {
        return false;
    }
    if (limits.maxArea > 0 && area > limits.maxArea) {
        return false;
    }

    const int minSide = std::min(rect.width, rect.height);
    const int maxSide = std::max(rect.width, rect.height);
    if (minSide < limits.minSize) {
        return false;
    }
    return limits.maxSize <= 0 || maxSide <= limits.maxSize;
}

std::vector<Component> findComponents(
    const cv::Mat& gray32,
    const cv::Mat& mask,
    const ComponentLimits& limits,
    cv::Mat& labels)
{
    cv::Mat stats;
    cv::Mat centroids;
    const int labelCount = cv::connectedComponentsWithStats(mask, labels, stats, centroids, 8, CV_32S);

    std::vector<Component> components;
    components.reserve(static_cast<size_t>(std::max(0, labelCount - 1)));
    for (int label = 1; label < labelCount; ++label) {
        const cv::Rect rect(
            stats.at<int>(label, cv::CC_STAT_LEFT),
            stats.at<int>(label, cv::CC_STAT_TOP),
            stats.at<int>(label, cv::CC_STAT_WIDTH),
            stats.at<int>(label, cv::CC_STAT_HEIGHT));
        const int area = stats.at<int>(label, cv::CC_STAT_AREA);
        if (!isWithinLimits(rect, area, limits)) {
            continue;
        }

        const cv::Mat labelRoi = labels(rect);
        const cv::Mat grayRoi = gray32(rect);
        cv::Mat componentMask;
        cv::compare(labelRoi, cv::Scalar(label), componentMask, cv::CMP_EQ);
        double peak = 0.0;
        cv::minMaxLoc(grayRoi, nullptr, &peak, nullptr, nullptr, componentMask);

        Component component;
        component.label = label;
        component.boundingRectInRoi = rect;
        component.centerInRoi = cv::Point2d(centroids.at<double>(label, 0), centroids.at<double>(label, 1));
        component.area = area;
        component.meanIntensity = cv::mean(grayRoi, componentMask)[0];
        component.peakIntensity = peak;
        components.push_back(std::move(component));
    }
    return components;
}

void selectAndOrderBrightComponents(std::vector<Component>& components, int rows, int cols, std::vector<std::string>& warnings)
{
    const int expectedCount = rows * cols;
    if (static_cast<int>(components.size()) > expectedCount) {
        std::sort(components.begin(), components.end(), [](const Component& a, const Component& b) {
            if (a.meanIntensity != b.meanIntensity) {
                return a.meanIntensity > b.meanIntensity;
            }
            if (a.peakIntensity != b.peakIntensity) {
                return a.peakIntensity > b.peakIntensity;
            }
            return a.area > b.area;
        });
        components.resize(static_cast<size_t>(expectedCount));
        warnings.push_back("More bright-source components than expected were detected; the strongest grid-sized set was selected.");
    }
    else if (static_cast<int>(components.size()) < expectedCount) {
        warnings.push_back("Fewer bright-source components than the configured grid were detected.");
    }

    std::sort(components.begin(), components.end(), [](const Component& a, const Component& b) {
        if (a.centerInRoi.y != b.centerInRoi.y) {
            return a.centerInRoi.y < b.centerInRoi.y;
        }
        return a.centerInRoi.x < b.centerInRoi.x;
    });

    if (static_cast<int>(components.size()) == expectedCount) {
        for (int row = 0; row < rows; ++row) {
            const auto first = components.begin() + static_cast<std::ptrdiff_t>(row * cols);
            const auto last = first + cols;
            std::sort(first, last, [](const Component& a, const Component& b) {
                return a.centerInRoi.x < b.centerInRoi.x;
            });
        }
    }
}

cv::Rect offsetRect(const cv::Rect& rect, const cv::Point& offset)
{
    return cv::Rect(rect.x + offset.x, rect.y + offset.y, rect.width, rect.height);
}

std::vector<BrightSource> makeBrightSources(const std::vector<Component>& components, const cv::Rect& roi, int gridCols)
{
    std::vector<BrightSource> sources;
    sources.reserve(components.size());
    for (int i = 0; i < static_cast<int>(components.size()); ++i) {
        const Component& component = components[static_cast<size_t>(i)];
        BrightSource source;
        source.id = i + 1;
        source.gridRow = i / gridCols;
        source.gridCol = i % gridCols;
        source.centerInRoi = component.centerInRoi;
        source.center = component.centerInRoi + cv::Point2d(roi.x, roi.y);
        source.boundingRectInRoi = component.boundingRectInRoi;
        source.boundingRect = offsetRect(component.boundingRectInRoi, roi.tl());
        source.area = component.area;
        source.meanIntensity = component.meanIntensity;
        source.peakIntensity = component.peakIntensity;
        sources.push_back(std::move(source));
    }
    return sources;
}

cv::Mat makeSourceExclusionMask(const cv::Mat& labels, const std::vector<Component>& sources, int padding)
{
    cv::Mat exclusion = cv::Mat::zeros(labels.size(), CV_8U);
    for (const Component& source : sources) {
        const cv::Rect& rect = source.boundingRectInRoi;
        const cv::Mat labelRoi = labels(rect);
        cv::Mat exclusionRoi = exclusion(rect);
        for (int row = 0; row < rect.height; ++row) {
            const int* labelRow = labelRoi.ptr<int>(row);
            uchar* exclusionRow = exclusionRoi.ptr<uchar>(row);
            for (int col = 0; col < rect.width; ++col) {
                if (labelRow[col] == source.label) {
                    exclusionRow[col] = 255;
                }
            }
        }
    }

    if (padding > 0) {
        const int kernelSize = padding * 2 + 1;
        const cv::Mat kernel = cv::getStructuringElement(cv::MORPH_ELLIPSE, cv::Size(kernelSize, kernelSize));
        cv::dilate(exclusion, exclusion, kernel);
    }
    return exclusion;
}

std::string gradeForSeverity(double severity, const GhostDetectionConfig& config)
{
    if (severity >= config.criticalSeverity) {
        return "critical";
    }
    if (severity >= config.majorSeverity) {
        return "major";
    }
    if (severity >= config.minorSeverity) {
        return "minor";
    }
    return "trace";
}

double componentScaleSupport(
    const Component& component,
    const cv::Mat& labels,
    const std::vector<cv::Mat>& scaleMasks)
{
    if (scaleMasks.empty()) {
        return 1.0;
    }

    const cv::Rect& rect = component.boundingRectInRoi;
    cv::Mat componentMask;
    cv::compare(labels(rect), cv::Scalar(component.label), componentMask, cv::CMP_EQ);
    const int minimumOverlap = std::max(1, component.area / 10);
    int supportedScales = 0;
    for (const cv::Mat& scaleMask : scaleMasks) {
        cv::Mat overlap;
        cv::bitwise_and(scaleMask(rect), componentMask, overlap);
        if (cv::countNonZero(overlap) >= minimumOverlap) {
            supportedScales++;
        }
    }
    return static_cast<double>(supportedScales) / static_cast<double>(scaleMasks.size());
}

std::vector<GhostCandidate> makeGhostCandidates(
    const std::vector<Component>& components,
    const std::vector<BrightSource>& brightSources,
    const cv::Rect& roi,
    double backgroundMean,
    double referenceBrightMean,
    const cv::Mat& labels,
    const std::vector<cv::Mat>& scaleMasks,
    const GhostDetectionConfig& config,
    bool& truncated)
{
    const double referenceDelta = std::max(Epsilon, referenceBrightMean - backgroundMean);
    std::vector<GhostCandidate> candidates;
    candidates.reserve(components.size());
    for (const Component& component : components) {
        double nearestDistance = std::numeric_limits<double>::infinity();
        const BrightSource* nearestSource = nullptr;
        for (const BrightSource& source : brightSources) {
            const double distance = cv::norm(component.centerInRoi - source.centerInRoi);
            if (distance < nearestDistance) {
                nearestDistance = distance;
                nearestSource = &source;
            }
        }

        if (nearestDistance < config.minDistanceFromBright) {
            continue;
        }

        const double relativeIntensity = std::max(0.0, (component.meanIntensity - backgroundMean) / referenceDelta);
        if (relativeIntensity < config.minRelativeIntensity) {
            continue;
        }

        const cv::Point2d center = component.centerInRoi + cv::Point2d(roi.x, roi.y);
        const cv::Point2d opticalCenter = config.opticalCenter.x >= 0.0
            ? config.opticalCenter
            : cv::Point2d(roi.x + (roi.width - 1) * 0.5, roi.y + (roi.height - 1) * 0.5);
        const cv::Point2d candidateDirection = nearestSource == nullptr ? cv::Point2d() : center - nearestSource->center;
        const cv::Point2d opticalDirection = nearestSource == nullptr ? cv::Point2d() : opticalCenter - nearestSource->center;
        const double candidateNorm = cv::norm(candidateDirection);
        const double opticalNorm = cv::norm(opticalDirection);
        double directionAngleDegrees = 90.0;
        double directionConfidence = 0.0;
        if (candidateNorm > Epsilon && opticalNorm > Epsilon) {
            const double cosine = std::max(-1.0, std::min(1.0,
                candidateDirection.dot(opticalDirection) / (candidateNorm * opticalNorm)));
            directionAngleDegrees = std::acos(cosine) * 180.0 / Pi;
            directionConfidence = clamp01(cosine);
        }
        if (config.useDirectionalConfidence && directionConfidence < config.minDirectionConfidence) {
            continue;
        }

        GhostCandidate candidate;
        candidate.centerInRoi = component.centerInRoi;
        candidate.center = center;
        candidate.boundingRectInRoi = component.boundingRectInRoi;
        candidate.boundingRect = offsetRect(component.boundingRectInRoi, roi.tl());
        candidate.area = component.area;
        candidate.meanIntensity = component.meanIntensity;
        candidate.peakIntensity = component.peakIntensity;
        candidate.relativeIntensity = relativeIntensity;
        candidate.peakRelativeIntensity = std::max(0.0, (component.peakIntensity - backgroundMean) / referenceDelta);
        candidate.nearestBrightSourceId = nearestSource == nullptr ? 0 : nearestSource->id;
        candidate.distanceToNearestBright = nearestDistance;
        candidate.directionAngleDegrees = directionAngleDegrees;
        candidate.directionConfidence = directionConfidence;
        candidate.scaleSupport = componentScaleSupport(component, labels, scaleMasks);
        const double signalConfidence = clamp01(relativeIntensity / 0.25);
        const double peakConfidence = clamp01(candidate.peakRelativeIntensity / 0.40);
        const double baseConfidence = 0.45 * signalConfidence + 0.25 * peakConfidence + 0.30 * candidate.scaleSupport;
        candidate.confidence = config.useDirectionalConfidence
            ? clamp01(0.75 * baseConfidence + 0.25 * directionConfidence)
            : clamp01(baseConfidence);
        candidate.severity = relativeIntensity * std::sqrt(static_cast<double>(component.area));
        candidate.severityGrade = gradeForSeverity(candidate.severity, config);
        candidates.push_back(std::move(candidate));
    }

    std::sort(candidates.begin(), candidates.end(), [](const GhostCandidate& a, const GhostCandidate& b) {
        return a.severity > b.severity;
    });
    if (config.maxCandidates > 0 && static_cast<int>(candidates.size()) > config.maxCandidates) {
        candidates.resize(static_cast<size_t>(config.maxCandidates));
        truncated = true;
    }

    std::sort(candidates.begin(), candidates.end(), [](const GhostCandidate& a, const GhostCandidate& b) {
        if (a.centerInRoi.y != b.centerInRoi.y) {
            return a.centerInRoi.y < b.centerInRoi.y;
        }
        return a.centerInRoi.x < b.centerInRoi.x;
    });
    for (int i = 0; i < static_cast<int>(candidates.size()); ++i) {
        candidates[static_cast<size_t>(i)].id = i + 1;
    }
    return candidates;
}

GhostDetectionSummary summarize(const std::vector<BrightSource>& sources, const std::vector<GhostCandidate>& candidates)
{
    GhostDetectionSummary summary;
    summary.brightSourceCount = static_cast<int>(sources.size());
    summary.candidateCount = static_cast<int>(candidates.size());
    double totalSeverity = 0.0;
    double totalConfidence = 0.0;
    for (const GhostCandidate& candidate : candidates) {
        summary.maxSeverity = std::max(summary.maxSeverity, candidate.severity);
        summary.maxConfidence = std::max(summary.maxConfidence, candidate.confidence);
        totalSeverity += candidate.severity;
        totalConfidence += candidate.confidence;
    }
    summary.meanSeverity = candidates.empty() ? 0.0 : totalSeverity / static_cast<double>(candidates.size());
    summary.meanConfidence = candidates.empty() ? 0.0 : totalConfidence / static_cast<double>(candidates.size());
    summary.grade = candidates.empty() ? "ok" : candidates.front().severityGrade;
    if (!candidates.empty()) {
        summary.grade = std::max_element(candidates.begin(), candidates.end(), [](const GhostCandidate& a, const GhostCandidate& b) {
            return a.severity < b.severity;
        })->severityGrade;
    }
    return summary;
}

nlohmann::json pointToJson(const cv::Point2d& point)
{
    return {
        { "x", point.x },
        { "y", point.y }
    };
}

nlohmann::json rectToJson(const cv::Rect& rect)
{
    return {
        { "x", rect.x },
        { "y", rect.y },
        { "width", rect.width },
        { "height", rect.height }
    };
}

nlohmann::json sizeToJson(const cv::Size& size)
{
    return {
        { "width", size.width },
        { "height", size.height }
    };
}

} // namespace

nlohmann::json ToJson(const BrightSource& source)
{
    return {
        { "id", source.id },
        { "gridRow", source.gridRow },
        { "gridCol", source.gridCol },
        { "center", pointToJson(source.center) },
        { "centerInRoi", pointToJson(source.centerInRoi) },
        { "boundingRect", rectToJson(source.boundingRect) },
        { "boundingRectInRoi", rectToJson(source.boundingRectInRoi) },
        { "area", source.area },
        { "meanIntensity", source.meanIntensity },
        { "peakIntensity", source.peakIntensity }
    };
}

nlohmann::json ToJson(const GhostCandidate& candidate)
{
    return {
        { "id", candidate.id },
        { "center", pointToJson(candidate.center) },
        { "centerInRoi", pointToJson(candidate.centerInRoi) },
        { "boundingRect", rectToJson(candidate.boundingRect) },
        { "boundingRectInRoi", rectToJson(candidate.boundingRectInRoi) },
        { "area", candidate.area },
        { "meanIntensity", candidate.meanIntensity },
        { "peakIntensity", candidate.peakIntensity },
        { "relativeIntensity", candidate.relativeIntensity },
        { "peakRelativeIntensity", candidate.peakRelativeIntensity },
        { "nearestBrightSourceId", candidate.nearestBrightSourceId },
        { "distanceToNearestBright", candidate.distanceToNearestBright },
        { "directionAngleDegrees", candidate.directionAngleDegrees },
        { "directionConfidence", candidate.directionConfidence },
        { "scaleSupport", candidate.scaleSupport },
        { "confidence", candidate.confidence },
        { "severity", candidate.severity },
        { "severityGrade", candidate.severityGrade }
    };
}

nlohmann::json ToJson(const GhostDetectionResult& result)
{
    nlohmann::json output = {
        { "success", result.success },
        { "statusCode", result.statusCode },
        { "message", result.message },
        { "warnings", result.warnings },
        { "imageSize", sizeToJson(result.imageSize) },
        { "roi", rectToJson(result.roi) },
        { "brightThresholdUsed", result.brightThresholdUsed },
        { "ghostThresholdUsed", result.ghostThresholdUsed },
        { "exposureNormalized", result.exposureNormalized },
        { "exposureLowUsed", result.exposureLowUsed },
        { "exposureHighUsed", result.exposureHighUsed },
        { "backgroundModelUsed", result.backgroundModelUsed },
        { "analyzedScaleLevels", result.analyzedScaleLevels },
        { "backgroundMeanIntensity", result.backgroundMeanIntensity },
        { "referenceBrightMeanIntensity", result.referenceBrightMeanIntensity },
        { "summary", {
            { "brightSourceCount", result.summary.brightSourceCount },
            { "candidateCount", result.summary.candidateCount },
            { "maxSeverity", result.summary.maxSeverity },
            { "meanSeverity", result.summary.meanSeverity },
            { "maxConfidence", result.summary.maxConfidence },
            { "meanConfidence", result.summary.meanConfidence },
            { "grade", result.summary.grade }
        } },
        { "brightSources", nlohmann::json::array() },
        { "candidates", nlohmann::json::array() }
    };

    for (const BrightSource& source : result.brightSources) {
        output["brightSources"].push_back(ToJson(source));
    }
    for (const GhostCandidate& candidate : result.candidates) {
        output["candidates"].push_back(ToJson(candidate));
    }
    return output;
}

GhostDetectionResult detectGhosts(const cv::Mat& image, const GhostDetectionConfig& config) noexcept
{
    GhostDetectionResult result;
    result.imageSize = image.empty() ? cv::Size() : image.size();

    try {
        std::string validationMessage;
        if (!validateConfig(config, validationMessage)) {
            result.statusCode = "invalid_config";
            result.message = validationMessage;
            return result;
        }

        cv::Mat gray32;
        if (!convertToNormalizedGray(image, config.channel, gray32)) {
            result.statusCode = "invalid_image";
            result.message = "Input must be a non-empty 8-bit or 16-bit image with one or three channels; the selected channel must exist.";
            return result;
        }

        if (!resolveRoi(image, config.roi, result.roi)) {
            result.statusCode = "invalid_roi";
            result.message = "ROI must be empty (whole image) or fully contained in the input image.";
            return result;
        }

        cv::Mat grayRoi = gray32(result.roi).clone();
        if (config.normalizeExposure) {
            result.exposureNormalized = normalizeExposureByPercentile(
                grayRoi,
                config.exposureLowPercentile,
                config.exposureHighPercentile,
                result.exposureLowUsed,
                result.exposureHighUsed);
            if (!result.exposureNormalized) {
                result.warnings.push_back("Exposure normalization was skipped because the selected percentile range has no contrast.");
            }
        }

        std::vector<cv::Mat> scaleResponses;
        scaleResponses.reserve(static_cast<size_t>(config.multiScaleLevels));
        for (int level = 0; level < config.multiScaleLevels; ++level) {
            scaleResponses.push_back(makeAnalysisResponse(grayRoi, config, level));
        }
        const cv::Mat& analysisGray = scaleResponses.front();
        result.backgroundModelUsed = config.backgroundKernel > 1;
        result.analyzedScaleLevels = static_cast<int>(scaleResponses.size());
        result.brightThresholdUsed = config.brightThresholdMode == ThresholdMode::Fixed
            ? config.brightThreshold
            : automaticOtsuThreshold(analysisGray, {});
        cv::Mat brightMask = thresholdMask(analysisGray, result.brightThresholdUsed, config.brightOpenKernel, config.brightCloseKernel);

        const ComponentLimits brightLimits = {
            config.brightMinArea,
            config.brightMaxArea,
            config.brightMinSize,
            config.brightMaxSize
        };
        cv::Mat brightLabels;
        const std::vector<Component> allBrightComponents = findComponents(analysisGray, brightMask, brightLimits, brightLabels);
        if (allBrightComponents.empty()) {
            result.statusCode = "no_bright_sources";
            result.message = "No bright source matched the configured threshold and size limits.";
            return result;
        }

        std::vector<Component> brightComponents = allBrightComponents;
        selectAndOrderBrightComponents(brightComponents, config.brightGridRows, config.brightGridCols, result.warnings);
        result.brightSources = makeBrightSources(brightComponents, result.roi, config.brightGridCols);
        result.referenceBrightMeanIntensity = std::accumulate(
            result.brightSources.begin(), result.brightSources.end(), 0.0,
            [](double total, const BrightSource& source) { return total + source.meanIntensity; }) /
            static_cast<double>(result.brightSources.size());

        const cv::Mat exclusionMask = makeSourceExclusionMask(brightLabels, allBrightComponents, config.sourceMaskPadding);
        cv::Mat validGhostMask;
        cv::bitwise_not(exclusionMask, validGhostMask);
        result.backgroundMeanIntensity = cv::mean(analysisGray, validGhostMask)[0];
        result.ghostThresholdUsed = config.ghostThresholdMode == ThresholdMode::Fixed
            ? config.ghostThreshold
            : automaticOtsuThreshold(analysisGray, validGhostMask);

        cv::Mat ghostMask = cv::Mat::zeros(analysisGray.size(), CV_8U);
        std::vector<cv::Mat> scaleMasks;
        scaleMasks.reserve(scaleResponses.size());
        for (int level = 0; level < static_cast<int>(scaleResponses.size()); ++level) {
            const double levelThreshold = result.ghostThresholdUsed *
                std::pow(config.multiScaleThresholdFactor, level);
            cv::Mat scaleMask = thresholdMask(
                scaleResponses[static_cast<size_t>(level)],
                levelThreshold,
                config.ghostOpenKernel,
                config.ghostCloseKernel);
            cv::bitwise_and(scaleMask, validGhostMask, scaleMask);
            cv::bitwise_or(ghostMask, scaleMask, ghostMask);
            scaleMasks.push_back(std::move(scaleMask));
        }

        const ComponentLimits ghostLimits = {
            config.ghostMinArea,
            config.ghostMaxArea,
            config.ghostMinSize,
            config.ghostMaxSize
        };
        cv::Mat ghostLabels;
        const std::vector<Component> ghostComponents = findComponents(analysisGray, ghostMask, ghostLimits, ghostLabels);
        bool truncated = false;
        result.candidates = makeGhostCandidates(
            ghostComponents,
            result.brightSources,
            result.roi,
            result.backgroundMeanIntensity,
            result.referenceBrightMeanIntensity,
            ghostLabels,
            scaleMasks,
            config,
            truncated);
        if (truncated) {
            result.warnings.push_back("Ghost candidates were limited to maxCandidates after severity ranking.");
        }

        result.summary = summarize(result.brightSources, result.candidates);
        result.success = true;
        result.statusCode = result.warnings.empty() ? "ok" : "ok_with_warnings";
        result.message = result.candidates.empty() ? "No ghost candidates detected." : "ok";
        return result;
    }
    catch (const cv::Exception& error) {
        result.statusCode = "opencv_error";
        result.message = error.what();
    }
    catch (const std::exception& error) {
        result.statusCode = "runtime_error";
        result.message = error.what();
    }
    catch (...) {
        result.statusCode = "unknown_error";
        result.message = "Unexpected error while detecting ghost candidates.";
    }
    return result;
}

} // namespace ghost
} // namespace cvcore
