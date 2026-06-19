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
#include <opencv2/opencv.hpp>
#include <algorithm>
#include <exception>

using namespace cvcore;

namespace
{
template <typename Func>
int GuardSfrExport(Func func) noexcept
{
    try {
        return func();
    }
    catch (const cv::Exception&) {
        return -4;
    }
    catch (const std::exception&) {
        return -5;
    }
    catch (...) {
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
    return GuardSfrExport([&]() -> int {
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
    return GuardSfrExport([&]() -> int {
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
