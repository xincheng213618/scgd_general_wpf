#include "rotated_template_matching.h"

#include <algorithm>
#include <cmath>
#include <limits>
#include <utility>

namespace cvcore {
namespace matching {

namespace {

constexpr double Epsilon = 1e-9;
constexpr int MaxAngleCount = 10000;
constexpr double MaxNmsRadius = 1000000.0;
constexpr double MinimumRelativeDynamicRange = 1e-3;
constexpr double MinimumNormalizedRoiDynamicRange = 1e-2;

enum class GrayConversionStatus
{
    Ok,
    Invalid,
    LowDynamicRange
};

struct RotatedTemplateModel
{
    cv::Mat image;
    cv::Mat mask;
    cv::Point2d centerOffset;
    std::array<cv::Point2d, 4> corners{};

    bool valid() const
    {
        return !image.empty() && !mask.empty();
    }
};

struct ResponsePeak
{
    cv::Point2d location;
    double score = 0.0;
};

bool isFinite(double value)
{
    return std::isfinite(value) != 0;
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

    const cv::Rect imageBounds(0, 0, imageSize.width, imageSize.height);
    effective = requested & imageBounds;
    clipped = effective != requested;
    return effective.width > 0 && effective.height > 0;
}

double minimumDynamicRange(int depth, double minimum, double maximum)
{
    double referenceRange = std::max({ 1.0, std::abs(minimum), std::abs(maximum) });
    switch (depth) {
    case CV_8U:
    case CV_8S:
        referenceRange = 255.0;
        break;
    case CV_16U:
    case CV_16S:
        referenceRange = 65535.0;
        break;
    default:
        break;
    }

    const double relativeMinimum = referenceRange * MinimumRelativeDynamicRange;
    return depth <= CV_32S ? std::max(2.0, relativeMinimum) : relativeMinimum;
}

GrayConversionStatus toNormalizedGray32(const cv::Mat& input, cv::Mat& output)
{
    if (input.empty() || (input.channels() != 1 && input.channels() != 3 && input.channels() != 4)) {
        return GrayConversionStatus::Invalid;
    }

    cv::Mat floatImage;
    input.convertTo(floatImage, CV_MAKETYPE(CV_32F, input.channels()));

    cv::Mat gray;
    if (floatImage.channels() == 1) {
        gray = floatImage;
    }
    else if (floatImage.channels() == 3) {
        cv::cvtColor(floatImage, gray, cv::COLOR_BGR2GRAY);
    }
    else {
        cv::cvtColor(floatImage, gray, cv::COLOR_BGRA2GRAY);
    }

    if (!cv::checkRange(gray, true, nullptr)) {
        return GrayConversionStatus::Invalid;
    }

    double minValue = 0.0;
    double maxValue = 0.0;
    cv::minMaxLoc(gray, &minValue, &maxValue);
    const double dynamicRange = maxValue - minValue;
    if (dynamicRange <= minimumDynamicRange(input.depth(), minValue, maxValue)) {
        return GrayConversionStatus::LowDynamicRange;
    }

    gray.convertTo(output, CV_32F, 1.0 / dynamicRange, -minValue / dynamicRange);
    return GrayConversionStatus::Ok;
}

bool hasUsefulNormalizedDynamicRange(const cv::Mat& image)
{
    double minimum = 0.0;
    double maximum = 0.0;
    cv::minMaxLoc(image, &minimum, &maximum);
    return maximum - minimum > MinimumNormalizedRoiDynamicRange;
}

cv::Point2d transformPoint(const cv::Mat& matrix, const cv::Point2d& point)
{
    return cv::Point2d(
        matrix.at<double>(0, 0) * point.x + matrix.at<double>(0, 1) * point.y + matrix.at<double>(0, 2),
        matrix.at<double>(1, 0) * point.x + matrix.at<double>(1, 1) * point.y + matrix.at<double>(1, 2));
}

RotatedTemplateModel rotateTemplate(const cv::Mat& templ, double angle)
{
    RotatedTemplateModel model;
    if (templ.empty()) {
        return model;
    }

    const cv::Point2d rotationCenter(templ.cols * 0.5, templ.rows * 0.5);
    cv::Mat matrix = cv::getRotationMatrix2D(rotationCenter, angle, 1.0);

    const std::array<cv::Point2d, 4> sourceCorners = {
        cv::Point2d(0.0, 0.0),
        cv::Point2d(static_cast<double>(templ.cols), 0.0),
        cv::Point2d(static_cast<double>(templ.cols), static_cast<double>(templ.rows)),
        cv::Point2d(0.0, static_cast<double>(templ.rows))
    };

    std::array<cv::Point2d, 4> transformedCorners{};
    double minX = std::numeric_limits<double>::max();
    double minY = std::numeric_limits<double>::max();
    double maxX = std::numeric_limits<double>::lowest();
    double maxY = std::numeric_limits<double>::lowest();
    for (size_t i = 0; i < sourceCorners.size(); ++i) {
        transformedCorners[i] = transformPoint(matrix, sourceCorners[i]);
        minX = std::min(minX, transformedCorners[i].x);
        minY = std::min(minY, transformedCorners[i].y);
        maxX = std::max(maxX, transformedCorners[i].x);
        maxY = std::max(maxY, transformedCorners[i].y);
    }

    matrix.at<double>(0, 2) -= minX;
    matrix.at<double>(1, 2) -= minY;
    const cv::Size canvasSize(
        std::max(1, static_cast<int>(std::ceil(maxX - minX))),
        std::max(1, static_cast<int>(std::ceil(maxY - minY))));

    cv::Mat rotatedImage;
    cv::warpAffine(templ, rotatedImage, matrix, canvasSize, cv::INTER_LINEAR, cv::BORDER_CONSTANT, cv::Scalar(0));

    cv::Mat sourceMask(templ.size(), CV_8U, cv::Scalar(255));
    cv::Mat rotatedMask;
    cv::warpAffine(sourceMask, rotatedMask, matrix, canvasSize, cv::INTER_NEAREST, cv::BORDER_CONSTANT, cv::Scalar(0));

    std::vector<cv::Point> nonZero;
    cv::findNonZero(rotatedMask, nonZero);
    if (nonZero.empty()) {
        return model;
    }

    const cv::Rect contentBounds = cv::boundingRect(nonZero);
    model.image = rotatedImage(contentBounds).clone();
    model.mask = rotatedMask(contentBounds).clone();
    for (size_t i = 0; i < sourceCorners.size(); ++i) {
        model.corners[i] = transformPoint(matrix, sourceCorners[i]) - cv::Point2d(contentBounds.x, contentBounds.y);
    }
    model.centerOffset = transformPoint(matrix, rotationCenter) - cv::Point2d(contentBounds.x, contentBounds.y);
    return model;
}

void sanitizeResponse(cv::Mat& response)
{
    for (int row = 0; row < response.rows; ++row) {
        float* values = response.ptr<float>(row);
        for (int col = 0; col < response.cols; ++col) {
            if (!std::isfinite(values[col])) {
                values[col] = -1.0f;
            }
        }
    }
}

bool calculateResponse(const cv::Mat& searchImage, const RotatedTemplateModel& model, cv::Mat& response)
{
    if (!model.valid() || searchImage.cols < model.image.cols || searchImage.rows < model.image.rows) {
        return false;
    }

    cv::matchTemplate(searchImage, model.image, response, cv::TM_CCORR_NORMED, model.mask);
    sanitizeResponse(response);
    return !response.empty();
}

double quadraticPeakOffset(double before, double center, double after)
{
    const double denominator = before - 2.0 * center + after;
    if (std::abs(denominator) <= Epsilon) {
        return 0.0;
    }

    const double offset = 0.5 * (before - after) / denominator;
    return std::abs(offset) <= 1.0 && isFinite(offset) ? offset : 0.0;
}

cv::Point2d refinePeak(const cv::Mat& response, const cv::Point& peak)
{
    cv::Point2d refined(peak.x, peak.y);
    if (peak.x > 0 && peak.x + 1 < response.cols) {
        refined.x += quadraticPeakOffset(
            response.at<float>(peak.y, peak.x - 1),
            response.at<float>(peak.y, peak.x),
            response.at<float>(peak.y, peak.x + 1));
    }
    if (peak.y > 0 && peak.y + 1 < response.rows) {
        refined.y += quadraticPeakOffset(
            response.at<float>(peak.y - 1, peak.x),
            response.at<float>(peak.y, peak.x),
            response.at<float>(peak.y + 1, peak.x));
    }
    return refined;
}

std::vector<ResponsePeak> extractPeaks(
    const cv::Mat& response,
    double minimumScore,
    int maximumCount,
    double suppressionRadius,
    bool subpixel)
{
    std::vector<ResponsePeak> peaks;
    if (response.empty() || maximumCount <= 0) {
        return peaks;
    }

    cv::Mat remaining = response.clone();
    const int radius = std::max(1, static_cast<int>(std::ceil(suppressionRadius)));
    for (int i = 0; i < maximumCount; ++i) {
        double maxValue = -1.0;
        cv::Point maxLocation;
        cv::minMaxLoc(remaining, nullptr, &maxValue, nullptr, &maxLocation);
        if (!isFinite(maxValue) || maxValue < minimumScore) {
            break;
        }

        ResponsePeak peak;
        peak.location = subpixel ? refinePeak(response, maxLocation) : cv::Point2d(maxLocation.x, maxLocation.y);
        peak.score = maxValue;
        peaks.push_back(peak);

        const cv::Rect suppression(
            std::max(0, maxLocation.x - radius),
            std::max(0, maxLocation.y - radius),
            std::min(remaining.cols, maxLocation.x + radius + 1) - std::max(0, maxLocation.x - radius),
            std::min(remaining.rows, maxLocation.y + radius + 1) - std::max(0, maxLocation.y - radius));
        remaining(suppression).setTo(cv::Scalar(-1.0));
    }

    return peaks;
}

bool refineAtFullResolution(
    const cv::Mat& searchImage,
    const RotatedTemplateModel& model,
    const cv::Point2d& predictedTopLeft,
    int radius,
    bool subpixel,
    ResponsePeak& refined)
{
    const int maxTopLeftX = searchImage.cols - model.image.cols;
    const int maxTopLeftY = searchImage.rows - model.image.rows;
    if (maxTopLeftX < 0 || maxTopLeftY < 0) {
        return false;
    }

    const int x0 = std::clamp(static_cast<int>(std::floor(predictedTopLeft.x)) - radius, 0, maxTopLeftX);
    const int y0 = std::clamp(static_cast<int>(std::floor(predictedTopLeft.y)) - radius, 0, maxTopLeftY);
    const int x1 = std::clamp(static_cast<int>(std::ceil(predictedTopLeft.x)) + radius, 0, maxTopLeftX);
    const int y1 = std::clamp(static_cast<int>(std::ceil(predictedTopLeft.y)) + radius, 0, maxTopLeftY);
    if (x1 < x0 || y1 < y0) {
        return false;
    }

    const cv::Rect localSearchRoi(x0, y0, x1 - x0 + model.image.cols, y1 - y0 + model.image.rows);
    cv::Mat response;
    if (!calculateResponse(searchImage(localSearchRoi), model, response)) {
        return false;
    }

    double maxValue = -1.0;
    cv::Point maxLocation;
    cv::minMaxLoc(response, nullptr, &maxValue, nullptr, &maxLocation);
    if (!isFinite(maxValue)) {
        return false;
    }

    cv::Point2d localPeak = subpixel ? refinePeak(response, maxLocation) : cv::Point2d(maxLocation.x, maxLocation.y);
    refined.location.x = std::clamp(x0 + localPeak.x, 0.0, static_cast<double>(maxTopLeftX));
    refined.location.y = std::clamp(y0 + localPeak.y, 0.0, static_cast<double>(maxTopLeftY));
    refined.score = maxValue;
    return true;
}

RotatedTemplateMatch buildMatch(
    const ResponsePeak& peak,
    const RotatedTemplateModel& model,
    double angle,
    const cv::Point& roiOffset)
{
    RotatedTemplateMatch match;
    match.angle = angle;
    match.score = peak.score;
    const cv::Point2d origin(peak.location.x + roiOffset.x, peak.location.y + roiOffset.y);
    match.center = origin + model.centerOffset;

    double minX = std::numeric_limits<double>::max();
    double minY = std::numeric_limits<double>::max();
    double maxX = std::numeric_limits<double>::lowest();
    double maxY = std::numeric_limits<double>::lowest();
    for (size_t i = 0; i < model.corners.size(); ++i) {
        match.corners[i] = origin + model.corners[i];
        minX = std::min(minX, match.corners[i].x);
        minY = std::min(minY, match.corners[i].y);
        maxX = std::max(maxX, match.corners[i].x);
        maxY = std::max(maxY, match.corners[i].y);
    }
    match.boundingBox = cv::Rect2d(minX, minY, maxX - minX, maxY - minY);
    return match;
}

std::vector<double> makeAngles(const RotatedTemplateMatchingConfig& config)
{
    const double range = config.angleMax - config.angleMin;
    const int count = static_cast<int>(std::floor(range / config.angleStep + Epsilon)) + 1;
    std::vector<double> angles;
    angles.reserve(static_cast<size_t>(std::max(0, count)));
    for (int i = 0; i < count; ++i) {
        angles.push_back(config.angleMin + i * config.angleStep);
    }
    return angles;
}

int buildPyramids(
    const cv::Mat& searchImage,
    const cv::Mat& templ,
    int requestedLevels,
    std::vector<cv::Mat>& searchPyramid,
    std::vector<cv::Mat>& templatePyramid)
{
    searchPyramid = { searchImage };
    templatePyramid = { templ };
    for (int level = 1; level < requestedLevels; ++level) {
        cv::Mat nextSearch;
        cv::Mat nextTemplate;
        cv::pyrDown(searchPyramid.back(), nextSearch);
        cv::pyrDown(templatePyramid.back(), nextTemplate);
        if (nextTemplate.cols < 8 || nextTemplate.rows < 8 ||
            nextSearch.cols < nextTemplate.cols || nextSearch.rows < nextTemplate.rows) {
            break;
        }
        searchPyramid.push_back(std::move(nextSearch));
        templatePyramid.push_back(std::move(nextTemplate));
    }
    return static_cast<int>(searchPyramid.size());
}

bool validateConfig(const RotatedTemplateMatchingConfig& config, std::string& message)
{
    if (!isFinite(config.angleMin) || !isFinite(config.angleMax) || !isFinite(config.angleStep) ||
        config.angleMin > config.angleMax || config.angleStep <= 0.0) {
        message = "angleMin/angleMax/angleStep must define a finite ascending range with a positive step.";
        return false;
    }
    if (!isFinite(config.scoreThreshold) || config.scoreThreshold < 0.0 || config.scoreThreshold > 1.0) {
        message = "scoreThreshold must be within [0, 1].";
        return false;
    }
    if (config.maxMatches <= 0 || config.maxMatches > 10000) {
        message = "maxMatches must be within [1, 10000].";
        return false;
    }
    if (!isFinite(config.nmsRadius) || config.nmsRadius < 0.0 || config.nmsRadius > MaxNmsRadius) {
        message = "nmsRadius must be finite and within [0, 1000000].";
        return false;
    }
    if (config.pyramidLevels <= 0 || config.pyramidLevels > 8) {
        message = "pyramidLevels must be within [1, 8].";
        return false;
    }

    const double angleCount = std::floor((config.angleMax - config.angleMin) / config.angleStep + Epsilon) + 1.0;
    if (!isFinite(angleCount) || angleCount <= 0.0 || angleCount > MaxAngleCount) {
        message = "The configured angle range contains too many samples.";
        return false;
    }
    return true;
}

double centerDistance(const RotatedTemplateMatch& lhs, const RotatedTemplateMatch& rhs)
{
    return std::hypot(lhs.center.x - rhs.center.x, lhs.center.y - rhs.center.y);
}

nlohmann::json pointJson(const cv::Point2d& point)
{
    return { { "x", point.x }, { "y", point.y } };
}

nlohmann::json rectJson(const cv::Rect& rect)
{
    return { { "x", rect.x }, { "y", rect.y }, { "width", rect.width }, { "height", rect.height } };
}

nlohmann::json rectJson(const cv::Rect2d& rect)
{
    return { { "x", rect.x }, { "y", rect.y }, { "width", rect.width }, { "height", rect.height } };
}

} // namespace

RotatedTemplateMatchingResult matchRotatedTemplate(
    const cv::Mat& source,
    const cv::Mat& templ,
    const cv::Rect& roi,
    const RotatedTemplateMatchingConfig& config)
{
    RotatedTemplateMatchingResult result;
    result.sourceSize = source.empty() ? cv::Size() : source.size();
    result.templateSize = templ.empty() ? cv::Size() : templ.size();

    std::string validationMessage;
    if (!validateConfig(config, validationMessage)) {
        result.statusCode = "invalid_config";
        result.message = validationMessage;
        return result;
    }

    cv::Mat sourceGray;
    cv::Mat templateGray;
    const GrayConversionStatus sourceStatus = toNormalizedGray32(source, sourceGray);
    if (sourceStatus != GrayConversionStatus::Ok) {
        result.statusCode = "invalid_source";
        result.message = sourceStatus == GrayConversionStatus::LowDynamicRange
            ? "Source image has insufficient dynamic range for normalized correlation."
            : "Source image is empty, contains non-finite data, or has an unsupported channel count.";
        return result;
    }
    const GrayConversionStatus templateStatus = toNormalizedGray32(templ, templateGray);
    if (templateStatus != GrayConversionStatus::Ok) {
        result.statusCode = "invalid_template";
        result.message = templateStatus == GrayConversionStatus::LowDynamicRange
            ? "Template image has insufficient dynamic range for normalized correlation."
            : "Template image is empty, contains non-finite data, or has an unsupported channel count.";
        return result;
    }

    bool roiClipped = false;
    if (!prepareRoi(sourceGray.size(), roi, result.searchRoi, roiClipped)) {
        result.statusCode = "invalid_roi";
        result.message = "ROI must have a positive intersection with the source image.";
        return result;
    }
    if (roiClipped) {
        result.warnings.push_back("ROI extended outside the source image and was clipped to the valid image bounds.");
    }
    if (!hasUsefulNormalizedDynamicRange(sourceGray(result.searchRoi))) {
        result.statusCode = "invalid_source";
        result.message = "The effective search ROI has insufficient dynamic range for normalized correlation.";
        return result;
    }
    if (result.searchRoi.width < templateGray.cols || result.searchRoi.height < templateGray.rows) {
        result.statusCode = "template_larger_than_roi";
        result.message = "The unrotated template is larger than the effective search ROI.";
        return result;
    }

    try {
        cv::Mat searchImage = sourceGray(result.searchRoi).clone();
        std::vector<cv::Mat> searchPyramid;
        std::vector<cv::Mat> templatePyramid;
        const int effectiveLevels = buildPyramids(
            searchImage, templateGray, config.pyramidLevels, searchPyramid, templatePyramid);
        if (effectiveLevels < config.pyramidLevels) {
            result.warnings.push_back("pyramidLevels was reduced because the template became too small or no longer fit the search image.");
        }

        const std::vector<double> angles = makeAngles(config);
        const long long requestedCandidates = static_cast<long long>(config.maxMatches) * 8LL;
        const int candidateLimit = static_cast<int>(std::clamp(requestedCandidates, 32LL, 512LL));
        std::vector<RotatedTemplateMatch> candidates;

        for (double angle : angles) {
            const RotatedTemplateModel fullModel = rotateTemplate(templatePyramid.front(), angle);
            if (!fullModel.valid() || fullModel.image.cols > searchImage.cols || fullModel.image.rows > searchImage.rows) {
                ++result.skippedAngles;
                continue;
            }

            ++result.evaluatedAngles;
            int level = effectiveLevels - 1;
            RotatedTemplateModel coarseModel;
            while (level > 0) {
                coarseModel = rotateTemplate(templatePyramid[static_cast<size_t>(level)], angle);
                if (coarseModel.valid() &&
                    coarseModel.image.cols <= searchPyramid[static_cast<size_t>(level)].cols &&
                    coarseModel.image.rows <= searchPyramid[static_cast<size_t>(level)].rows) {
                    break;
                }
                --level;
            }

            if (level == 0) {
                cv::Mat response;
                if (!calculateResponse(searchImage, fullModel, response)) {
                    continue;
                }
                const std::vector<ResponsePeak> peaks = extractPeaks(
                    response, config.scoreThreshold, candidateLimit, std::max(1.0, config.nmsRadius), config.subpixel);
                for (const ResponsePeak& peak : peaks) {
                    candidates.push_back(buildMatch(peak, fullModel, angle, result.searchRoi.tl()));
                }
                continue;
            }

            cv::Mat coarseResponse;
            if (!calculateResponse(searchPyramid[static_cast<size_t>(level)], coarseModel, coarseResponse)) {
                continue;
            }

            const double scale = static_cast<double>(1 << level);
            const std::vector<ResponsePeak> coarsePeaks = extractPeaks(
                coarseResponse, 0.0, candidateLimit, std::max(1.0, config.nmsRadius / scale), false);
            if (coarsePeaks.empty()) {
                continue;
            }

            const int refinementRadius = std::max(4, static_cast<int>(std::ceil(scale * 2.0)));
            for (const ResponsePeak& coarsePeak : coarsePeaks) {
                const cv::Point2d predictedCenter = (coarsePeak.location + coarseModel.centerOffset) * scale;
                const cv::Point2d predictedTopLeft = predictedCenter - fullModel.centerOffset;
                ResponsePeak refined;
                if (refineAtFullResolution(
                    searchImage, fullModel, predictedTopLeft, refinementRadius, config.subpixel, refined) &&
                    refined.score >= config.scoreThreshold) {
                    candidates.push_back(buildMatch(refined, fullModel, angle, result.searchRoi.tl()));
                }
            }
        }

        result.candidateCount = static_cast<int>(candidates.size());
        if (result.skippedAngles > 0) {
            result.warnings.push_back(std::to_string(result.skippedAngles) +
                " angle sample(s) were skipped because the rotated template did not fit the effective search ROI.");
        }
        if (result.evaluatedAngles == 0) {
            result.statusCode = "template_larger_than_roi";
            result.message = "The rotated template did not fit the effective search ROI at any configured angle.";
            return result;
        }

        std::stable_sort(candidates.begin(), candidates.end(), [](const auto& lhs, const auto& rhs) {
            if (lhs.score != rhs.score) {
                return lhs.score > rhs.score;
            }
            if (lhs.angle != rhs.angle) {
                return lhs.angle < rhs.angle;
            }
            if (lhs.center.y != rhs.center.y) {
                return lhs.center.y < rhs.center.y;
            }
            return lhs.center.x < rhs.center.x;
        });

        result.matches.reserve(static_cast<size_t>(config.maxMatches));
        for (const RotatedTemplateMatch& candidate : candidates) {
            bool suppressed = false;
            for (const RotatedTemplateMatch& accepted : result.matches) {
                if (centerDistance(candidate, accepted) <= config.nmsRadius) {
                    suppressed = true;
                    break;
                }
            }
            if (!suppressed) {
                result.matches.push_back(candidate);
                if (static_cast<int>(result.matches.size()) >= config.maxMatches) {
                    break;
                }
            }
        }
    }
    catch (const cv::Exception& exception) {
        result.statusCode = "opencv_error";
        result.message = exception.what();
        return result;
    }

    if (result.matches.empty()) {
        result.statusCode = "no_matches";
        result.message = "No match reached scoreThreshold after angle search and non-maximum suppression.";
        return result;
    }

    result.success = true;
    result.statusCode = result.warnings.empty() ? "ok" : "ok_with_warnings";
    result.message = "ok";
    return result;
}

nlohmann::json ToJson(const RotatedTemplateMatch& match)
{
    nlohmann::json corners = nlohmann::json::array();
    for (const cv::Point2d& corner : match.corners) {
        corners.push_back(pointJson(corner));
    }
    return {
        { "center", pointJson(match.center) },
        { "angleDegrees", match.angle },
        { "score", match.score },
        { "boundingBox", rectJson(match.boundingBox) },
        { "corners", std::move(corners) }
    };
}

nlohmann::json ToJson(const RotatedTemplateMatchingResult& result)
{
    nlohmann::json matches = nlohmann::json::array();
    for (const RotatedTemplateMatch& match : result.matches) {
        matches.push_back(ToJson(match));
    }
    return {
        { "success", result.success },
        { "statusCode", result.statusCode },
        { "message", result.message },
        { "sourceSize", { { "width", result.sourceSize.width }, { "height", result.sourceSize.height } } },
        { "templateSize", { { "width", result.templateSize.width }, { "height", result.templateSize.height } } },
        { "searchRoi", rectJson(result.searchRoi) },
        { "evaluatedAngles", result.evaluatedAngles },
        { "skippedAngles", result.skippedAngles },
        { "candidateCount", result.candidateCount },
        { "warnings", result.warnings },
        { "matches", std::move(matches) }
    };
}

} // namespace matching
} // namespace cvcore
