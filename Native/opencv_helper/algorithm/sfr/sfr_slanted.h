#pragma once

/**
 * @file sfr_slanted.h
 * @brief Slanted-edge SFR calculation algorithm
 *
 * This file implements the ISO 12233 slanted-edge SFR measurement method.
 * Originally from packages/sfr/include/sfr/slanted.h
 */

#include <tuple>
#include <vector>
#include <opencv2/opencv.hpp>
#include "sfr_base.h"

namespace cvcore {
namespace sfr {

/**
 * @brief Auto-rotate image to make edge vertical
 * @param src Input image
 * @return Rotated image
 */
cv::Mat auto_rotate_vertical(const cv::Mat& src);

/**
 * @brief Fit a line to edge data using centroid method
 * @param mat Edge ROI image
 * @return (slope, offset) tuple
 */
std::tuple<double, double> line_fit(const cv::Mat& mat);

/**
 * @brief Polynomial edge fitting
 * @param mat Edge ROI image
 * @param npol Polynomial degree
 * @param loc_out Output edge locations per row (optional)
 * @return Polynomial coefficients
 */
std::vector<double> poly_edge_fit(const cv::Mat& mat,
                                  int npol,
                                  std::vector<double>* loc_out = nullptr);

/**
 * @brief Calculate Edge Spread Function (ESF)
 * @param mat Edge ROI image
 * @param fitme Fitted edge polynomial coefficients
 * @param nbin Binning factor for supersampling
 * @return ESF values
 */
std::vector<double> esf(const cv::Mat& mat,
                        const std::vector<double>& fitme,
                        int nbin);

/**
 * @brief Calculate Line Spread Function (LSF) from ESF
 * @param esf Edge Spread Function
 * @return Line Spread Function
 */
std::vector<double> lsf(const std::vector<double>& esf);

/**
 * @brief Calculate MTF from LSF
 * @param lsf Line Spread Function
 * @return MTF values (modulation transfer function)
 */
std::vector<double> mtf(const std::vector<double>& lsf);

/**
 * @brief Calculate MTF10 frequency
 * @param mtf MTF values
 * @return Frequency where MTF = 0.1
 */
double mtf10(const std::vector<double>& mtf);

/**
 * @brief Calculate MTF50 frequency
 * @param mtf MTF values
 * @return Frequency where MTF = 0.5
 */
double mtf50(const std::vector<double>& mtf);

/**
 * @brief SFR calculation result structure
 */
struct SFRResult {
    double vslope = 0.0;                    ///< Edge slope
    std::vector<double> freq;               ///< Frequency axis (cy/pixel)
    std::vector<double> sfr;                ///< SFR values
    double mtf10_norm = 0.0;                ///< Normalized MTF10 (0~1)
    double mtf50_norm = 0.0;                ///< Normalized MTF50 (0~1)
    double mtf10_cypix = 0.0;               ///< MTF10 frequency (cy/pixel)
    double mtf50_cypix = 0.0;               ///< MTF50 frequency (cy/pixel)

    bool isValid() const {
        return !freq.empty() && !sfr.empty();
    }
};

/**
 * @brief Calculate SFR using slanted-edge method
 * @param img Input image (ROI containing slanted edge)
 * @param del Pixel pitch (um per pixel)
 * @param npol Polynomial degree for edge fitting (default: 5)
 * @param nbin Binning factor for supersampling (default: 4)
 * @param vslope Edge slope (-1 for auto-detect)
 * @return SFR calculation result
 */
SFRResult calculateSlantedEdgeSFR(const cv::Mat& img,
                                   double del = 1.0,
                                   int npol = 5,
                                   int nbin = 4,
                                   double vslope = -1);

// Legacy name alias
inline SFRResult CalSFR(const cv::Mat& img,
                         double del = 1.0,
                         int npol = 5,
                         int nbin = 4,
                         double vslope = -1) {
    return calculateSlantedEdgeSFR(img, del, npol, nbin, vslope);
}

} // namespace sfr
} // namespace cvcore

// Backward compatibility
using cvcore::sfr::SFRResult;
using cvcore::sfr::CalSFR;
