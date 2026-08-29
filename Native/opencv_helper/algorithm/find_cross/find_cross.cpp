#ifndef NOMINMAX
#define NOMINMAX
#endif

#include "find_cross.h"

#include "../../../include/opencv_media_export.h"
#include "../../native_log.h"

#include <algorithm>
#include <array>
#include <cctype>
#include <cmath>
#include <combaseapi.h>
#include <cstring>
#include <exception>
#include <limits>
#include <string>
#include <string_view>
#include <vector>

#include <opencv2/calib3d.hpp>
#include <opencv2/imgproc.hpp>

using json = nlohmann::json;

namespace cvnative::find_cross
{
namespace
{

constexpr double kRadiansToDegrees = 180.0 / CV_PI;
constexpr double kMicrometersPerMillimeter = 1000.0;

const json* FindMember(const json& value, std::initializer_list<const char*> names)
{
    if (!value.is_object()) {
        return nullptr;
    }
    for (const char* name : names) {
        const auto iterator = value.find(name);
        if (iterator != value.end()) {
            return &*iterator;
        }
    }
    return nullptr;
}

bool ReadFiniteNumber(
    const json& object,
    std::initializer_list<const char*> names,
    double& output,
    std::string& error,
    const char* displayName,
    bool strictlyPositive = false)
{
    const json* value = FindMember(object, names);
    if (value == nullptr) {
        return true;
    }
    if (!value->is_number()) {
        error = std::string(displayName) + " must be numeric";
        return false;
    }
    const double parsed = value->get<double>();
    if (!std::isfinite(parsed) || (strictlyPositive && parsed <= 0.0)) {
        error = std::string(displayName) + (strictlyPositive
            ? " must be finite and greater than zero"
            : " must be finite");
        return false;
    }
    output = parsed;
    return true;
}

std::string ToLower(std::string value)
{
    std::transform(value.begin(), value.end(), value.begin(), [](unsigned char character) {
        return static_cast<char>(std::tolower(character));
    });
    return value;
}

void AddUnique(std::vector<std::string>& values, const std::string& value)
{
    if (std::find(values.begin(), values.end(), value) == values.end()) {
        values.push_back(value);
    }
}

void RecordIgnoredMember(
    const json& object,
    std::initializer_list<const char*> names,
    std::vector<std::string>& ignoredParameters)
{
    if (!object.is_object()) {
        return;
    }
    for (const char* name : names) {
        if (object.contains(name)) {
            AddUnique(ignoredParameters, name);
        }
    }
}

const json* RecordIgnoredObject(
    const json& object,
    std::initializer_list<const char*> names,
    const char* canonicalName,
    std::vector<std::string>& ignoredParameters)
{
    const json* nested = FindMember(object, names);
    if (nested == nullptr) {
        return nullptr;
    }
    if (!nested->is_object()) {
        AddUnique(ignoredParameters, canonicalName);
        return nested;
    }
    for (const auto& [key, unused] : nested->items()) {
        (void)unused;
        AddUnique(ignoredParameters, std::string(canonicalName) + "." + key);
    }
    return nested;
}

void RecordIgnoredLegacyParameters(const json& value, FindCrossConfig& config)
{
    RecordIgnoredMember(value, {
        "caclWay", "CaclWay", "threshold", "Threshold", "blurKernel", "BlurKernel",
        "maxLineGap", "MaxLineGap",
        "findEndPointWay", "FindEndPointWay", "binaryByContours", "BinaryByContours",
        "singleErodeKernel", "SingleErodeKernel",
        "binaryRateInContours", "BinaryRateInContours"
    }, config.ignoredParameters);

    const json* debug = RecordIgnoredObject(
        value, { "debugCfg", "DebugCfg" }, "debugCfg", config.ignoredParameters);
    if (const json* checkLine = FindMember(value, { "CheckLine", "checkLine" })) {
        if (!checkLine->is_object()) {
            AddUnique(config.ignoredParameters, "CheckLine");
        }
        else {
            for (const auto& [key, unused] : checkLine->items()) {
                (void)unused;
                if (ToLower(key) != "floangle") {
                    AddUnique(config.ignoredParameters, std::string("CheckLine.") + key);
                }
            }
        }
    }
    RecordIgnoredObject(
        value, { "mathMaskRect", "MathMaskRect" }, "mathMaskRect", config.ignoredParameters);
    RecordIgnoredObject(
        value, { "erodeAndDiate", "ErodeAndDiate" }, "erodeAndDiate",
        config.ignoredParameters);

    if (const json* optics = FindMember(value, { "opticsParams", "OpticsParams" });
        optics != nullptr && optics->is_object()) {
        for (const char* name : { "objectDistance", "ObjectDistance" }) {
            if (optics->contains(name)) {
                AddUnique(config.ignoredParameters, std::string("opticsParams.") + name);
            }
        }
    }

    if (!config.ignoredParameters.empty()) {
        AddUnique(config.warnings, "LegacyParametersIgnored");
    }
    if (debug != nullptr && debug->is_object()) {
        if (const json* enabled = FindMember(*debug, { "Debug", "debug" });
            enabled != nullptr && enabled->is_boolean() && enabled->get<bool>()) {
            AddUnique(config.warnings, "DebugFilesNotWrittenDiagnosticsEmbedded");
        }
    }
}

double NormalizeAxisAngle(double angleDegrees)
{
    while (angleDegrees >= 90.0) {
        angleDegrees -= 180.0;
    }
    while (angleDegrees < -90.0) {
        angleDegrees += 180.0;
    }
    return angleDegrees;
}

double LineAngleDegrees(const cv::Point2d& from, const cv::Point2d& to)
{
    return NormalizeAxisAngle(std::atan2(to.y - from.y, to.x - from.x) * kRadiansToDegrees);
}

cv::Point2d AverageCorners(const std::array<cv::Point2d, 4>& corners)
{
    cv::Point2d center{};
    for (const cv::Point2d& corner : corners) {
        center += corner;
    }
    return center * 0.25;
}

double Cross(const cv::Point2d& left, const cv::Point2d& right)
{
    return left.x * right.y - left.y * right.x;
}

bool IntersectDiagonals(const std::array<cv::Point2d, 4>& corners, cv::Point2d& center)
{
    const cv::Point2d firstDirection = corners[2] - corners[0];
    const cv::Point2d secondDirection = corners[3] - corners[1];
    const double denominator = Cross(firstDirection, secondDirection);
    const double scale = std::max(
        1.0,
        cv::norm(firstDirection) * cv::norm(secondDirection));
    if (!std::isfinite(denominator) || std::abs(denominator) <= scale * 1e-9) {
        return false;
    }

    const double firstParameter = Cross(corners[1] - corners[0], secondDirection) / denominator;
    center = corners[0] + firstDirection * firstParameter;
    return std::isfinite(center.x) && std::isfinite(center.y);
}

double ComputeAllEdgesRotation(
    const std::array<cv::Point2d, 4>& corners,
    const std::array<luminous::LuminousSideQuality, 4>& sideQuality)
{
    // Convert every side into the horizontal-axis orientation and average in
    // doubled-angle space. This treats a line as an axis, not a directed ray.
    const std::array<double, 4> horizontalAngles{
        LineAngleDegrees(corners[0], corners[1]),
        NormalizeAxisAngle(LineAngleDegrees(corners[1], corners[2]) - 90.0),
        LineAngleDegrees(corners[3], corners[2]),
        NormalizeAxisAngle(LineAngleDegrees(corners[0], corners[3]) - 90.0)
    };

    double cosine = 0.0;
    double sine = 0.0;
    for (size_t index = 0; index < horizontalAngles.size(); ++index) {
        const double confidence = std::clamp(sideQuality[index].confidence, 0.0, 1.0);
        const double weight = std::max(0.05, confidence);
        const double doubledRadians = 2.0 * horizontalAngles[index] / kRadiansToDegrees;
        cosine += weight * std::cos(doubledRadians);
        sine += weight * std::sin(doubledRadians);
    }
    return NormalizeAxisAngle(0.5 * std::atan2(sine, cosine) * kRadiansToDegrees);
}

const char* CenterMethodName(CenterMethod method)
{
    return method == CenterMethod::CornerAverage ? "CornerAverage" : "DiagonalIntersection";
}

const char* RotationMethodName(RotationMethod method)
{
    return method == RotationMethod::AllEdges ? "AllEdges" : "TopEdge";
}

bool ParseDetectionMode(const json& value, FindCrossConfig& config, std::string& error)
{
    if (!value.is_string()) {
        error = "DetectionMode must be a string";
        return false;
    }
    config.requestedDetectionMode = value.get<std::string>();
    const std::string normalized = ToLower(config.requestedDetectionMode);
    if (normalized == "patterncross" || normalized == "patterncrossv1" || normalized == "pattern") {
        config.detectionMode = DetectionMode::PatternCross;
        return true;
    }
    if (normalized == "outerpanel" || normalized == "outerpanelassist"
        || normalized == "robustouteredgesv2") {
        config.detectionMode = DetectionMode::OuterPanelAssist;
        return true;
    }
    error = "DetectionMode must be PatternCross or OuterPanel";
    return false;
}

bool ParsePatternPolarity(const json& value, PatternCrossConfig& config, std::string& error)
{
    if (!value.is_string()) {
        error = "PatternPolarity must be a string";
        return false;
    }
    const std::string normalized = ToLower(value.get<std::string>());
    if (normalized == "auto") config.polarity = PatternPolarity::Auto;
    else if (normalized == "bright") config.polarity = PatternPolarity::Bright;
    else if (normalized == "dark") config.polarity = PatternPolarity::Dark;
    else {
        error = "PatternPolarity must be Auto, Bright, or Dark";
        return false;
    }
    return true;
}

bool ReadBoolean(
    const json& object,
    std::initializer_list<const char*> names,
    bool& output,
    std::string& error,
    const char* displayName)
{
    const json* value = FindMember(object, names);
    if (value == nullptr) return true;
    if (!value->is_boolean()) {
        error = std::string(displayName) + " must be boolean";
        return false;
    }
    output = value->get<bool>();
    return true;
}

bool ParsePatternConfig(const json& value, FindCrossConfig& config, std::string& error)
{
    config.pattern.minConfidence = config.luminous.minConfidence;
    config.pattern.maxProcessingSize = config.luminous.maxProcessingSize;
    if (const json* mode = FindMember(value, { "DetectionMode", "detectionMode" })) {
        if (!ParseDetectionMode(*mode, config, error)) return false;
    }
    if (const json* polarity = FindMember(value, { "PatternPolarity", "patternPolarity" })) {
        if (!ParsePatternPolarity(*polarity, config.pattern, error)) return false;
    }
    if (!ReadFiniteNumber(
        value, { "ExpectedAngleDegrees", "expectedAngleDegrees" },
        config.pattern.expectedAngleDegrees, error, "ExpectedAngleDegrees")
        || !ReadFiniteNumber(
            value, { "AngleToleranceDegrees", "angleToleranceDegrees" },
            config.pattern.angleToleranceDegrees, error, "AngleToleranceDegrees")
        || !ReadFiniteNumber(
            value, { "MinPatternContrast", "minPatternContrast" },
            config.pattern.minContrast, error, "MinPatternContrast")
        || !ReadFiniteNumber(
            value, { "MinArmLengthPixels", "minArmLengthPixels" },
            config.pattern.minArmLengthPixels, error, "MinArmLengthPixels")
        || !ReadFiniteNumber(
            value, { "MinArmCoverage", "minArmCoverage" },
            config.pattern.minArmCoverage, error, "MinArmCoverage")) {
        return false;
    }
    if (FindMember(value, { "AngleToleranceDegrees", "angleToleranceDegrees" }) == nullptr) {
        if (const json* checkLine = FindMember(value, { "CheckLine", "checkLine" });
            checkLine != nullptr && checkLine->is_object()) {
            if (!ReadFiniteNumber(
                *checkLine, { "floAngle", "FloAngle" },
                config.pattern.angleToleranceDegrees, error, "CheckLine.floAngle")) {
                return false;
            }
        }
    }
    if (FindMember(value, { "MinArmLengthPixels", "minArmLengthPixels" }) == nullptr) {
        double legacyLineLength = config.pattern.minArmLengthPixels * 2.0;
        if (!ReadFiniteNumber(
            value, { "minLineLength", "MinLineLength" }, legacyLineLength,
            error, "minLineLength")) {
            return false;
        }
        if (FindMember(value, { "minLineLength", "MinLineLength" }) != nullptr) {
            config.pattern.minArmLengthPixels = legacyLineLength * 0.5;
        }
    }

    if (config.pattern.expectedAngleDegrees < -180.0
        || config.pattern.expectedAngleDegrees > 180.0) {
        error = "ExpectedAngleDegrees must be within [-180, 180]";
        return false;
    }
    if (config.pattern.angleToleranceDegrees <= 0.0
        || config.pattern.angleToleranceDegrees > 45.0) {
        error = "AngleToleranceDegrees must be within (0, 45]";
        return false;
    }
    if (config.pattern.minContrast <= 0.0 || config.pattern.minContrast > 1.0) {
        error = "MinPatternContrast must be within (0, 1]";
        return false;
    }
    if (config.pattern.minArmLengthPixels < 1.0
        || config.pattern.minArmLengthPixels > 4096.0) {
        error = "MinArmLengthPixels must be within [1, 4096]";
        return false;
    }
    if (config.pattern.minArmCoverage < 0.0 || config.pattern.minArmCoverage > 1.0) {
        error = "MinArmCoverage must be within [0, 1]";
        return false;
    }
    return true;
}

bool ParseCenterMethod(const json& value, FindCrossConfig& config, std::string& error)
{
    if (!value.is_string()) {
        error = "CenterMethod must be a string";
        return false;
    }
    config.requestedCenterMethod = value.get<std::string>();
    const std::string normalized = ToLower(config.requestedCenterMethod);
    if (normalized == "diagonalintersection" || normalized == "legacycompatible") {
        config.centerMethod = CenterMethod::DiagonalIntersection;
        if (normalized == "legacycompatible") {
            AddUnique(config.warnings, "CompatibilityAliasNotVendorEquivalent");
        }
        return true;
    }
    if (normalized == "corneraverage") {
        config.centerMethod = CenterMethod::CornerAverage;
        return true;
    }
    error = "CenterMethod must be DiagonalIntersection, CornerAverage, or LegacyCompatible";
    return false;
}

bool ParseRotationMethod(const json& value, FindCrossConfig& config, std::string& error)
{
    if (value.is_number_integer()) {
        const int method = value.get<int>();
        if (method == 0) {
            config.requestedRotationMethod = "TopEdge";
            config.rotationMethod = RotationMethod::TopEdge;
            return true;
        }
        if (method == 1) {
            config.requestedRotationMethod = "AllEdges";
            config.rotationMethod = RotationMethod::AllEdges;
            return true;
        }
        error = "RotationMethod numeric value must be 0 (TopEdge) or 1 (AllEdges)";
        return false;
    }
    if (!value.is_string()) {
        error = "RotationMethod must be a string or integer";
        return false;
    }

    config.requestedRotationMethod = value.get<std::string>();
    const std::string normalized = ToLower(config.requestedRotationMethod);
    if (normalized == "topedge" || normalized == "legacycompatible") {
        config.rotationMethod = RotationMethod::TopEdge;
        if (normalized == "legacycompatible") {
            AddUnique(config.warnings, "CompatibilityAliasNotVendorEquivalent");
        }
        return true;
    }
    if (normalized == "alledges") {
        config.rotationMethod = RotationMethod::AllEdges;
        return true;
    }
    error = "RotationMethod must be TopEdge, AllEdges, or LegacyCompatible";
    return false;
}

} // namespace

bool ParseFindCrossConfig(const json& value, FindCrossConfig& config, std::string& error)
{
    error.clear();
    if (!value.is_object()) {
        error = "configuration must be a JSON object";
        return false;
    }

    RecordIgnoredLegacyParameters(value, config);

    json luminousOptions = value;
    const std::array<std::pair<const char*, const char*>, 8> aliases{
        std::pair{ "minConfidence", "MinConfidence" },
        std::pair{ "minAreaRatio", "MinAreaRatio" },
        std::pair{ "maxAreaRatio", "MaxAreaRatio" },
        std::pair{ "searchWidthRatio", "SearchWidthRatio" },
        std::pair{ "minEdgeContrast", "MinEdgeContrast" },
        std::pair{ "caliperCount", "CaliperCount" },
        std::pair{ "maxProcessingSize", "MaxProcessingSize" },
        std::pair{ "allowBorder", "AllowBorder" }
    };
    for (const auto& [alias, canonical] : aliases) {
        if (!luminousOptions.contains(canonical) && luminousOptions.contains(alias)) {
            luminousOptions[canonical] = luminousOptions.at(alias);
        }
    }
    if (!luminous::ParseFindLuminousAreaV2Config(luminousOptions, config.luminous, error)) {
        return false;
    }
    if (!ParsePatternConfig(value, config, error)) {
        return false;
    }

    if (const json* name = FindMember(value, { "name", "Name" })) {
        if (!name->is_string()) {
            error = "name must be a string";
            return false;
        }
        config.name = name->get<std::string>();
        if (config.name.empty()) {
            config.name = "Point_1";
        }
    }

    if (const json* method = FindMember(value, { "CenterMethod", "centerMethod" })) {
        if (!ParseCenterMethod(*method, config, error)) {
            return false;
        }
    }
    if (const json* method = FindMember(value, { "RotationMethod", "rotationMethod" })) {
        if (!ParseRotationMethod(*method, config, error)) {
            return false;
        }
    }

    if (const json* optics = FindMember(value, { "opticsParams", "OpticsParams" })) {
        if (!optics->is_object()) {
            error = "opticsParams must be an object";
            return false;
        }
        if (const json* standardCenter = FindMember(*optics, { "stdCenter", "StdCenter" })) {
            if (!standardCenter->is_object()) {
                error = "opticsParams.stdCenter must be an object";
                return false;
            }
            if (!ReadFiniteNumber(
                *standardCenter, { "x", "X" }, config.optics.standardCenter.x,
                error, "opticsParams.stdCenter.x")
                || !ReadFiniteNumber(
                    *standardCenter, { "y", "Y" }, config.optics.standardCenter.y,
                    error, "opticsParams.stdCenter.y")) {
                return false;
            }
            config.optics.standardCenterSpecified = true;
        }
        if (!ReadFiniteNumber(
            *optics, { "focusLength", "FocusLength" }, config.optics.focusLengthMm,
            error, "opticsParams.focusLength", true)
            || !ReadFiniteNumber(
                *optics, { "sensorPixSize", "SensorPixSize" }, config.optics.sensorPixelSizeUm,
                error, "opticsParams.sensorPixSize", true)) {
            return false;
        }
        if (const json* distortion = FindMember(*optics, { "distortion", "Distortion" })) {
            if (!distortion->is_object()) {
                error = "opticsParams.distortion must be an object";
                return false;
            }
            if (!ReadBoolean(
                *distortion, { "Enabled", "enabled" }, config.optics.distortion.enabled,
                error, "opticsParams.distortion.Enabled")
                || !ReadFiniteNumber(
                    *distortion, { "K1", "k1" }, config.optics.distortion.k1,
                    error, "opticsParams.distortion.K1")
                || !ReadFiniteNumber(
                    *distortion, { "K2", "k2" }, config.optics.distortion.k2,
                    error, "opticsParams.distortion.K2")
                || !ReadFiniteNumber(
                    *distortion, { "P1", "p1" }, config.optics.distortion.p1,
                    error, "opticsParams.distortion.P1")
                || !ReadFiniteNumber(
                    *distortion, { "P2", "p2" }, config.optics.distortion.p2,
                    error, "opticsParams.distortion.P2")
                || !ReadFiniteNumber(
                    *distortion, { "K3", "k3" }, config.optics.distortion.k3,
                    error, "opticsParams.distortion.K3")) {
                return false;
            }
            const json* fx = FindMember(*distortion, { "Fx", "fx" });
            const json* fy = FindMember(*distortion, { "Fy", "fy" });
            const json* cx = FindMember(*distortion, { "Cx", "cx" });
            const json* cy = FindMember(*distortion, { "Cy", "cy" });
            const bool anyIntrinsic = fx != nullptr || fy != nullptr || cx != nullptr || cy != nullptr;
            const bool allIntrinsics = fx != nullptr && fy != nullptr && cx != nullptr && cy != nullptr;
            if (anyIntrinsic && !allIntrinsics) {
                error = "opticsParams.distortion Fx/Fy/Cx/Cy must be provided together";
                return false;
            }
            if (config.optics.distortion.enabled && !allIntrinsics) {
                error = "enabled opticsParams.distortion requires calibrated Fx/Fy/Cx/Cy";
                return false;
            }
            if (allIntrinsics) {
                if (!ReadFiniteNumber(
                    *distortion, { "Fx", "fx" }, config.optics.distortion.fx,
                    error, "opticsParams.distortion.Fx", true)
                    || !ReadFiniteNumber(
                        *distortion, { "Fy", "fy" }, config.optics.distortion.fy,
                        error, "opticsParams.distortion.Fy", true)
                    || !ReadFiniteNumber(
                        *distortion, { "Cx", "cx" }, config.optics.distortion.cx,
                        error, "opticsParams.distortion.Cx")
                    || !ReadFiniteNumber(
                        *distortion, { "Cy", "cy" }, config.optics.distortion.cy,
                        error, "opticsParams.distortion.Cy")) {
                    return false;
                }
                config.optics.distortion.intrinsicsSpecified = true;
            }
        }
    }

    if (const json* offset = FindMember(value, { "CalibrationOffset", "calibrationOffset" })) {
        if (!offset->is_object()) {
            error = "CalibrationOffset must be an object";
            return false;
        }
        if (!ReadFiniteNumber(
            *offset, { "x", "X" }, config.calibrationOffset.x,
            error, "CalibrationOffset.x")
            || !ReadFiniteNumber(
                *offset, { "y", "Y" }, config.calibrationOffset.y,
                error, "CalibrationOffset.y")) {
            return false;
        }
    }
    if (!ReadFiniteNumber(
        value, { "CenterOffsetX", "centerOffsetX" }, config.calibrationOffset.x,
        error, "CenterOffsetX")
        || !ReadFiniteNumber(
            value, { "CenterOffsetY", "centerOffsetY" }, config.calibrationOffset.y,
            error, "CenterOffsetY")) {
        return false;
    }
    return true;
}

struct CameraModel
{
    double fx = 1.0;
    double fy = 1.0;
    double cx = 0.0;
    double cy = 0.0;
    cv::Vec<double, 5> coefficients{};
};

CameraModel ResolveCameraModel(const OpticsParams& optics)
{
    const double nominalFocalPixels = optics.focusLengthMm * kMicrometersPerMillimeter
        / optics.sensorPixelSizeUm;
    CameraModel model;
    model.fx = optics.distortion.intrinsicsSpecified
        ? optics.distortion.fx : nominalFocalPixels;
    model.fy = optics.distortion.intrinsicsSpecified
        ? optics.distortion.fy : nominalFocalPixels;
    model.cx = optics.distortion.intrinsicsSpecified
        ? optics.distortion.cx : optics.standardCenter.x;
    model.cy = optics.distortion.intrinsicsSpecified
        ? optics.distortion.cy : optics.standardCenter.y;
    model.coefficients = cv::Vec<double, 5>(
        optics.distortion.k1,
        optics.distortion.k2,
        optics.distortion.p1,
        optics.distortion.p2,
        optics.distortion.k3);
    return model;
}

cv::Matx33d CameraMatrix(const CameraModel& model, const cv::Point2d& origin = {})
{
    return cv::Matx33d(
        model.fx, 0.0, model.cx - origin.x,
        0.0, model.fy, model.cy - origin.y,
        0.0, 0.0, 1.0);
}

bool IsFinitePoint(const cv::Point2d& point)
{
    return std::isfinite(point.x) && std::isfinite(point.y);
}

bool ValidateUndistortMaps(const cv::Mat& mapX, const cv::Mat& mapY, const cv::Size& sourceSize)
{
    if (mapX.empty() || mapY.empty() || mapX.type() != CV_32FC1
        || mapY.type() != CV_32FC1 || sourceSize.width < 5 || sourceSize.height < 5) {
        return false;
    }

    constexpr int gridSize = 7;
    int finiteSamples = 0;
    int insideSamples = 0;
    int validJacobians = 0;
    int totalSamples = 0;
    for (int gridY = 1; gridY < gridSize - 1; ++gridY) {
        const int y = std::clamp(
            cvRound(gridY * (sourceSize.height - 1.0) / (gridSize - 1.0)),
            1, sourceSize.height - 2);
        for (int gridX = 1; gridX < gridSize - 1; ++gridX) {
            const int x = std::clamp(
                cvRound(gridX * (sourceSize.width - 1.0) / (gridSize - 1.0)),
                1, sourceSize.width - 2);
            totalSamples++;
            const cv::Point2d mapped(mapX.at<float>(y, x), mapY.at<float>(y, x));
            if (!IsFinitePoint(mapped)) continue;
            finiteSamples++;
            if (mapped.x >= 0.0 && mapped.y >= 0.0
                && mapped.x <= sourceSize.width - 1.0
                && mapped.y <= sourceSize.height - 1.0) {
                insideSamples++;
            }

            const cv::Point2d derivativeX(
                (mapX.at<float>(y, x + 1) - mapX.at<float>(y, x - 1)) * 0.5,
                (mapY.at<float>(y, x + 1) - mapY.at<float>(y, x - 1)) * 0.5);
            const cv::Point2d derivativeY(
                (mapX.at<float>(y + 1, x) - mapX.at<float>(y - 1, x)) * 0.5,
                (mapY.at<float>(y + 1, x) - mapY.at<float>(y - 1, x)) * 0.5);
            const double determinant = Cross(derivativeX, derivativeY);
            const double scaleX = cv::norm(derivativeX);
            const double scaleY = cv::norm(derivativeY);
            if (IsFinitePoint(derivativeX) && IsFinitePoint(derivativeY)
                && determinant > 1e-4 && determinant < 1e4
                && scaleX >= 0.01 && scaleX <= 100.0
                && scaleY >= 0.01 && scaleY <= 100.0) {
                validJacobians++;
            }
        }
    }
    return finiteSamples == totalSamples
        && insideSamples * 10 >= totalSamples * 3
        && validJacobians * 4 >= totalSamples * 3;
}

bool BuildUndistortedDetectionImage(
    const cv::Mat& source,
    const cv::Point2d& globalOffset,
    const OpticsParams& optics,
    cv::Mat& corrected)
{
    if (!optics.distortion.enabled) {
        corrected = source;
        return true;
    }
    const CameraModel model = ResolveCameraModel(optics);
    const cv::Matx33d localCamera = CameraMatrix(model, globalOffset);
    cv::Mat mapX;
    cv::Mat mapY;
    cv::initUndistortRectifyMap(
        localCamera,
        model.coefficients,
        cv::Matx33d::eye(),
        localCamera,
        source.size(),
        CV_32FC1,
        mapX,
        mapY);
    if (!ValidateUndistortMaps(mapX, mapY, source.size())) return false;
    cv::remap(
        source,
        corrected,
        mapX,
        mapY,
        cv::INTER_LINEAR,
        cv::BORDER_CONSTANT,
        cv::Scalar());
    return !corrected.empty();
}

bool ApplyForwardDistortion(
    const std::vector<cv::Point2d>& corrected,
    const OpticsParams& optics,
    std::vector<cv::Point2d>& raw)
{
    raw.clear();
    raw.reserve(corrected.size());
    if (!optics.distortion.enabled) {
        raw = corrected;
        return true;
    }

    const CameraModel model = ResolveCameraModel(optics);
    for (const cv::Point2d& point : corrected) {
        if (!IsFinitePoint(point)) return false;
        const double x = (point.x - model.cx) / model.fx;
        const double y = (point.y - model.cy) / model.fy;
        const double r2 = x * x + y * y;
        const double r4 = r2 * r2;
        const double r6 = r4 * r2;
        const double radial = 1.0 + model.coefficients[0] * r2
            + model.coefficients[1] * r4 + model.coefficients[4] * r6;
        const double distortedX = x * radial + 2.0 * model.coefficients[2] * x * y
            + model.coefficients[3] * (r2 + 2.0 * x * x);
        const double distortedY = y * radial + model.coefficients[2] * (r2 + 2.0 * y * y)
            + 2.0 * model.coefficients[3] * x * y;
        const cv::Point2d mapped(
            model.fx * distortedX + model.cx,
            model.fy * distortedY + model.cy);
        if (!IsFinitePoint(mapped)) return false;
        raw.push_back(mapped);
    }
    return true;
}

bool IsInsideEvidenceRoi(
    const cv::Point2d& point,
    const cv::Point2d& globalOffset,
    const cv::Size& size,
    double margin = 2.0)
{
    return point.x >= globalOffset.x - margin
        && point.y >= globalOffset.y - margin
        && point.x <= globalOffset.x + size.width - 1.0 + margin
        && point.y <= globalOffset.y + size.height - 1.0 + margin;
}

bool IntersectLinePair(
    const cv::Point2d& firstPoint,
    const cv::Point2d& firstDirection,
    const cv::Point2d& secondPoint,
    const cv::Point2d& secondDirection,
    cv::Point2d& intersection);

bool ValidatePatternGeometry(
    const cv::Point2d& correctedCenter,
    const std::array<cv::Point2d, 4>& correctedEndpoints,
    const cv::Point2d& rawCenter,
    const std::array<cv::Point2d, 4>& rawEndpoints,
    const cv::Point2d& globalOffset,
    const cv::Size& imageSize,
    const PatternCrossConfig& config)
{
    if (!IsFinitePoint(correctedCenter) || !IsFinitePoint(rawCenter)
        || !IsInsideEvidenceRoi(rawCenter, globalOffset, imageSize)) {
        return false;
    }
    for (const cv::Point2d& point : correctedEndpoints) {
        if (!IsFinitePoint(point)) return false;
    }
    for (const cv::Point2d& point : rawEndpoints) {
        if (!IsFinitePoint(point) || !IsInsideEvidenceRoi(point, globalOffset, imageSize)) {
            return false;
        }
    }

    const cv::Point2d primary = correctedEndpoints[1] - correctedEndpoints[0];
    const cv::Point2d secondary = correctedEndpoints[3] - correctedEndpoints[2];
    const double primaryLength = cv::norm(primary);
    const double secondaryLength = cv::norm(secondary);
    const double minimumAxisLength = std::max(2.0, config.minArmLengthPixels * 1.5);
    if (primaryLength < minimumAxisLength || secondaryLength < minimumAxisLength) return false;
    const double sine = std::abs(Cross(primary, secondary))
        / (primaryLength * secondaryLength);
    if (!std::isfinite(sine) || sine < 0.95) return false;

    cv::Point2d intersection;
    if (!IntersectLinePair(
        correctedEndpoints[0], primary,
        correctedEndpoints[2], secondary,
        intersection)
        || cv::norm(intersection - correctedCenter)
            > std::max(3.0, config.minArmLengthPixels * 0.20)) {
        return false;
    }

    const double rawPrimaryLength = cv::norm(rawEndpoints[1] - rawEndpoints[0]);
    const double rawSecondaryLength = cv::norm(rawEndpoints[3] - rawEndpoints[2]);
    const double primaryRatio = rawPrimaryLength / primaryLength;
    const double secondaryRatio = rawSecondaryLength / secondaryLength;
    return std::isfinite(primaryRatio) && std::isfinite(secondaryRatio)
        && primaryRatio >= 0.02 && primaryRatio <= 50.0
        && secondaryRatio >= 0.02 && secondaryRatio <= 50.0;
}

bool ValidateOuterGeometry(
    const cv::Point2d& correctedCenter,
    const std::array<cv::Point2d, 4>& correctedCorners,
    const cv::Point2d& rawCenter,
    const std::array<cv::Point2d, 4>& rawCorners,
    const cv::Point2d& globalOffset,
    const cv::Size& imageSize)
{
    if (!IsFinitePoint(correctedCenter) || !IsFinitePoint(rawCenter)
        || !IsInsideEvidenceRoi(rawCenter, globalOffset, imageSize)) {
        return false;
    }
    double doubledArea = 0.0;
    for (size_t index = 0; index < correctedCorners.size(); ++index) {
        const size_t next = (index + 1) % correctedCorners.size();
        if (!IsFinitePoint(correctedCorners[index]) || !IsFinitePoint(rawCorners[index])
            || !IsInsideEvidenceRoi(rawCorners[index], globalOffset, imageSize)) {
            return false;
        }
        const double correctedLength = cv::norm(correctedCorners[next] - correctedCorners[index]);
        const double rawLength = cv::norm(rawCorners[next] - rawCorners[index]);
        if (correctedLength < 2.0 || rawLength / correctedLength < 0.02
            || rawLength / correctedLength > 50.0) {
            return false;
        }
        doubledArea += Cross(correctedCorners[index], correctedCorners[next]);
    }
    return std::isfinite(doubledArea) && std::abs(doubledArea) > 8.0;
}

double CombinePerpendicularAxes(double primaryAngle, double secondaryAngle)
{
    const double secondaryAsPrimary = NormalizeAxisAngle(secondaryAngle - 90.0);
    const double primaryRadians = 2.0 * primaryAngle / kRadiansToDegrees;
    const double secondaryRadians = 2.0 * secondaryAsPrimary / kRadiansToDegrees;
    return NormalizeAxisAngle(0.5 * std::atan2(
        std::sin(primaryRadians) + std::sin(secondaryRadians),
        std::cos(primaryRadians) + std::cos(secondaryRadians)) * kRadiansToDegrees);
}

bool IntersectLinePair(
    const cv::Point2d& firstPoint,
    const cv::Point2d& firstDirection,
    const cv::Point2d& secondPoint,
    const cv::Point2d& secondDirection,
    cv::Point2d& intersection)
{
    const double denominator = Cross(firstDirection, secondDirection);
    if (!std::isfinite(denominator) || std::abs(denominator) <= 1e-9) return false;
    const double parameter = Cross(secondPoint - firstPoint, secondDirection) / denominator;
    intersection = firstPoint + firstDirection * parameter;
    return std::isfinite(intersection.x) && std::isfinite(intersection.y);
}

FindCrossResult FindCross(
    const cv::Mat& image,
    const cv::Point2d& globalOffset,
    const FindCrossConfig& config)
{
    FindCrossResult result;
    result.patternMode = config.detectionMode == DetectionMode::PatternCross;
    result.rotationMethodUsed = RotationMethodName(config.rotationMethod);
    result.centerMethodUsed = result.patternMode
        ? "PatternAxisIntersection"
        : CenterMethodName(config.centerMethod);
    result.distortionApplied = config.optics.distortion.enabled;

    cv::Mat detectionImage;
    if (!BuildUndistortedDetectionImage(
        image, globalOffset, config.optics, detectionImage)) {
        if (result.patternMode) {
            result.pattern.failureReason = "InvalidDistortionGeometry";
        }
        else {
            result.detection.failureReason = "InvalidDistortionGeometry";
        }
        return result;
    }

    if (result.patternMode) {
        // PatternCross has two independently fitted, near-perpendicular axes.
        // A single-axis angle throws away half of the evidence and is more
        // sensitive to a local stain or broken segment. Production output is
        // therefore always the robust two-axis circular mean. The legacy
        // RotationMethod field remains parseable for payload compatibility,
        // but is diagnostic-only in Pattern mode.
        result.rotationMethodUsed = "RobustTwoAxis";
        result.pattern = FindPatternCross(detectionImage, config.pattern);
        if (!result.pattern.success) return result;

        for (size_t index = 0; index < result.globalArmEndpoints.size(); ++index) {
            result.globalArmEndpoints[index] = result.pattern.armEndpoints[index] + globalOffset;
        }
        cv::Point2d fittedIntersection;
        const bool hasIntersection = IntersectLinePair(
            result.globalArmEndpoints[0],
            result.globalArmEndpoints[1] - result.globalArmEndpoints[0],
            result.globalArmEndpoints[2],
            result.globalArmEndpoints[3] - result.globalArmEndpoints[2],
            fittedIntersection);
        if (!hasIntersection) {
            result.pattern.success = false;
            result.pattern.failureReason = config.optics.distortion.enabled
                ? "InvalidDistortionGeometry" : "InvalidCenterGeometry";
            return result;
        }
        result.globalCenter = fittedIntersection;

        std::vector<cv::Point2d> correctedGeometry{ result.globalCenter };
        correctedGeometry.insert(
            correctedGeometry.end(),
            result.globalArmEndpoints.begin(),
            result.globalArmEndpoints.end());
        std::vector<cv::Point2d> rawGeometry;
        if (!ApplyForwardDistortion(correctedGeometry, config.optics, rawGeometry)
            || rawGeometry.size() != correctedGeometry.size()) {
            result.pattern.success = false;
            result.pattern.failureReason = "InvalidDistortionGeometry";
            return result;
        }
        result.rawGlobalCenter = rawGeometry[0];
        for (size_t index = 0; index < result.rawGlobalArmEndpoints.size(); ++index) {
            result.rawGlobalArmEndpoints[index] = rawGeometry[index + 1];
        }
        if (!ValidatePatternGeometry(
            result.globalCenter,
            result.globalArmEndpoints,
            result.rawGlobalCenter,
            result.rawGlobalArmEndpoints,
            globalOffset,
            image.size(),
            config.pattern)) {
            result.pattern.success = false;
            result.pattern.failureReason = config.optics.distortion.enabled
                ? "InvalidDistortionGeometry" : "InvalidCenterGeometry";
            return result;
        }
        result.hasCenter = true;
        result.globalCenter += config.calibrationOffset;
        result.topEdgeRotationDegrees = result.pattern.primaryAngleDegrees;
        result.allEdgesRotationDegrees = result.pattern.combinedAngleDegrees;
    }
    else {
        result.detection = luminous::FindLuminousAreaV2(detectionImage, config.luminous);
        if (!result.detection.hasCorners) return result;
        for (size_t index = 0; index < result.globalCorners.size(); ++index) {
            result.globalCorners[index] = cv::Point2d(result.detection.corners[index]) + globalOffset;
        }
        cv::Point2d correctedCenter;
        if (config.centerMethod == CenterMethod::DiagonalIntersection) {
            result.hasCenter = IntersectDiagonals(result.globalCorners, correctedCenter);
            if (!result.hasCenter) {
                correctedCenter = AverageCorners(result.globalCorners);
                result.hasCenter = IsFinitePoint(correctedCenter);
                result.centerMethodUsed = "CornerAverageFallback";
            }
        }
        else {
            correctedCenter = AverageCorners(result.globalCorners);
            result.hasCenter = IsFinitePoint(correctedCenter);
        }
        if (!result.hasCenter) return result;

        std::vector<cv::Point2d> correctedGeometry{ correctedCenter };
        correctedGeometry.insert(
            correctedGeometry.end(), result.globalCorners.begin(), result.globalCorners.end());
        std::vector<cv::Point2d> rawGeometry;
        if (!ApplyForwardDistortion(correctedGeometry, config.optics, rawGeometry)
            || rawGeometry.size() != correctedGeometry.size()) {
            result.hasCenter = false;
            result.detection.success = false;
            result.detection.failureReason = "InvalidDistortionGeometry";
            return result;
        }
        std::array<cv::Point2d, 4> rawCorners{};
        std::copy(rawGeometry.begin() + 1, rawGeometry.end(), rawCorners.begin());
        result.rawGlobalCenter = rawGeometry[0];
        if (!ValidateOuterGeometry(
            correctedCenter,
            result.globalCorners,
            result.rawGlobalCenter,
            rawCorners,
            globalOffset,
            image.size())) {
            result.hasCenter = false;
            result.detection.success = false;
            result.detection.failureReason = config.optics.distortion.enabled
                ? "InvalidDistortionGeometry" : "InvalidCenterGeometry";
            return result;
        }
        result.globalCenter = correctedCenter + config.calibrationOffset;
        result.hasCenter = true;
        result.topEdgeRotationDegrees = LineAngleDegrees(
            result.globalCorners[0], result.globalCorners[1]);
        result.allEdgesRotationDegrees = ComputeAllEdgesRotation(
            result.globalCorners, result.detection.sideQuality);
    }
    result.selectedRotationDegrees = result.patternMode
        ? result.allEdgesRotationDegrees
        : (config.rotationMethod == RotationMethod::AllEdges
            ? result.allEdgesRotationDegrees
            : result.topEdgeRotationDegrees);

    if (result.hasCenter) {
        const double pixelSizeMm = config.optics.sensorPixelSizeUm / kMicrometersPerMillimeter;
        result.tiltXDegrees = std::atan(
            (result.globalCenter.x - config.optics.standardCenter.x)
            * pixelSizeMm / config.optics.focusLengthMm) * kRadiansToDegrees;
        result.tiltYDegrees = -std::atan(
            (result.globalCenter.y - config.optics.standardCenter.y)
            * pixelSizeMm / config.optics.focusLengthMm) * kRadiansToDegrees;
    }
    return result;
}

} // namespace cvnative::find_cross

namespace
{

constexpr int ExportInvalidArgument = -1;
constexpr int ExportAllocationFailed = -3;
constexpr int ExportInvalidJson = -4;
constexpr int ExportOpenCvException = -5;
constexpr int ExportStdException = -6;
constexpr int ExportUnknownException = -7;

thread_local std::string findCrossLastError;

void ClearFindCrossLastError() noexcept
{
    findCrossLastError.clear();
}

void SetFindCrossLastError(std::string_view message) noexcept
{
    try {
        findCrossLastError.assign(message.data(), message.size());
    }
    catch (...) {
        findCrossLastError.clear();
    }
}

int CopyJsonResult(const json& output, char** result)
{
    const std::string text = output.dump();
    const size_t length = text.size() + 1;
    if (length > static_cast<size_t>(std::numeric_limits<int>::max())) {
        return ExportAllocationFailed;
    }
    char* buffer = static_cast<char*>(CoTaskMemAlloc(length));
    if (buffer == nullptr) {
        return ExportAllocationFailed;
    }
    std::memcpy(buffer, text.c_str(), length);
    *result = buffer;
    return static_cast<int>(length);
}

json SideQualityJson(const cvnative::luminous::FindLuminousAreaV2Result& detection)
{
    static constexpr std::array<const char*, 4> names{ "Top", "Right", "Bottom", "Left" };
    json output = json::array();
    for (size_t index = 0; index < detection.sideQuality.size(); ++index) {
        const auto& side = detection.sideQuality[index];
        output.push_back({
            { "Name", names[index] },
            { "Coverage", side.coverage },
            { "InlierRatio", side.inlierRatio },
            { "ContrastP10", side.contrastP10 },
            { "FitRms", side.fitRms },
            { "MaxGap", side.maxGap },
            { "Confidence", std::clamp(side.confidence, 0.0, 1.0) },
            { "SampleCount", side.sampleCount },
            { "InlierCount", side.inlierCount }
        });
    }
    return output;
}

json PatternArmQualityJson(const cvnative::find_cross::PatternCrossResult& pattern)
{
    static constexpr std::array<const char*, 4> names{
        "PrimaryNegative", "PrimaryPositive", "SecondaryNegative", "SecondaryPositive"
    };
    json output = json::array();
    for (size_t index = 0; index < pattern.armQuality.size(); ++index) {
        const auto& arm = pattern.armQuality[index];
        output.push_back({
            { "Name", names[index] },
            { "Coverage", arm.coverage },
            { "Contrast", arm.contrast },
            { "Span", arm.span },
            { "FitRms", arm.fitRms },
            { "SampleCount", arm.sampleCount },
            { "InlierCount", arm.inlierCount }
        });
    }
    return output;
}

json BuildOutput(
    const cvnative::find_cross::FindCrossResult& result,
    const cvnative::find_cross::FindCrossConfig& config,
    const cv::Rect& effectiveRoi)
{
    json output;
    output["result"] = json::array();

    const bool algorithmSuccess = result.patternMode
        ? result.pattern.success
        : result.detection.success;
    const bool success = algorithmSuccess && result.hasCenter;
    const double confidence = result.patternMode
        ? result.pattern.confidence
        : result.detection.confidence;
    const std::string failureReason = algorithmSuccess
        ? (result.hasCenter ? "" : "InvalidCenterGeometry")
        : (result.patternMode ? result.pattern.failureReason : result.detection.failureReason);
    std::vector<std::string> warnings = result.patternMode
        ? result.pattern.warnings
        : result.detection.warnings;
    for (const std::string& warning : config.warnings) {
        if (std::find(warnings.begin(), warnings.end(), warning) == warnings.end()) {
            warnings.push_back(warning);
        }
    }
    if (result.centerMethodUsed == "CornerAverageFallback") {
        warnings.push_back("DiagonalIntersectionFallback");
    }

    json diagnostics;
    diagnostics["Success"] = success;
    diagnostics["Algorithm"] = result.patternMode ? "PatternCrossV1" : "OuterPanelAssistV2";
    diagnostics["DetectionMode"] = result.patternMode ? "PatternCross" : "OuterPanel";
    diagnostics["RequestedDetectionMode"] = config.requestedDetectionMode;
    diagnostics["CenterMethod"] = result.centerMethodUsed;
    diagnostics["RequestedCenterMethod"] = config.requestedCenterMethod;
    diagnostics["RotationMethod"] = result.rotationMethodUsed;
    diagnostics["RequestedRotationMethod"] = config.requestedRotationMethod;
    diagnostics["CenterSubpixel"] = result.hasCenter
        ? json{ { "x", result.globalCenter.x }, { "y", result.globalCenter.y } }
        : json(nullptr);
    diagnostics["RawGeometricCenter"] = result.hasCenter
        ? json{ { "x", result.rawGlobalCenter.x }, { "y", result.rawGlobalCenter.y } }
        : json(nullptr);
    diagnostics["AppliedOffset"] = {
        { "x", config.calibrationOffset.x },
        { "y", config.calibrationOffset.y }
    };
    diagnostics["EffectiveOptics"] = {
        { "StandardCenter", {
            { "x", config.optics.standardCenter.x },
            { "y", config.optics.standardCenter.y }
        } },
        { "FocusLengthMm", config.optics.focusLengthMm },
        { "SensorPixelSizeUm", config.optics.sensorPixelSizeUm },
        { "Distortion", {
            { "Enabled", config.optics.distortion.enabled },
            { "K1", config.optics.distortion.k1 },
            { "K2", config.optics.distortion.k2 },
            { "P1", config.optics.distortion.p1 },
            { "P2", config.optics.distortion.p2 },
            { "K3", config.optics.distortion.k3 },
            { "Fx", config.optics.distortion.intrinsicsSpecified
                ? config.optics.distortion.fx
                : config.optics.focusLengthMm * 1000.0 / config.optics.sensorPixelSizeUm },
            { "Fy", config.optics.distortion.intrinsicsSpecified
                ? config.optics.distortion.fy
                : config.optics.focusLengthMm * 1000.0 / config.optics.sensorPixelSizeUm },
            { "Cx", config.optics.distortion.intrinsicsSpecified
                ? config.optics.distortion.cx : config.optics.standardCenter.x },
            { "Cy", config.optics.distortion.intrinsicsSpecified
                ? config.optics.distortion.cy : config.optics.standardCenter.y },
            { "IntrinsicsSource", config.optics.distortion.intrinsicsSpecified
                ? "Calibration" : "NominalOpticsFallback" }
        } },
        { "StandardCenterSource", config.optics.standardCenterSpecified
            ? "Configuration" : "ImageCenterDefault" }
    };
    diagnostics["IgnoredParameters"] = config.ignoredParameters;
    diagnostics["Confidence"] = std::clamp(confidence, 0.0, 1.0);
    diagnostics["PatternPolarity"] = result.patternMode
        ? json(result.pattern.polarityUsed)
        : json(nullptr);
    diagnostics["PatternContrast"] = result.patternMode
        ? json(result.pattern.patternContrast)
        : json(nullptr);
    diagnostics["OrthogonalityError"] = result.patternMode
        ? json(result.pattern.orthogonalityErrorDegrees)
        : json(nullptr);
    diagnostics["DistortionApplied"] = result.distortionApplied;
    diagnostics["Corners"] = json::array();
    if (result.detection.hasCorners) {
        for (const cv::Point2d& corner : result.globalCorners) {
            diagnostics["Corners"].push_back({ { "x", corner.x }, { "y", corner.y } });
        }
    }
    diagnostics["ArmEndpoints"] = json::array();
    diagnostics["RawArmEndpoints"] = json::array();
    if (result.patternMode && result.pattern.success) {
        for (const cv::Point2d& point : result.globalArmEndpoints) {
            diagnostics["ArmEndpoints"].push_back({ { "x", point.x }, { "y", point.y } });
        }
        for (const cv::Point2d& point : result.rawGlobalArmEndpoints) {
            diagnostics["RawArmEndpoints"].push_back({ { "x", point.x }, { "y", point.y } });
        }
    }
    diagnostics["SideQuality"] = result.patternMode
        ? json::array()
        : SideQualityJson(result.detection);
    diagnostics["ArmQuality"] = result.patternMode
        ? PatternArmQualityJson(result.pattern)
        : json::array();
    diagnostics["RotationCandidates"] = algorithmSuccess
        ? json{
            { "TopEdge", result.topEdgeRotationDegrees },
            { "AllEdges", result.allEdgesRotationDegrees },
            { "PrimaryAxis", result.topEdgeRotationDegrees },
            { "TwoAxis", result.allEdgesRotationDegrees }
        }
        : json(nullptr);
    diagnostics["FailureReason"] = failureReason;
    diagnostics["Warnings"] = warnings;
    diagnostics["EffectiveRoi"] = {
        { "x", effectiveRoi.x },
        { "y", effectiveRoi.y },
        { "w", effectiveRoi.width },
        { "h", effectiveRoi.height }
    };

    if (success) {
        output["result"].push_back({
            { "center", {
                { "x", static_cast<int>(std::lround(result.globalCenter.x)) },
                { "y", static_cast<int>(std::lround(result.globalCenter.y)) }
            } },
            { "h", effectiveRoi.height },
            { "name", config.name },
            { "rotationAngle", result.selectedRotationDegrees },
            { "tilt", {
                { "tilt_x", result.tiltXDegrees },
                { "tilt_y", result.tiltYDegrees }
            } },
            { "w", effectiveRoi.width },
            { "x", effectiveRoi.x },
            { "y", effectiveRoi.y }
        });
    }
    output["Success"] = diagnostics["Success"];
    output["Algorithm"] = diagnostics["Algorithm"];
    output["FailureReason"] = diagnostics["FailureReason"];
    output["Warnings"] = diagnostics["Warnings"];
    output["diagnostics"] = std::move(diagnostics);
    return output;
}

} // namespace

extern "C" COLORVISIONCORE_API int M_FindCrossLocal(
    HImage image,
    RoiRect roi,
    const char* configJson,
    char** resultJson)
{
    ClearFindCrossLastError();
    if (resultJson != nullptr) {
        *resultJson = nullptr;
    }

    try {
        if (configJson == nullptr || resultJson == nullptr) {
            return ExportInvalidArgument;
        }
        const cv::Mat imageMat = HImageToMatView(image);
        if (imageMat.empty()) {
            return ExportInvalidArgument;
        }

        const cv::Rect imageBounds(0, 0, imageMat.cols, imageMat.rows);
        const bool emptyRoi = roi.x == 0 && roi.y == 0 && roi.width == 0 && roi.height == 0;
        const cv::Rect requestedRoi(roi.x, roi.y, roi.width, roi.height);
        if (!emptyRoi && (requestedRoi.width <= 0 || requestedRoi.height <= 0
            || (requestedRoi & imageBounds) != requestedRoi)) {
            return ExportInvalidArgument;
        }
        const cv::Rect effectiveRoi = emptyRoi ? imageBounds : requestedRoi;

        const json parsedJson = json::parse(configJson);
        cvnative::find_cross::FindCrossConfig config;
        std::string configError;
        if (!cvnative::find_cross::ParseFindCrossConfig(parsedJson, config, configError)) {
            SetFindCrossLastError(configError.empty()
                ? "FindCross configuration validation failed"
                : configError);
            return ExportInvalidJson;
        }
        if (!config.optics.standardCenterSpecified) {
            config.optics.standardCenter = cv::Point2d(
                imageMat.cols * 0.5,
                imageMat.rows * 0.5);
        }

        const cvnative::find_cross::FindCrossResult result = cvnative::find_cross::FindCross(
            imageMat(effectiveRoi),
            cv::Point2d(effectiveRoi.x, effectiveRoi.y),
            config);
        return CopyJsonResult(BuildOutput(result, config, effectiveRoi), resultJson);
    }
    catch (const json::exception& exception) {
        SetFindCrossLastError(exception.what());
        cvnative::LogException(
            "find_cross.export", "M_FindCrossLocal", ExportInvalidJson,
            "json::exception", exception.what());
        return ExportInvalidJson;
    }
    catch (const cv::Exception& exception) {
        SetFindCrossLastError(exception.what());
        cvnative::LogException(
            "find_cross.export", "M_FindCrossLocal", ExportOpenCvException,
            "cv::Exception", exception.what());
        return ExportOpenCvException;
    }
    catch (const std::exception& exception) {
        SetFindCrossLastError(exception.what());
        cvnative::LogException(
            "find_cross.export", "M_FindCrossLocal", ExportStdException,
            "std::exception", exception.what());
        return ExportStdException;
    }
    catch (...) {
        SetFindCrossLastError("Unknown exception while executing M_FindCrossLocal");
        cvnative::LogException(
            "find_cross.export", "M_FindCrossLocal", ExportUnknownException, "unknown");
        return ExportUnknownException;
    }
}

extern "C" COLORVISIONCORE_API int M_FindCrossLocalGetLastError(
    char* buffer,
    std::uint32_t bufferLength)
{
    try {
        if (findCrossLastError.size() >= static_cast<std::size_t>((std::numeric_limits<int>::max)())) {
            cvnative::LogFailure(
                cvnative::LogLevel::Error,
                "find_cross.export",
                "M_FindCrossLocalGetLastError",
                ExportStdException,
                "last-error message exceeds the supported return length");
            return ExportStdException;
        }

        const int required = static_cast<int>(findCrossLastError.size() + 1);
        if (buffer != nullptr && bufferLength >= static_cast<std::uint32_t>(required)) {
            std::memcpy(buffer, findCrossLastError.c_str(), static_cast<std::size_t>(required));
        }
        return required;
    }
    catch (const std::exception& exception) {
        cvnative::LogException(
            "find_cross.export",
            "M_FindCrossLocalGetLastError",
            ExportStdException,
            "std::exception",
            exception.what());
        return ExportStdException;
    }
    catch (...) {
        cvnative::LogException(
            "find_cross.export",
            "M_FindCrossLocalGetLastError",
            ExportUnknownException,
            "unknown");
        return ExportUnknownException;
    }
}
