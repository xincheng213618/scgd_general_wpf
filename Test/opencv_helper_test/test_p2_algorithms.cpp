#ifndef NOMINMAX
#define NOMINMAX
#endif

#include <opencv2/opencv.hpp>
#include <nlohmann/json.hpp>

#include "../../Native/include/opencv_media_export.h"

#include <algorithm>
#include <array>
#include <cmath>
#include <cstdint>
#include <cstring>
#include <exception>
#include <functional>
#include <iostream>
#include <limits>
#include <string>
#include <utility>
#include <vector>

namespace {

using json = nlohmann::json;

HImage MakeImageView(cv::Mat& image)
{
    HImage view{};
    view.rows = image.rows;
    view.cols = image.cols;
    view.channels = image.channels();
    view.depth = CvDepthToHImageDepth(image.depth());
    view.stride = static_cast<int>(image.step);
    view.isDispose = false;
    view.pData = image.data;
    return view;
}

void Expect(bool condition, const std::string& message, bool& success)
{
    if (!condition) {
        std::cerr << "P2 test failure: " << message << std::endl;
        success = false;
    }
}

bool IsFiniteNumber(const json& value)
{
    return value.is_number() && std::isfinite(value.get<double>());
}

bool IsFinitePoint(const json& value)
{
    return value.is_object() && value.contains("x") && value.contains("y") &&
        IsFiniteNumber(value.at("x")) && IsFiniteNumber(value.at("y"));
}

double PointDistance(const json& point, const cv::Point2d& expected)
{
    return std::hypot(point.at("x").get<double>() - expected.x, point.at("y").get<double>() - expected.y);
}

template <typename Callable>
bool InvokeJsonExport(const char* name, Callable&& callable, json& output)
{
    char* rawResult = nullptr;
    const int returnValue = callable(&rawResult);
    if (returnValue <= 0 || rawResult == nullptr) {
        std::cerr << name << " returned " << returnValue << " without an owned JSON result." << std::endl;
        return false;
    }

    const size_t actualLength = std::strlen(rawResult) + 1;
    output = json::parse(rawResult, nullptr, false);
    const int freeResult = FreeResult(rawResult);
    if (returnValue != static_cast<int>(actualLength)) {
        std::cerr << name << " returned an incorrect JSON buffer length." << std::endl;
        return false;
    }
    if (freeResult != 0) {
        std::cerr << name << " result release failed." << std::endl;
        return false;
    }
    if (output.is_discarded() || !output.is_object()) {
        std::cerr << name << " returned invalid JSON." << std::endl;
        return false;
    }
    return true;
}

bool TestGhostExport()
{
    cv::Mat image = cv::Mat::zeros(192, 256, CV_8UC1);
    const cv::Point brightCenter(62, 96);
    const cv::Point extraBrightCenter(132, 48);
    const cv::Point ghostCenter(190, 100);
    cv::circle(image, brightCenter, 12, cv::Scalar(255), cv::FILLED, cv::LINE_8);
    cv::circle(image, extraBrightCenter, 10, cv::Scalar(245), cv::FILLED, cv::LINE_8);
    cv::circle(image, ghostCenter, 8, cv::Scalar(90), cv::FILLED, cv::LINE_8);

    const json config = {
        { "brightThresholdMode", "fixed" },
        { "ghostThresholdMode", "fixed" },
        { "brightThreshold", 0.75 },
        { "ghostThreshold", 0.20 },
        { "brightGridRows", 1 },
        { "brightGridCols", 1 },
        { "brightMinArea", 100 },
        { "brightMaxArea", 1000 },
        { "brightMinSize", 8 },
        { "brightMaxSize", 40 },
        { "ghostMinArea", 40 },
        { "ghostMaxArea", 500 },
        { "ghostMinSize", 5 },
        { "ghostMaxSize", 30 },
        { "brightOpenKernel", 1 },
        { "brightCloseKernel", 1 },
        { "ghostOpenKernel", 1 },
        { "ghostCloseKernel", 1 },
        { "sourceMaskPadding", 3 },
        { "minDistanceFromBright", 40.0 },
        { "minRelativeIntensity", 0.05 },
        { "maxCandidates", 8 }
    };
    const std::string configText = config.dump();
    HImage view = MakeImageView(image);

    json output;
    if (!InvokeJsonExport("M_DetectGhosts", [&](char** result) {
        return M_DetectGhosts(view, RoiRect{}, configText.c_str(), result);
    }, output)) {
        return false;
    }

    bool success = true;
    Expect(output.value("success", false), "Ghost detection did not succeed.", success);
    Expect(output.value("algorithm", std::string()) == "GhostDetection", "Ghost metadata is missing.", success);
    Expect(output.contains("brightSources") && output.at("brightSources").is_array() &&
        output.at("brightSources").size() == 1, "Expected exactly one bright source.", success);
    Expect(output.contains("candidates") && output.at("candidates").is_array() &&
        output.at("candidates").size() == 1,
        "Expected only the dim circle, not the extra bright source, to be a ghost candidate.", success);

    if (output.contains("brightSources") && !output.at("brightSources").empty()) {
        const json& source = output.at("brightSources").front();
        Expect(IsFinitePoint(source.value("center", json())), "Bright-source center is not finite.", success);
        if (IsFinitePoint(source.value("center", json()))) {
            Expect(PointDistance(source.at("center"), brightCenter) <= 1.5, "Bright-source center is inaccurate.", success);
        }
    }
    if (output.contains("candidates") && !output.at("candidates").empty()) {
        const json& candidate = output.at("candidates").front();
        Expect(IsFinitePoint(candidate.value("center", json())), "Ghost center is not finite.", success);
        if (IsFinitePoint(candidate.value("center", json()))) {
            Expect(PointDistance(candidate.at("center"), ghostCenter) <= 1.5, "Ghost center is inaccurate.", success);
        }
        Expect(candidate.value("area", 0) >= 150, "Ghost area is unexpectedly small.", success);
        Expect(candidate.value("meanIntensity", 0.0) > 0.30, "Ghost mean intensity was not measured.", success);
        Expect(candidate.value("peakIntensity", 0.0) >= candidate.value("meanIntensity", 0.0),
            "Ghost peak intensity is below its mean.", success);
        Expect(candidate.value("relativeIntensity", 0.0) > 0.20, "Ghost relative intensity is unexpectedly low.", success);
        Expect(IsFiniteNumber(candidate.value("severity", json())), "Ghost severity is not finite.", success);
    }
    return success;
}

bool TestEnhancedGhostExport()
{
    cv::Mat image(192, 256, CV_16UC1);
    for (int y = 0; y < image.rows; ++y) {
        std::uint16_t* row = image.ptr<std::uint16_t>(y);
        for (int x = 0; x < image.cols; ++x) {
            row[x] = static_cast<std::uint16_t>(5000 + x * 20 + y * 5);
        }
    }

    const cv::Point brightCenter(45, 96);
    const cv::Point ghostCenter(180, 96);
    const cv::Point directionalDecoy(45, 30);
    cv::circle(image, brightCenter, 11, cv::Scalar(60000), cv::FILLED, cv::LINE_AA);
    cv::circle(image, ghostCenter, 11, cv::Scalar(19000), cv::FILLED, cv::LINE_AA);
    cv::circle(image, directionalDecoy, 9, cv::Scalar(19000), cv::FILLED, cv::LINE_AA);

    const json config = {
        { "brightThresholdMode", "fixed" },
        { "ghostThresholdMode", "fixed" },
        { "brightThreshold", 0.55 },
        { "ghostThreshold", 0.07 },
        { "brightMinArea", 80 },
        { "brightMaxArea", 1000 },
        { "brightMinSize", 7 },
        { "brightMaxSize", 40 },
        { "ghostMinArea", 30 },
        { "ghostMaxArea", 2000 },
        { "ghostMinSize", 4 },
        { "ghostMaxSize", 50 },
        { "brightOpenKernel", 1 },
        { "brightCloseKernel", 1 },
        { "ghostOpenKernel", 1 },
        { "ghostCloseKernel", 3 },
        { "sourceMaskPadding", 5 },
        { "minDistanceFromBright", 30.0 },
        { "normalizeExposure", true },
        { "exposureLowPercentile", 0.01 },
        { "exposureHighPercentile", 0.995 },
        { "backgroundKernel", 41 },
        { "multiScaleLevels", 3 },
        { "multiScaleFactor", 1.6 },
        { "multiScaleThresholdFactor", 0.85 },
        { "opticalCenter", { { "x", 128.0 }, { "y", 96.0 } } },
        { "useDirectionalConfidence", true },
        { "minDirectionConfidence", 0.75 },
        { "maxCandidates", 8 }
    };
    const std::string configText = config.dump();
    HImage view = MakeImageView(image);

    json output;
    if (!InvokeJsonExport("M_DetectGhosts enhanced", [&](char** result) {
        return M_DetectGhosts(view, RoiRect{}, configText.c_str(), result);
    }, output)) {
        return false;
    }

    bool success = true;
    Expect(output.value("success", false), "Enhanced Ghost detection did not succeed.", success);
    Expect(output.value("exposureNormalized", false), "Exposure normalization was not applied.", success);
    Expect(output.value("backgroundModelUsed", false), "Background model was not applied.", success);
    Expect(output.value("analyzedScaleLevels", 0) == 3, "Ghost scale-level count is incorrect.", success);
    Expect(output.contains("candidates") && output.at("candidates").is_array() && output.at("candidates").size() == 1,
        "Directional filtering did not retain exactly the aligned ghost.", success);
    if (output.contains("candidates") && output.at("candidates").size() == 1) {
        const json& candidate = output.at("candidates").front();
        Expect(PointDistance(candidate.at("center"), ghostCenter) <= 2.0, "Enhanced Ghost center is inaccurate.", success);
        Expect(candidate.value("directionConfidence", 0.0) > 0.95, "Aligned Ghost direction confidence is too low.", success);
        Expect(candidate.value("directionAngleDegrees", 180.0) < 5.0, "Aligned Ghost direction angle is too large.", success);
        Expect(candidate.value("scaleSupport", 0.0) >= 2.0 / 3.0, "Ghost is not supported across enough scales.", success);
        Expect(candidate.value("confidence", 0.0) > 0.50, "Enhanced Ghost confidence is unexpectedly low.", success);
    }
    return success;
}

bool TestKeyboardHaloExport()
{
    cv::Mat image = cv::Mat::zeros(160, 160, CV_8UC1);
    const cv::Rect keyRect(60, 60, 40, 40);
    const cv::Rect haloBounds(46, 46, 68, 68);
    image(haloBounds).setTo(cv::Scalar(50));
    image(keyRect).setTo(cv::Scalar(220));

    const json config = {
        { "keyRects", json::array({ {
            { "x", keyRect.x }, { "y", keyRect.y },
            { "width", keyRect.width }, { "height", keyRect.height }
        } }) },
        { "innerInsetRatio", 0.10 },
        { "haloGapRatio", 0.10 },
        { "haloWidthRatio", 0.25 },
        { "minimumValidPixels", 10 }
    };
    const std::string configText = config.dump();
    HImage view = MakeImageView(image);

    json output;
    if (!InvokeJsonExport("M_AnalyzeKeyboardHalo", [&](char** result) {
        return M_AnalyzeKeyboardHalo(view, RoiRect{}, configText.c_str(), result);
    }, output)) {
        return false;
    }

    bool success = true;
    Expect(output.value("success", false), "Keyboard halo measurement did not succeed.", success);
    Expect(output.value("keyRectSource", std::string()) == "config", "Explicit key rectangles were not used.", success);
    Expect(output.contains("keys") && output.at("keys").is_array() && output.at("keys").size() == 1,
        "Expected one key halo measurement.", success);
    if (output.contains("keys") && output.at("keys").size() == 1) {
        const json& key = output.at("keys").front();
        const double expectedKeyMean = 220.0 / 255.0;
        const double expectedHaloMean = 50.0 / 255.0;
        const double expectedRatio = 50.0 / 220.0;
        Expect(key.value("ratioValid", false), "Halo/key ratio is invalid.", success);
        Expect(std::abs(key.value("keyMean", -1.0) - expectedKeyMean) <= 0.01,
            "Key mean does not match the synthetic key.", success);
        Expect(std::abs(key.value("haloMean", -1.0) - expectedHaloMean) <= 0.01,
            "Halo mean does not match the synthetic ring.", success);
        Expect(std::abs(key.value("haloToKeyRatio", -1.0) - expectedRatio) <= 0.02,
            "Halo/key ratio is outside tolerance.", success);
    }

    json automaticConfig = config;
    automaticConfig.erase("keyRects");
    automaticConfig["detection"] = {
        { "threshold", 0.50 }, { "minArea", 500 }, { "maxArea", 2500 },
        { "openKernel", 1 }, { "closeKernel", 1 }
    };
    const std::string automaticConfigText = automaticConfig.dump();
    json automaticOutput;
    if (!InvokeJsonExport("M_AnalyzeKeyboardHalo automatic", [&](char** result) {
        return M_AnalyzeKeyboardHalo(view, RoiRect{}, automaticConfigText.c_str(), result);
    }, automaticOutput)) {
        return false;
    }
    Expect(automaticOutput.value("success", false), "Automatic key detection did not produce a halo measurement.", success);
    Expect(automaticOutput.value("keyRectSource", std::string()) == "automatic",
        "Automatic key detection source metadata is incorrect.", success);
    Expect(automaticOutput.contains("keys") && automaticOutput.at("keys").size() == 1,
        "Automatic key detection did not find exactly one key.", success);
    return success;
}

bool TestLedArrayExport()
{
    constexpr int Rows = 3;
    constexpr int Cols = 4;
    cv::Mat image = cv::Mat::zeros(180, 220, CV_8UC1);
    std::array<std::array<cv::Point, Cols>, Rows> centers{};
    for (int row = 0; row < Rows; ++row) {
        for (int col = 0; col < Cols; ++col) {
            centers[static_cast<size_t>(row)][static_cast<size_t>(col)] = cv::Point(40 + col * 45, 45 + row * 45);
            cv::circle(image, centers[static_cast<size_t>(row)][static_cast<size_t>(col)], 5,
                cv::Scalar(205), cv::FILLED, cv::LINE_8);
        }
    }

    json detections = json::array();
    int sourceId = 1;
    for (int row = Rows - 1; row >= 0; --row) {
        for (int col = Cols - 1; col >= 0; --col) {
            const cv::Point point = centers[static_cast<size_t>(row)][static_cast<size_t>(col)];
            detections.push_back({
                { "id", sourceId++ },
                { "center", { { "x", point.x }, { "y", point.y } } },
                { "boundingRect", { { "x", point.x - 5 }, { "y", point.y - 5 }, { "width", 11 }, { "height", 11 } } },
                { "area", 81.0 }
            });
        }
    }

    const json config = {
        { "rows", Rows },
        { "cols", Cols },
        { "sampleRadius", 3 },
        { "assignmentGatePixels", 12.0 },
        { "minimumBrightness", 0.50 },
        { "minimumArea", 60.0 },
        { "maximumArea", 100.0 },
        { "detections", std::move(detections) }
    };
    const std::string configText = config.dump();
    HImage view = MakeImageView(image);

    json output;
    if (!InvokeJsonExport("M_AnalyzeLedArray", [&](char** result) {
        return M_AnalyzeLedArray(view, RoiRect{}, configText.c_str(), result);
    }, output)) {
        return false;
    }

    bool success = true;
    Expect(output.value("success", false), "LED array analysis did not succeed.", success);
    Expect(output.value("detectionSource", std::string()) == "config", "Explicit LED detections were not used.", success);
    Expect(output.value("expectedCount", 0) == Rows * Cols, "LED expected count is incorrect.", success);
    Expect(output.value("matchedCount", 0) == Rows * Cols, "Not all LED detections were matched.", success);
    Expect(output.value("missingCount", -1) == 0, "Complete LED array contains missing points.", success);
    Expect(output.value("extraCount", -1) == 0, "Complete LED array contains extra points.", success);
    Expect(output.contains("points") && output.at("points").is_array() && output.at("points").size() == Rows * Cols,
        "LED output point count is incorrect.", success);

    if (output.contains("points") && output.at("points").size() == Rows * Cols) {
        for (int index = 0; index < Rows * Cols; ++index) {
            const json& point = output.at("points").at(static_cast<size_t>(index));
            const int expectedRow = index / Cols;
            const int expectedCol = index % Cols;
            Expect(point.value("id", 0) == index + 1, "LED point IDs are not stable.", success);
            Expect(point.value("row", -1) == expectedRow && point.value("col", -1) == expectedCol,
                "LED points are not in row-major order.", success);
            Expect(point.value("detected", false), "A complete LED grid cell is not marked detected.", success);
            if (IsFinitePoint(point.value("detectedCenter", json()))) {
                Expect(PointDistance(point.at("detectedCenter"),
                    centers[static_cast<size_t>(expectedRow)][static_cast<size_t>(expectedCol)]) <= 1.0,
                    "LED detected center is unstable.", success);
            }
            else {
                Expect(false, "LED detected center is not finite.", success);
            }
        }
    }

    json automaticConfig = config;
    automaticConfig.erase("detections");
    automaticConfig["detection"] = {
        { "threshold", 0.50 }, { "minArea", 60 }, { "maxArea", 100 },
        { "openKernel", 1 }, { "closeKernel", 1 }
    };
    const std::string automaticConfigText = automaticConfig.dump();
    json automaticOutput;
    if (!InvokeJsonExport("M_AnalyzeLedArray automatic", [&](char** result) {
        return M_AnalyzeLedArray(view, RoiRect{}, automaticConfigText.c_str(), result);
    }, automaticOutput)) {
        return false;
    }
    Expect(automaticOutput.value("success", false), "Automatic LED detection did not produce a grid.", success);
    Expect(automaticOutput.value("detectionSource", std::string()) == "automatic",
        "Automatic LED detection source metadata is incorrect.", success);
    Expect(automaticOutput.value("matchedCount", 0) == Rows * Cols,
        "Automatic LED detection did not match the complete grid.", success);
    return success;
}

cv::Point2d TransformPoint(const cv::Mat& matrix, const cv::Point2d& point)
{
    return cv::Point2d(
        matrix.at<double>(0, 0) * point.x + matrix.at<double>(0, 1) * point.y + matrix.at<double>(0, 2),
        matrix.at<double>(1, 0) * point.x + matrix.at<double>(1, 1) * point.y + matrix.at<double>(1, 2));
}

struct RotatedFixture
{
    cv::Mat image;
    cv::Mat mask;
    cv::Point2d centerOffset;
};

cv::Mat CreateAsymmetricTemplate()
{
    cv::Mat templ = cv::Mat::zeros(35, 45, CV_8UC1);
    cv::rectangle(templ, cv::Rect(5, 5, 8, 25), cv::Scalar(255), cv::FILLED);
    cv::rectangle(templ, cv::Rect(5, 23, 29, 7), cv::Scalar(255), cv::FILLED);
    cv::circle(templ, cv::Point(35, 8), 4, cv::Scalar(145), cv::FILLED, cv::LINE_8);
    cv::rectangle(templ, cv::Rect(19, 10, 7, 6), cv::Scalar(80), cv::FILLED);
    return templ;
}

RotatedFixture RotateFixture(const cv::Mat& templ, double angleDegrees)
{
    const cv::Point2d rotationCenter(templ.cols * 0.5, templ.rows * 0.5);
    cv::Mat matrix = cv::getRotationMatrix2D(rotationCenter, angleDegrees, 1.0);
    const std::array<cv::Point2d, 4> corners = {
        cv::Point2d(0.0, 0.0),
        cv::Point2d(static_cast<double>(templ.cols), 0.0),
        cv::Point2d(static_cast<double>(templ.cols), static_cast<double>(templ.rows)),
        cv::Point2d(0.0, static_cast<double>(templ.rows))
    };

    double minX = std::numeric_limits<double>::max();
    double minY = std::numeric_limits<double>::max();
    double maxX = std::numeric_limits<double>::lowest();
    double maxY = std::numeric_limits<double>::lowest();
    for (const cv::Point2d& corner : corners) {
        const cv::Point2d transformed = TransformPoint(matrix, corner);
        minX = std::min(minX, transformed.x);
        minY = std::min(minY, transformed.y);
        maxX = std::max(maxX, transformed.x);
        maxY = std::max(maxY, transformed.y);
    }
    matrix.at<double>(0, 2) -= minX;
    matrix.at<double>(1, 2) -= minY;

    const cv::Size canvasSize(
        std::max(1, static_cast<int>(std::ceil(maxX - minX))),
        std::max(1, static_cast<int>(std::ceil(maxY - minY))));
    RotatedFixture fixture;
    cv::warpAffine(templ, fixture.image, matrix, canvasSize, cv::INTER_LINEAR, cv::BORDER_CONSTANT, cv::Scalar(0));
    cv::Mat sourceMask(templ.size(), CV_8U, cv::Scalar(255));
    cv::warpAffine(sourceMask, fixture.mask, matrix, canvasSize, cv::INTER_NEAREST, cv::BORDER_CONSTANT, cv::Scalar(0));

    std::vector<cv::Point> nonZero;
    cv::findNonZero(fixture.mask, nonZero);
    const cv::Rect contentBounds = cv::boundingRect(nonZero);
    fixture.image = fixture.image(contentBounds).clone();
    fixture.mask = fixture.mask(contentBounds).clone();
    fixture.centerOffset = TransformPoint(matrix, rotationCenter) - cv::Point2d(contentBounds.x, contentBounds.y);
    return fixture;
}

bool TestRotatedTemplateExport()
{
    cv::Mat templ = CreateAsymmetricTemplate();

    constexpr double EmbeddedAngle = 20.0;
    const RotatedFixture fixture = RotateFixture(templ, EmbeddedAngle);
    cv::Mat source = cv::Mat::zeros(220, 320, CV_8UC1);
    const cv::Point topLeft(135, 82);
    fixture.image.copyTo(source(cv::Rect(topLeft, fixture.image.size())), fixture.mask);
    const cv::Point2d expectedCenter = cv::Point2d(topLeft) + fixture.centerOffset;

    const json config = {
        { "angleMin", 10.0 },
        { "angleMax", 30.0 },
        { "angleStep", 2.0 },
        { "scoreThreshold", 0.80 },
        { "maxMatches", 1 },
        { "nmsRadius", 20.0 },
        { "pyramidLevels", 1 },
        { "subpixel", true }
    };
    const std::string configText = config.dump();
    HImage sourceView = MakeImageView(source);
    HImage templateView = MakeImageView(templ);

    json output;
    if (!InvokeJsonExport("M_MatchRotatedTemplate", [&](char** result) {
        return M_MatchRotatedTemplate(sourceView, templateView, RoiRect{}, configText.c_str(), result);
    }, output)) {
        return false;
    }

    bool success = true;
    Expect(output.value("success", false), "Rotated-template matching did not succeed.", success);
    Expect(output.contains("matches") && output.at("matches").is_array() && !output.at("matches").empty(),
        "Rotated-template matching returned no match.", success);
    if (output.contains("matches") && !output.at("matches").empty()) {
        const json& match = output.at("matches").front();
        Expect(IsFinitePoint(match.value("center", json())), "Template-match center is not finite.", success);
        if (IsFinitePoint(match.value("center", json()))) {
            Expect(PointDistance(match.at("center"), expectedCenter) <= 2.0,
                "Template-match center is outside tolerance.", success);
        }
        Expect(std::abs(match.value("angleDegrees", -999.0) - EmbeddedAngle) <= 2.1,
            "Template-match angle is outside one search step.", success);
        Expect(match.value("score", 0.0) >= 0.80, "Template-match score is below threshold.", success);
    }

    cv::Mat constantTemplate(templ.size(), templ.type(), cv::Scalar(96));
    HImage constantTemplateView = MakeImageView(constantTemplate);
    json rejectedOutput;
    if (!InvokeJsonExport("M_MatchRotatedTemplate constant template", [&](char** result) {
        return M_MatchRotatedTemplate(sourceView, constantTemplateView, RoiRect{}, configText.c_str(), result);
    }, rejectedOutput)) {
        return false;
    }
    Expect(!rejectedOutput.value("success", true), "Constant template produced a false-positive match.", success);
    Expect(rejectedOutput.value("statusCode", std::string()) == "invalid_template",
        "Constant template did not return invalid_template.", success);
    return success;
}

bool TestScaledOccludedTemplateExport()
{
    cv::Mat templ = CreateAsymmetricTemplate();
    constexpr double EmbeddedScale = 1.30;
    constexpr double EmbeddedAngle = 16.0;
    cv::Mat scaledTemplate;
    cv::resize(templ, scaledTemplate, cv::Size(), EmbeddedScale, EmbeddedScale, cv::INTER_LINEAR);
    const RotatedFixture fixture = RotateFixture(scaledTemplate, EmbeddedAngle);

    cv::Mat source = cv::Mat::zeros(260, 380, CV_8UC1);
    const cv::Point topLeft(150, 95);
    fixture.image.copyTo(source(cv::Rect(topLeft, fixture.image.size())), fixture.mask);
    const cv::Rect occlusion(
        topLeft.x + fixture.image.cols * 2 / 3,
        topLeft.y + fixture.image.rows / 4,
        std::max(2, fixture.image.cols / 4),
        std::max(2, fixture.image.rows / 2));
    source(occlusion).setTo(cv::Scalar(0));
    const cv::Point2d expectedCenter = cv::Point2d(topLeft) + fixture.centerOffset;

    const json config = {
        { "angleMin", 10.0 }, { "angleMax", 22.0 }, { "angleStep", 2.0 },
        { "scaleMin", 1.10 }, { "scaleMax", 1.40 }, { "scaleStep", 0.10 },
        { "featureMode", "gradient" }, { "occlusionTolerance", 0.35 },
        { "scoreThreshold", 0.78 }, { "maxMatches", 1 }, { "nmsRadius", 24.0 },
        { "pyramidLevels", 1 }, { "subpixel", true }
    };
    const std::string configText = config.dump();
    HImage sourceView = MakeImageView(source);
    HImage templateView = MakeImageView(templ);
    json output;
    if (!InvokeJsonExport("M_MatchRotatedTemplate scale/gradient/occlusion", [&](char** result) {
        return M_MatchRotatedTemplate(sourceView, templateView, RoiRect{}, configText.c_str(), result);
    }, output)) {
        return false;
    }

    bool success = true;
    Expect(output.value("success", false), "Scaled occluded gradient template was not matched.", success);
    Expect(output.contains("matches") && !output.at("matches").empty(),
        "Scaled template search returned no matches.", success);
    if (output.contains("matches") && !output.at("matches").empty()) {
        const json& match = output.at("matches").front();
        Expect(PointDistance(match.at("center"), expectedCenter) <= 3.0,
            "Scaled template center is outside tolerance.", success);
        Expect(std::abs(match.value("angleDegrees", -999.0) - EmbeddedAngle) <= 2.1,
            "Scaled template angle is outside one search step.", success);
        Expect(std::abs(match.value("scale", 0.0) - EmbeddedScale) <= 0.11,
            "Scaled template factor is outside one search step.", success);
        Expect(match.value("score", 0.0) >= 0.78, "Robust template score is below threshold.", success);
        Expect(match.value("visibleFraction", 1.0) < 0.90,
            "Occlusion-tolerant result did not report trimmed visibility.", success);
    }
    return success;
}

void DrawCross(cv::Mat& image, const cv::Point& center, int armLength, int thickness)
{
    cv::line(image, cv::Point(center.x - armLength, center.y), cv::Point(center.x + armLength, center.y),
        cv::Scalar(255), thickness, cv::LINE_8);
    cv::line(image, cv::Point(center.x, center.y - armLength), cv::Point(center.x, center.y + armLength),
        cv::Scalar(255), thickness, cv::LINE_8);
}

bool TestBinocularFusionExport()
{
    cv::Mat image = cv::Mat::zeros(240, 320, CV_8UC1);
    const cv::Point center(160, 120);
    DrawCross(image, center, 13, 5);
    DrawCross(image, cv::Point(162, 55), 13, 5);
    DrawCross(image, cv::Point(158, 185), 13, 5);
    DrawCross(image, cv::Point(85, 122), 13, 5);
    DrawCross(image, cv::Point(235, 118), 13, 5);

    const json config = {
        { "threshold", 128.0 },
        { "blurKernel", 1 },
        { "morphKernel", 1 },
        { "minArea", 100 },
        { "maxArea", 1000 },
        { "pixelSize", 3.76 },
        { "focalLength", 30.0 },
        { "opticalCenter", { { "x", center.x }, { "y", center.y } } },
        { "maxCandidates", 16 }
    };
    const std::string configText = config.dump();
    HImage view = MakeImageView(image);

    json output;
    if (!InvokeJsonExport("M_CalBinocularFusion", [&](char** result) {
        return M_CalBinocularFusion(view, RoiRect{}, configText.c_str(), result);
    }, output)) {
        return false;
    }

    bool success = true;
    Expect(output.value("success", false), "Five-cross binocular fusion did not succeed.", success);
    Expect(output.contains("points") && output.at("points").is_object(), "Binocular roles are missing.", success);
    if (output.contains("points") && output.at("points").is_object()) {
        const json& points = output.at("points");
        const std::array<const char*, 5> roles = { "center", "top", "bottom", "left", "right" };
        for (const char* role : roles) {
            Expect(points.contains(role) && points.at(role).is_object(), std::string("Missing binocular role: ") + role, success);
            if (points.contains(role) && points.at(role).is_object()) {
                Expect(points.at(role).value("role", std::string()) == role,
                    std::string("Incorrect binocular role label: ") + role, success);
                Expect(IsFinitePoint(points.at(role).value("center", json())),
                    std::string("Non-finite binocular point: ") + role, success);
            }
        }
        if (points.contains("center") && IsFinitePoint(points.at("center").value("center", json()))) {
            Expect(PointDistance(points.at("center").at("center"), center) <= 3.0,
                "Detected binocular center is outside tolerance.", success);
        }
    }
    Expect(IsFiniteNumber(output.value("rollDegrees", json())), "Binocular roll is not finite.", success);
    Expect(IsFiniteNumber(output.value("horizontalOffsetDegrees", json())), "Horizontal offset angle is not finite.", success);
    Expect(IsFiniteNumber(output.value("verticalOffsetDegrees", json())), "Vertical offset angle is not finite.", success);

    const std::array<cv::Point, 5> positions = {
        center, cv::Point(162, 55), cv::Point(158, 185), cv::Point(85, 122), cv::Point(235, 118)
    };
    const auto expectRejectedShape = [&](const char* name, bool circles) {
        cv::Mat negativeImage = cv::Mat::zeros(image.size(), image.type());
        for (const cv::Point& position : positions) {
            if (circles) {
                cv::circle(negativeImage, position, 11, cv::Scalar(255), cv::FILLED, cv::LINE_8);
            }
            else {
                cv::rectangle(negativeImage, cv::Rect(position.x - 10, position.y - 10, 21, 21),
                    cv::Scalar(255), cv::FILLED, cv::LINE_8);
            }
        }
        HImage negativeView = MakeImageView(negativeImage);
        json negativeOutput;
        const bool invoked = InvokeJsonExport(name, [&](char** result) {
            return M_CalBinocularFusion(negativeView, RoiRect{}, configText.c_str(), result);
        }, negativeOutput);
        Expect(invoked, std::string(name) + " did not return structured JSON.", success);
        if (invoked) {
            Expect(!negativeOutput.value("success", true),
                std::string(name) + " was incorrectly accepted as five crosses.", success);
        }
    };
    expectRejectedShape("M_CalBinocularFusion solid squares", false);
    expectRejectedShape("M_CalBinocularFusion solid circles", true);
    return success;
}

cv::Point ProjectStereoPoint(
    const cv::Point3d& point,
    const cv::Matx33d& camera,
    const cv::Vec3d& translation)
{
    const cv::Vec3d cameraPoint(point.x + translation[0], point.y + translation[1], point.z + translation[2]);
    return cv::Point(
        cvRound(camera(0, 0) * cameraPoint[0] / cameraPoint[2] + camera(0, 2)),
        cvRound(camera(1, 1) * cameraPoint[1] / cameraPoint[2] + camera(1, 2)));
}

bool TestStereoBinocularFusionExport()
{
    const cv::Matx33d camera(
        600.0, 0.0, 320.0,
        0.0, 600.0, 240.0,
        0.0, 0.0, 1.0);
    const cv::Vec3d rightTranslation(-80.0, 0.0, 0.0);
    const std::array<cv::Point3d, 5> worldPoints = {
        cv::Point3d(0.0, 0.0, 1200.0),
        cv::Point3d(0.0, -120.0, 1200.0),
        cv::Point3d(0.0, 120.0, 1200.0),
        cv::Point3d(-120.0, 0.0, 1200.0),
        cv::Point3d(120.0, 0.0, 1200.0)
    };

    cv::Mat left = cv::Mat::zeros(480, 640, CV_8UC1);
    cv::Mat right = cv::Mat::zeros(480, 640, CV_8UC1);
    for (const cv::Point3d& point : worldPoints) {
        DrawCross(left, ProjectStereoPoint(point, camera, cv::Vec3d()), 11, 5);
        DrawCross(right, ProjectStereoPoint(point, camera, rightTranslation), 11, 5);
    }

    const json detection = {
        { "threshold", 128.0 }, { "blurKernel", 1 }, { "morphKernel", 1 },
        { "minArea", 80 }, { "maxArea", 800 }, { "maxCandidates", 16 }
    };
    const json config = {
        { "leftDetection", detection },
        { "rightDetection", detection },
        { "minimumParallaxPixels", 5.0 },
        { "maximumReprojectionErrorPixels", 1.5 },
        { "calibration", {
            { "leftCameraMatrix", { 600.0, 0.0, 320.0, 0.0, 600.0, 240.0, 0.0, 0.0, 1.0 } },
            { "rightCameraMatrix", { 600.0, 0.0, 320.0, 0.0, 600.0, 240.0, 0.0, 0.0, 1.0 } },
            { "leftDistCoeffs", { 0.0, 0.0, 0.0, 0.0, 0.0 } },
            { "rightDistCoeffs", { 0.0, 0.0, 0.0, 0.0, 0.0 } },
            { "rotation", { 1.0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0, 1.0 } },
            { "translation", { -80.0, 0.0, 0.0 } }
        } }
    };
    const std::string configText = config.dump();
    HImage leftView = MakeImageView(left);
    HImage rightView = MakeImageView(right);
    json output;
    if (!InvokeJsonExport("M_CalStereoBinocularFusion", [&](char** result) {
        return M_CalStereoBinocularFusion(
            leftView, rightView, RoiRect{}, RoiRect{}, configText.c_str(), result);
    }, output)) {
        return false;
    }

    bool success = true;
    Expect(output.value("success", false), "Calibrated stereo fusion did not succeed.", success);
    Expect(output.value("validPointCount", 0) == 5, "Stereo fusion did not triangulate all five crosses.", success);
    Expect(std::abs(output.value("baselineMm", 0.0) - 80.0) <= 1e-6,
        "Stereo baseline is incorrect.", success);
    Expect(std::abs(output.value("meanDepthMm", 0.0) - 1200.0) <= 5.0,
        "Stereo mean depth is outside tolerance.", success);
    Expect(output.value("meanReprojectionErrorPixels", 99.0) <= 0.25,
        "Stereo reprojection error is unexpectedly high.", success);
    Expect(output.value("confidence", 0.0) >= 0.70, "Stereo confidence is unexpectedly low.", success);
    Expect(output.contains("points") && output.at("points").is_array() && output.at("points").size() == 5,
        "Stereo result does not contain five point records.", success);
    if (output.contains("points") && output.at("points").is_array()) {
        for (const json& point : output.at("points")) {
            Expect(point.value("valid", false), "A stereo point failed quality limits.", success);
            Expect(point.contains("pointMm") && IsFiniteNumber(point.at("pointMm").at("z")),
                "Stereo point depth is not finite.", success);
        }
    }
    return success;
}

bool TestFailureResultClearing()
{
    cv::Mat image(32, 32, CV_8UC1, cv::Scalar(32));
    cv::Mat templ(7, 9, CV_8UC1, cv::Scalar(64));
    HImage imageView = MakeImageView(image);
    HImage templateView = MakeImageView(templ);
    const HImage emptyImage{};

    using ExportCall = std::function<int(HImage, const char*, char**)>;
    const std::vector<std::pair<std::string, ExportCall>> calls = {
        { "M_DetectGhosts", [](HImage input, const char* config, char** result) {
            return M_DetectGhosts(input, RoiRect{}, config, result);
        } },
        { "M_AnalyzeKeyboardHalo", [](HImage input, const char* config, char** result) {
            return M_AnalyzeKeyboardHalo(input, RoiRect{}, config, result);
        } },
        { "M_AnalyzeLedArray", [](HImage input, const char* config, char** result) {
            return M_AnalyzeLedArray(input, RoiRect{}, config, result);
        } },
        { "M_MatchRotatedTemplate", [templateView](HImage input, const char* config, char** result) {
            return M_MatchRotatedTemplate(input, templateView, RoiRect{}, config, result);
        } },
        { "M_CalBinocularFusion", [](HImage input, const char* config, char** result) {
            return M_CalBinocularFusion(input, RoiRect{}, config, result);
        } }
    };

    bool success = true;
    for (const auto& item : calls) {
        char* invalidJsonResult = reinterpret_cast<char*>(static_cast<std::uintptr_t>(1));
        const int invalidJsonReturn = item.second(imageView, "{not-json", &invalidJsonResult);
        Expect(invalidJsonReturn < 0, item.first + " accepted invalid JSON.", success);
        Expect(invalidJsonResult == nullptr, item.first + " did not clear result for invalid JSON.", success);

        char* emptyImageResult = reinterpret_cast<char*>(static_cast<std::uintptr_t>(1));
        const int emptyImageReturn = item.second(emptyImage, "{}", &emptyImageResult);
        Expect(emptyImageReturn < 0, item.first + " accepted an empty image.", success);
        Expect(emptyImageResult == nullptr, item.first + " did not clear result for an empty image.", success);
    }

    char* invalidStereoJsonResult = reinterpret_cast<char*>(static_cast<std::uintptr_t>(1));
    const int invalidStereoJsonReturn = M_CalStereoBinocularFusion(
        imageView, imageView, RoiRect{}, RoiRect{}, "{not-json", &invalidStereoJsonResult);
    Expect(invalidStereoJsonReturn < 0, "M_CalStereoBinocularFusion accepted invalid JSON.", success);
    Expect(invalidStereoJsonResult == nullptr,
        "M_CalStereoBinocularFusion did not clear result for invalid JSON.", success);

    char* emptyStereoImageResult = reinterpret_cast<char*>(static_cast<std::uintptr_t>(1));
    const int emptyStereoImageReturn = M_CalStereoBinocularFusion(
        emptyImage, imageView, RoiRect{}, RoiRect{}, "{}", &emptyStereoImageResult);
    Expect(emptyStereoImageReturn < 0, "M_CalStereoBinocularFusion accepted an empty image.", success);
    Expect(emptyStereoImageResult == nullptr,
        "M_CalStereoBinocularFusion did not clear result for an empty image.", success);
    return success;
}

template <typename Callable>
bool RunCase(const char* name, Callable&& callable)
{
    try {
        const bool success = callable();
        std::cout << "P2 " << name << ": " << (success ? "PASS" : "FAIL") << std::endl;
        return success;
    }
    catch (const std::exception& error) {
        std::cerr << "P2 " << name << " threw: " << error.what() << std::endl;
        return false;
    }
    catch (...) {
        std::cerr << "P2 " << name << " threw an unknown exception." << std::endl;
        return false;
    }
}

} // namespace

bool RunP2AlgorithmTests()
{
    bool success = true;
    success = RunCase("Ghost", TestGhostExport) && success;
    success = RunCase("EnhancedGhost", TestEnhancedGhostExport) && success;
    success = RunCase("KeyboardHalo", TestKeyboardHaloExport) && success;
    success = RunCase("LedArray", TestLedArrayExport) && success;
    success = RunCase("RotatedTemplate", TestRotatedTemplateExport) && success;
    success = RunCase("ScaledOccludedTemplate", TestScaledOccludedTemplateExport) && success;
    success = RunCase("BinocularFusion", TestBinocularFusionExport) && success;
    success = RunCase("StereoBinocularFusion", TestStereoBinocularFusionExport) && success;
    success = RunCase("FailureResultClearing", TestFailureResultClearing) && success;
    return success;
}
