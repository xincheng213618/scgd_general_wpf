#include "surface_defect.h"

#include <algorithm>
#include <cmath>
#include <numeric>

namespace cvcore {
namespace surface_defect {

namespace {

int normalizedOddKernel(int value)
{
    if (value <= 1) {
        return 0;
    }

    return (value % 2 == 0) ? value + 1 : value;
}

std::vector<int> normalizedScales(const std::vector<int>& scales)
{
    std::vector<int> output;
    output.reserve(scales.size());
    for (int scale : scales) {
        int normalized = normalizedOddKernel(scale);
        if (normalized > 1) {
            output.push_back(normalized);
        }
    }

    if (output.empty()) {
        output = { 31, 61, 121 };
    }

    std::sort(output.begin(), output.end());
    output.erase(std::unique(output.begin(), output.end()), output.end());
    return output;
}

cv::Mat selectAnalysisChannel(const cv::Mat& image, int channel)
{
    if (image.empty()) {
        return {};
    }

    cv::Mat gray;
    if (image.channels() == 1) {
        gray = image;
    }
    else if (channel >= 0 && channel < image.channels()) {
        cv::extractChannel(image, gray, channel);
    }
    else if (image.channels() == 3) {
        cv::cvtColor(image, gray, cv::COLOR_BGR2GRAY);
    }
    else if (image.channels() == 4) {
        cv::cvtColor(image, gray, cv::COLOR_BGRA2GRAY);
    }

    return gray;
}

bool convertToAnalysisFloat(const cv::Mat& image, int channel, cv::Mat& gray32)
{
    cv::Mat gray = selectAnalysisChannel(image, channel);
    if (gray.empty()) {
        return false;
    }

    double scale = 1.0;
    switch (gray.depth())
    {
    case CV_8U:
        scale = 1.0 / 255.0;
        break;
    case CV_16U:
        scale = 1.0 / 65535.0;
        break;
    case CV_32F:
    case CV_64F:
        scale = 1.0;
        break;
    default:
        return false;
    }

    gray.convertTo(gray32, CV_32F, scale);
    cv::patchNaNs(gray32, 0.0);
    return !gray32.empty();
}

void buildSignedRelativeDelta(const cv::Mat& source32, int scale, cv::Mat& delta)
{
    cv::Mat background;
    cv::GaussianBlur(source32, background, cv::Size(scale, scale), 0.0, 0.0, cv::BORDER_REPLICATE);

    cv::Mat denominator;
    cv::absdiff(background, cv::Scalar::all(0.0), denominator);
    cv::Mat epsilon(denominator.size(), denominator.type(), cv::Scalar::all(1e-6));
    cv::max(denominator, epsilon, denominator);

    cv::subtract(source32, background, delta);
    cv::divide(delta, denominator, delta);
}

void thresholdResidual(const cv::Mat& residual, double threshold, int openKernel, int closeKernel, cv::Mat& mask)
{
    cv::threshold(residual, mask, threshold, 255.0, cv::THRESH_BINARY);
    mask.convertTo(mask, CV_8U);

    int openSize = normalizedOddKernel(openKernel);
    if (openSize > 1) {
        cv::Mat kernel = cv::getStructuringElement(cv::MORPH_ELLIPSE, cv::Size(openSize, openSize));
        cv::morphologyEx(mask, mask, cv::MORPH_OPEN, kernel);
    }

    int closeSize = normalizedOddKernel(closeKernel);
    if (closeSize > 1) {
        cv::Mat kernel = cv::getStructuringElement(cv::MORPH_ELLIPSE, cv::Size(closeSize, closeSize));
        cv::morphologyEx(mask, mask, cv::MORPH_CLOSE, kernel);
    }
}

double aspectRatio(const cv::Rect& rect)
{
    const int minSide = std::max(1, std::min(rect.width, rect.height));
    const int maxSide = std::max(rect.width, rect.height);
    return static_cast<double>(maxSide) / static_cast<double>(minSide);
}

std::string gradeForSeverity(double severity, const SurfaceDefectConfig& config)
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
    return severity > 0.0 ? "trace" : "ok";
}

std::string classifyDefect(const std::string& polarity, int area, double aspect, int scale, const SurfaceDefectConfig& config)
{
    if (config.enableLineDetect && aspect >= config.lineAspectRatio) {
        return polarity == "bright" ? "brightLine" : "darkLine";
    }

    if (area >= config.muraMinArea || scale >= 61) {
        return polarity == "bright" ? "brightMura" : "darkMura";
    }

    return polarity == "bright" ? "brightSpot" : "darkSpot";
}

bool rectsTouchOrOverlap(const cv::Rect& a, const cv::Rect& b, int distance)
{
    cv::Rect expanded(
        a.x - distance,
        a.y - distance,
        a.width + distance * 2,
        a.height + distance * 2);
    return (expanded & b).area() > 0;
}

void appendComponents(
    const cv::Mat& signedDelta,
    const cv::Mat& mask,
    const std::string& polarity,
    int scale,
    const SurfaceDefectConfig& config,
    std::vector<SurfaceDefectItem>& defects)
{
    cv::Mat labels;
    cv::Mat stats;
    cv::Mat centroids;
    const int labelCount = cv::connectedComponentsWithStats(mask, labels, stats, centroids, 8, CV_32S);

    for (int label = 1; label < labelCount; ++label) {
        const int area = stats.at<int>(label, cv::CC_STAT_AREA);
        if (area < config.minArea || (config.maxArea > 0 && area > config.maxArea)) {
            continue;
        }

        const cv::Rect rect(
            stats.at<int>(label, cv::CC_STAT_LEFT),
            stats.at<int>(label, cv::CC_STAT_TOP),
            stats.at<int>(label, cv::CC_STAT_WIDTH),
            stats.at<int>(label, cv::CC_STAT_HEIGHT));
        if (rect.width <= 0 || rect.height <= 0) {
            continue;
        }

        cv::Mat componentMask;
        cv::compare(labels, label, componentMask, cv::CMP_EQ);

        cv::Scalar mean = cv::mean(signedDelta, componentMask);
        double minDelta = 0.0;
        double maxDelta = 0.0;
        cv::minMaxLoc(signedDelta, &minDelta, &maxDelta, nullptr, nullptr, componentMask);

        const double maxDeltaAbs = std::max(std::abs(minDelta), std::abs(maxDelta));
        const double severity = maxDeltaAbs * std::sqrt(static_cast<double>(area));
        if (severity < config.minSeverity) {
            continue;
        }

        const double aspect = aspectRatio(rect);
        SurfaceDefectItem item;
        item.scale = scale;
        item.polarity = polarity;
        item.boundingRect = rect;
        item.center = cv::Point2d(centroids.at<double>(label, 0), centroids.at<double>(label, 1));
        item.area = area;
        item.meanDelta = mean[0];
        item.minDelta = minDelta;
        item.maxDelta = maxDelta;
        item.maxDeltaAbs = maxDeltaAbs;
        item.severity = severity;
        item.aspectRatio = aspect;
        item.fillRatio = static_cast<double>(area) / static_cast<double>(std::max(1, rect.area()));
        item.type = classifyDefect(polarity, area, aspect, scale, config);
        defects.push_back(std::move(item));
    }
}

std::vector<SurfaceDefectItem> mergeDefects(std::vector<SurfaceDefectItem> defects, const SurfaceDefectConfig& config)
{
    std::sort(defects.begin(), defects.end(), [](const auto& a, const auto& b) {
        return a.severity > b.severity;
    });

    std::vector<SurfaceDefectItem> selected;
    selected.reserve(defects.size());
    for (const SurfaceDefectItem& defect : defects) {
        bool duplicate = false;
        for (const SurfaceDefectItem& existing : selected) {
            if (defect.polarity == existing.polarity &&
                rectsTouchOrOverlap(defect.boundingRect, existing.boundingRect, config.mergeDistance)) {
                duplicate = true;
                break;
            }
        }

        if (duplicate) {
            continue;
        }

        selected.push_back(defect);
        if (config.maxDefects > 0 && static_cast<int>(selected.size()) >= config.maxDefects) {
            break;
        }
    }

    std::sort(selected.begin(), selected.end(), [](const auto& a, const auto& b) {
        if (a.boundingRect.y != b.boundingRect.y) {
            return a.boundingRect.y < b.boundingRect.y;
        }
        return a.boundingRect.x < b.boundingRect.x;
    });

    for (int i = 0; i < static_cast<int>(selected.size()); ++i) {
        selected[static_cast<size_t>(i)].id = i + 1;
    }

    return selected;
}

SurfaceDefectSummary summarize(const std::vector<SurfaceDefectItem>& defects, const SurfaceDefectConfig& config)
{
    SurfaceDefectSummary summary;
    summary.defectCount = static_cast<int>(defects.size());
    double totalSeverity = 0.0;
    for (const SurfaceDefectItem& defect : defects) {
        if (defect.polarity == "dark") {
            summary.darkCount++;
        }
        else if (defect.polarity == "bright") {
            summary.brightCount++;
        }

        summary.maxSeverity = std::max(summary.maxSeverity, defect.severity);
        totalSeverity += defect.severity;
    }

    summary.meanSeverity = defects.empty() ? 0.0 : totalSeverity / static_cast<double>(defects.size());
    summary.grade = gradeForSeverity(summary.maxSeverity, config);
    return summary;
}

} // namespace

SurfaceDefectResult detectSurfaceDefects(const cv::Mat& image, const SurfaceDefectConfig& config)
{
    SurfaceDefectResult result;
    result.imageSize = image.empty() ? cv::Size() : cv::Size(image.cols, image.rows);

    cv::Mat source32;
    if (!convertToAnalysisFloat(image, config.channel, source32)) {
        result.statusCode = "invalid_image";
        result.message = "Invalid image or unsupported channel count/depth.";
        return result;
    }

    std::vector<SurfaceDefectItem> defects;
    const std::vector<int> scales = normalizedScales(config.scales);
    for (int scale : scales) {
        cv::Mat delta;
        buildSignedRelativeDelta(source32, scale, delta);

        if (config.enableBright && config.brightThreshold > 0.0) {
            cv::Mat brightMask;
            thresholdResidual(delta, config.brightThreshold, config.openKernel, config.closeKernel, brightMask);
            appendComponents(delta, brightMask, "bright", scale, config, defects);
        }

        if (config.enableDark && config.darkThreshold > 0.0) {
            cv::Mat darkResidual;
            cv::multiply(delta, -1.0, darkResidual);
            cv::Mat darkMask;
            thresholdResidual(darkResidual, config.darkThreshold, config.openKernel, config.closeKernel, darkMask);
            appendComponents(delta, darkMask, "dark", scale, config, defects);
        }
    }

    result.defects = mergeDefects(std::move(defects), config);
    result.summary = summarize(result.defects, config);
    result.success = true;
    result.statusCode = "ok";
    result.message = result.defects.empty() ? "No surface defects detected." : "ok";
    return result;
}

} // namespace surface_defect
} // namespace cvcore
