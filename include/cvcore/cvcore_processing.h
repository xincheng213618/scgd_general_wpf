#pragma once

/**
 * @file cvcore_processing.h
 * @brief Unified image processing operations interface
 */

#include "cvcore_image.h"

namespace cvcore {

// ============================================================================
// Processing Options Base
// ============================================================================

struct ProcessingOptions {
    ProcessingBackend backend = ProcessingBackend::Auto;
    ProcessingContext* context = nullptr;  // nullptr = use default
    bool async = false;
    ProgressCallback progressCallback;
};

// ============================================================================
// Color Processing
// ============================================================================

struct WhiteBalanceOptions : ProcessingOptions {
    double redBalance = 1.0;
    double greenBalance = 1.0;
    double blueBalance = 1.0;
};

struct GammaCorrectionOptions : ProcessingOptions {
    double gamma = 1.0;
};

struct BrightnessContrastOptions : ProcessingOptions {
    double alpha = 1.0;   // contrast
    double beta = 0.0;    // brightness
};

struct AutoLevelsOptions : ProcessingOptions {
    float lowCut = 0.4f;   // percentage
    float highCut = 0.4f;  // percentage
};

struct PseudoColorOptions : ProcessingOptions {
    cv::ColormapTypes colormap = cv::COLORMAP_JET;
    uint32_t minValue = 0;
    uint32_t maxValue = 255;
    bool autoRange = false;
    uint32_t dataMin = 0;
    uint32_t dataMax = 65535;
};

CV_CORE_API Result<Image> adjustWhiteBalance(const Image& src, const WhiteBalanceOptions& options);
CV_CORE_API Result<Image> applyGammaCorrection(const Image& src, const GammaCorrectionOptions& options);
CV_CORE_API Result<Image> adjustBrightnessContrast(const Image& src, const BrightnessContrastOptions& options);
CV_CORE_API Result<Image> autoLevels(const Image& src, const AutoLevelsOptions& options);
CV_CORE_API Result<Image> applyPseudoColor(const Image& src, const PseudoColorOptions& options);
CV_CORE_API Result<Image> automaticColorAdjustment(const Image& src, const ProcessingOptions& options);
CV_CORE_API Result<Image> automaticToneAdjustment(const Image& src, float clipPercent, const ProcessingOptions& options);

// ============================================================================
// Filtering
// ============================================================================

struct GaussianBlurOptions : ProcessingOptions {
    int kernelSize = 5;
    double sigma = 0.0;  // 0 = auto from kernel size
};

struct MedianBlurOptions : ProcessingOptions {
    int kernelSize = 3;
};

struct SharpenOptions : ProcessingOptions {
    double amount = 1.0;
    double radius = 1.0;
};

CV_CORE_API Result<Image> applyGaussianBlur(const Image& src, const GaussianBlurOptions& options);
CV_CORE_API Result<Image> applyMedianBlur(const Image& src, const MedianBlurOptions& options);
CV_CORE_API Result<Image> applySharpen(const Image& src, const SharpenOptions& options);
CV_CORE_API Result<Image> applyCannyEdge(const Image& src, double threshold1, double threshold2, const ProcessingOptions& options);
CV_CORE_API Result<Image> applyHistogramEqualization(const Image& src, const ProcessingOptions& options);

// ============================================================================
// Geometric Transformations
// ============================================================================

struct ResizeOptions : ProcessingOptions {
    int width = 0;
    int height = 0;
    double fx = 0.0;
    double fy = 0.0;
    int interpolation = cv::INTER_LINEAR;
};

struct CropOptions : ProcessingOptions {
    int x = 0;
    int y = 0;
    int width = 0;
    int height = 0;
};

CV_CORE_API Result<Image> resize(const Image& src, const ResizeOptions& options);
CV_CORE_API Result<Image> crop(const Image& src, const CropOptions& options);
CV_CORE_API Result<Image> rotate(const Image& src, double angle, const ProcessingOptions& options);
CV_CORE_API Result<Image> flip(const Image& src, int flipCode, const ProcessingOptions& options);

// ============================================================================
// Focus Stacking (Fusion)
// ============================================================================

struct FocusStackingOptions : ProcessingOptions {
    int step = 2;                    // Half-window size for curve fitting
    int numStreams = 2;              // CUDA streams for async processing
    bool useMultiStream = false;     // Enable multi-stream processing
};

/**
 * @brief Focus stacking (depth of field extension)
 * @param images Sequence of images with different focus planes
 * @param options Processing options
 * @return Fused image with extended depth of field
 */
CV_CORE_API Result<Image> focusStacking(const ImageSequence& images, const FocusStackingOptions& options);

// Legacy name alias
inline Result<Image> fusion(const ImageSequence& images, const FocusStackingOptions& options) {
    return focusStacking(images, options);
}

// ============================================================================
// Feature Detection
// ============================================================================

struct CornerDetectionOptions : ProcessingOptions {
    int threshold = -1;  // -1 = use Otsu
    bool removeBorder = true;
};

struct LightBeadDetectionOptions : ProcessingOptions {
    int threshold = 20;
    int minSize = 2;
    int maxSize = 20;
    int rows = 0;  // Expected grid rows (0 = auto)
    int cols = 0;  // Expected grid cols (0 = auto)
};

struct CornerDetectionResult {
    std::vector<cv::Point2f> corners;
    cv::Rect boundingRect;
    double confidence;
};

struct LightBeadDetectionResult {
    std::vector<cv::Point> centers;
    std::vector<cv::Point> missingCenters;
    std::vector<cv::Point> hull;
    double convexHullArea;
    cv::Rect boundingRect;
};

CV_CORE_API Result<CornerDetectionResult> findLuminousAreaCorners(const Image& src, const CornerDetectionOptions& options);
CV_CORE_API Result<cv::Rect> findLuminousArea(const Image& src, const CornerDetectionOptions& options);
CV_CORE_API Result<LightBeadDetectionResult> findLightBeads(const Image& src, const LightBeadDetectionOptions& options);

// ============================================================================
// Utility Functions
// ============================================================================

CV_CORE_API Result<Image> removeMoire(const Image& src, const ProcessingOptions& options);
CV_CORE_API Result<Image> drawPoints(const Image& src, const std::vector<cv::Point>& points,
                                      int radius, const cv::Scalar& color, int thickness,
                                      const ProcessingOptions& options);

// Batch processing
CV_CORE_API Result<std::vector<Image>> processBatch(
    const ImageSequence& images,
    std::function<Result<Image>(const Image&)> processor,
    const ProcessingOptions& options);

// ============================================================================
// Processing with Statistics
// ============================================================================

template<typename Func, typename... Args>
auto processWithStats(Func&& func, Args&&... args) -> std::pair<decltype(func(args...)), ProcessingStats> {
    auto start = std::chrono::high_resolution_clock::now();

    auto result = std::forward<Func>(func)(std::forward<Args>(args)...);

    auto end = std::chrono::high_resolution_clock::now();
    ProcessingStats stats;
    stats.duration = std::chrono::duration_cast<std::chrono::microseconds>(end - start);

    return { result, stats };
}

} // namespace cvcore
