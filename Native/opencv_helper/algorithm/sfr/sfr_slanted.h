#pragma once

/**
 * @file sfr_slanted.h
 * @brief Slanted-edge SFR calculation algorithm
 */

#include <vector>
#include <string>
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

// Diagnostic API. Legacy SFRResult and its capped metrics remain unchanged.
struct SfrAnalysisOptions {
    std::string encoding = "unknown"; // unknown, linear, srgb, power
    double decodeExponent = 2.2;
    double blackLevel = 0.0;
    double whiteLevel = 0.0; // 0 selects type full scale: 255, 65535 or 1
    double minimumContrast = 0.02;
    double minimumSnr = 10.0;
    double maximumFitRms = 0.35;
    bool displayTarget = false;
};

struct SfrChannelAnalysis {
    std::string channel;
    bool valid = false;
    std::string reason;
    std::vector<std::string> warnings;
    double contrast = 0.0;
    double noise = 0.0;
    double snr = 0.0;
    double clippedFraction = 0.0;
    double angleDegrees = 0.0;
    double fitRms = 0.0;
    double binCoverage = 0.0;
    double edgeIntercept = 0.0;
    double edgeSlope = 0.0;
    bool rotated = false;
    bool plateausAvailable = false;
    bool fitAvailable = false;
    bool samplingAvailable = false;
    SFRResult curve;
    std::vector<double> edgePositions;
    std::vector<double> esf;
    std::vector<double> lsfPositions;
    std::vector<double> lsf;
};

// A single slanted edge; curves use cycles per input pixel, ESF/LSF positions use input pixels.
// Quality thresholds are diagnostic defaults, not ISO or customer acceptance limits.
std::vector<SfrChannelAnalysis> analyzeSlantedEdge(const cv::Mat& image, const SfrAnalysisOptions& options);

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
