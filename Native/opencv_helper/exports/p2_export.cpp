#include "../pch.h"
#include "../algorithm/binocular/binocular_fusion.h"
#include "../algorithm/ghost/ghost_detection.h"
#include "../algorithm/keyboard_led/keyboard_led.h"
#include "../algorithm/matching/rotated_template_matching.h"
#include "../../include/opencv_media_export.h"

#include <combaseapi.h>
#include <nlohmann/json.hpp>

#include <algorithm>
#include <cctype>
#include <cmath>
#include <cstring>
#include <limits>
#include <stdexcept>
#include <string>
#include <vector>

namespace
{
using json = nlohmann::json;

constexpr int ExportInvalidArgument = -1;
constexpr int ExportAllocationFailed = -3;
constexpr int ExportInvalidJson = -4;
constexpr int ExportOpenCvException = -5;
constexpr int ExportStdException = -6;
constexpr int ExportUnknownException = -7;

template <typename Func>
int GuardExport(Func func) noexcept
{
    try {
        return func();
    }
    catch (const json::exception&) {
        return ExportInvalidJson;
    }
    catch (const std::invalid_argument&) {
        return ExportInvalidJson;
    }
    catch (const cv::Exception&) {
        return ExportOpenCvException;
    }
    catch (const std::exception&) {
        return ExportStdException;
    }
    catch (...) {
        return ExportUnknownException;
    }
}

bool TryParseConfig(const char* text, json& config)
{
    config = json::object();
    if (text == nullptr || text[0] == '\0') {
        return true;
    }

    config = json::parse(text, nullptr, false);
    return !config.is_discarded() && config.is_object();
}

int CopyJsonResult(const json& value, char** result)
{
    if (result == nullptr) {
        return ExportInvalidArgument;
    }

    *result = nullptr;
    const std::string output = value.dump();
    const size_t length = output.size() + 1;
    if (length > static_cast<size_t>((std::numeric_limits<int>::max)())) {
        return ExportAllocationFailed;
    }

    char* buffer = static_cast<char*>(CoTaskMemAlloc(length));
    if (buffer == nullptr) {
        return ExportAllocationFailed;
    }

    std::memcpy(buffer, output.c_str(), length);
    *result = buffer;
    return static_cast<int>(length);
}

cv::Rect ToRect(const RoiRect& roi)
{
    return roi.width > 0 && roi.height > 0 ? cv::Rect(roi.x, roi.y, roi.width, roi.height) : cv::Rect();
}

bool ResolveSearchRoi(const cv::Mat& image, const RoiRect& requested, cv::Rect& resolved)
{
    resolved = ToRect(requested);
    if (resolved.empty()) {
        resolved = cv::Rect(0, 0, image.cols, image.rows);
        return true;
    }

    const cv::Rect bounds(0, 0, image.cols, image.rows);
    return (resolved & bounds) == resolved;
}

int ReadCoordinate(const json& value, const char* lower, const char* upper)
{
    if (value.contains(lower)) {
        return value.at(lower).get<int>();
    }
    return value.at(upper).get<int>();
}

cv::Rect ParseRect(const json& value)
{
    if (value.is_array() && value.size() == 4) {
        return cv::Rect(value.at(0).get<int>(), value.at(1).get<int>(), value.at(2).get<int>(), value.at(3).get<int>());
    }
    if (!value.is_object()) {
        throw std::invalid_argument("A rectangle must be an object or [x, y, width, height].");
    }
    return cv::Rect(
        ReadCoordinate(value, "x", "X"),
        ReadCoordinate(value, "y", "Y"),
        ReadCoordinate(value, "width", "Width"),
        ReadCoordinate(value, "height", "Height"));
}

double ReadPointCoordinate(const json& value, const char* lower, const char* upper)
{
    if (value.contains(lower)) {
        return value.at(lower).get<double>();
    }
    return value.at(upper).get<double>();
}

cv::Point2d ParsePoint(const json& value)
{
    if (value.is_array() && value.size() == 2) {
        return cv::Point2d(value.at(0).get<double>(), value.at(1).get<double>());
    }
    if (!value.is_object()) {
        throw std::invalid_argument("A point must be an object or [x, y].");
    }
    return cv::Point2d(ReadPointCoordinate(value, "x", "X"), ReadPointCoordinate(value, "y", "Y"));
}

const json& NestedObjectOrSelf(const json& config, const char* key)
{
    if (!config.contains(key)) {
        return config;
    }
    const json& nested = config.at(key);
    if (!nested.is_object()) {
        throw std::invalid_argument(std::string(key) + " must be an object.");
    }
    return nested;
}

cvcore::ghost::ThresholdMode ParseThresholdMode(
    const json& config,
    const char* key,
    cvcore::ghost::ThresholdMode fallback)
{
    if (!config.contains(key)) {
        return fallback;
    }

    std::string mode = config.at(key).get<std::string>();
    std::transform(mode.begin(), mode.end(), mode.begin(), [](unsigned char c) { return static_cast<char>(std::tolower(c)); });
    if (mode == "auto" || mode == "automatic" || mode == "otsu") {
        return cvcore::ghost::ThresholdMode::Automatic;
    }
    if (mode == "fixed") {
        return cvcore::ghost::ThresholdMode::Fixed;
    }
    throw std::invalid_argument(std::string(key) + " must be auto or fixed.");
}

cvcore::ghost::GhostDetectionConfig ParseGhostConfig(const json& value, const RoiRect& roi)
{
    using cvcore::ghost::ThresholdMode;
    cvcore::ghost::GhostDetectionConfig config;
    config.roi = ToRect(roi);
    config.channel = value.value("channel", config.channel);
    config.brightThreshold = value.value("brightThreshold", config.brightThreshold);
    config.ghostThreshold = value.value("ghostThreshold", config.ghostThreshold);
    config.brightThresholdMode = ParseThresholdMode(
        value, "brightThresholdMode", value.contains("brightThreshold") ? ThresholdMode::Fixed : config.brightThresholdMode);
    config.ghostThresholdMode = ParseThresholdMode(
        value, "ghostThresholdMode", value.contains("ghostThreshold") ? ThresholdMode::Fixed : config.ghostThresholdMode);
    config.brightGridRows = value.value("brightGridRows", config.brightGridRows);
    config.brightGridCols = value.value("brightGridCols", config.brightGridCols);
    config.brightMinArea = value.value("brightMinArea", config.brightMinArea);
    config.brightMaxArea = value.value("brightMaxArea", config.brightMaxArea);
    config.brightMinSize = value.value("brightMinSize", config.brightMinSize);
    config.brightMaxSize = value.value("brightMaxSize", config.brightMaxSize);
    config.ghostMinArea = value.value("ghostMinArea", config.ghostMinArea);
    config.ghostMaxArea = value.value("ghostMaxArea", config.ghostMaxArea);
    config.ghostMinSize = value.value("ghostMinSize", config.ghostMinSize);
    config.ghostMaxSize = value.value("ghostMaxSize", config.ghostMaxSize);
    config.brightOpenKernel = value.value("brightOpenKernel", config.brightOpenKernel);
    config.brightCloseKernel = value.value("brightCloseKernel", config.brightCloseKernel);
    config.ghostOpenKernel = value.value("ghostOpenKernel", config.ghostOpenKernel);
    config.ghostCloseKernel = value.value("ghostCloseKernel", config.ghostCloseKernel);
    config.sourceMaskPadding = value.value("sourceMaskPadding", config.sourceMaskPadding);
    config.minDistanceFromBright = value.value("minDistanceFromBright", config.minDistanceFromBright);
    config.minRelativeIntensity = value.value("minRelativeIntensity", config.minRelativeIntensity);
    config.maxCandidates = value.value("maxCandidates", config.maxCandidates);
    config.minorSeverity = value.value("minorSeverity", config.minorSeverity);
    config.majorSeverity = value.value("majorSeverity", config.majorSeverity);
    config.criticalSeverity = value.value("criticalSeverity", config.criticalSeverity);
    return config;
}

cvcore::keyboard_led::GrayMode ParseGrayMode(const json& value, cvcore::keyboard_led::GrayMode fallback)
{
    if (!value.contains("mode")) {
        return fallback;
    }

    std::string mode = value.at("mode").get<std::string>();
    std::transform(mode.begin(), mode.end(), mode.begin(), [](unsigned char c) { return static_cast<char>(std::tolower(c)); });
    if (mode == "max" || mode == "maxchannel") return cvcore::keyboard_led::GrayMode::MaxChannel;
    if (mode == "average" || mode == "averagechannels") return cvcore::keyboard_led::GrayMode::AverageChannels;
    if (mode == "luminance" || mode == "luma") return cvcore::keyboard_led::GrayMode::Luminance;
    if (mode == "channel") return cvcore::keyboard_led::GrayMode::Channel;
    throw std::invalid_argument("gray.mode must be maxChannel, averageChannels, luminance, or channel.");
}

cvcore::keyboard_led::GrayConfig ParseGrayConfig(const json& root)
{
    cvcore::keyboard_led::GrayConfig config;
    const json& value = NestedObjectOrSelf(root, "gray");
    config.mode = ParseGrayMode(value, config.mode);
    config.channel = value.value("channel", config.channel);
    config.validMin = value.value("validMin", config.validMin);
    config.validMax = value.value("validMax", config.validMax);
    return config;
}

cvcore::keyboard_led::KeyHaloConfig ParseHaloConfig(const json& value)
{
    cvcore::keyboard_led::KeyHaloConfig config;
    config.gray = ParseGrayConfig(value);
    config.innerInsetRatio = value.value("innerInsetRatio", config.innerInsetRatio);
    config.haloGapRatio = value.value("haloGapRatio", config.haloGapRatio);
    config.haloWidthRatio = value.value("haloWidthRatio", config.haloWidthRatio);
    config.excludeKeyRectsFromHalo = value.value("excludeKeyRectsFromHalo", config.excludeKeyRectsFromHalo);
    config.minimumValidPixels = value.value("minimumValidPixels", config.minimumValidPixels);
    return config;
}

cvcore::keyboard_led::LedArrayConfig ParseLedConfig(const json& value)
{
    cvcore::keyboard_led::LedArrayConfig config;
    config.gray = ParseGrayConfig(value);
    config.rows = value.value("rows", config.rows);
    config.cols = value.value("cols", config.cols);
    config.sampleRadius = value.value("sampleRadius", config.sampleRadius);
    config.assignmentGateRatio = value.value("assignmentGateRatio", config.assignmentGateRatio);
    config.assignmentGatePixels = value.value("assignmentGatePixels", config.assignmentGatePixels);
    config.autoClusterTolerancePixels = value.value("autoClusterTolerancePixels", config.autoClusterTolerancePixels);
    config.autoClusterSeparationRatio = value.value("autoClusterSeparationRatio", config.autoClusterSeparationRatio);
    config.minimumBrightness = value.value("minimumBrightness", config.minimumBrightness);
    config.minimumArea = value.value("minimumArea", config.minimumArea);
    config.maximumArea = value.value("maximumArea", config.maximumArea);
    config.maximumDetections = value.value("maximumDetections", config.maximumDetections);
    config.maximumExpectedPoints = value.value("maximumExpectedPoints", config.maximumExpectedPoints);
    return config;
}

struct DetectionConfig
{
    double threshold = -1.0;
    int minArea = 4;
    int maxArea = 0;
    int openKernel = 1;
    int closeKernel = 3;
};

DetectionConfig ParseDetectionConfig(const json& root, int defaultMinArea)
{
    DetectionConfig config;
    config.minArea = defaultMinArea;
    if (!root.contains("detection")) {
        return config;
    }

    const json& value = root.at("detection");
    if (!value.is_object()) {
        throw std::invalid_argument("detection must be an object.");
    }
    config.threshold = value.value("threshold", config.threshold);
    config.minArea = value.value("minArea", config.minArea);
    config.maxArea = value.value("maxArea", config.maxArea);
    config.openKernel = value.value("openKernel", config.openKernel);
    config.closeKernel = value.value("closeKernel", config.closeKernel);
    return config;
}

bool ToNormalizedGray(const cv::Mat& image, const cvcore::keyboard_led::GrayConfig& config, cv::Mat& gray32)
{
    if (image.empty() || (image.depth() != CV_8U && image.depth() != CV_16U) ||
        (image.channels() != 1 && image.channels() != 3)) {
        return false;
    }

    const double scale = image.depth() == CV_8U ? 1.0 / 255.0 : 1.0 / 65535.0;
    cv::Mat normalized;
    image.convertTo(normalized, CV_MAKETYPE(CV_32F, image.channels()), scale);
    if (image.channels() == 1) {
        if (config.mode == cvcore::keyboard_led::GrayMode::Channel && config.channel != 0) {
            return false;
        }
        gray32 = normalized;
        return true;
    }

    switch (config.mode) {
    case cvcore::keyboard_led::GrayMode::MaxChannel: {
        std::vector<cv::Mat> channels;
        cv::split(normalized, channels);
        cv::max(channels[0], channels[1], gray32);
        cv::max(gray32, channels[2], gray32);
        break;
    }
    case cvcore::keyboard_led::GrayMode::AverageChannels: {
        std::vector<cv::Mat> channels;
        cv::split(normalized, channels);
        gray32 = (channels[0] + channels[1] + channels[2]) / 3.0;
        break;
    }
    case cvcore::keyboard_led::GrayMode::Luminance:
        cv::cvtColor(normalized, gray32, cv::COLOR_BGR2GRAY);
        break;
    case cvcore::keyboard_led::GrayMode::Channel:
        if (config.channel < 0 || config.channel >= image.channels()) {
            return false;
        }
        cv::extractChannel(normalized, gray32, config.channel);
        break;
    }
    return !gray32.empty();
}

int NormalizedOddKernel(int value)
{
    if (value <= 1) return 0;
    return value % 2 == 0 ? value + 1 : value;
}

void ApplyMorphology(cv::Mat& mask, int openKernel, int closeKernel)
{
    const int openSize = NormalizedOddKernel(openKernel);
    if (openSize > 1) {
        cv::morphologyEx(mask, mask, cv::MORPH_OPEN,
            cv::getStructuringElement(cv::MORPH_ELLIPSE, cv::Size(openSize, openSize)));
    }
    const int closeSize = NormalizedOddKernel(closeKernel);
    if (closeSize > 1) {
        cv::morphologyEx(mask, mask, cv::MORPH_CLOSE,
            cv::getStructuringElement(cv::MORPH_ELLIPSE, cv::Size(closeSize, closeSize)));
    }
}

std::vector<cvcore::keyboard_led::LedDetection> DetectBrightComponents(
    const cv::Mat& image,
    const cv::Rect& searchRoi,
    const cvcore::keyboard_led::GrayConfig& grayConfig,
    const DetectionConfig& config)
{
    if ((config.threshold < 0.0 && config.threshold != -1.0) || config.threshold > 1.0 ||
        config.minArea <= 0 || config.maxArea < 0 || (config.maxArea > 0 && config.maxArea < config.minArea) ||
        config.openKernel < 0 || config.closeKernel < 0 || config.openKernel > 4095 || config.closeKernel > 4095) {
        throw std::invalid_argument("Invalid automatic-detection threshold or area limits.");
    }

    cv::Mat gray32;
    if (!ToNormalizedGray(image, grayConfig, gray32)) {
        return {};
    }

    cv::Mat gray8;
    gray32(searchRoi).convertTo(gray8, CV_8U, 255.0);
    cv::Mat mask;
    if (config.threshold < 0.0) {
        cv::threshold(gray8, mask, 0.0, 255.0, cv::THRESH_BINARY | cv::THRESH_OTSU);
    }
    else {
        cv::threshold(gray8, mask, config.threshold * 255.0, 255.0, cv::THRESH_BINARY);
    }
    ApplyMorphology(mask, config.openKernel, config.closeKernel);

    cv::Mat labels;
    cv::Mat stats;
    cv::Mat centroids;
    const int count = cv::connectedComponentsWithStats(mask, labels, stats, centroids, 8, CV_32S);
    std::vector<cvcore::keyboard_led::LedDetection> detections;
    detections.reserve(static_cast<size_t>(std::max(0, count - 1)));
    for (int label = 1; label < count; ++label) {
        const int area = stats.at<int>(label, cv::CC_STAT_AREA);
        if (area < config.minArea || (config.maxArea > 0 && area > config.maxArea)) {
            continue;
        }

        cvcore::keyboard_led::LedDetection detection;
        detection.center = cv::Point2d(
            centroids.at<double>(label, 0) + searchRoi.x,
            centroids.at<double>(label, 1) + searchRoi.y);
        detection.boundingRect = cv::Rect(
            stats.at<int>(label, cv::CC_STAT_LEFT) + searchRoi.x,
            stats.at<int>(label, cv::CC_STAT_TOP) + searchRoi.y,
            stats.at<int>(label, cv::CC_STAT_WIDTH),
            stats.at<int>(label, cv::CC_STAT_HEIGHT));
        detection.area = area;
        detections.push_back(detection);
    }

    std::sort(detections.begin(), detections.end(), [](const auto& left, const auto& right) {
        if (left.center.y != right.center.y) return left.center.y < right.center.y;
        return left.center.x < right.center.x;
    });
    for (int i = 0; i < static_cast<int>(detections.size()); ++i) {
        detections[static_cast<size_t>(i)].id = i + 1;
    }
    return detections;
}

std::vector<cv::Rect> ParseKeyRects(const json& config)
{
    std::vector<cv::Rect> rects;
    if (!config.contains("keyRects")) {
        return rects;
    }
    const json& values = config.at("keyRects");
    if (!values.is_array()) {
        throw std::invalid_argument("keyRects must be an array.");
    }
    rects.reserve(values.size());
    for (const json& value : values) {
        rects.push_back(ParseRect(value));
    }
    return rects;
}

std::vector<cvcore::keyboard_led::LedDetection> ParseLedDetections(const json& config)
{
    std::vector<cvcore::keyboard_led::LedDetection> detections;
    if (!config.contains("detections")) {
        return detections;
    }
    const json& values = config.at("detections");
    if (!values.is_array()) {
        throw std::invalid_argument("detections must be an array.");
    }

    detections.reserve(values.size());
    for (size_t index = 0; index < values.size(); ++index) {
        const json& value = values.at(index);
        if (!value.is_object() || !value.contains("center")) {
            throw std::invalid_argument("Each LED detection must contain a center.");
        }
        cvcore::keyboard_led::LedDetection detection;
        detection.id = value.value("id", static_cast<int>(index + 1));
        detection.center = ParsePoint(value.at("center"));
        detection.area = value.value("area", 0.0);
        detection.boundingRect = value.contains("boundingRect")
            ? ParseRect(value.at("boundingRect"))
            : cv::Rect();
        detections.push_back(detection);
    }
    return detections;
}

cvcore::matching::RotatedTemplateMatchingConfig ParseMatchingConfig(const json& value)
{
    cvcore::matching::RotatedTemplateMatchingConfig config;
    config.angleMin = value.value("angleMin", config.angleMin);
    config.angleMax = value.value("angleMax", config.angleMax);
    config.angleStep = value.value("angleStep", config.angleStep);
    config.scoreThreshold = value.value("scoreThreshold", config.scoreThreshold);
    config.maxMatches = value.value("maxMatches", config.maxMatches);
    config.nmsRadius = value.value("nmsRadius", config.nmsRadius);
    config.pyramidLevels = value.value("pyramidLevels", config.pyramidLevels);
    config.subpixel = value.value("subpixel", config.subpixel);
    return config;
}

cvcore::binocular::BinocularFusionConfig ParseBinocularConfig(const json& value)
{
    cvcore::binocular::BinocularFusionConfig config;
    config.threshold = value.value("threshold", config.threshold);
    config.blurKernel = value.value("blurKernel", config.blurKernel);
    config.morphKernel = value.value("morphKernel", config.morphKernel);
    config.minArea = value.value("minArea", config.minArea);
    config.maxArea = value.value("maxArea", config.maxArea);
    config.pixelSize = value.value("pixelSize", config.pixelSize);
    config.focalLength = value.value("focalLength", config.focalLength);
    config.virtualImageDistance = value.value("virtualImageDistance", config.virtualImageDistance);
    config.maxCandidates = value.value("maxCandidates", config.maxCandidates);
    if (value.contains("opticalCenter")) {
        config.opticalCenter = ParsePoint(value.at("opticalCenter"));
    }
    return config;
}

void AddMetadata(json& output, const char* algorithm)
{
    output["algorithm"] = algorithm;
    output["version"] = "1.0";
}
}

COLORVISIONCORE_API int M_DetectGhosts(HImage img, RoiRect roi, const char* config, char** result)
{
    return GuardExport([&]() -> int {
        if (result != nullptr) *result = nullptr;
        cv::Mat image = HImageToMatView(img);
        json parsed;
        if (result == nullptr || image.empty()) return ExportInvalidArgument;
        if (!TryParseConfig(config, parsed)) return ExportInvalidJson;

        const auto detection = cvcore::ghost::detectGhosts(image, ParseGhostConfig(parsed, roi));
        json output = cvcore::ghost::ToJson(detection);
        AddMetadata(output, "GhostDetection");
        return CopyJsonResult(output, result);
    });
}

COLORVISIONCORE_API int M_AnalyzeKeyboardHalo(HImage img, RoiRect roi, const char* config, char** result)
{
    return GuardExport([&]() -> int {
        if (result != nullptr) *result = nullptr;
        cv::Mat image = HImageToMatView(img);
        json parsed;
        if (result == nullptr || image.empty()) return ExportInvalidArgument;
        if (!TryParseConfig(config, parsed)) return ExportInvalidJson;

        const auto haloConfig = ParseHaloConfig(parsed);
        std::vector<cv::Rect> keyRects = ParseKeyRects(parsed);
        bool autoDetected = keyRects.empty();
        if (autoDetected) {
            cv::Rect searchRoi;
            if (!ResolveSearchRoi(image, roi, searchRoi)) return ExportInvalidArgument;
            const auto detections = DetectBrightComponents(image, searchRoi, haloConfig.gray, ParseDetectionConfig(parsed, 100));
            keyRects.reserve(detections.size());
            for (const auto& detection : detections) keyRects.push_back(detection.boundingRect);
        }

        const auto measurement = cvcore::keyboard_led::measureKeyHalo(image, keyRects, haloConfig);
        json output = cvcore::keyboard_led::ToJson(measurement);
        AddMetadata(output, "KeyboardHalo");
        output["keyRectSource"] = autoDetected ? "automatic" : "config";
        return CopyJsonResult(output, result);
    });
}

COLORVISIONCORE_API int M_AnalyzeLedArray(HImage img, RoiRect roi, const char* config, char** result)
{
    return GuardExport([&]() -> int {
        if (result != nullptr) *result = nullptr;
        cv::Mat image = HImageToMatView(img);
        json parsed;
        if (result == nullptr || image.empty()) return ExportInvalidArgument;
        if (!TryParseConfig(config, parsed)) return ExportInvalidJson;

        const auto ledConfig = ParseLedConfig(parsed);
        std::vector<cvcore::keyboard_led::LedDetection> detections = ParseLedDetections(parsed);
        bool autoDetected = detections.empty();
        if (autoDetected) {
            cv::Rect searchRoi;
            if (!ResolveSearchRoi(image, roi, searchRoi)) return ExportInvalidArgument;
            detections = DetectBrightComponents(image, searchRoi, ledConfig.gray, ParseDetectionConfig(parsed, 4));
        }

        const auto analysis = cvcore::keyboard_led::analyzeLedArray(image, detections, ledConfig);
        json output = cvcore::keyboard_led::ToJson(analysis);
        AddMetadata(output, "LedArray");
        output["detectionSource"] = autoDetected ? "automatic" : "config";
        return CopyJsonResult(output, result);
    });
}

COLORVISIONCORE_API int M_MatchRotatedTemplate(
    HImage img,
    HImage templateImage,
    RoiRect roi,
    const char* config,
    char** result)
{
    return GuardExport([&]() -> int {
        if (result != nullptr) *result = nullptr;
        cv::Mat image = HImageToMatView(img);
        cv::Mat templ = HImageToMatView(templateImage);
        json parsed;
        if (result == nullptr || image.empty() || templ.empty()) return ExportInvalidArgument;
        if (!TryParseConfig(config, parsed)) return ExportInvalidJson;

        const auto matching = cvcore::matching::matchRotatedTemplate(image, templ, ToRect(roi), ParseMatchingConfig(parsed));
        json output = cvcore::matching::ToJson(matching);
        AddMetadata(output, "RotatedTemplateMatching");
        return CopyJsonResult(output, result);
    });
}

COLORVISIONCORE_API int M_CalBinocularFusion(HImage img, RoiRect roi, const char* config, char** result)
{
    return GuardExport([&]() -> int {
        if (result != nullptr) *result = nullptr;
        cv::Mat image = HImageToMatView(img);
        json parsed;
        if (result == nullptr || image.empty()) return ExportInvalidArgument;
        if (!TryParseConfig(config, parsed)) return ExportInvalidJson;

        const auto fusion = cvcore::binocular::calculateBinocularFusion(image, ToRect(roi), ParseBinocularConfig(parsed));
        json output = cvcore::binocular::ToJson(fusion);
        AddMetadata(output, "BinocularFiveCrossFusion");
        return CopyJsonResult(output, result);
    });
}
