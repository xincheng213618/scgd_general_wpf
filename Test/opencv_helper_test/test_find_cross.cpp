#ifndef NOMINMAX
#define NOMINMAX
#endif

#include "../../Native/include/opencv_media_export.h"

#include "CVCIEFile.hpp"

#include <array>
#include <chrono>
#include <cmath>
#include <filesystem>
#include <iostream>
#include <stdexcept>
#include <string>
#include <utility>
#include <vector>

#include <nlohmann/json.hpp>
#include <opencv2/imgproc.hpp>

using json = nlohmann::json;

bool ReadCIEFile(const std::string& filePath, CVCIEFile& fileInfo);

namespace
{

constexpr double kRadiansToDegrees = 180.0 / CV_PI;

HImage MakeHImage(const cv::Mat& image)
{
    HImage result{};
    result.rows = image.rows;
    result.cols = image.cols;
    result.channels = image.channels();
    result.depth = static_cast<int>(image.elemSize1() * 8);
    result.stride = static_cast<int>(image.step);
    result.pData = const_cast<unsigned char*>(image.data);
    return result;
}

bool CallFindCross(
    const cv::Mat& image,
    const RoiRect& roi,
    const json& config,
    json& output,
    int& returnCode)
{
    const std::string configText = config.dump();
    char* result = nullptr;
    returnCode = M_FindCrossLocal(MakeHImage(image), roi, configText.c_str(), &result);
    if (returnCode <= 0 || result == nullptr) {
        if (result != nullptr) {
            FreeResult(result);
        }
        return false;
    }
    output = json::parse(result, nullptr, false);
    FreeResult(result);
    return !output.is_discarded();
}

void DrawPatternCross(
    cv::Mat& image,
    const cv::Point2f& center,
    double angleDegrees,
    uint16_t value,
    int thickness = 7)
{
    const double radians = angleDegrees * CV_PI / 180.0;
    const cv::Point2f primary(
        static_cast<float>(std::cos(radians)),
        static_cast<float>(std::sin(radians)));
    const cv::Point2f secondary(-primary.y, primary.x);
    cv::line(
        image,
        center - primary * 120.0f,
        center + primary * 120.0f,
        cv::Scalar(value), thickness, cv::LINE_AA);
    cv::line(
        image,
        center - secondary * 95.0f,
        center + secondary * 95.0f,
        cv::Scalar(value), thickness, cv::LINE_AA);
}

cv::Point2d DistortTestPoint(
    const cv::Point2d& point,
    double fx,
    double fy,
    double cx,
    double cy,
    double k1,
    double k2,
    double p1,
    double p2,
    double k3)
{
    const double x = (point.x - cx) / fx;
    const double y = (point.y - cy) / fy;
    const double r2 = x * x + y * y;
    const double r4 = r2 * r2;
    const double r6 = r4 * r2;
    const double radial = 1.0 + k1 * r2 + k2 * r4 + k3 * r6;
    return {
        fx * (x * radial + 2.0 * p1 * x * y + p2 * (r2 + 2.0 * x * x)) + cx,
        fy * (y * radial + p1 * (r2 + 2.0 * y * y) + 2.0 * p2 * x * y) + cy
    };
}

cv::Mat MakeRotatedPanel(
    const cv::Size& size,
    const cv::RotatedRect& panel,
    const cv::Point2f& patternCenter,
    double patternAngleDegrees,
    bool includePattern = true,
    bool brightPattern = false)
{
    cv::Mat image(size, CV_16UC1, cv::Scalar(1200));
    cv::Point2f floatingCorners[4]{};
    panel.points(floatingCorners);
    std::vector<cv::Point> corners;
    for (const cv::Point2f& corner : floatingCorners) {
        corners.emplace_back(cvRound(corner.x), cvRound(corner.y));
    }
    cv::fillConvexPoly(image, corners, cv::Scalar(52000), cv::LINE_AA);

    if (includePattern) {
        DrawPatternCross(
            image,
            patternCenter,
            patternAngleDegrees,
            brightPattern ? 62000 : 43000);
    }
    cv::GaussianBlur(image, image, cv::Size(5, 5), 1.1);
    return image;
}

struct PatternMeasurement
{
    cv::Point2d center{};
    double rotationDegrees = 0.0;
    double confidence = 0.0;
};

double PatternAxisAngleError(double actualDegrees, double expectedDegrees)
{
    double difference = actualDegrees - expectedDegrees;
    while (difference >= 90.0) difference -= 180.0;
    while (difference < -90.0) difference += 180.0;
    return std::abs(difference);
}

bool ReadPatternMeasurement(const json& output, PatternMeasurement& measurement)
{
    if (!output.value("Success", false)
        || !output.contains("result") || !output["result"].is_array()
        || output["result"].size() != 1
        || !output.contains("diagnostics") || !output["diagnostics"].is_object()) {
        return false;
    }
    const json& diagnostics = output["diagnostics"];
    if (!diagnostics.contains("CenterSubpixel")
        || !diagnostics["CenterSubpixel"].is_object()) {
        return false;
    }
    measurement.center.x = diagnostics["CenterSubpixel"].value("x", NAN);
    measurement.center.y = diagnostics["CenterSubpixel"].value("y", NAN);
    measurement.rotationDegrees = output["result"][0].value("rotationAngle", NAN);
    measurement.confidence = diagnostics.value("Confidence", NAN);
    return std::isfinite(measurement.center.x)
        && std::isfinite(measurement.center.y)
        && std::isfinite(measurement.rotationDegrees)
        && std::isfinite(measurement.confidence);
}

void ApplyHorizontalIlluminationGradient(cv::Mat& image, double leftScale, double rightScale)
{
    for (int y = 0; y < image.rows; ++y) {
        uint16_t* row = image.ptr<uint16_t>(y);
        for (int x = 0; x < image.cols; ++x) {
            const double fraction = image.cols > 1
                ? x / static_cast<double>(image.cols - 1)
                : 0.0;
            const double scale = leftScale + (rightScale - leftScale) * fraction;
            row[x] = cv::saturate_cast<uint16_t>(row[x] * scale);
        }
    }
}

void AddDeterministicStains(cv::Mat& image)
{
    const std::array<std::pair<cv::Point, int>, 4> stains{
        std::pair{ cv::Point(420, 300), 18 },
        std::pair{ cv::Point(865, 315), 14 },
        std::pair{ cv::Point(835, 610), 20 },
        std::pair{ cv::Point(445, 625), 11 }
    };
    for (const auto& [center, radius] : stains) {
        cv::circle(image, center, radius, cv::Scalar(3000), cv::FILLED, cv::LINE_AA);
    }
}

void AddEdgeLightLeak(cv::Mat& image)
{
    cv::Mat leakMask = cv::Mat::zeros(image.size(), CV_32F);
    cv::ellipse(
        leakMask,
        cv::Point(970, 450),
        cv::Size(180, 150),
        12.0, 0.0, 360.0,
        cv::Scalar(1.0), cv::FILLED, cv::LINE_AA);
    cv::GaussianBlur(leakMask, leakMask, cv::Size(), 35.0, 35.0, cv::BORDER_REPLICATE);
    cv::Mat source32;
    image.convertTo(source32, CV_32F);
    cv::Mat inverseMask;
    cv::subtract(cv::Scalar::all(1.0), leakMask, inverseMask);
    cv::multiply(source32, inverseMask, source32);
    source32 += leakMask * 65000.0;
    source32.convertTo(image, image.type());
}

void AddDeterministicBadPixels(cv::Mat& image, double fraction = 0.001)
{
    cv::RNG random(0xC055);
    const int count = std::max(1, static_cast<int>(std::lround(image.total() * fraction)));
    for (int index = 0; index < count; ++index) {
        const int x = random.uniform(0, image.cols);
        const int y = random.uniform(0, image.rows);
        image.at<uint16_t>(y, x) = index % 2 == 0 ? 0 : 65535;
    }
}

bool HasGlobalPointsInside(const json& diagnostics, const char* propertyName, const RoiRect& roi)
{
    if (!diagnostics.contains(propertyName) || !diagnostics[propertyName].is_array()
        || diagnostics[propertyName].size() != 4) {
        return false;
    }
    for (const json& corner : diagnostics[propertyName]) {
        const double x = corner.value("x", -1.0);
        const double y = corner.value("y", -1.0);
        if (x < roi.x || y < roi.y || x > roi.x + roi.width || y > roi.y + roi.height) {
            return false;
        }
    }
    return true;
}

bool ContainsString(const json& values, const std::string& expected)
{
    if (!values.is_array()) {
        return false;
    }
    for (const json& value : values) {
        if (value.is_string() && value.get<std::string>() == expected) {
            return true;
        }
    }
    return false;
}

bool RunGeometryAndLegacyPayloadTest()
{
    const cv::Point2f expectedCenter(645.0f, 446.0f);
    const double expectedRotation = -3.0;
    const cv::Mat image = MakeRotatedPanel(
        cv::Size(1200, 900),
        cv::RotatedRect(expectedCenter, cv::Size2f(650.0f, 400.0f), 7.0f),
        expectedCenter,
        expectedRotation);
    const RoiRect roi{ 170, 90, 930, 730 };

    // This deliberately retains the field payload used by the on-site SDK.
    // Unknown legacy options must remain harmless adapter inputs.
    json config = {
        { "caclWay", 1 },
        { "debugCfg", {
            { "Debug", true }, { "debugPath", "Result\\" }, { "debugImgResize", 2 }
        } },
        { "CheckLine", { { "rho", 5 }, { "houghV", 100 }, { "floAngle", 10 } } },
        { "threshold", 21 },
        { "blurKernel", 3 },
        { "maxLineGap", 40 },
        { "minLineLength", 120 },
        { "findEndPointWay", 1 },
        { "binaryByContours", true },
        { "singleErodeKernel", 15 },
        { "binaryRateInContours", 0.7 },
        { "name", "Synthetic_Point_1" },
        { "MinConfidence", 0.20 },
        { "MaxProcessingSize", 1200 },
        { "RotationMethod", "LegacyCompatible" },
        { "CalibrationOffset", { { "x", 0.25 }, { "y", -0.5 } } },
        { "opticsParams", {
            { "stdCenter", { { "x", 600.0 }, { "y", 450.0 } } },
            { "focusLength", 25.4 },
            { "sensorPixSize", 3.76 },
            { "distortion", {
                { "Enabled", true }, { "K1", 0.0 }, { "K2", 0.0 },
                { "P1", 0.0 }, { "P2", 0.0 }, { "K3", 0.0 },
                { "Fx", 6755.319148936171 }, { "Fy", 6755.319148936171 },
                { "Cx", 600.0 }, { "Cy", 450.0 }
            } },
            { "objectDistance", 10000 }
        } }
    };

    json output;
    int returnCode = 0;
    if (!CallFindCross(image, roi, config, output, returnCode)) {
        std::cerr << "FindCross legacy-compatible call failed, code=" << returnCode << std::endl;
        return false;
    }
    if (!output.contains("result") || !output["result"].is_array()
        || output["result"].size() != 1 || !output.contains("diagnostics")) {
        std::cerr << "FindCross output envelope mismatch: " << output.dump() << std::endl;
        return false;
    }

    const json& item = output["result"][0];
    const json& diagnostics = output["diagnostics"];
    const double centerX = diagnostics["CenterSubpixel"].value("x", -1.0);
    const double centerY = diagnostics["CenterSubpixel"].value("y", -1.0);
    const double rawCenterX = diagnostics["RawGeometricCenter"].value("x", -1.0);
    const double rawCenterY = diagnostics["RawGeometricCenter"].value("y", -1.0);
    const double rotation = item.value("rotationAngle", 999.0);
    const double expectedTiltX = std::atan((centerX - 600.0) * 0.00376 / 25.4) * kRadiansToDegrees;
    const double expectedTiltY = -std::atan((centerY - 450.0) * 0.00376 / 25.4) * kRadiansToDegrees;

    const bool geometryMatches = std::abs(rawCenterX - expectedCenter.x) <= 2.0
        && std::abs(rawCenterY - expectedCenter.y) <= 2.0
        && std::abs(centerX - rawCenterX - 0.25) <= 1e-3
        && std::abs(centerY - rawCenterY + 0.5) <= 1e-3
        && std::abs(rotation - expectedRotation) <= 1.0;
    const bool legacyMatches = item.value("name", std::string()) == "Synthetic_Point_1"
        && item.value("x", -1) == roi.x
        && item.value("y", -1) == roi.y
        && item.value("w", -1) == roi.width
        && item.value("h", -1) == roi.height
        && item["center"].value("x", -1) == static_cast<int>(std::lround(centerX))
        && item["center"].value("y", -1) == static_cast<int>(std::lround(centerY));
    const bool diagnosticsMatch = diagnostics.value("Success", false)
        && diagnostics.value("Algorithm", std::string()) == "PatternCrossV1"
        && diagnostics.value("DetectionMode", std::string()) == "PatternCross"
        && diagnostics.value("CenterMethod", std::string()) == "PatternAxisIntersection"
        && diagnostics.value("RotationMethod", std::string()) == "RobustTwoAxis"
        && diagnostics.value("RequestedRotationMethod", std::string()) == "LegacyCompatible"
        && ContainsString(diagnostics["IgnoredParameters"], "caclWay")
        && ContainsString(diagnostics["IgnoredParameters"], "debugCfg.Debug")
        && ContainsString(diagnostics["IgnoredParameters"], "CheckLine.houghV")
        && ContainsString(diagnostics["IgnoredParameters"], "opticsParams.objectDistance")
        && ContainsString(diagnostics["Warnings"], "LegacyParametersIgnored")
        && ContainsString(
            diagnostics["Warnings"], "DebugFilesNotWrittenDiagnosticsEmbedded")
        && ContainsString(
            diagnostics["Warnings"], "CompatibilityAliasNotVendorEquivalent")
        && diagnostics["EffectiveOptics"].value(
            "StandardCenterSource", std::string()) == "Configuration"
        && diagnostics["AppliedOffset"].value("x", 0.0) == 0.25
        && diagnostics["AppliedOffset"].value("y", 0.0) == -0.5
        && diagnostics.value("DistortionApplied", false)
        && diagnostics.contains("ArmQuality") && diagnostics["ArmQuality"].size() == 4
        && diagnostics.contains("RotationCandidates")
        && HasGlobalPointsInside(diagnostics, "ArmEndpoints", roi)
        && HasGlobalPointsInside(diagnostics, "RawArmEndpoints", roi);
    const bool tiltMatches = std::abs(item["tilt"].value("tilt_x", 999.0) - expectedTiltX) <= 1e-9
        && std::abs(item["tilt"].value("tilt_y", 999.0) - expectedTiltY) <= 1e-9;
    if (!geometryMatches || !legacyMatches || !diagnosticsMatch || !tiltMatches) {
        std::cerr << "FindCross geometry/contract mismatch: " << output.dump(2) << std::endl;
        return false;
    }

    config["RotationMethod"] = "TopEdge";
    config.erase("CalibrationOffset");
    json compatibilityOverrideOutput;
    if (!CallFindCross(image, roi, config, compatibilityOverrideOutput, returnCode)
        || compatibilityOverrideOutput["result"].size() != 1
        || compatibilityOverrideOutput["diagnostics"].value(
            "RotationMethod", std::string()) != "RobustTwoAxis"
        || std::abs(compatibilityOverrideOutput["result"][0].value(
            "rotationAngle", 999.0) - rotation) > 1e-9) {
        std::cerr << "Pattern rotation strategy must remain robust two-axis: "
            << compatibilityOverrideOutput.dump() << std::endl;
        return false;
    }
    return true;
}

bool RunPatternPolarityAndPanelIndependenceTest()
{
    const cv::Size imageSize(1200, 900);
    const cv::Point2f expectedCenter(645.0f, 446.0f);
    const double expectedRotation = 2.5;
    const cv::Mat firstPanel = MakeRotatedPanel(
        imageSize,
        cv::RotatedRect(expectedCenter, cv::Size2f(650.0f, 400.0f), -8.0f),
        expectedCenter,
        expectedRotation);
    cv::Mat secondPanel = MakeRotatedPanel(
        imageSize,
        cv::RotatedRect(expectedCenter, cv::Size2f(650.0f, 400.0f), 9.0f),
        expectedCenter,
        expectedRotation);
    const cv::Mat brightPatternGray = MakeRotatedPanel(
        imageSize,
        cv::RotatedRect(expectedCenter, cv::Size2f(650.0f, 400.0f), -4.0f),
        expectedCenter,
        expectedRotation,
        true,
        true);
    cv::Mat brightPattern;
    cv::cvtColor(brightPatternGray, brightPattern, cv::COLOR_GRAY2BGR);
    for (int y = 0; y < secondPanel.rows; ++y) {
        uint16_t* row = secondPanel.ptr<uint16_t>(y);
        for (int x = 0; x < secondPanel.cols; ++x) {
            const double scale = 0.58 + 0.42 * x / (secondPanel.cols - 1.0);
            row[x] = cv::saturate_cast<uint16_t>(row[x] * scale);
        }
    }

    const RoiRect roi{ 170, 90, 930, 730 };
    const json config = {
        { "MinConfidence", 0.20 },
        { "MinArmLengthPixels", 60 },
        { "MaxProcessingSize", 1200 }
    };
    json firstOutput;
    json secondOutput;
    json brightOutput;
    int returnCode = 0;
    if (!CallFindCross(firstPanel, roi, config, firstOutput, returnCode)
        || !CallFindCross(secondPanel, roi, config, secondOutput, returnCode)
        || !CallFindCross(brightPattern, roi, config, brightOutput, returnCode)
        || firstOutput["result"].size() != 1
        || secondOutput["result"].size() != 1
        || brightOutput["result"].size() != 1) {
        std::cerr << "FindCross polarity/panel-independence call failed" << std::endl;
        return false;
    }

    const json& firstDiagnostics = firstOutput["diagnostics"];
    const json& secondDiagnostics = secondOutput["diagnostics"];
    const json& brightDiagnostics = brightOutput["diagnostics"];
    const json& defaultOptics = firstDiagnostics["EffectiveOptics"];
    const json& defaultStandardCenter = defaultOptics["StandardCenter"];
    auto MatchesPattern = [&](const json& output, const char* polarity) {
        const json& diagnostics = output["diagnostics"];
        const double x = diagnostics["CenterSubpixel"].value("x", -1.0);
        const double y = diagnostics["CenterSubpixel"].value("y", -1.0);
        const double rotation = output["result"][0].value("rotationAngle", 999.0);
        return std::hypot(x - expectedCenter.x, y - expectedCenter.y) <= 3.0
            && std::abs(rotation - expectedRotation) <= 0.6
            && diagnostics.value("Confidence", 0.0) > 0.50
            && diagnostics.value("PatternPolarity", std::string()) == polarity;
    };
    const bool stable = MatchesPattern(firstOutput, "Dark")
        && MatchesPattern(secondOutput, "Dark")
        && MatchesPattern(brightOutput, "Bright")
        && std::hypot(
            firstDiagnostics["CenterSubpixel"].value("x", -1.0)
                - secondDiagnostics["CenterSubpixel"].value("x", -1.0),
            firstDiagnostics["CenterSubpixel"].value("y", -1.0)
                - secondDiagnostics["CenterSubpixel"].value("y", -1.0)) <= 2.0;
    const bool defaultsMatch = firstDiagnostics.value(
        "RequestedRotationMethod", std::string()) == "AllEdges"
        && firstDiagnostics.value("RotationMethod", std::string()) == "RobustTwoAxis"
        && defaultOptics.value("StandardCenterSource", std::string()) == "ImageCenterDefault"
        && std::abs(defaultStandardCenter.value("x", -1.0) - imageSize.width * 0.5) <= 1e-9
        && std::abs(defaultStandardCenter.value("y", -1.0) - imageSize.height * 0.5) <= 1e-9;
    if (!stable || !defaultsMatch) {
        std::cerr << "FindCross polarity/panel-independence regression mismatch\nfirst="
            << firstOutput.dump(2) << "\nsecond=" << secondOutput.dump(2)
            << "\nbright=" << brightOutput.dump(2) << std::endl;
        return false;
    }

    json strictContrastConfig = config;
    strictContrastConfig["MinPatternContrast"] = 0.20;
    json strictContrastOutput;
    if (!CallFindCross(firstPanel, roi, strictContrastConfig, strictContrastOutput, returnCode)
        || strictContrastOutput.value("Success", true)
        || !strictContrastOutput["result"].empty()
        || strictContrastOutput["diagnostics"].value("FailureReason", std::string())
            != "LowPatternContrast") {
        std::cerr << "MinPatternContrast must be a hard full-resolution limit: "
            << strictContrastOutput.dump(2) << std::endl;
        return false;
    }
    return true;
}

bool RunValidCandidateSurvivesPartialClutterTest()
{
    cv::Mat image(900, 1200, CV_16UC1, cv::Scalar(40000));
    const cv::Point validCenter(300, 300);
    cv::line(image, cv::Point(180, 300), cv::Point(420, 300), cv::Scalar(35000), 7, cv::LINE_AA);
    cv::line(image, cv::Point(300, 190), cv::Point(300, 410), cv::Scalar(35000), 7, cv::LINE_AA);

    // This higher-contrast feature is intentionally invalid: its lower arm is
    // too short for MinArmCoverage. It must not outrank the complete Pattern.
    cv::line(image, cv::Point(730, 500), cv::Point(970, 500), cv::Scalar(5000), 7, cv::LINE_AA);
    cv::line(image, cv::Point(850, 390), cv::Point(850, 538), cv::Scalar(5000), 7, cv::LINE_AA);
    cv::GaussianBlur(image, image, cv::Size(5, 5), 1.0);

    const json config = {
        { "PatternPolarity", "Dark" },
        { "ExpectedAngleDegrees", 0.0 },
        { "AngleToleranceDegrees", 5.0 },
        { "MinArmLengthPixels", 60 },
        { "MinArmCoverage", 0.90 },
        { "MinConfidence", 0.20 },
        { "MaxProcessingSize", 1200 }
    };
    json output;
    int returnCode = 0;
    if (!CallFindCross(image, RoiRect{ 0, 0, 0, 0 }, config, output, returnCode)
        || !output.value("Success", false)
        || output["result"].size() != 1) {
        std::cerr << "Invalid partial clutter hid the valid Pattern: "
            << output.dump(2) << std::endl;
        return false;
    }
    const json& center = output["diagnostics"]["CenterSubpixel"];
    if (std::hypot(
        center.value("x", -1.0) - validCenter.x,
        center.value("y", -1.0) - validCenter.y) > 2.0) {
        std::cerr << "Pattern clutter regression selected the wrong candidate: "
            << output.dump(2) << std::endl;
        return false;
    }
    return true;
}

bool RunNonzeroDistortionPatternTest()
{
    constexpr double fx = 800.0;
    constexpr double fy = 800.0;
    constexpr double cx = 600.0;
    constexpr double cy = 450.0;
    constexpr double k1 = -0.15;
    constexpr double k2 = 0.03;
    constexpr double p1 = 0.001;
    constexpr double p2 = -0.0005;
    constexpr double k3 = 0.0;
    const cv::Point2d expectedCenter(950.0, 700.0);
    constexpr double expectedAngle = 10.0;

    cv::Mat image(900, 1200, CV_16UC1, cv::Scalar(5000));
    const double radians = expectedAngle * CV_PI / 180.0;
    const std::array<cv::Point2d, 2> axes{
        cv::Point2d(std::cos(radians), std::sin(radians)),
        cv::Point2d(-std::sin(radians), std::cos(radians))
    };
    const std::array<double, 2> halfLengths{ 130.0, 105.0 };
    for (size_t axis = 0; axis < axes.size(); ++axis) {
        std::vector<cv::Point> polyline;
        for (int sample = -130; sample <= 130; ++sample) {
            const double distance = halfLengths[axis] * sample / 130.0;
            const cv::Point2d corrected = expectedCenter + axes[axis] * distance;
            const cv::Point2d raw = DistortTestPoint(
                corrected, fx, fy, cx, cy, k1, k2, p1, p2, k3);
            polyline.emplace_back(cvRound(raw.x), cvRound(raw.y));
        }
        cv::polylines(image, std::vector<std::vector<cv::Point>>{ polyline },
            false, cv::Scalar(50000), 9, cv::LINE_AA);
    }
    cv::GaussianBlur(image, image, cv::Size(5, 5), 1.0);

    json config = {
        { "PatternPolarity", "Bright" },
        { "ExpectedAngleDegrees", expectedAngle },
        { "AngleToleranceDegrees", 12.0 },
        { "MinArmLengthPixels", 60 },
        { "MinConfidence", 0.20 },
        { "MaxProcessingSize", 1200 },
        { "opticsParams", {
            { "stdCenter", { { "x", cx }, { "y", cy } } },
            { "focusLength", 3.2 },
            { "sensorPixSize", 4.0 },
            { "distortion", {
                { "Enabled", true },
                { "K1", k1 }, { "K2", k2 }, { "P1", p1 }, { "P2", p2 }, { "K3", k3 },
                { "Fx", fx }, { "Fy", fy }, { "Cx", cx }, { "Cy", cy }
            } }
        } }
    };

    const RoiRect roi{ 650, 420, 500, 430 };
    json output;
    int returnCode = 0;
    if (!CallFindCross(image, roi, config, output, returnCode)
        || !output.value("Success", false)
        || output["result"].size() != 1) {
        std::cerr << "Nonzero distortion Pattern correction failed: "
            << output.dump(2) << std::endl;
        return false;
    }
    const json& diagnostics = output["diagnostics"];
    const json& center = diagnostics["CenterSubpixel"];
    const json& rawCenter = diagnostics["RawGeometricCenter"];
    const cv::Point2d expectedRawCenter = DistortTestPoint(
        expectedCenter, fx, fy, cx, cy, k1, k2, p1, p2, k3);
    const bool correctedMatches = std::hypot(
        center.value("x", -1.0) - expectedCenter.x,
        center.value("y", -1.0) - expectedCenter.y) <= 2.5
        && std::abs(output["result"][0].value("rotationAngle", 999.0) - expectedAngle) <= 0.8;
    const bool rawMatches = std::hypot(
        rawCenter.value("x", -1.0) - expectedRawCenter.x,
        rawCenter.value("y", -1.0) - expectedRawCenter.y) <= 3.0
        && std::hypot(
            rawCenter.value("x", -1.0) - center.value("x", -1.0),
            rawCenter.value("y", -1.0) - center.value("y", -1.0)) > 10.0;
    if (!correctedMatches || !rawMatches
        || !diagnostics.value("DistortionApplied", false)
        || diagnostics["EffectiveOptics"]["Distortion"].value(
            "IntrinsicsSource", std::string()) != "Calibration"
        || !HasGlobalPointsInside(
            diagnostics, "RawArmEndpoints", roi)) {
        std::cerr << "Distortion coordinate contract mismatch: "
            << output.dump(2) << std::endl;
        return false;
    }

    config["opticsParams"]["distortion"]["K1"] = 1e20;
    json rejected;
    if (!CallFindCross(image, roi, config, rejected, returnCode)
        || rejected.value("Success", true)
        || !rejected["result"].empty()
        || rejected["diagnostics"].value("FailureReason", std::string())
            != "InvalidDistortionGeometry") {
        std::cerr << "Degenerate distortion must be rejected: "
            << rejected.dump(2) << std::endl;
        return false;
    }
    return true;
}

bool RunIndustrialPatternPerturbationMatrixTest()
{
    const cv::Size imageSize(1200, 900);
    const cv::Point2d expectedCenter(645.0, 446.0);
    constexpr double expectedRotation = -3.0;
    const cv::RotatedRect panel(
        cv::Point2f(
            static_cast<float>(expectedCenter.x),
            static_cast<float>(expectedCenter.y)),
        cv::Size2f(650.0f, 400.0f),
        7.0f);
    const cv::Mat clean = MakeRotatedPanel(
        imageSize,
        panel,
        cv::Point2f(
            static_cast<float>(expectedCenter.x),
            static_cast<float>(expectedCenter.y)),
        expectedRotation);

    // Production exposes the Pattern result, optical calibration and ROI. These
    // tests intentionally use the native defaults for all detector tuning so a
    // future implementation cannot require per-defect threshold recipes.
    const json config = json::object();
    json cleanOutput;
    int returnCode = 0;
    PatternMeasurement cleanMeasurement;
    if (!CallFindCross(clean, RoiRect{ 0, 0, 0, 0 }, config, cleanOutput, returnCode)
        || !ReadPatternMeasurement(cleanOutput, cleanMeasurement)
        || cv::norm(cleanMeasurement.center - expectedCenter) > 1.0
        || PatternAxisAngleError(cleanMeasurement.rotationDegrees, expectedRotation) > 0.5
        || cleanMeasurement.confidence < 0.50) {
        std::cerr << "FindCross industrial clean baseline failed: "
            << cleanOutput.dump(2) << std::endl;
        return false;
    }

    std::vector<std::pair<std::string, cv::Mat>> stableCases;

    cv::Mat stained = clean.clone();
    AddDeterministicStains(stained);
    stableCases.emplace_back("stains-away-from-pattern", std::move(stained));

    cv::Mat leaked = clean.clone();
    AddEdgeLightLeak(leaked);
    stableCases.emplace_back("edge-light-leak", std::move(leaked));

    cv::Mat gradient = clean.clone();
    ApplyHorizontalIlluminationGradient(gradient, 0.58, 1.0);
    stableCases.emplace_back("panel-illumination-gradient", std::move(gradient));

    cv::Mat anomalousBrightSpot = clean.clone();
    cv::circle(
        anomalousBrightSpot,
        cv::Point(850, 600),
        24,
        cv::Scalar(65535),
        cv::FILLED,
        cv::LINE_AA);
    stableCases.emplace_back("isolated-anomalous-bright-spot", std::move(anomalousBrightSpot));

    cv::Mat dim;
    clean.convertTo(dim, clean.type(), 0.20);
    stableCases.emplace_back("globally-dim-pattern", std::move(dim));

    cv::Mat badPixels = clean.clone();
    AddDeterministicBadPixels(badPixels);
    stableCases.emplace_back("dead-and-hot-pixels", std::move(badPixels));

    cv::Mat partiallyOccluded = clean.clone();
    cv::rectangle(
        partiallyOccluded,
        cv::Rect(675, 423, 13, 47),
        cv::Scalar(52000),
        cv::FILLED,
        cv::LINE_AA);
    stableCases.emplace_back("short-local-arm-occlusion", std::move(partiallyOccluded));

    cv::Mat combined = clean.clone();
    ApplyHorizontalIlluminationGradient(combined, 0.72, 1.0);
    AddDeterministicStains(combined);
    AddEdgeLightLeak(combined);
    cv::circle(
        combined,
        cv::Point(850, 600),
        18,
        cv::Scalar(65535),
        cv::FILLED,
        cv::LINE_AA);
    AddDeterministicBadPixels(combined, 0.0005);
    combined.convertTo(combined, combined.type(), 0.55);
    stableCases.emplace_back("combined-production-artifacts", std::move(combined));

    for (const auto& [name, image] : stableCases) {
        json output;
        PatternMeasurement measurement;
        if (!CallFindCross(image, RoiRect{ 0, 0, 0, 0 }, config, output, returnCode)
            || !ReadPatternMeasurement(output, measurement)) {
            std::cerr << "FindCross industrial stable case rejected: " << name
                << " output=" << output.dump(2) << std::endl;
            return false;
        }

        const double centerError = cv::norm(measurement.center - expectedCenter);
        const double rotationError = PatternAxisAngleError(
            measurement.rotationDegrees, expectedRotation);
        const double centerDrift = cv::norm(measurement.center - cleanMeasurement.center);
        const double rotationDrift = PatternAxisAngleError(
            measurement.rotationDegrees, cleanMeasurement.rotationDegrees);
        if (centerError > 1.0
            || rotationError > 0.5
            || centerDrift > 0.75
            || rotationDrift > 0.35
            || measurement.confidence < 0.50) {
            std::cerr << "FindCross industrial stability regression: " << name
                << " centerError=" << centerError
                << " rotationError=" << rotationError
                << " centerDrift=" << centerDrift
                << " rotationDrift=" << rotationDrift
                << " confidence=" << measurement.confidence
                << " output=" << output.dump(2) << std::endl;
            return false;
        }
        std::cout << "  industrial stable " << name
            << ": centerError=" << centerError
            << " rotationError=" << rotationError
            << " confidence=" << measurement.confidence << std::endl;
    }

    std::vector<std::pair<std::string, cv::Mat>> rejectedCases;
    rejectedCases.emplace_back(
        "pattern-missing",
        MakeRotatedPanel(
            imageSize,
            panel,
            cv::Point2f(
                static_cast<float>(expectedCenter.x),
                static_cast<float>(expectedCenter.y)),
            expectedRotation,
            false));

    cv::Mat brokenArm = clean.clone();
    const double angleRadians = expectedRotation * CV_PI / 180.0;
    const cv::Point2d positiveAxis(std::cos(angleRadians), std::sin(angleRadians));
    cv::line(
        brokenArm,
        expectedCenter + positiveAxis * 4.0,
        expectedCenter + positiveAxis * 145.0,
        cv::Scalar(52000),
        25,
        cv::LINE_AA);
    rejectedCases.emplace_back("one-arm-missing", std::move(brokenArm));

    rejectedCases.emplace_back(
        "all-black-screen",
        cv::Mat(imageSize, CV_16UC1, cv::Scalar(0)));

    cv::Mat ambiguousPattern = MakeRotatedPanel(
        imageSize,
        panel,
        cv::Point2f(
            static_cast<float>(expectedCenter.x),
            static_cast<float>(expectedCenter.y)),
        expectedRotation,
        false);
    DrawPatternCross(
        ambiguousPattern,
        cv::Point2f(
            static_cast<float>(expectedCenter.x),
            static_cast<float>(expectedCenter.y)),
        expectedRotation,
        43000);
    // The second Pattern has 70% of the primary dark contrast against the
    // 52000-count panel. It is weaker in the coarse ranking but remains a
    // complete, full-resolution Pattern and therefore must fail uniqueness.
    DrawPatternCross(
        ambiguousPattern,
        cv::Point2f(500.0f, 350.0f),
        expectedRotation,
        45700);
    cv::GaussianBlur(
        ambiguousPattern, ambiguousPattern, cv::Size(5, 5), 1.1, 1.1,
        cv::BORDER_REPLICATE);
    json ambiguousOutput;
    if (!CallFindCross(
            ambiguousPattern, RoiRect{ 0, 0, 0, 0 }, config,
            ambiguousOutput, returnCode)
        || ambiguousOutput.value("Success", true)
        || !ambiguousOutput.contains("result")
        || !ambiguousOutput["result"].is_array()
        || !ambiguousOutput["result"].empty()
        || ambiguousOutput.value("FailureReason", std::string())
            != "AmbiguousPattern"
        || ambiguousOutput["diagnostics"].value(
            "FailureReason", std::string()) != "AmbiguousPattern") {
        std::cerr << "FindCross weaker second Pattern must fail as ambiguous: "
            << ambiguousOutput.dump(2) << std::endl;
        return false;
    }
    std::cout << "  industrial rejected weaker second-complete-pattern: AmbiguousPattern"
        << std::endl;

    cv::Mat leakWithoutPattern = MakeRotatedPanel(
        imageSize,
        panel,
        cv::Point2f(
            static_cast<float>(expectedCenter.x),
            static_cast<float>(expectedCenter.y)),
        expectedRotation,
        false);
    AddEdgeLightLeak(leakWithoutPattern);
    rejectedCases.emplace_back(
        "light-leak-without-pattern",
        std::move(leakWithoutPattern));

    for (const auto& [name, image] : rejectedCases) {
        json output;
        if (!CallFindCross(image, RoiRect{ 0, 0, 0, 0 }, config, output, returnCode)
            || output.value("Success", true)
            || !output.contains("result") || !output["result"].is_array()
            || !output["result"].empty()
            || output.value("FailureReason", std::string()).empty()) {
            std::cerr << "FindCross unsafe failure acceptance: " << name
                << " output=" << output.dump(2) << std::endl;
            return false;
        }
        std::cout << "  industrial rejected " << name
            << ": " << output.value("FailureReason", std::string()) << std::endl;
    }
    return true;
}

bool RunSevereBlurQualityGateTest()
{
    const cv::Size imageSize(1200, 900);
    const cv::Point2d expectedCenter(645.0, 446.0);
    constexpr double expectedRotation = -3.0;
    const cv::Mat clean = MakeRotatedPanel(
        imageSize,
        cv::RotatedRect(
            cv::Point2f(
                static_cast<float>(expectedCenter.x),
                static_cast<float>(expectedCenter.y)),
            cv::Size2f(650.0f, 400.0f),
            7.0f),
        cv::Point2f(
            static_cast<float>(expectedCenter.x),
            static_cast<float>(expectedCenter.y)),
        expectedRotation);

    // minLineLength=120 is the deployed legacy payload and maps to a 60-pixel
    // minimum arm. No sharpness threshold is exposed to the caller.
    const json config = { { "minLineLength", 120 } };
    cv::Mat moderateBlur;
    cv::GaussianBlur(clean, moderateBlur, cv::Size(), 5.0, 5.0, cv::BORDER_REPLICATE);
    json moderateOutput;
    int returnCode = 0;
    PatternMeasurement moderateMeasurement;
    if (!CallFindCross(
            moderateBlur, RoiRect{ 0, 0, 0, 0 }, config, moderateOutput, returnCode)
        || !ReadPatternMeasurement(moderateOutput, moderateMeasurement)
        || cv::norm(moderateMeasurement.center - expectedCenter) > 1.0
        || PatternAxisAngleError(
            moderateMeasurement.rotationDegrees, expectedRotation) > 0.5
        || moderateMeasurement.confidence < 0.50) {
        std::cerr << "FindCross moderate blur should remain measurable: "
            << moderateOutput.dump(2) << std::endl;
        return false;
    }

    for (double sigma : { 6.0, 8.0 }) {
        cv::Mat severeBlur;
        cv::GaussianBlur(clean, severeBlur, cv::Size(), sigma, sigma, cv::BORDER_REPLICATE);
        json output;
        if (!CallFindCross(
                severeBlur, RoiRect{ 0, 0, 0, 0 }, config, output, returnCode)
            || output.value("Success", true)
            || !output.contains("result") || !output["result"].is_array()
            || !output["result"].empty()
            || output.value("FailureReason", std::string()) != "PoorPatternSharpness"
            || !output.contains("diagnostics")
            || !output["diagnostics"].contains("ArmQuality")
            || !output["diagnostics"]["ArmQuality"].is_array()
            || output["diagnostics"]["ArmQuality"].size() != 4) {
            std::cerr << "FindCross severe blur was not safely rejected, sigma="
                << sigma << " output=" << output.dump(2) << std::endl;
            return false;
        }

        const json& diagnostics = output["diagnostics"];
        double maximumFitRms = 0.0;
        for (const json& arm : diagnostics["ArmQuality"]) {
            maximumFitRms = std::max(maximumFitRms, arm.value("FitRms", 0.0));
        }
        const double patternContrast = diagnostics.value("PatternContrast", 0.0);
        if (maximumFitRms <= 0.45
            || patternContrast <= 0.0
            || patternContrast >= 0.03) {
            std::cerr << "FindCross severe blur rejection lacks quality evidence, sigma="
                << sigma
                << " fitRms=" << maximumFitRms
                << " contrast=" << patternContrast
                << " output=" << output.dump(2) << std::endl;
            return false;
        }
        std::cout << "  severe blur rejected sigma=" << sigma
            << ": fitRms=" << maximumFitRms
            << " contrast=" << patternContrast << std::endl;
    }
    return true;
}

bool RunMissingPatternAndOuterPanelAssistTest()
{
    const cv::Size imageSize(1200, 900);
    const cv::Point2f panelCenter(645.0f, 446.0f);
    cv::Mat noPattern = MakeRotatedPanel(
        imageSize,
        cv::RotatedRect(panelCenter, cv::Size2f(650.0f, 400.0f), 6.0f),
        panelCenter,
        0.0,
        false);
    for (int y = 0; y < noPattern.rows; ++y) {
        uint16_t* row = noPattern.ptr<uint16_t>(y);
        for (int x = 0; x < noPattern.cols; ++x) {
            const double scale = 0.62 + 0.38 * x / (noPattern.cols - 1.0);
            row[x] = cv::saturate_cast<uint16_t>(row[x] * scale);
        }
    }
    cv::ellipse(
        noPattern,
        cv::Point(610, 440),
        cv::Size(145, 105),
        12.0, 0.0, 360.0,
        cv::Scalar(500), cv::FILLED, cv::LINE_AA);

    const RoiRect roi{ 170, 90, 930, 730 };
    json rejected;
    int returnCode = 0;
    if (!CallFindCross(noPattern, roi, json::object(), rejected, returnCode)
        || rejected.value("Success", true)
        || !rejected["result"].empty()
        || rejected["diagnostics"].value("FailureReason", std::string()).empty()) {
        std::cerr << "Missing Pattern must be rejected: " << rejected.dump(2) << std::endl;
        return false;
    }

    const json outerPanelConfig = {
        { "DetectionMode", "OuterPanel" },
        { "MinConfidence", 0.20 },
        { "MaxProcessingSize", 1200 }
    };
    json outerPanel;
    if (!CallFindCross(noPattern, roi, outerPanelConfig, outerPanel, returnCode)
        || !outerPanel.value("Success", false)
        || outerPanel["result"].size() != 1
        || outerPanel["diagnostics"].value("Algorithm", std::string())
            != "OuterPanelAssistV2") {
        std::cerr << "Explicit OuterPanel assist failed: " << outerPanel.dump(2) << std::endl;
        return false;
    }
    return true;
}

bool RunFailureContractTest()
{
    cv::Mat noSignal(300, 400, CV_8UC1, cv::Scalar(17));
    char* result = reinterpret_cast<char*>(1);
    const int malformedCode = M_FindCrossLocal(
        MakeHImage(noSignal), RoiRect{ 0, 0, 0, 0 }, "{", &result);
    if (malformedCode != -4 || result != nullptr) {
        std::cerr << "FindCross malformed JSON contract failed" << std::endl;
        return false;
    }
    const int malformedErrorLength = M_FindCrossLocalGetLastError(nullptr, 0);
    std::array<char, 2> undersizedBuffer{ 'x', '\0' };
    if (malformedErrorLength <= 1
        || M_FindCrossLocalGetLastError(undersizedBuffer.data(), 1) != malformedErrorLength
        || undersizedBuffer[0] != 'x') {
        std::cerr << "FindCross malformed JSON last-error sizing contract failed" << std::endl;
        return false;
    }
    std::vector<char> malformedError(static_cast<size_t>(malformedErrorLength));
    if (M_FindCrossLocalGetLastError(
        malformedError.data(), static_cast<std::uint32_t>(malformedError.size()))
        != malformedErrorLength
        || std::string(malformedError.data()).find("parse error") == std::string::npos) {
        std::cerr << "FindCross malformed JSON last-error detail failed" << std::endl;
        return false;
    }

    result = reinterpret_cast<char*>(1);
    const int invalidConfigCode = M_FindCrossLocal(
        MakeHImage(noSignal), RoiRect{ 0, 0, 0, 0 },
        R"({"opticsParams":{"focusLength":0}})", &result);
    if (invalidConfigCode != -4 || result != nullptr) {
        std::cerr << "FindCross invalid config contract failed" << std::endl;
        return false;
    }
    const int configErrorLength = M_FindCrossLocalGetLastError(nullptr, 0);
    if (configErrorLength <= 1) {
        std::cerr << "FindCross invalid config last-error length failed" << std::endl;
        return false;
    }
    std::vector<char> configError(static_cast<size_t>(configErrorLength));
    if (M_FindCrossLocalGetLastError(
            configError.data(), static_cast<std::uint32_t>(configError.size()))
            != configErrorLength
        || std::string(configError.data())
            != "opticsParams.focusLength must be finite and greater than zero") {
        std::cerr << "FindCross invalid config last-error detail failed" << std::endl;
        return false;
    }

    result = reinterpret_cast<char*>(1);
    const int missingDistortionIntrinsicsCode = M_FindCrossLocal(
        MakeHImage(noSignal), RoiRect{ 0, 0, 0, 0 },
        R"({"opticsParams":{"distortion":{"Enabled":true,"K1":-0.1}}})", &result);
    if (missingDistortionIntrinsicsCode != -4 || result != nullptr) {
        std::cerr << "FindCross enabled distortion without calibration must fail" << std::endl;
        return false;
    }
    const int distortionErrorLength = M_FindCrossLocalGetLastError(nullptr, 0);
    std::vector<char> distortionError(static_cast<size_t>(distortionErrorLength));
    if (distortionErrorLength <= 1
        || M_FindCrossLocalGetLastError(
            distortionError.data(), static_cast<std::uint32_t>(distortionError.size()))
            != distortionErrorLength
        || std::string(distortionError.data()).find("requires calibrated Fx/Fy/Cx/Cy")
            == std::string::npos) {
        std::cerr << "FindCross missing distortion calibration error detail failed" << std::endl;
        return false;
    }

    result = reinterpret_cast<char*>(1);
    const int invalidRoiCode = M_FindCrossLocal(
        MakeHImage(noSignal), RoiRect{ 390, 0, 20, 20 }, "{}", &result);
    if (invalidRoiCode != -1 || result != nullptr) {
        std::cerr << "FindCross invalid ROI contract failed" << std::endl;
        return false;
    }

    json rejected;
    int returnCode = 0;
    if (!CallFindCross(noSignal, RoiRect{ 0, 0, 0, 0 }, json::object(), rejected, returnCode)
        || !rejected["result"].empty()
        || rejected["diagnostics"].value("Success", true)
        || rejected["diagnostics"].value("FailureReason", std::string()).empty()) {
        std::cerr << "FindCross algorithm rejection contract failed: " << rejected.dump() << std::endl;
        return false;
    }
    if (M_FindCrossLocalGetLastError(nullptr, 0) != 1) {
        std::cerr << "FindCross last-error was not cleared on the next call" << std::endl;
        return false;
    }
    return true;
}

} // namespace

bool RunFindCrossLocalSyntheticTests()
{
    std::cout << "M_FindCrossLocal synthetic regression..." << std::endl;
    if (!RunGeometryAndLegacyPayloadTest()
        || !RunPatternPolarityAndPanelIndependenceTest()
        || !RunValidCandidateSurvivesPartialClutterTest()
        || !RunNonzeroDistortionPatternTest()
        || !RunIndustrialPatternPerturbationMatrixTest()
        || !RunSevereBlurQualityGateTest()
        || !RunMissingPatternAndOuterPanelAssistTest()
        || !RunFailureContractTest()) {
        return false;
    }
    std::cout << "M_FindCrossLocal synthetic regression passed" << std::endl;
    return true;
}

int RunFindCrossLocalCvRawCommand(int argc, char* argv[])
{
    if (argc != 3 && argc != 7 && argc != 13) {
        std::cerr << "Usage: --find-cross-cvraw <file.cvraw> "
            "[x y width height "
            "[expectedCenterX expectedCenterY expectedRotation "
            "centerTolerance rotationTolerance minConfidence]]" << std::endl;
        return 2;
    }

    CVCIEFile file;
    const std::filesystem::path path = std::filesystem::u8path(argv[2]);
    if (!ReadCIEFile(path.string(), file) || file.Data.empty()
        || file.Rows <= 0 || file.Cols <= 0 || file.Channels <= 0) {
        std::cerr << "Unable to read cvraw: " << path << std::endl;
        return 2;
    }

    RoiRect roi{ 0, 0, 0, 0 };
    bool hasExpected = false;
    double expectedCenterX = 0.0;
    double expectedCenterY = 0.0;
    double expectedRotation = 0.0;
    double centerTolerance = 0.0;
    double rotationTolerance = 0.0;
    double minConfidence = 0.0;
    try {
        if (argc >= 7) {
            roi = RoiRect{
                std::stoi(argv[3]), std::stoi(argv[4]),
                std::stoi(argv[5]), std::stoi(argv[6])
            };
        }
        if (argc == 13) {
            hasExpected = true;
            expectedCenterX = std::stod(argv[7]);
            expectedCenterY = std::stod(argv[8]);
            expectedRotation = std::stod(argv[9]);
            centerTolerance = std::stod(argv[10]);
            rotationTolerance = std::stod(argv[11]);
            minConfidence = std::stod(argv[12]);
            if (centerTolerance < 0.0 || rotationTolerance < 0.0
                || minConfidence < 0.0 || minConfidence > 1.0) {
                throw std::out_of_range("invalid expectation bounds");
            }
        }
    }
    catch (const std::exception&) {
        std::cerr << "ROI/expectation values are invalid" << std::endl;
        return 2;
    }

    HImage image{};
    image.rows = file.Rows;
    image.cols = file.Cols;
    image.channels = file.Channels;
    image.depth = file.Bpp;
    image.stride = file.Cols * file.Channels * std::max(1, file.Bpp / 8);
    image.pData = file.Data.data();

    const json config = {
        { "name", "Point_1" },
        { "MinConfidence", 0.20 },
        { "MaxProcessingSize", 1600 },
        { "opticsParams", {
            { "focusLength", 25.4 },
            { "sensorPixSize", 3.76 }
        } }
    };
    const std::string configText = config.dump();
    char* result = nullptr;
    const auto started = std::chrono::steady_clock::now();
    const int returnCode = M_FindCrossLocal(image, roi, configText.c_str(), &result);
    const double elapsedMs = std::chrono::duration<double, std::milli>(
        std::chrono::steady_clock::now() - started).count();
    std::cout << "ReturnCode=" << returnCode << " ElapsedMs=" << elapsedMs << std::endl;
    if (returnCode <= 0 || result == nullptr) {
        return 1;
    }

    std::cout << result << std::endl;
    const json output = json::parse(result, nullptr, false);
    FreeResult(result);
    if (output.is_discarded()
        || !output.contains("result")
        || !output["result"].is_array()
        || !output.contains("diagnostics")) {
        std::cerr << "FindCross cvraw result contract failed" << std::endl;
        return 1;
    }

    const bool success = output.value("Success", false);
    if (!success) {
        const std::string reason = output.value("FailureReason", std::string());
        if (!output["result"].empty() || reason.empty() || hasExpected) {
            std::cerr << "FindCross cvraw rejection contract failed" << std::endl;
            return 1;
        }
        std::cout << "Pattern rejected as expected for an unlabelled debug sample: "
            << reason << std::endl;
        return 0;
    }
    if (output["result"].size() != 1) {
        std::cerr << "FindCross cvraw success must contain one result" << std::endl;
        return 1;
    }

    const json& item = output["result"][0];
    const json& diagnostics = output["diagnostics"];
    const double centerX = diagnostics["CenterSubpixel"].value("x", NAN);
    const double centerY = diagnostics["CenterSubpixel"].value("y", NAN);
    const double rotation = item.value("rotationAngle", NAN);
    const double confidence = diagnostics.value("Confidence", NAN);
    const double roiX = roi.width == 0 ? 0.0 : roi.x;
    const double roiY = roi.height == 0 ? 0.0 : roi.y;
    const double roiWidth = roi.width == 0 ? file.Cols : roi.width;
    const double roiHeight = roi.height == 0 ? file.Rows : roi.height;
    const bool genericRangeValid = std::isfinite(centerX) && std::isfinite(centerY)
        && std::isfinite(rotation) && std::isfinite(confidence)
        && centerX >= roiX && centerX <= roiX + roiWidth
        && centerY >= roiY && centerY <= roiY + roiHeight
        && std::abs(rotation) <= 90.0
        && confidence >= 0.0 && confidence <= 1.0;
    if (!genericRangeValid) {
        std::cerr << "FindCross cvraw output is outside generic valid ranges" << std::endl;
        return 1;
    }

    if (hasExpected) {
        const double centerError = std::hypot(
            centerX - expectedCenterX,
            centerY - expectedCenterY);
        const double rotationError = std::abs(rotation - expectedRotation);
        if (centerError > centerTolerance
            || rotationError > rotationTolerance
            || confidence < minConfidence) {
            std::cerr << "FindCross cvraw regression failed: centerError=" << centerError
                << " rotationError=" << rotationError
                << " confidence=" << confidence << std::endl;
            return 1;
        }
        std::cout << "Expected regression passed: centerError=" << centerError
            << " rotationError=" << rotationError
            << " confidence=" << confidence << std::endl;
    }
    return 0;
}
