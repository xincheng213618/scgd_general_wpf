#pragma once

/**
 * @file sfr_cylinder.h
 * @brief Cylinder target SFR calculation algorithm
 *
 * This file implements SFR measurement using cylindrical targets.
 * Originally from packages/sfr/include/sfr/cylinder.h
 */

#include <vector>
#include <opencv2/opencv.hpp>
#include "sfr_base.h"

namespace cvcore {
namespace sfr {

/**
 * @brief Circle structure for cylinder target
 */
struct Circle {
    float cx = 0.0f;  ///< Center X
    float cy = 0.0f;  ///< Center Y
    float r = 0.0f;   ///< Radius
};

/**
 * @brief Fit a circle to image data
 * @param mat Input image
 * @param thresh Threshold for circle detection
 * @return Fitted circle parameters
 */
Circle fitCircle(const cv::Mat& mat, int thresh = 80);

/**
 * @brief Calculate ESF for cylinder target
 * @param mat Input image
 * @param cir Fitted circle
 * @param roi ROI size around circle
 * @param binsize Bin size for sampling
 * @param n_fit Number of fit points
 * @return ESF points (x, intensity)
 */
std::vector<cv::Point2d> cylinderESF(const cv::Mat& mat,
                                      const Circle& cir,
                                      float roi = 15.0f,
                                      float binsize = 0.032f,
                                      int n_fit = 25);

/**
 * @brief Calculate LSF from cylinder ESF
 * @param esf Edge Spread Function points
 * @param n_fit Number of fit points
 * @return LSF points
 */
std::vector<cv::Point2d> cylinderLSF(const std::vector<cv::Point2d>& esf,
                                      int n_fit = 25);

/**
 * @brief Calculate MTF from cylinder LSF
 * @param lsf Line Spread Function points
 * @param ratio Sampling ratio
 * @return MTF points (frequency, MTF)
 */
std::vector<cv::Point2d> cylinderMTF(const std::vector<cv::Point2d>& lsf,
                                      double ratio);

/**
 * @brief Calculate MTF10 from cylinder MTF
 * @param mtf MTF points
 * @return Frequency where MTF = 0.1
 */
double cylinderMTF10(const std::vector<cv::Point2d>& mtf);

/**
 * @brief Cylinder SFR result structure
 */
struct CylinderSFRResult {
    Circle circle;                          ///< Fitted circle
    std::vector<cv::Point2d> esf;           ///< ESF points
    std::vector<cv::Point2d> lsf;           ///< LSF points
    std::vector<cv::Point2d> mtf;           ///< MTF points
    double mtf10 = 0.0;                     ///< MTF10 frequency
    double mtf50 = 0.0;                     ///< MTF50 frequency (calculated if available)

    bool isValid() const {
        return !mtf.empty() && circle.r > 0;
    }
};

/**
 * @brief Calculate SFR using cylinder target method
 * @param mat Input image containing cylinder target
 * @param thresh Threshold for circle detection
 * @param roi ROI size around circle
 * @param binsize Bin size for sampling
 * @param n_fit Number of fit points
 * @return Cylinder SFR result
 */
CylinderSFRResult calculateCylinderSFR(const cv::Mat& mat,
                                        int thresh = 80,
                                        float roi = 15.0f,
                                        float binsize = 0.032f,
                                        int n_fit = 25);

} // namespace sfr
} // namespace cvcore

// Backward compatibility
using cvcore::sfr::Circle;
using cvcore::sfr::CylinderSFRResult;
