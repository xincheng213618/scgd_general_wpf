#pragma once

/**
 * @file sfr_base.h
 * @brief SFR (Spatial Frequency Response) base utilities
 *
 * This file contains common utilities and constants used by SFR algorithms.
 * Originally from packages/sfr/include/sfr/general.h
 */

#include <vector>
#include <opencv2/opencv.hpp>

namespace cvcore {
namespace sfr {

// Constants
constexpr int DEFAULT_FACTOR = 4;
const cv::Matx<double, 1, 3> DEFAULT_KERNEL(-0.5, 0, 0.5);

// Window functions
/**
 * @brief Generate Tukey window (tapered cosine window)
 * @param n0 Window length
 * @param mid Midpoint for tapering
 * @return Window coefficients
 */
std::vector<double> tukey(int n0, double mid);

/**
 * @brief Generate Tukey window with alpha parameter
 * @param n Window length
 * @param alpha Taper ratio (0 = rectangular, 1 = Hann window)
 * @param mid Midpoint for tapering
 * @return Window coefficients
 */
std::vector<double> tukey2(int n, double alpha, double mid);

/**
 * @brief Generate Hamming window
 * @param n Window length
 * @return Window coefficients
 */
inline std::vector<double> hamming(int n) {
    return tukey(n, (n + 1) / 2.0);
}

/**
 * @brief Center shift a signal
 * @param x Input signal
 * @param center New center position
 * @return Shifted signal
 */
std::vector<double> center_shift(const std::vector<double>& x, int center);

/**
 * @brief Polynomial fitting
 * @param x X coordinates
 * @param y Y coordinates
 * @param degree Polynomial degree
 * @return Polynomial coefficients (lowest degree first)
 */
std::vector<double> polyfit(const std::vector<double>& x,
                            const std::vector<double>& y,
                            int degree);

/**
 * @brief Polynomial evaluation
 * @param x X coordinates to evaluate at
 * @param coeff Polynomial coefficients (lowest degree first)
 * @return Evaluated values
 */
std::vector<double> polyval(const std::vector<double>& x,
                            const std::vector<double>& coeff);

/**
 * @brief FIR filter correction for MTF
 * @param n Filter length
 * @param m Filter order
 * @return Correction coefficients
 */
std::vector<double> fir2fix(int n, int m);

} // namespace sfr
} // namespace cvcore

// Backward compatibility
namespace sfr = cvcore::sfr;
