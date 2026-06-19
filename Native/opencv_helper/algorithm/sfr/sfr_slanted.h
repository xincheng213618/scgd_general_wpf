#pragma once

/**
 * @file sfr_slanted.h
 * @brief Slanted-edge SFR calculation algorithm
 */

#include <vector>
#include <opencv2/opencv.hpp>

namespace cvcore {
namespace sfr {

struct SFRResult {
    double edgeSlope = 0.0;
    std::vector<double> freq;
    std::vector<double> sfr;
    double mtf10_norm = 0.0;
    double mtf50_norm = 0.0;
    double mtf10_cypix = 0.0;
    double mtf50_cypix = 0.0;

    bool isValid() const {
        return !freq.empty() && !sfr.empty();
    }
};

struct SFRMultiChannelResult {
    int channelCount = 0;
    SFRResult red;
    SFRResult green;
    SFRResult blue;
    SFRResult luminance;

    bool isValid() const {
        if (channelCount == 1) {
            return luminance.isValid();
        }
        if (channelCount == 4) {
            return red.isValid() && green.isValid() && blue.isValid() && luminance.isValid();
        }
        return false;
    }
};

SFRResult calculateSlantedEdgeSFR(const cv::Mat& img,
                                  double pixelPitch = 1.0,
                                  int polynomialDegree = 5,
                                  int binning = 4,
                                  double edgeSlope = -1.0);

SFRMultiChannelResult calculateSlantedEdgeSFRMultiChannel(const cv::Mat& img,
                                                          double pixelPitch = 1.0,
                                                          int polynomialDegree = 5,
                                                          int binning = 4,
                                                          double edgeSlope = -1.0);

} // namespace sfr
} // namespace cvcore
