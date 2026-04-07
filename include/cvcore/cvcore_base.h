#pragma once

/**
 * @file cvcore_base.h
 * @brief Core module base definitions and common types
 *
 * This file provides the foundational types and macros used throughout
 * the Core image processing module.
 */

#include <opencv2/opencv.hpp>
#include <string>
#include <vector>
#include <memory>
#include <optional>
#include <functional>
#include <chrono>

// ============================================================================
// Export Macros
// ============================================================================

#ifdef OPENCV_EXPORTS
    #define CV_CORE_API __declspec(dllexport)
#else
    #define CV_CORE_API __declspec(dllimport)
#endif

// ============================================================================
// Version Information
// ============================================================================

#define CV_CORE_VERSION_MAJOR 2
#define CV_CORE_VERSION_MINOR 0
#define CV_CORE_VERSION_PATCH 0

// ============================================================================
// Common Constants
// ============================================================================

namespace cvcore {

// Image depth constants
constexpr int DEPTH_8U = CV_8U;
constexpr int DEPTH_16U = CV_16U;
constexpr int DEPTH_32F = CV_32F;
constexpr int DEPTH_64F = CV_64F;

// Channel constants
constexpr int CHANNELS_GRAY = 1;
constexpr int CHANNELS_BGR = 3;
constexpr int CHANNELS_BGRA = 4;

// Default parameters
constexpr double DEFAULT_GAMMA = 1.0;
constexpr double DEFAULT_CONTRAST = 1.0;
constexpr double DEFAULT_BRIGHTNESS = 0.0;
constexpr int DEFAULT_KERNEL_SIZE = 3;

// Compression types
enum class CompressionType {
    None = 0,
    Zlib = 1,
    Gzip = 2,
    Lz4 = 3
};

// Processing backends
enum class ProcessingBackend {
    Auto,       // Automatically select best backend
    CPU,        // CPU only
    CUDA,       // NVIDIA CUDA
    OpenCL,     // OpenCL (if available)
    OpenMP      // OpenMP parallel CPU
};

// ============================================================================
// Error Handling
// ============================================================================

enum class ErrorCode {
    Success = 0,
    InvalidParameter = -1,
    InvalidImage = -2,
    UnsupportedFormat = -3,
    OutOfMemory = -4,
    FileNotFound = -5,
    FileReadError = -6,
    FileWriteError = -7,
    CUDAError = -8,
    OpenCLError = -9,
    UnknownError = -99
};

struct Error {
    ErrorCode code;
    std::string message;
    std::string function;
    std::string file;
    int line;

    Error(ErrorCode c, const std::string& msg, const char* func = "",
          const char* f = "", int l = 0)
        : code(c), message(msg), function(func), file(f), line(l) {}

    bool isSuccess() const { return code == ErrorCode::Success; }
    bool isFailure() const { return code != ErrorCode::Success; }
};

// Result type for operations that may fail
template<typename T>
using Result = std::pair<T, std::optional<Error>>;

// Helper macros for error handling
#define CVCORE_RETURN_ERROR(code, msg) \
    return { T(), std::make_optional<Error>(code, msg, __FUNCTION__, __FILE__, __LINE__) }

#define CVCORE_RETURN_SUCCESS(value) \
    return { value, std::nullopt }

#define CVCORE_CHECK_IMAGE(img) \
    do { \
        if (img.empty()) { \
            return { {}, std::make_optional<Error>(ErrorCode::InvalidImage, \
                "Empty image", __FUNCTION__, __FILE__, __LINE__) }; \
        } \
    } while(0)

// ============================================================================
// Image Descriptor
// ============================================================================

struct ImageDescriptor {
    int width = 0;
    int height = 0;
    int channels = 3;
    int depth = DEPTH_8U;

    ImageDescriptor() = default;
    ImageDescriptor(int w, int h, int c = 3, int d = DEPTH_8U)
        : width(w), height(h), channels(c), depth(d) {}

    explicit ImageDescriptor(const cv::Mat& mat)
        : width(mat.cols), height(mat.rows),
          channels(mat.channels()), depth(mat.depth()) {}

    size_t sizeBytes() const {
        return static_cast<size_t>(width) * height * channels * (depth == DEPTH_8U ? 1 :
                                                                  depth == DEPTH_16U ? 2 :
                                                                  depth == DEPTH_32F ? 4 : 8);
    }

    bool isValid() const {
        return width > 0 && height > 0 && channels > 0;
    }

    cv::Size size() const { return cv::Size(width, height); }
    int cvType() const { return CV_MAKETYPE(depth, channels); }
};

// ============================================================================
// Processing Statistics
// ============================================================================

struct ProcessingStats {
    std::chrono::microseconds duration{0};
    size_t bytesProcessed = 0;
    std::string backendUsed;
    int threadsUsed = 1;

    double throughputMBps() const {
        auto seconds = duration.count() / 1e6;
        return seconds > 0 ? (bytesProcessed / (1024.0 * 1024.0)) / seconds : 0.0;
    }
};

// ============================================================================
// Progress Callback
// ============================================================================

using ProgressCallback = std::function<bool(int percent, const std::string& stage)>;

// ============================================================================
// Context for Processing
// ============================================================================

class ProcessingContext {
public:
    ProcessingBackend backend = ProcessingBackend::Auto;
    int numThreads = -1;  // -1 means use all available
    bool enableProfiling = false;
    ProgressCallback progressCallback;

    // CUDA specific
    int cudaDeviceId = 0;
    int cudaStreamCount = 2;

    // Memory pool
    bool useMemoryPool = true;
    size_t memoryPoolSize = 256 * 1024 * 1024;  // 256MB

    static ProcessingContext& defaultContext() {
        static ProcessingContext ctx;
        return ctx;
    }
};

} // namespace cvcore
