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

using namespace cvcore;

// Helper function to convert SFRResult to output parameters
static void fillSFRResult(const sfr::SFRResult& result,
    double* freq, double* sfr_out, int maxLen, int* outLen,
    double* mtf10_norm, double* mtf50_norm,
    double* mtf10_cypix, double* mtf50_cypix) {

    int N = static_cast<int>(result.freq.size());
    if (N > maxLen) N = maxLen;

    for (int i = 0; i < N; ++i) {
        freq[i] = result.freq[i];
        sfr_out[i] = result.sfr[i];
    }

    *outLen = N;
    *mtf10_norm = result.mtf10_norm;
    *mtf50_norm = result.mtf50_norm;
    *mtf10_cypix = result.mtf10_cypix;
    *mtf50_cypix = result.mtf50_cypix;
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
    if (!freq || !sfr || !outLen ||
        !mtf10_norm || !mtf50_norm || !mtf10_cypix || !mtf50_cypix) {
        return -1;
    }

    cv::Mat mat(img.rows, img.cols, img.type(), img.pData);
    if (mat.empty()) return -2;

    cv::Rect mroi(roi.x, roi.y, roi.width, roi.height);
    bool use_roi = (mroi.width > 0 && mroi.height > 0 &&
        (mroi & cv::Rect(0, 0, mat.cols, mat.rows)) == mroi);
    mat = use_roi ? mat(mroi) : mat;

    auto result = sfr::calculateSlantedEdgeSFR(mat, del, /*npol=*/5, /*nbin=*/4);

    if (!result.isValid()) {
        *outLen = 0;
        return -3;
    }

    fillSFRResult(result, freq, sfr, maxLen, outLen,
        mtf10_norm, mtf50_norm, mtf10_cypix, mtf50_cypix);

    return 0;
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
    if (!freq || !sfr_l || !outLen || !channelCount ||
        !mtf10_norm_l || !mtf50_norm_l || !mtf10_cypix_l || !mtf50_cypix_l) {
        return -1;
    }

    cv::Mat mat(img.rows, img.cols, img.type(), img.pData);
    if (mat.empty()) return -2;

    cv::Rect mroi(roi.x, roi.y, roi.width, roi.height);
    bool use_roi = (mroi.width > 0 && mroi.height > 0 &&
        (mroi & cv::Rect(0, 0, mat.cols, mat.rows)) == mroi);
    mat = use_roi ? mat(mroi) : mat;

    cv::Mat mat8;
    if (mat.depth() != CV_8U) {
        cv::Mat tmp;
        cv::normalize(mat, tmp, 0, 255, cv::NORM_MINMAX);
        tmp.convertTo(mat8, CV_8U);
    }
    else {
        mat8 = mat;
    }

    bool isRGB = (mat8.channels() == 3 || mat8.channels() == 4);

    if (isRGB) {
        *channelCount = 4;

        if (!sfr_r || !sfr_g || !sfr_b ||
            !mtf10_norm_r || !mtf50_norm_r || !mtf10_cypix_r || !mtf50_cypix_r ||
            !mtf10_norm_g || !mtf50_norm_g || !mtf10_cypix_g || !mtf50_cypix_g ||
            !mtf10_norm_b || !mtf50_norm_b || !mtf10_cypix_b || !mtf50_cypix_b) {
            return -1;
        }

        std::vector<cv::Mat> channels;
        cv::split(mat8, channels);

        // R channel
        auto result_r = sfr::calculateSlantedEdgeSFR(channels[2], del, 5, 4);
        fillSFRResult(result_r, freq, sfr_r, maxLen, outLen,
            mtf10_norm_r, mtf50_norm_r, mtf10_cypix_r, mtf50_cypix_r);

        // G channel
        auto result_g = sfr::calculateSlantedEdgeSFR(channels[1], del, 5, 4);
        fillSFRResult(result_g, freq, sfr_g, maxLen, outLen,
            mtf10_norm_g, mtf50_norm_g, mtf10_cypix_g, mtf50_cypix_g);

        // B channel
        auto result_b = sfr::calculateSlantedEdgeSFR(channels[0], del, 5, 4);
        fillSFRResult(result_b, freq, sfr_b, maxLen, outLen,
            mtf10_norm_b, mtf50_norm_b, mtf10_cypix_b, mtf50_cypix_b);

        // L channel (Y = 0.213*R + 0.715*G + 0.072*B)
        cv::Mat gray;
        cv::cvtColor(mat8, gray, cv::COLOR_BGR2GRAY);
        auto result_l = sfr::calculateSlantedEdgeSFR(gray, del, 5, 4);
        fillSFRResult(result_l, freq, sfr_l, maxLen, outLen,
            mtf10_norm_l, mtf50_norm_l, mtf10_cypix_l, mtf50_cypix_l);
    }
    else {
        *channelCount = 1;
        auto result = sfr::calculateSlantedEdgeSFR(mat8, del, 5, 4);
        fillSFRResult(result, freq, sfr_l, maxLen, outLen,
            mtf10_norm_l, mtf50_norm_l, mtf10_cypix_l, mtf50_cypix_l);
    }

    return 0;
}

// New C++ interface exports
COLORVISIONCORE_API sfr::SFRResult CalSFR_CPP(const cv::Mat& img,
    double del,
    int npol,
    int nbin,
    double vslope) {
    return sfr::calculateSlantedEdgeSFR(img, del, npol, nbin, vslope);
}

COLORVISIONCORE_API sfr::CylinderSFRResult CalCylinderSFR_CPP(const cv::Mat& mat,
    int thresh,
    float roi,
    float binsize,
    int n_fit) {
    return sfr::calculateCylinderSFR(mat, thresh, roi, binsize, n_fit);
}
