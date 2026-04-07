// Optimized Image Processing Algorithms with OpenCV Parallel Framework
// This file contains optimized versions of algorithms from algorithm.cpp

#include "pch.h"
#include "algorithm.h"
#include <opencv2/core/parallel/parallel_backend.hpp>
#include <opencv2/core/parallel.hpp>

// ============================================================================
// Parallel Histogram Calculation
// ============================================================================

class ParallelHistogramCalculator : public cv::ParallelLoopBody {
private:
    const cv::Mat& src_;
    std::vector<std::vector<int>>& histograms_;

public:
    ParallelHistogramCalculator(const cv::Mat& src, std::vector<std::vector<int>>& histograms)
        : src_(src), histograms_(histograms) {}

    void operator()(const cv::Range& range) const override {
        // Thread-local histogram to avoid contention
        std::vector<int> localBHist(256, 0);
        std::vector<int> localGHist(256, 0);
        std::vector<int> localRHist(256, 0);

        for (int y = range.start; y < range.end; ++y) {
            const cv::Vec3b* row = src_.ptr<cv::Vec3b>(y);
            for (int x = 0; x < src_.cols; ++x) {
                localBHist[row[x][0]]++;
                localGHist[row[x][1]]++;
                localRHist[row[x][2]]++;
            }
        }

        // Merge into global histograms (thread-safe)
        for (int i = 0; i < 256; ++i) {
            cv::utils::atomic_fetch_add(&histograms_[0][i], localBHist[i]);
            cv::utils::atomic_fetch_add(&histograms_[1][i], localGHist[i]);
            cv::utils::atomic_fetch_add(&histograms_[2][i], localRHist[i]);
        }
    }
};

void autoLevelsAdjustParallel(cv::Mat& src, cv::Mat& dst)
{
    CV_Assert(!src.empty() && src.channels() == 3);

    const float LowCut = 0.4f;
    const float HighCut = 0.4f;
    const int TotalPixels = src.cols * src.rows;
    const float LowTh = LowCut * 0.01f * TotalPixels;
    const float HighTh = HighCut * 0.01f * TotalPixels;

    // Parallel histogram calculation
    std::vector<std::vector<int>> histograms(3, std::vector<int>(256, 0));
    cv::parallel_for_(cv::Range(0, src.rows),
                      ParallelHistogramCalculator(src, histograms));

    // Find min/max for each channel (sequential, negligible overhead)
    auto findMinMax = [](const std::vector<int>& hist, float lowTh, float highTh) {
        int minVal = 0, maxVal = 255;
        int sum = 0;
        for (int i = 0; i < 256; ++i) {
            sum += hist[i];
            if (sum >= lowTh) { minVal = i; break; }
        }
        sum = 0;
        for (int i = 255; i >= 0; --i) {
            sum += hist[i];
            if (sum >= highTh) { maxVal = i; break; }
        }
        return std::make_pair(minVal, maxVal);
    };

    auto [BMin, BMax] = findMinMax(histograms[0], LowTh, HighTh);
    auto [GMin, GMax] = findMinMax(histograms[1], LowTh, HighTh);
    auto [RMin, RMax] = findMinMax(histograms[2], LowTh, HighTh);

    // Build lookup tables
    auto buildLUT = [](int minVal, int maxVal) {
        std::vector<uchar> lut(256);
        float range = maxVal - minVal;
        if (range <= 0) {
            std::fill(lut.begin(), lut.end(), 0);
            return lut;
        }
        for (int i = 0; i <= minVal; ++i) lut[i] = 0;
        for (int i = maxVal; i < 256; ++i) lut[i] = 255;
        for (int i = minVal + 1; i < maxVal; ++i) {
            lut[i] = cv::saturate_cast<uchar>((i - minVal) / range * 255.0f);
        }
        return lut;
    };

    auto BTable = buildLUT(BMin, BMax);
    auto GTable = buildLUT(GMin, GMax);
    auto RTable = buildLUT(RMin, RMax);

    // Parallel LUT application
    dst.create(src.size(), src.type());
    cv::parallel_for_(cv::Range(0, src.rows), [&](const cv::Range& range) {
        for (int y = range.start; y < range.end; ++y) {
            const cv::Vec3b* srcRow = src.ptr<cv::Vec3b>(y);
            cv::Vec3b* dstRow = dst.ptr<cv::Vec3b>(y);
            for (int x = 0; x < src.cols; ++x) {
                dstRow[x][0] = BTable[srcRow[x][0]];
                dstRow[x][1] = GTable[srcRow[x][1]];
                dstRow[x][2] = RTable[srcRow[x][2]];
            }
        }
    });
}

// ============================================================================
// Parallel Pixel-wise Operations
// ============================================================================

void AdjustWhiteBalanceParallel(const cv::Mat& src, cv::Mat& dst,
                                 double redBalance, double greenBalance, double blueBalance)
{
    CV_Assert(src.channels() == 3);

    std::vector<cv::Mat> channels(3);
    cv::split(src, channels);

    // Parallel channel processing
    std::vector<std::future<void>> futures;
    futures.push_back(std::async(std::launch::deferred | std::launch::async, [&]() {
        channels[2] *= redBalance;
    }));
    futures.push_back(std::async(std::launch::deferred | std::launch::async, [&]() {
        channels[1] *= greenBalance;
    }));
    futures.push_back(std::async(std::launch::deferred | std::launch::async, [&]() {
        channels[0] *= blueBalance;
    }));

    for (auto& f : futures) f.wait();

    cv::merge(channels, dst);

    double maxVal = (src.depth() == CV_8U) ? 255.0 : 65535.0;
    cv::threshold(dst, dst, maxVal, maxVal, cv::THRESH_TRUNC);
}

// ============================================================================
// SIMD-optimized Operations (using OpenCV's universal intrinsics)
// ============================================================================

#include <opencv2/core/hal/intrin.hpp>

void applyLUT_SIMD(const cv::Mat& image, const cv::Mat& lut, cv::Mat& result)
{
    result.create(image.rows, image.cols, CV_8UC3);
    const cv::Vec3b* lutPtr = lut.ptr<cv::Vec3b>();

    cv::parallel_for_(cv::Range(0, image.rows), [&](const cv::Range& range) {
        for (int y = range.start; y < range.end; ++y) {
            const uint8_t* srcRow = image.ptr<uint8_t>(y);
            cv::Vec3b* dstRow = result.ptr<cv::Vec3b>(y);

            int x = 0;
#if CV_SIMD
            // SIMD optimization for continuous memory
            const int vecWidth = cv::v_uint8::nlanes;
            for (; x <= image.cols - vecWidth; x += vecWidth) {
                cv::v_uint8 indices = cv::vx_load(srcRow + x);
                // Gather operation would be needed for LUT
                // Fallback to scalar for non-continuous LUT access
                for (int i = 0; i < vecWidth; ++i) {
                    dstRow[x + i] = lutPtr[srcRow[x + i]];
                }
            }
#endif
            // Scalar fallback
            for (; x < image.cols; ++x) {
                dstRow[x] = lutPtr[srcRow[x]];
            }
        }
    });
}

// ============================================================================
// Parallel Image Statistics
// ============================================================================

void computeImageStatisticsParallel(const cv::Mat& src,
                                     double& mean, double& stddev,
                                     double& minVal, double& maxVal)
{
    // Use OpenCV's built-in parallel functions
    cv::Scalar meanScalar, stddevScalar;
    cv::meanStdDev(src, meanScalar, stddevScalar);
    mean = meanScalar[0];
    stddev = stddevScalar[0];

    double minMax[2];
    cv::minMaxLoc(src, &minMax[0], &minMax[1]);
    minVal = minMax[0];
    maxVal = minMax[1];
}

// ============================================================================
// Optimized Batch Processing
// ============================================================================

class BatchProcessor {
public:
    template<typename Func>
    static void processParallel(std::vector<cv::Mat>& images, Func&& processor, int numThreads = -1) {
        if (numThreads < 0) {
            numThreads = cv::getNumThreads();
        }

        cv::parallel_for_(cv::Range(0, static_cast<int>(images.size())),
                          [&](const cv::Range& range) {
            for (int i = range.start; i < range.end; ++i) {
                processor(images[i]);
            }
        }, numThreads);
    }

    template<typename Func>
    static std::vector<cv::Mat> processWithReturn(const std::vector<cv::Mat>& images,
                                                   Func&& processor,
                                                   int numThreads = -1) {
        std::vector<cv::Mat> results(images.size());

        if (numThreads < 0) {
            numThreads = cv::getNumThreads();
        }

        cv::parallel_for_(cv::Range(0, static_cast<int>(images.size())),
                          [&](const cv::Range& range) {
            for (int i = range.start; i < range.end; ++i) {
                results[i] = processor(images[i]);
            }
        }, numThreads);

        return results;
    }
};

// ============================================================================
// TBB-specific optimizations (if TBB is available)
// ============================================================================

#ifdef HAVE_TBB
#include <tbb/parallel_for.h>
#include <tbb/blocked_range.h>

void processWithTBB(cv::Mat& image, std::function<void(cv::Mat&)> processor) {
    tbb::parallel_for(
        tbb::blocked_range<int>(0, image.rows),
        [&](const tbb::blocked_range<int>& range) {
            cv::Mat roi = image.rowRange(range.begin(), range.end());
            processor(roi);
        }
    );
}
#endif

// ============================================================================
// Performance Benchmarking Helper
// ============================================================================

class PerformanceTimer {
private:
    std::chrono::high_resolution_clock::time_point start_;
    std::string name_;

public:
    explicit PerformanceTimer(const std::string& name)
        : name_(name), start_(std::chrono::high_resolution_clock::now()) {}

    ~PerformanceTimer() {
        auto end = std::chrono::high_resolution_clock::now();
        auto duration = std::chrono::duration_cast<std::chrono::microseconds>(end - start_);
        std::cout << "[" << name_ << "] " << duration.count() / 1000.0 << " ms" << std::endl;
    }
};

#define TIME_FUNCTION PerformanceTimer _timer(__FUNCTION__)

// ============================================================================
// Usage Example
// ============================================================================

/*
// Original sequential version
void processImages(std::vector<cv::Mat>& images) {
    for (auto& img : images) {
        autoLevelsAdjust(img, img);
    }
}

// Optimized parallel version
void processImagesParallel(std::vector<cv::Mat>& images) {
    BatchProcessor::processParallel(images, [](cv::Mat& img) {
        cv::Mat temp;
        autoLevelsAdjustParallel(img, temp);
        img = temp;
    });
}
*/
