#include "calibration_geometric.h"

#include <opencv2/calib3d.hpp>
#include <opencv2/core.hpp>
#include <opencv2/imgproc.hpp>

#include <nlohmann/json.hpp>

#include <algorithm>
#include <array>
#include <cmath>
#include <cstddef>
#include <cstdint>
#include <cstring>
#include <fstream>
#include <limits>
#include <memory>
#include <string>
#include <utility>
#include <vector>

namespace cvcore::calibration {
namespace {

using json = nlohmann::json;

bool readJson(const std::filesystem::path& file, json& root, std::string& error)
{
    std::ifstream stream(file, std::ios::binary);
    if (!stream) {
        error = "Unable to open calibration file";
        return false;
    }

    try {
        stream >> root;
    }
    catch (const json::exception& exception) {
        error = "Invalid calibration JSON: " + std::string(exception.what());
        return false;
    }

    if (!root.is_object()) {
        error = "Calibration JSON root must be an object";
        return false;
    }
    return true;
}

bool readInt(const json& root, const char* key, int& value, std::string& error)
{
    const auto iterator = root.find(key);
    if (iterator == root.end() || !iterator->is_number()) {
        error = std::string("Missing numeric calibration field: ") + key;
        return false;
    }

    const double candidate = iterator->get<double>();
    if (!std::isfinite(candidate)
        || candidate < static_cast<double>((std::numeric_limits<int>::min)())
        || candidate > static_cast<double>((std::numeric_limits<int>::max)())) {
        error = std::string("Calibration field is outside the integer range: ") + key;
        return false;
    }
    value = static_cast<int>(candidate);
    return true;
}

bool readDouble(const json& root, const char* key, double& value, std::string& error)
{
    const auto iterator = root.find(key);
    if (iterator == root.end() || !iterator->is_number()) {
        error = std::string("Missing numeric calibration field: ") + key;
        return false;
    }
    value = iterator->get<double>();
    if (!std::isfinite(value)) {
        error = std::string("Calibration field must be finite: ") + key;
        return false;
    }
    return true;
}

bool readDoubleArray(
    const json& root,
    const char* key,
    std::vector<double>& values,
    std::string& error)
{
    const auto iterator = root.find(key);
    if (iterator == root.end() || !iterator->is_array()) {
        error = std::string("Missing calibration array: ") + key;
        return false;
    }

    values.clear();
    values.reserve(iterator->size());
    for (const auto& entry : *iterator) {
        if (!entry.is_number()) {
            error = std::string("Calibration array contains a non-number: ") + key;
            return false;
        }
        const double value = entry.get<double>();
        if (!std::isfinite(value)) {
            error = std::string("Calibration array contains a non-finite value: ") + key;
            return false;
        }
        values.push_back(value);
    }
    return true;
}

int imageType(const ImageView& image)
{
    const int depth = image.bitsPerChannel == 8 ? CV_8U : CV_16U;
    return CV_MAKETYPE(depth, static_cast<int>(image.channels));
}

std::size_t imageByteCount(const ImageView& image)
{
    return static_cast<std::size_t>(image.width)
        * static_cast<std::size_t>(image.height)
        * static_cast<std::size_t>(image.channels)
        * static_cast<std::size_t>(image.bitsPerChannel / 8);
}

bool validCvDimensions(const ImageView& image, std::string& error)
{
    if (image.width > static_cast<std::uint32_t>((std::numeric_limits<int>::max)())
        || image.height > static_cast<std::uint32_t>((std::numeric_limits<int>::max)())) {
        error = "Image dimensions exceed OpenCV's integer range";
        return false;
    }
    return true;
}

bool copyResultToRaw(const cv::Mat& result, const ImageView& raw, std::string& error)
{
    if (result.cols != static_cast<int>(raw.width)
        || result.rows != static_cast<int>(raw.height)
        || result.type() != imageType(raw)) {
        error = "Calibration result layout does not match the in-place RAW buffer";
        return false;
    }
    if (!result.isContinuous()) {
        error = "Calibration result is not contiguous";
        return false;
    }
    std::memcpy(raw.data, result.data, imageByteCount(raw));
    return true;
}

bool validDistinctOutput(
    const ImageView& source,
    const ImageView& destination,
    std::string& error)
{
    if (source.data == destination.data) {
        error = "Geometric calibration requires distinct source and destination buffers";
        return false;
    }
    if (destination.data == nullptr
        || source.width != destination.width
        || source.height != destination.height
        || source.bitsPerChannel != destination.bitsPerChannel
        || source.channels != destination.channels
        || destination.dataLength < imageByteCount(source)) {
        error = "Geometric calibration output layout does not match its source";
        return false;
    }
    return true;
}

bool validLegacyRoi(
    const ExecutionOptions& options,
    int fullWidth,
    int fullHeight)
{
    const auto x = static_cast<std::uint64_t>(options.roi[0]);
    const auto y = static_cast<std::uint64_t>(options.roi[1]);
    const auto width = static_cast<std::uint64_t>(options.roi[2]);
    const auto height = static_cast<std::uint64_t>(options.roi[3]);
    return width != 0 && height != 0
        && x + width < static_cast<std::uint64_t>(fullWidth)
        && y + height < static_cast<std::uint64_t>(fullHeight);
}

class DistortionCalibration final : public CalibrationItem {
public:
    DistortionCalibration() = default;

    [[nodiscard]] CalibrationType type() const noexcept override
    {
        return CalibrationType::Distortion;
    }

    [[nodiscard]] bool requiresDistinctOutput() const noexcept override
    {
        return true;
    }

    [[nodiscard]] std::unique_ptr<CalibrationItem> cloneForContext() const override
    {
        return std::unique_ptr<CalibrationItem>(new DistortionCalibration(*this));
    }

    [[nodiscard]] std::uint64_t cacheFootprintBytes() const noexcept override
    {
        return sizeof(*this)
            + static_cast<std::uint64_t>(mapX_.total()) * mapX_.elemSize()
            + static_cast<std::uint64_t>(mapY_.total()) * mapY_.elemSize();
    }

    bool load(const json& root, std::string& error)
    {
        if (!readInt(root, "w", width_, error)
            || !readInt(root, "h", height_, error)) {
            return false;
        }
        if (width_ <= 0 || height_ <= 0) {
            error = "Distortion calibration dimensions must be positive";
            return false;
        }

        showWidth_ = width_;
        showHeight_ = height_;
        if (root.contains("s_w") && !readInt(root, "s_w", showWidth_, error)) {
            return false;
        }
        if (root.contains("s_h") && !readInt(root, "s_h", showHeight_, error)) {
            return false;
        }
        if (showWidth_ <= 0 || showHeight_ <= 0) {
            error = "Distortion output dimensions must be positive";
            return false;
        }

        useFisheye_ = root.value("useFisheye", false);
        if (root.contains("alpha")) {
            if (!readDouble(root, "alpha", alpha_, error)) {
                return false;
            }
        }

        std::vector<double> camera;
        std::vector<double> distortion;
        if (!readDoubleArray(root, "cameraMatrix", camera, error)
            || !readDoubleArray(root, "distCoeffs", distortion, error)) {
            return false;
        }
        if (camera.size() < cameraMatrix_.size()) {
            error = "cameraMatrix must contain nine values";
            return false;
        }
        if (distortion.size() < 4) {
            error = "distCoeffs must contain at least four values";
            return false;
        }

        for (std::size_t index = 0; index < cameraMatrix_.size(); ++index) {
            cameraMatrix_[index] = static_cast<float>(camera[index]);
        }
        for (std::size_t index = 0;
             index < distortionCoefficients_.size() && index < distortion.size();
             ++index) {
            distortionCoefficients_[index] = static_cast<float>(distortion[index]);
        }

        if (useFisheye_) {
            cameraCenterX_ = static_cast<int>(cameraMatrix_[2]);
            cameraCenterY_ = static_cast<int>(cameraMatrix_[5]);
            cameraMatrix_[2] = static_cast<float>(width_ / 2);
            cameraMatrix_[5] = static_cast<float>(height_ / 2);
        }

        try {
            buildMaps(width_, height_, 0, 0, false, mapX_, mapY_);
        }
        catch (const cv::Exception& exception) {
            error = "Unable to create distortion maps: " + std::string(exception.what());
            return false;
        }
        return true;
    }

    bool apply(
        const ImageView& raw,
        float*,
        const ExecutionOptions& options,
        std::string& error) override
    {
        fallbackScratch_.create(
            static_cast<int>(raw.height),
            static_cast<int>(raw.width),
            imageType(raw));
        ImageView destination = raw;
        destination.data = fallbackScratch_.data;
        destination.dataLength = fallbackScratch_.total() * fallbackScratch_.elemSize();
        return applyOutOfPlace(raw, destination, options, error)
            && copyResultToRaw(fallbackScratch_, raw, error);
    }

    bool applyOutOfPlace(
        const ImageView& raw,
        const ImageView& destination,
        const ExecutionOptions& options,
        std::string& error) override
    {
        if (!validCvDimensions(raw, error)) {
            return false;
        }
        if (!validDistinctOutput(raw, destination, error)) {
            return false;
        }

        try {
            const bool useRoi = validLegacyRoi(options, width_, height_);
            const cv::Mat* selectedMapX = &mapX_;
            const cv::Mat* selectedMapY = &mapY_;
            int offsetX = width_ / 2 - cameraCenterX_;
            int offsetY = height_ / 2 - cameraCenterY_;

            if (useRoi) {
                const int roiX = static_cast<int>(options.roi[0]);
                const int roiY = static_cast<int>(options.roi[1]);
                const int roiWidth = static_cast<int>(options.roi[2]);
                const int roiHeight = static_cast<int>(options.roi[3]);
                if (raw.width != static_cast<std::uint32_t>(roiWidth)
                    || raw.height != static_cast<std::uint32_t>(roiHeight)) {
                    error = "Distortion ROI dimensions do not match the RAW buffer";
                    return false;
                }

                if (roiX_ != roiX || roiY_ != roiY
                    || roiWidth_ != roiWidth || roiHeight_ != roiHeight
                    || roiMapX_.empty() || roiMapY_.empty()) {
                    buildMaps(roiWidth, roiHeight, roiX, roiY, true, roiMapX_, roiMapY_);
                    roiX_ = roiX;
                    roiY_ = roiY;
                    roiWidth_ = roiWidth;
                    roiHeight_ = roiHeight;
                }
                selectedMapX = &roiMapX_;
                selectedMapY = &roiMapY_;
                offsetX = roiWidth / 2 - cameraCenterX_ + roiX;
                offsetY = roiHeight / 2 - cameraCenterY_ + roiY;
            }
            else {
                if (raw.width != static_cast<std::uint32_t>(width_)
                    || raw.height != static_cast<std::uint32_t>(height_)) {
                    error = "RAW dimensions do not match the distortion calibration";
                    return false;
                }
                if (selectedMapX->cols != static_cast<int>(raw.width)
                    || selectedMapX->rows != static_cast<int>(raw.height)) {
                    error = "Distortion show size cannot be written to an in-place RAW buffer";
                    return false;
                }
            }

            cv::Mat source(
                static_cast<int>(raw.height),
                static_cast<int>(raw.width),
                imageType(raw),
                raw.data);
            cv::Mat output(
                static_cast<int>(destination.height),
                static_cast<int>(destination.width),
                imageType(destination),
                destination.data);
            cv::Mat remapSource = source;

            if (useFisheye_ && (offsetX != 0 || offsetY != 0)) {
                // The legacy integer warp is exactly a zero-bordered view of
                // this cropped source rectangle. Maps are rebased to the
                // rectangle in buildMaps, avoiding a full-frame translation.
                const int copyWidth = source.cols - std::abs(offsetX);
                const int copyHeight = source.rows - std::abs(offsetY);
                if (copyWidth <= 0 || copyHeight <= 0) {
                    output.setTo(cv::Scalar::all(0));
                    return true;
                }
                remapSource = source(cv::Rect(
                    (std::max)(0, -offsetX),
                    (std::max)(0, -offsetY),
                    copyWidth,
                    copyHeight));
            }

            cv::remap(
                remapSource,
                output,
                *selectedMapX,
                *selectedMapY,
                cv::INTER_LINEAR,
                cv::BORDER_CONSTANT);
            return true;
        }
        catch (const cv::Exception& exception) {
            error = "Distortion calibration failed: " + std::string(exception.what());
            return false;
        }
    }

private:
    DistortionCalibration(const DistortionCalibration& source)
        : width_(source.width_)
        , height_(source.height_)
        , showWidth_(source.showWidth_)
        , showHeight_(source.showHeight_)
        , cameraCenterX_(source.cameraCenterX_)
        , cameraCenterY_(source.cameraCenterY_)
        , alpha_(source.alpha_)
        , useFisheye_(source.useFisheye_)
        , cameraMatrix_(source.cameraMatrix_)
        , distortionCoefficients_(source.distortionCoefficients_)
        , mapX_(source.mapX_)
        , mapY_(source.mapY_)
    {
    }

    void buildMaps(
        int outputWidth,
        int outputHeight,
        int roiX,
        int roiY,
        bool roi,
        cv::Mat& mapX,
        cv::Mat& mapY) const
    {
        std::array<float, 9> adjusted = cameraMatrix_;
        if (roi) {
            if (useFisheye_) {
                adjusted[2] = static_cast<float>(outputWidth / 2);
                adjusted[5] = static_cast<float>(outputHeight / 2);
            }
            else {
                adjusted[2] -= static_cast<float>(roiX);
                adjusted[5] -= static_cast<float>(roiY);
            }
        }

        cv::Mat camera(3, 3, CV_32FC1, adjusted.data());
        const cv::Size imageSize(outputWidth, outputHeight);
        if (useFisheye_) {
            cv::Mat distortion(4, 1, CV_32FC1,
                const_cast<float*>(distortionCoefficients_.data()));
            cv::Mat newCamera;
            const cv::Size newSize = roi
                ? imageSize
                : cv::Size(showWidth_, showHeight_);
            cv::fisheye::estimateNewCameraMatrixForUndistortRectify(
                camera,
                distortion,
                imageSize,
                cv::Matx33d::eye(),
                newCamera,
                alpha_,
                newSize);
            cv::fisheye::initUndistortRectifyMap(
                camera,
                distortion,
                cv::Matx33d::eye(),
                newCamera,
                newSize,
                CV_32FC1,
                mapX,
                mapY);

            const int offsetX = roi
                ? outputWidth / 2 - cameraCenterX_ + roiX
                : width_ / 2 - cameraCenterX_;
            const int offsetY = roi
                ? outputHeight / 2 - cameraCenterY_ + roiY
                : height_ / 2 - cameraCenterY_;
            // Positive shifts move the valid rectangle's origin in the
            // translated image; rebase float maps to the cropped source view.
            mapX -= static_cast<float>((std::max)(0, offsetX));
            mapY -= static_cast<float>((std::max)(0, offsetY));
        }
        else {
            cv::Mat distortion(5, 1, CV_32FC1,
                const_cast<float*>(distortionCoefficients_.data()));
            const cv::Mat newCamera = cv::getOptimalNewCameraMatrix(
                camera,
                distortion,
                imageSize,
                alpha_,
                imageSize,
                nullptr);
            cv::initUndistortRectifyMap(
                camera,
                distortion,
                cv::Mat(),
                newCamera,
                imageSize,
                CV_32FC1,
                mapX,
                mapY);
        }
    }

    int width_ = 0;
    int height_ = 0;
    int showWidth_ = 0;
    int showHeight_ = 0;
    int cameraCenterX_ = 0;
    int cameraCenterY_ = 0;
    double alpha_ = 0.0;
    bool useFisheye_ = false;
    std::array<float, 9> cameraMatrix_{};
    std::array<float, 5> distortionCoefficients_{};

    cv::Mat mapX_;
    cv::Mat mapY_;
    cv::Mat roiMapX_;
    cv::Mat roiMapY_;
    int roiX_ = -1;
    int roiY_ = -1;
    int roiWidth_ = -1;
    int roiHeight_ = -1;

    cv::Mat fallbackScratch_;
};

struct PixelOffset {
    int x = 0;
    int y = 0;
};

bool readOffset(const json& value, PixelOffset& offset)
{
    if (!value.is_object() || !value.contains("X") || !value.contains("Y")
        || !value["X"].is_number() || !value["Y"].is_number()) {
        return false;
    }
    offset.x = static_cast<int>(value["X"].get<double>());
    offset.y = static_cast<int>(value["Y"].get<double>());
    return true;
}

class ColorShiftCalibration final : public CalibrationItem {
public:
    ColorShiftCalibration() = default;

    [[nodiscard]] CalibrationType type() const noexcept override
    {
        return CalibrationType::ColorShift;
    }

    [[nodiscard]] bool requiresDistinctOutput() const noexcept override
    {
        return true;
    }

    [[nodiscard]] std::unique_ptr<CalibrationItem> cloneForContext() const override
    {
        return std::unique_ptr<CalibrationItem>(new ColorShiftCalibration(*this));
    }

    bool load(const json& root, std::string& error)
    {
        fillOffset_ = root.value("fillOffset", false);
        const auto iterator = root.find("offset");
        if (iterator == root.end()) {
            error = "ColorShift is missing offset";
            return false;
        }

        if (iterator->is_array()) {
            if (iterator->size() != channelOffsets_.size()) {
                error = "ColorShift offset array must contain three entries";
                return false;
            }
            for (std::size_t index = 0; index < channelOffsets_.size(); ++index) {
                if (!readOffset((*iterator)[index], channelOffsets_[index])) {
                    error = "ColorShift contains an invalid channel offset";
                    return false;
                }
            }
            hasChannelOffsets_ = true;
            return true;
        }

        if (!readOffset(*iterator, commonOffset_)) {
            error = "ColorShift contains an invalid offset";
            return false;
        }
        return true;
    }

    bool apply(
        const ImageView& raw,
        float*,
        const ExecutionOptions& options,
        std::string& error) override
    {
        fallbackScratch_.create(
            static_cast<int>(raw.height),
            static_cast<int>(raw.width),
            imageType(raw));
        ImageView destination = raw;
        destination.data = fallbackScratch_.data;
        destination.dataLength = fallbackScratch_.total() * fallbackScratch_.elemSize();
        return applyOutOfPlace(raw, destination, options, error)
            && copyResultToRaw(fallbackScratch_, raw, error);
    }

    bool applyOutOfPlace(
        const ImageView& raw,
        const ImageView& destination,
        const ExecutionOptions& options,
        std::string& error) override
    {
        if (raw.channels != 3) {
            error = "ColorShift requires three channels";
            return false;
        }
        if (!validCvDimensions(raw, error)) {
            return false;
        }
        if (!validDistinctOutput(raw, destination, error)) {
            return false;
        }

        try {
            cv::Mat source(
                static_cast<int>(raw.height),
                static_cast<int>(raw.width),
                imageType(raw),
                raw.data);
            cv::Mat output(
                static_cast<int>(destination.height),
                static_cast<int>(destination.width),
                imageType(destination),
                destination.data);

            if (!hasChannelOffsets_) {
                if (commonOffset_.x == 0 && commonOffset_.y == 0) {
                    source.copyTo(output);
                    return true;
                }
                warp(source, output, commonOffset_);
                preserveLegacyFillBehavior(output, commonOffset_);
                return true;
            }

            if (options.interleavedBgr) {
                cv::split(source, planes_);
            }
            else {
                // The legacy non-BGR path consumes planar R/G/B and produces
                // interleaved B/G/R. Keep that byte-layout conversion intact.
                const std::size_t planeBytes = static_cast<std::size_t>(raw.width)
                    * raw.height * (raw.bitsPerChannel / 8);
                planes_.resize(3);
                const int planeType = CV_MAKETYPE(
                    raw.bitsPerChannel == 8 ? CV_8U : CV_16U,
                    1);
                planes_[0] = cv::Mat(
                    static_cast<int>(raw.height),
                    static_cast<int>(raw.width),
                    planeType,
                    raw.data + planeBytes * 2);
                planes_[1] = cv::Mat(
                    static_cast<int>(raw.height),
                    static_cast<int>(raw.width),
                    planeType,
                    raw.data + planeBytes);
                planes_[2] = cv::Mat(
                    static_cast<int>(raw.height),
                    static_cast<int>(raw.width),
                    planeType,
                    raw.data);
            }

            std::array<cv::Mat, 3> mergePlanes;
            for (std::size_t index = 0; index < mergePlanes.size(); ++index) {
                const auto& offset = channelOffsets_[index];
                if (offset.x == 0 && offset.y == 0) {
                    mergePlanes[index] = planes_[index];
                }
                else {
                    warp(planes_[index], warpedPlanes_[index], offset);
                    preserveLegacyFillBehavior(warpedPlanes_[index], offset);
                    mergePlanes[index] = warpedPlanes_[index];
                }
            }

            cv::merge(mergePlanes.data(), mergePlanes.size(), output);
            return true;
        }
        catch (const cv::Exception& exception) {
            error = "ColorShift calibration failed: " + std::string(exception.what());
            return false;
        }
    }

private:
    ColorShiftCalibration(const ColorShiftCalibration& source)
        : fillOffset_(source.fillOffset_)
        , hasChannelOffsets_(source.hasChannelOffsets_)
        , commonOffset_(source.commonOffset_)
        , channelOffsets_(source.channelOffsets_)
    {
    }

    static void warp(const cv::Mat& source, cv::Mat& destination, const PixelOffset& offset)
    {
        const cv::Matx23f translation(
            1.0F, 0.0F, static_cast<float>(offset.x),
            0.0F, 1.0F, static_cast<float>(offset.y));
        cv::warpAffine(
            source,
            destination,
            translation,
            source.size(),
            cv::INTER_LINEAR,
            cv::BORDER_CONSTANT);
    }

    void preserveLegacyFillBehavior(cv::Mat& image, const PixelOffset& offset) const
    {
        if (!fillOffset_) {
            return;
        }

        // Preserve the historical implementation exactly: the horizontal
        // uncovered strip is filled from the adjacent shifted strip, while the
        // vertical strip is copied onto itself and therefore remains unchanged.
        const int width = std::abs(offset.x);
        if (width == 0) {
            return;
        }
        const int sourceX = offset.x < 0
            ? image.cols + offset.x * 2
            : offset.x;
        const int destinationX = offset.x < 0
            ? image.cols + offset.x
            : 0;
        const cv::Mat sourceStrip(image, cv::Rect(sourceX, 0, width, image.rows));
        sourceStrip.copyTo(image(cv::Rect(destinationX, 0, width, image.rows)));
    }

    bool fillOffset_ = false;
    bool hasChannelOffsets_ = false;
    PixelOffset commonOffset_{};
    std::array<PixelOffset, 3> channelOffsets_{};
    std::vector<cv::Mat> planes_;
    std::array<cv::Mat, 3> warpedPlanes_{};
    cv::Mat fallbackScratch_;
};

class ColorDiffCalibration final : public CalibrationItem {
public:
    ColorDiffCalibration() = default;

    [[nodiscard]] CalibrationType type() const noexcept override
    {
        return CalibrationType::ColorDiff;
    }

    [[nodiscard]] bool requiresDistinctOutput() const noexcept override
    {
        return true;
    }

    [[nodiscard]] std::unique_ptr<CalibrationItem> cloneForContext() const override
    {
        return std::unique_ptr<CalibrationItem>(new ColorDiffCalibration(*this));
    }

    [[nodiscard]] std::uint64_t cacheFootprintBytes() const noexcept override
    {
        return sizeof(*this)
            + static_cast<std::uint64_t>(coefficientsGr_.capacity()) * sizeof(double)
            + static_cast<std::uint64_t>(coefficientsGb_.capacity()) * sizeof(double)
            + static_cast<std::uint64_t>(mapGr_.total()) * mapGr_.elemSize()
            + static_cast<std::uint64_t>(mapGb_.total()) * mapGb_.elemSize();
    }

    bool load(const json& root, std::string& error)
    {
        if (!readInt(root, "w", width_, error)
            || !readInt(root, "h", height_, error)
            || !readInt(root, "CenterCol", centerColumn_, error)
            || !readInt(root, "CenterRow", centerRow_, error)
            || !readDouble(root, "MeasDis", measurementDistance_, error)
            || !readDouble(root, "CalibDis", calibrationDistance_, error)
            || !readDoubleArray(root, "ColorDiffCoeffs_GR", coefficientsGr_, error)
            || !readDoubleArray(root, "ColorDiffCoeffs_GB", coefficientsGb_, error)) {
            return false;
        }

        std::vector<double> shiftGr;
        std::vector<double> shiftGb;
        if (!readDoubleArray(root, "ColRowCoeffs_GR", shiftGr, error)
            || !readDoubleArray(root, "ColRowCoeffs_GB", shiftGb, error)) {
            return false;
        }
        if (shiftGr.size() != 2 || shiftGb.size() != 2) {
            error = "ColorDiff ColRowCoeffs arrays must contain two values";
            return false;
        }
        shiftGr_ = { shiftGr[0], shiftGr[1] };
        shiftGb_ = { shiftGb[0], shiftGb[1] };

        if (width_ < 3 || height_ < 3
            || width_ > (std::numeric_limits<std::uint16_t>::max)()
            || height_ > (std::numeric_limits<std::uint16_t>::max)()) {
            error = "ColorDiff dimensions must be between 3 and 65535 pixels";
            return false;
        }
        if (coefficientsGr_.empty() || coefficientsGb_.empty()) {
            error = "ColorDiff coefficient arrays must not be empty";
            return false;
        }
        if (measurementDistance_ == 0.0) {
            error = "ColorDiff MeasDis must not be zero";
            return false;
        }

        try {
            buildMaps(
                width_, height_, centerColumn_, centerRow_,
                mapGr_, mapGb_);
        }
        catch (const cv::Exception& exception) {
            error = "Unable to create ColorDiff maps: " + std::string(exception.what());
            return false;
        }
        return true;
    }

    bool apply(
        const ImageView& raw,
        float*,
        const ExecutionOptions& options,
        std::string& error) override
    {
        fallbackScratch_.create(
            static_cast<int>(raw.height),
            static_cast<int>(raw.width),
            imageType(raw));
        ImageView destination = raw;
        destination.data = fallbackScratch_.data;
        destination.dataLength = fallbackScratch_.total() * fallbackScratch_.elemSize();
        return applyOutOfPlace(raw, destination, options, error)
            && copyResultToRaw(fallbackScratch_, raw, error);
    }

    bool applyOutOfPlace(
        const ImageView& raw,
        const ImageView& destination,
        const ExecutionOptions& options,
        std::string& error) override
    {
        if (!validCvDimensions(raw, error)) {
            return false;
        }
        if (!validDistinctOutput(raw, destination, error)) {
            return false;
        }

        try {
            const cv::Mat* selectedGr = nullptr;
            const cv::Mat* selectedGb = nullptr;
            if (!selectMaps(raw, options, selectedGr, selectedGb, error)) {
                return false;
            }

            if (!options.interleavedBgr) {
                if (raw.channels != 3) {
                    error = "Planar ColorDiff requires ImageView.channels == 3";
                    return false;
                }
                return raw.bitsPerChannel == 8
                    ? applyPlanar<std::uint8_t>(raw, destination, *selectedGr, *selectedGb)
                    : applyPlanar<std::uint16_t>(raw, destination, *selectedGr, *selectedGb);
            }

            switch (options.rgbType) {
            case 0:
                if (raw.channels != 3) {
                    error = "Combined ColorDiff requires a three-channel BGR image";
                    return false;
                }
                return raw.bitsPerChannel == 8
                    ? applyInterleaved<std::uint8_t>(raw, destination, *selectedGr, *selectedGb)
                    : applyInterleaved<std::uint16_t>(raw, destination, *selectedGr, *selectedGb);
            case 1:
            case 2:
            case 3:
                if (raw.channels != 1) {
                    error = "Single-channel ColorDiff requires ImageView.channels == 1";
                    return false;
                }
                if (options.rgbType == 2) {
                    std::memcpy(destination.data, raw.data, imageByteCount(raw));
                    return true;
                }
                return raw.bitsPerChannel == 8
                    ? applySingle<std::uint8_t>(
                        raw,
                        destination,
                        options.rgbType == 1 ? *selectedGr : *selectedGb)
                    : applySingle<std::uint16_t>(
                        raw,
                        destination,
                        options.rgbType == 1 ? *selectedGr : *selectedGb);
            default:
                error = "ColorDiff rgbType must be 0 (BGR), 1 (R), 2 (G), or 3 (B)";
                return false;
            }
        }
        catch (const cv::Exception& exception) {
            error = "ColorDiff calibration failed: " + std::string(exception.what());
            return false;
        }
    }

private:
    ColorDiffCalibration(const ColorDiffCalibration& source)
        : width_(source.width_)
        , height_(source.height_)
        , centerColumn_(source.centerColumn_)
        , centerRow_(source.centerRow_)
        , measurementDistance_(source.measurementDistance_)
        , calibrationDistance_(source.calibrationDistance_)
        , coefficientsGr_(source.coefficientsGr_)
        , coefficientsGb_(source.coefficientsGb_)
        , shiftGr_(source.shiftGr_)
        , shiftGb_(source.shiftGb_)
        , mapGr_(source.mapGr_)
        , mapGb_(source.mapGb_)
    {
    }

    using MapEntry = cv::Vec<std::uint16_t, 2>;

    void buildMaps(
        int width,
        int height,
        int centerColumn,
        int centerRow,
        cv::Mat& destinationGr,
        cv::Mat& destinationGb) const
    {
        destinationGr.create(height, width, CV_16UC2);
        destinationGb.create(height, width, CV_16UC2);
        cv::parallel_for_(cv::Range(0, height), [&](const cv::Range& range) {
            for (int row = range.start; row < range.end; ++row) {
                auto* mapGr = destinationGr.ptr<MapEntry>(row);
                auto* mapGb = destinationGb.ptr<MapEntry>(row);
                for (int column = 0; column < width; ++column) {
                    const double deltaColumn = static_cast<double>(column - centerColumn);
                    const double deltaRow = static_cast<double>(row - centerRow);
                    const double radius = std::sqrt(
                        std::pow(deltaColumn, 2.0) + std::pow(deltaRow, 2.0));
                    const double angle = std::atan2(deltaColumn, deltaRow);
                    const double angleSin = std::sin(angle);
                    const double angleCos = std::cos(angle);

                    const auto writeEntry = [&](const std::vector<double>& coefficients,
                                                const std::array<double, 2>& columnRowShift,
                                                MapEntry& entry) {
                        double targetRadius = 0.0;
                        for (std::size_t index = 0; index < coefficients.size(); ++index) {
                            targetRadius += coefficients[index]
                                * std::pow(radius, static_cast<int>(index));
                        }
                        targetRadius = targetRadius
                            / measurementDistance_ * calibrationDistance_;

                        const double mappedRow = (radius - targetRadius) * angleCos
                            + centerRow - columnRowShift[1];
                        const double mappedColumn = (radius - targetRadius) * angleSin
                            + centerColumn - columnRowShift[0];
                        int sourceRow = static_cast<int>(mappedRow + 0.5);
                        int sourceColumn = static_cast<int>(mappedColumn + 0.5);
                        sourceRow = (std::max)(1, (std::min)(sourceRow, height - 2));
                        sourceColumn = (std::max)(1, (std::min)(sourceColumn, width - 2));

                        entry[0] = static_cast<std::uint16_t>(sourceColumn);
                        entry[1] = static_cast<std::uint16_t>(sourceRow);
                    };
                    writeEntry(coefficientsGr_, shiftGr_, mapGr[column]);
                    writeEntry(coefficientsGb_, shiftGb_, mapGb[column]);
                }
            }
        });
    }

    bool selectMaps(
        const ImageView& raw,
        const ExecutionOptions& options,
        const cv::Mat*& selectedGr,
        const cv::Mat*& selectedGb,
        std::string& error)
    {
        if (!validLegacyRoi(options, width_, height_)) {
            if (raw.width != static_cast<std::uint32_t>(width_)
                || raw.height != static_cast<std::uint32_t>(height_)) {
                error = "RAW dimensions do not match the ColorDiff calibration";
                return false;
            }
            selectedGr = &mapGr_;
            selectedGb = &mapGb_;
            return true;
        }

        const int roiX = static_cast<int>(options.roi[0]);
        const int roiY = static_cast<int>(options.roi[1]);
        const int roiWidth = static_cast<int>(options.roi[2]);
        const int roiHeight = static_cast<int>(options.roi[3]);
        if (roiWidth < 3 || roiHeight < 3) {
            error = "ColorDiff ROI dimensions must be at least 3 by 3";
            return false;
        }
        if (raw.width != static_cast<std::uint32_t>(roiWidth)
            || raw.height != static_cast<std::uint32_t>(roiHeight)) {
            error = "ColorDiff ROI dimensions do not match the RAW buffer";
            return false;
        }

        if (roiX_ != roiX || roiY_ != roiY
            || roiWidth_ != roiWidth || roiHeight_ != roiHeight
            || roiMapGr_.empty() || roiMapGb_.empty()) {
            buildMaps(
                roiWidth, roiHeight, centerColumn_ - roiX, centerRow_ - roiY,
                roiMapGr_, roiMapGb_);
            roiX_ = roiX;
            roiY_ = roiY;
            roiWidth_ = roiWidth;
            roiHeight_ = roiHeight;
        }

        selectedGr = &roiMapGr_;
        selectedGb = &roiMapGb_;
        return true;
    }

    template<typename Sample>
    bool applyInterleaved(
        const ImageView& raw,
        const ImageView& output,
        const cv::Mat& mapGr,
        const cv::Mat& mapGb)
    {
        const int width = static_cast<int>(raw.width);
        const int height = static_cast<int>(raw.height);
        const auto* source = reinterpret_cast<const Sample*>(raw.data);
        auto* destination = reinterpret_cast<Sample*>(output.data);

        cv::parallel_for_(cv::Range(0, height), [&](const cv::Range& range) {
            for (int row = range.start; row < range.end; ++row) {
                const auto* gr = mapGr.ptr<MapEntry>(row);
                const auto* gb = mapGb.ptr<MapEntry>(row);
                const bool borderRow = row == 0 || row == height - 1;
                for (int column = 0; column < width; ++column) {
                    const std::size_t pixel = static_cast<std::size_t>(row) * width + column;
                    const std::size_t destinationIndex = pixel * 3;
                    destination[destinationIndex + 1] = source[destinationIndex + 1];
                    if (borderRow || column == 0 || column == width - 1) {
                        destination[destinationIndex] = 0;
                        destination[destinationIndex + 2] = 0;
                        continue;
                    }

                    const std::size_t sourceRed =
                        (static_cast<std::size_t>(gr[column][1]) * width + gr[column][0]) * 3 + 2;
                    const std::size_t sourceBlue =
                        (static_cast<std::size_t>(gb[column][1]) * width + gb[column][0]) * 3;
                    destination[destinationIndex + 2] = source[sourceRed];
                    destination[destinationIndex] = source[sourceBlue];
                }
            }
        });
        return true;
    }

    template<typename Sample>
    bool applySingle(
        const ImageView& raw,
        const ImageView& output,
        const cv::Mat& map)
    {
        const int width = static_cast<int>(raw.width);
        const int height = static_cast<int>(raw.height);
        const auto* source = reinterpret_cast<const Sample*>(raw.data);
        auto* destination = reinterpret_cast<Sample*>(output.data);

        cv::parallel_for_(cv::Range(0, height), [&](const cv::Range& range) {
            for (int row = range.start; row < range.end; ++row) {
                const auto* mapRow = map.ptr<MapEntry>(row);
                const bool borderRow = row == 0 || row == height - 1;
                for (int column = 0; column < width; ++column) {
                    const std::size_t destinationIndex =
                        static_cast<std::size_t>(row) * width + column;
                    if (borderRow || column == 0 || column == width - 1) {
                        destination[destinationIndex] = 0;
                    }
                    else {
                        const std::size_t sourceIndex =
                            static_cast<std::size_t>(mapRow[column][1]) * width
                            + mapRow[column][0];
                        destination[destinationIndex] = source[sourceIndex];
                    }
                }
            }
        });
        return true;
    }

    template<typename Sample>
    bool applyPlanar(
        const ImageView& raw,
        const ImageView& output,
        const cv::Mat& mapGr,
        const cv::Mat& mapGb)
    {
        const int width = static_cast<int>(raw.width);
        const int height = static_cast<int>(raw.height);
        const std::size_t pixels = static_cast<std::size_t>(width) * height;
        const auto* source = reinterpret_cast<const Sample*>(raw.data);
        auto* destination = reinterpret_cast<Sample*>(output.data);

        cv::parallel_for_(cv::Range(0, height), [&](const cv::Range& range) {
            for (int row = range.start; row < range.end; ++row) {
                const auto* gr = mapGr.ptr<MapEntry>(row);
                const auto* gb = mapGb.ptr<MapEntry>(row);
                const bool borderRow = row == 0 || row == height - 1;
                for (int column = 0; column < width; ++column) {
                    const std::size_t pixel = static_cast<std::size_t>(row) * width + column;
                    destination[pixels + pixel] = source[pixels + pixel];
                    if (borderRow || column == 0 || column == width - 1) {
                        destination[pixel] = 0;
                        destination[pixels * 2 + pixel] = 0;
                        continue;
                    }

                    const std::size_t sourceRed =
                        static_cast<std::size_t>(gr[column][1]) * width + gr[column][0];
                    const std::size_t sourceBlue =
                        static_cast<std::size_t>(gb[column][1]) * width + gb[column][0];
                    destination[pixel] = source[sourceRed];
                    destination[pixels * 2 + pixel] = source[pixels * 2 + sourceBlue];
                }
            }
        });
        return true;
    }

    int width_ = 0;
    int height_ = 0;
    int centerColumn_ = 0;
    int centerRow_ = 0;
    double measurementDistance_ = 0.0;
    double calibrationDistance_ = 0.0;
    std::vector<double> coefficientsGr_;
    std::vector<double> coefficientsGb_;
    std::array<double, 2> shiftGr_{};
    std::array<double, 2> shiftGb_{};

    cv::Mat mapGr_;
    cv::Mat mapGb_;
    cv::Mat roiMapGr_;
    cv::Mat roiMapGb_;
    int roiX_ = -1;
    int roiY_ = -1;
    int roiWidth_ = -1;
    int roiHeight_ = -1;

    cv::Mat fallbackScratch_;
};

class AngleShiftCalibration final : public CalibrationItem {
public:
    AngleShiftCalibration() = default;

    [[nodiscard]] CalibrationType type() const noexcept override
    {
        return CalibrationType::AngleShift;
    }

    [[nodiscard]] bool requiresDistinctOutput() const noexcept override
    {
        return true;
    }

    [[nodiscard]] std::unique_ptr<CalibrationItem> cloneForContext() const override
    {
        return std::unique_ptr<CalibrationItem>(new AngleShiftCalibration(*this));
    }

    bool load(const json& root, std::string& error)
    {
        if (!readInt(root, "optical_center_x", opticalCenterX_, error)
            || !readInt(root, "optical_center_y", opticalCenterY_, error)
            || !readDouble(root, "interpolate_ratio", interpolationRatio_, error)
            || !readInt(root, "coefficient_order", coefficientOrder_, error)
            || !readInt(root, "target_row", targetRows_, error)
            || !readInt(root, "target_col", targetColumns_, error)
            || !readDoubleArray(root, "coeff_r", coefficientsRed_, error)
            || !readDoubleArray(root, "coeff_g", coefficientsGreen_, error)
            || !readDoubleArray(root, "coeff_b", coefficientsBlue_, error)
            || !readDoubleArray(root, "rowColShift", rowColumnShift_, error)) {
            return false;
        }

        if (coefficientOrder_ < 0) {
            error = "AngleShift coefficient_order must not be negative";
            return false;
        }
        const std::size_t expectedCount = static_cast<std::size_t>(coefficientOrder_) + 1;
        if (coefficientsRed_.size() != expectedCount
            || coefficientsGreen_.size() != expectedCount
            || coefficientsBlue_.size() != expectedCount) {
            error = "AngleShift coefficient array length does not match coefficient_order";
            return false;
        }
        if (rowColumnShift_.size() != 2) {
            error = "AngleShift rowColShift must contain two values";
            return false;
        }
        if (interpolationRatio_ <= 0.0) {
            error = "AngleShift interpolate_ratio must be positive";
            return false;
        }
        if (targetRows_ <= 0 || targetColumns_ <= 0) {
            error = "AngleShift target dimensions must be positive";
            return false;
        }
        return true;
    }

    bool apply(
        const ImageView& raw,
        float*,
        const ExecutionOptions& options,
        std::string& error) override
    {
        fallbackScratch_.create(
            static_cast<int>(raw.height),
            static_cast<int>(raw.width),
            imageType(raw));
        ImageView destination = raw;
        destination.data = fallbackScratch_.data;
        destination.dataLength = fallbackScratch_.total() * fallbackScratch_.elemSize();
        return applyOutOfPlace(raw, destination, options, error)
            && copyResultToRaw(fallbackScratch_, raw, error);
    }

    bool applyOutOfPlace(
        const ImageView& raw,
        const ImageView& destination,
        const ExecutionOptions&,
        std::string& error) override
    {
        if (raw.channels != 3) {
            error = "AngleShift requires a three-channel image";
            return false;
        }
        if (!validCvDimensions(raw, error)) {
            return false;
        }
        if (!validDistinctOutput(raw, destination, error)) {
            return false;
        }
        if (raw.width != static_cast<std::uint32_t>(targetColumns_)
            || raw.height != static_cast<std::uint32_t>(targetRows_)) {
            error = "AngleShift target dimensions do not match the RAW buffer";
            return false;
        }

        try {
            if (!ensureMaps(
                    static_cast<int>(raw.width),
                    static_cast<int>(raw.height),
                    error)) {
                return false;
            }

            cv::Mat source(
                static_cast<int>(raw.height),
                static_cast<int>(raw.width),
                imageType(raw),
                raw.data);

            // Resizing the interleaved image is channel-independent and gives
            // the same cubic samples as the legacy split/resize path while
            // eliminating three split/merge full-frame copies.
            cv::resize(
                source,
                resized_,
                cv::Size(resizedColumns_, resizedRows_),
                0.0,
                0.0,
                cv::INTER_CUBIC);
            if (raw.bitsPerChannel == 8) {
                render<std::uint8_t>(destination);
            }
            else {
                render<std::uint16_t>(destination);
            }
            return true;
        }
        catch (const cv::Exception& exception) {
            error = "AngleShift calibration failed: " + std::string(exception.what());
            return false;
        }
    }

private:
    AngleShiftCalibration(const AngleShiftCalibration& source)
        : opticalCenterX_(source.opticalCenterX_)
        , opticalCenterY_(source.opticalCenterY_)
        , interpolationRatio_(source.interpolationRatio_)
        , coefficientOrder_(source.coefficientOrder_)
        , targetRows_(source.targetRows_)
        , targetColumns_(source.targetColumns_)
        , coefficientsRed_(source.coefficientsRed_)
        , coefficientsGreen_(source.coefficientsGreen_)
        , coefficientsBlue_(source.coefficientsBlue_)
        , rowColumnShift_(source.rowColumnShift_)
    {
    }

    using IndexEntry = cv::Vec<std::int32_t, 3>;

    bool ensureMaps(int inputColumns, int inputRows, std::string& error)
    {
        if (inputColumns_ == inputColumns && inputRows_ == inputRows
            && !sourceIndices_.empty()) {
            return true;
        }

        const double scaledColumns = inputColumns * interpolationRatio_;
        const double scaledRows = inputRows * interpolationRatio_;
        if (!std::isfinite(scaledColumns) || !std::isfinite(scaledRows)
            || scaledColumns < 1.0 || scaledRows < 1.0
            || scaledColumns > (std::numeric_limits<std::uint16_t>::max)()
            || scaledRows > (std::numeric_limits<std::uint16_t>::max)()) {
            error = "AngleShift resized dimensions exceed the legacy ushort range";
            return false;
        }

        // The old implementation explicitly casts these sizes to ushort.
        resizedColumns_ = static_cast<std::uint16_t>(scaledColumns);
        resizedRows_ = static_cast<std::uint16_t>(scaledRows);
        const std::uint64_t resizedPixels =
            static_cast<std::uint64_t>(resizedColumns_) * resizedRows_;
        if (resizedPixels > static_cast<std::uint64_t>((std::numeric_limits<std::int32_t>::max)())) {
            error = "AngleShift resized image is too large for cached source indices";
            return false;
        }

        const double opticalColumn = opticalCenterX_ * interpolationRatio_;
        const double opticalRow = opticalCenterY_ * interpolationRatio_;
        sourceIndices_.create(targetRows_, targetColumns_, CV_32SC3);

        cv::parallel_for_(cv::Range(0, targetRows_), [&](const cv::Range& range) {
            for (int row = range.start; row < range.end; ++row) {
                auto* indices = sourceIndices_.ptr<IndexEntry>(row);
                const double deltaRow = row * interpolationRatio_ - opticalRow;
                for (int column = 0; column < targetColumns_; ++column) {
                    const double deltaColumn = column * interpolationRatio_ - opticalColumn;
                    const double radius = std::sqrt(
                        deltaRow * deltaRow + deltaColumn * deltaColumn);
                    const double denominator = radius + 1.0e-12;
                    const double angleCos = deltaColumn / denominator;
                    const double angleSin = deltaRow / denominator;

                    indices[column][0] = correctedIndex(
                        coefficientsBlue_, radius, angleCos, angleSin,
                        opticalColumn, opticalRow);
                    indices[column][1] = correctedIndex(
                        coefficientsGreen_, radius, angleCos, angleSin,
                        opticalColumn, opticalRow);
                    indices[column][2] = correctedIndex(
                        coefficientsRed_, radius, angleCos, angleSin,
                        opticalColumn, opticalRow);
                }
            }
        });

        inputColumns_ = inputColumns;
        inputRows_ = inputRows;
        return true;
    }

    std::int32_t correctedIndex(
        const std::vector<double>& coefficients,
        double radius,
        double angleCos,
        double angleSin,
        double opticalColumn,
        double opticalRow) const
    {
        double correctedRadius = 0.0;
        double radiusPower = 1.0;
        for (const double coefficient : coefficients) {
            correctedRadius += coefficient * radiusPower;
            radiusPower *= radius;
        }

        const double sourceColumnValue = (radius - correctedRadius) * angleCos
            + opticalColumn - rowColumnShift_[1];
        const double sourceRowValue = (radius - correctedRadius) * angleSin
            + opticalRow - rowColumnShift_[0];
        const int sourceColumn = static_cast<int>(sourceColumnValue);
        const int sourceRow = static_cast<int>(sourceRowValue);

        if (sourceColumn < 0 || sourceColumn >= resizedColumns_
            || sourceRow < 0 || sourceRow >= resizedRows_
            || radius > static_cast<double>(resizedRows_) / 2.0
            || radius > static_cast<double>(resizedColumns_) / 2.0) {
            return -1;
        }
        return sourceRow * resizedColumns_ + sourceColumn;
    }

    template<typename Sample>
    void render(const ImageView& output)
    {
        const auto* source = resized_.ptr<Sample>();
        auto* destination = reinterpret_cast<Sample*>(output.data);
        cv::parallel_for_(cv::Range(0, targetRows_), [&](const cv::Range& range) {
            for (int row = range.start; row < range.end; ++row) {
                const auto* indices = sourceIndices_.ptr<IndexEntry>(row);
                for (int column = 0; column < targetColumns_; ++column) {
                    const std::size_t outputPixel =
                        static_cast<std::size_t>(row) * targetColumns_ + column;
                    for (int channel = 0; channel < 3; ++channel) {
                        const auto sourcePixel = indices[column][channel];
                        destination[outputPixel * 3 + channel] = sourcePixel < 0
                            ? static_cast<Sample>(0)
                            : source[static_cast<std::size_t>(sourcePixel) * 3 + channel];
                    }
                }
            }
        });
    }

    int opticalCenterX_ = 0;
    int opticalCenterY_ = 0;
    double interpolationRatio_ = 0.0;
    int coefficientOrder_ = 0;
    int targetRows_ = 0;
    int targetColumns_ = 0;
    std::vector<double> coefficientsRed_;
    std::vector<double> coefficientsGreen_;
    std::vector<double> coefficientsBlue_;
    std::vector<double> rowColumnShift_;

    int inputColumns_ = -1;
    int inputRows_ = -1;
    int resizedColumns_ = 0;
    int resizedRows_ = 0;
    cv::Mat sourceIndices_;
    cv::Mat resized_;
    cv::Mat fallbackScratch_;
};

} // namespace

std::unique_ptr<CalibrationItem> loadGeometricCalibration(
    CalibrationType type,
    const std::filesystem::path& file,
    std::string& error)
{
    if (type != CalibrationType::Distortion
        && type != CalibrationType::ColorShift
        && type != CalibrationType::ColorDiff
        && type != CalibrationType::AngleShift) {
        return nullptr;
    }

    error.clear();
    json root;
    if (!readJson(file, root, error)) {
        return nullptr;
    }

    try {
        switch (type) {
        case CalibrationType::Distortion: {
            auto item = std::make_unique<DistortionCalibration>();
            if (!item->load(root, error)) {
                return nullptr;
            }
            return item;
        }
        case CalibrationType::ColorShift: {
            auto item = std::make_unique<ColorShiftCalibration>();
            if (!item->load(root, error)) {
                return nullptr;
            }
            return item;
        }
        case CalibrationType::ColorDiff: {
            auto item = std::make_unique<ColorDiffCalibration>();
            if (!item->load(root, error)) {
                return nullptr;
            }
            return item;
        }
        case CalibrationType::AngleShift: {
            auto item = std::make_unique<AngleShiftCalibration>();
            if (!item->load(root, error)) {
                return nullptr;
            }
            return item;
        }
        default:
            break;
        }
    }
    catch (const json::exception& exception) {
        error = "Invalid calibration JSON value: " + std::string(exception.what());
        return nullptr;
    }
    catch (const std::exception& exception) {
        error = "Unable to load geometric calibration: " + std::string(exception.what());
        return nullptr;
    }

    error = "Unsupported geometric calibration type";
    return nullptr;
}

} // namespace cvcore::calibration
