/**
 * @file sfr_export.cpp
 * @brief C interface exports for SFR module
 *
 * This file provides C-compatible exports for the SFR module,
 * maintaining backward compatibility with existing code.
 */

#include "../pch.h"
#include "../include/cvcore/sfr.h"
#include "../include/custom_structs.h"
#include "../include/opencv_media_export.h"
#include "../native_log.h"
#include <opencv2/opencv.hpp>
#include <algorithm>
#include <exception>
#include <combaseapi.h>
#include <nlohmann/json.hpp>
#include <cstring>

using namespace cvcore;

COLORVISIONCORE_API int M_AnalyzeSfrV2(HImage img, RoiRect roi, const char* config, char** result)
{
    if (!result) return -1;
    *result = nullptr;
    try {
        using nlohmann::json;
        const auto settings = config && *config ? json::parse(config) : json::object();
        if (!settings.is_object()) return -1;
        sfr::SfrAnalysisOptions options;
        options.encoding = settings.value("encoding", options.encoding);
        options.decodeExponent = settings.value("decodeExponent", options.decodeExponent);
        options.blackLevel = settings.value("blackLevel", options.blackLevel);
        options.whiteLevel = settings.value("whiteLevel", options.whiteLevel);
        options.minimumContrast = settings.value("minimumContrast", options.minimumContrast);
        options.minimumSnr = settings.value("minimumSnr", options.minimumSnr);
        options.maximumFitRms = settings.value("maximumFitRms", options.maximumFitRms);
        options.displayTarget = settings.value("displayTarget", options.displayTarget);
        cv::Mat image = HImageToMatView(img);
        if (image.empty()) return -2;
        const bool full = roi.x == 0 && roi.y == 0 && roi.width == 0 && roi.height == 0;
        if (full) roi = {0, 0, image.cols, image.rows};
        if (roi.x < 0 || roi.y < 0 || roi.width <= 0 || roi.height <= 0 ||
            roi.width > image.cols || roi.height > image.rows || roi.x > image.cols - roi.width || roi.y > image.rows - roi.height) return -1;
        const auto channels = sfr::analyzeSlantedEdge(image(cv::Rect(roi.x, roi.y, roi.width, roi.height)), options);
        json data = { {"algorithmVersion", "2.0"}, {"edgeLocalization", "lowpass_peak_v1"}, {"unit", "cycles/pixel"}, {"nyquist", 0.5},
            {"roi", {{"x", roi.x}, {"y", roi.y}, {"width", roi.width}, {"height", roi.height}}},
            {"sourceDepth", image.depth()}, {"channels", json::array()} };
        for (const auto& c : channels) {
            json row = {{"channel", c.channel}, {"valid", c.valid}, {"reason", c.reason}, {"warnings", c.warnings},
                {"contrast", c.contrast}, {"noise", c.noise}, {"snr", c.snr}, {"clippedFraction", c.clippedFraction},
                {"angleDegrees", c.angleDegrees}, {"fitRms", c.fitRms}, {"binCoverage", c.binCoverage},
                {"edgeIntercept", c.edgeIntercept}, {"edgeSlope", c.edgeSlope}, {"rotated", c.rotated},
                {"plateausAvailable", c.plateausAvailable}, {"fitAvailable", c.fitAvailable}, {"samplingAvailable", c.samplingAvailable}};
            // Never expose numerical legacy fallbacks on a failed measurement.
            row["frequencies"] = c.valid ? json(c.curve.freq) : json::array();
            row["mtf"] = c.valid ? json(c.curve.sfr) : json::array();
            row["mtf50"] = c.valid && std::isfinite(c.curve.mtf50_cypix) ? json(c.curve.mtf50_cypix) : json(nullptr);
            row["mtf10"] = c.valid && std::isfinite(c.curve.mtf10_cypix) ? json(c.curve.mtf10_cypix) : json(nullptr);
            row["edgePositions"] = c.valid ? json(c.edgePositions) : json::array();
            row["esf"] = c.valid ? json(c.esf) : json::array();
            row["lsfPositions"] = c.valid ? json(c.lsfPositions) : json::array();
            row["lsf"] = c.valid ? json(c.lsf) : json::array();
            data["channels"].push_back(std::move(row));
        }
        const std::string text = data.dump();
        char* buffer = static_cast<char*>(CoTaskMemAlloc(text.size() + 1));
        if (!buffer) return -3;
        std::memcpy(buffer, text.c_str(), text.size() + 1);
        *result = buffer;
        return static_cast<int>(text.size() + 1);
    }
    catch (const std::exception& ex) {
        cvnative::LogException("sfr.export", __func__, -4, "std::exception", ex.what());
        return -4;
    }
    catch (...) { return -6; }
}

namespace
{
template <typename Func>
int GuardSfrExport(const char* operation, Func func) noexcept
{
    try {
        const int result = func();
        if (result < 0) {
            const auto level = result == -3 ? cvnative::LogLevel::Warn : cvnative::LogLevel::Debug;
            cvnative::LogFailure(level, "sfr.export", operation, result);
        }
        return result;
    }
    catch (const cv::Exception& ex) {
        cvnative::LogException("sfr.export", operation, -4, "cv::Exception", ex.what());
        return -4;
    }
    catch (const std::exception& ex) {
        cvnative::LogException("sfr.export", operation, -5, "std::exception", ex.what());
        return -5;
    }
    catch (...) {
        cvnative::LogException("sfr.export", operation, -6, "unknown");
        return -6;
    }
}

void ClearInt(int* value) noexcept
{
    if (value != nullptr) {
        *value = 0;
    }
}

void ClearDouble(double* value) noexcept
{
    if (value != nullptr) {
        *value = 0.0;
    }
}

void ClearSfrMetrics(
    double* mtf10_norm, double* mtf50_norm,
    double* mtf10_cypix, double* mtf50_cypix) noexcept
{
    ClearDouble(mtf10_norm);
    ClearDouble(mtf50_norm);
    ClearDouble(mtf10_cypix);
    ClearDouble(mtf50_cypix);
}

int SfrOutputLength(const sfr::SFRResult& result, int maxLen) noexcept
{
    int length = static_cast<int>(std::min(result.freq.size(), result.sfr.size()));
    return std::min(length, maxLen);
}

void FillFrequency(const sfr::SFRResult& result, double* freq, int length)
{
    std::copy_n(result.freq.data(), length, freq);
}

void FillSfrValuesAndMetrics(const sfr::SFRResult& result,
    double* sfr_out, int length,
    double* mtf10_norm, double* mtf50_norm,
    double* mtf10_cypix, double* mtf50_cypix)
{
    std::copy_n(result.sfr.data(), length, sfr_out);
    *mtf10_norm = result.mtf10_norm;
    *mtf50_norm = result.mtf50_norm;
    *mtf10_cypix = result.mtf10_cypix;
    *mtf50_cypix = result.mtf50_cypix;
}

void FillSFRResult(const sfr::SFRResult& result,
    double* freq, double* sfr_out, int maxLen, int* outLen,
    double* mtf10_norm, double* mtf50_norm,
    double* mtf10_cypix, double* mtf50_cypix)
{
    int length = SfrOutputLength(result, maxLen);
    FillFrequency(result, freq, length);
    FillSfrValuesAndMetrics(result, sfr_out, length,
        mtf10_norm, mtf50_norm, mtf10_cypix, mtf50_cypix);
    *outLen = length;
}
}

COLORVISIONCORE_API int M_CalSFR(
    HImage img,
    double del,
    RoiRect roi,
    double* freq,
    double* sfr,
    int    maxLen,
    int* outLen,
    double* mtf10_norm,
    double* mtf50_norm,
    double* mtf10_cypix,
    double* mtf50_cypix)
{
    return GuardSfrExport(__func__, [&]() -> int {
        ClearInt(outLen);
        ClearSfrMetrics(mtf10_norm, mtf50_norm, mtf10_cypix, mtf50_cypix);

        if (!freq || !sfr || !outLen ||
            !mtf10_norm || !mtf50_norm || !mtf10_cypix || !mtf50_cypix ||
            maxLen <= 0) {
            return -1;
        }

        cv::Mat mat = HImageToMatView(img);
        if (mat.empty()) return -2;

        cv::Rect mroi(roi.x, roi.y, roi.width, roi.height);
        bool use_roi = (mroi.width > 0 && mroi.height > 0 &&
            (mroi & cv::Rect(0, 0, mat.cols, mat.rows)) == mroi);
        mat = use_roi ? mat(mroi) : mat;

        auto result = sfr::calculateSlantedEdgeSFR(mat, del, /*npol=*/5, /*nbin=*/4);

        if (!result.isValid()) {
            return -3;
        }

        FillSFRResult(result, freq, sfr, maxLen, outLen,
            mtf10_norm, mtf50_norm, mtf10_cypix, mtf50_cypix);

        return 0;
        });
}

COLORVISIONCORE_API int M_CalSFRMultiChannel(
    HImage img,
    double del,
    RoiRect roi,
    double* freq,
    double* sfr_r,
    double* sfr_g,
    double* sfr_b,
    double* sfr_l,
    int    maxLen,
    int* outLen,
    int* channelCount,
    double* mtf10_norm_r, double* mtf50_norm_r, double* mtf10_cypix_r, double* mtf50_cypix_r,
    double* mtf10_norm_g, double* mtf50_norm_g, double* mtf10_cypix_g, double* mtf50_cypix_g,
    double* mtf10_norm_b, double* mtf50_norm_b, double* mtf10_cypix_b, double* mtf50_cypix_b,
    double* mtf10_norm_l, double* mtf50_norm_l, double* mtf10_cypix_l, double* mtf50_cypix_l)
{
    return GuardSfrExport(__func__, [&]() -> int {
        ClearInt(outLen);
        ClearInt(channelCount);
        ClearSfrMetrics(mtf10_norm_r, mtf50_norm_r, mtf10_cypix_r, mtf50_cypix_r);
        ClearSfrMetrics(mtf10_norm_g, mtf50_norm_g, mtf10_cypix_g, mtf50_cypix_g);
        ClearSfrMetrics(mtf10_norm_b, mtf50_norm_b, mtf10_cypix_b, mtf50_cypix_b);
        ClearSfrMetrics(mtf10_norm_l, mtf50_norm_l, mtf10_cypix_l, mtf50_cypix_l);

        if (!freq || !sfr_l || !outLen || !channelCount ||
            !mtf10_norm_l || !mtf50_norm_l || !mtf10_cypix_l || !mtf50_cypix_l ||
            maxLen <= 0) {
            return -1;
        }

        cv::Mat mat = HImageToMatView(img);
        if (mat.empty()) return -2;

        cv::Rect mroi(roi.x, roi.y, roi.width, roi.height);
        bool use_roi = (mroi.width > 0 && mroi.height > 0 &&
            (mroi & cv::Rect(0, 0, mat.cols, mat.rows)) == mroi);
        mat = use_roi ? mat(mroi) : mat;

        const bool isRGB = (mat.channels() == 3 || mat.channels() == 4);
        if (mat.channels() != 1 && !isRGB) {
            return -1;
        }

        if (isRGB) {
            if (!sfr_r || !sfr_g || !sfr_b ||
                !mtf10_norm_r || !mtf50_norm_r || !mtf10_cypix_r || !mtf50_cypix_r ||
                !mtf10_norm_g || !mtf50_norm_g || !mtf10_cypix_g || !mtf50_cypix_g ||
                !mtf10_norm_b || !mtf50_norm_b || !mtf10_cypix_b || !mtf50_cypix_b) {
                return -1;
            }
        }

        auto result = sfr::calculateSlantedEdgeSFRMultiChannel(mat, del, 5, 4);
        if (!result.isValid()) {
            *channelCount = 0;
            return -3;
        }

        *channelCount = result.channelCount;
        if (result.channelCount == 4) {
            int length = std::min(
                std::min(SfrOutputLength(result.red, maxLen), SfrOutputLength(result.green, maxLen)),
                std::min(SfrOutputLength(result.blue, maxLen), SfrOutputLength(result.luminance, maxLen)));
            FillFrequency(result.luminance, freq, length);
            FillSfrValuesAndMetrics(result.red, sfr_r, length,
                mtf10_norm_r, mtf50_norm_r, mtf10_cypix_r, mtf50_cypix_r);
            FillSfrValuesAndMetrics(result.green, sfr_g, length,
                mtf10_norm_g, mtf50_norm_g, mtf10_cypix_g, mtf50_cypix_g);
            FillSfrValuesAndMetrics(result.blue, sfr_b, length,
                mtf10_norm_b, mtf50_norm_b, mtf10_cypix_b, mtf50_cypix_b);
            FillSfrValuesAndMetrics(result.luminance, sfr_l, length,
                mtf10_norm_l, mtf50_norm_l, mtf10_cypix_l, mtf50_cypix_l);
            *outLen = length;
        }
        else {
            FillSFRResult(result.luminance, freq, sfr_l, maxLen, outLen,
                mtf10_norm_l, mtf50_norm_l, mtf10_cypix_l, mtf50_cypix_l);
        }

        return 0;
        });
}

COLORVISIONCORE_API int M_LocateBmwTargetV1(HImage img, RoiRect roi, char** result)
{
    if (!result) return -1;
    *result=nullptr;
    try {
        auto image=HImageToMatView(img);
        if(image.empty()) return -2;
        if(roi.x<0||roi.y<0||roi.width<=0||roi.height<=0||roi.width>image.cols||roi.height>image.rows||
            roi.x>image.cols-roi.width||roi.y>image.rows-roi.height||roi.width>8192||roi.height>8192||
            static_cast<int64_t>(roi.width)*roi.height>16000000) return -1;
        auto target=sfr::locateBmwTarget(image(cv::Rect(roi.x,roi.y,roi.width,roi.height)));
        auto rect=[&](cv::Rect r) { return nlohmann::json{{"x",r.empty()?0:r.x+roi.x},{"y",r.empty()?0:r.y+roi.y},{"width",r.width},{"height",r.height}}; };
        nlohmann::json data={{"located",target.located},{"reason",target.reason},{"targetRoi",rect(target.target)},
            {"centerX",target.located?target.center.x+roi.x:0},{"centerY",target.located?target.center.y+roi.y:0},{"edges",nlohmann::json::array()}};
        for(int id=0;id<4;++id) data["edges"].push_back({{"id",id},{"roi",rect(target.edges[id])}});
        auto text=data.dump();
        auto buffer=static_cast<char*>(CoTaskMemAlloc(text.size()+1));
        if(!buffer) return -3;
        std::memcpy(buffer,text.c_str(),text.size()+1); *result=buffer;
        return static_cast<int>(text.size()+1);
    } catch(const std::exception& ex) { cvnative::LogException("sfr.bmw",__func__,-4,"std::exception",ex.what()); return -4; }
    catch(...) { return -6; }
}
