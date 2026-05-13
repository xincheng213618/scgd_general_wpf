#pragma once

/**
 * @file cvcore_image.h
 * @brief Unified image processing interface
 *
 * Provides a consistent API for image processing operations
 * with support for multiple backends (CPU, CUDA, OpenCL).
 */

#include "cvcore_base.h"

namespace cvcore {

// ============================================================================
// Image Class - Wrapper around cv::Mat with additional metadata
// ============================================================================

class CV_CORE_API Image {
private:
    cv::Mat data_;
    ImageDescriptor descriptor_;
    std::string sourcePath_;
    std::chrono::system_clock::time_point timestamp_;

public:
    // Constructors
    Image() = default;
    explicit Image(const cv::Mat& mat);
    explicit Image(cv::Mat&& mat);
    Image(int width, int height, int channels, int depth);
    Image(const ImageDescriptor& desc);

    // Factory methods
    static Result<Image> fromFile(const std::string& path);
    static Result<Image> fromFile(const std::string& path, int flags);
    static Result<Image> create(int width, int height, int channels, int depth);
    static Image zeros(const ImageDescriptor& desc);
    static Image zeros(int width, int height, int channels, int depth);

    // Conversion
    cv::Mat& mat() { return data_; }
    const cv::Mat& mat() const { return data_; }
    operator cv::Mat&() { return data_; }
    operator const cv::Mat&() const { return data_; }

    // Accessors
    int width() const { return descriptor_.width; }
    int height() const { return descriptor_.height; }
    int channels() const { return descriptor_.channels; }
    int depth() const { return descriptor_.depth; }
    ImageDescriptor descriptor() const { return descriptor_; }
    bool empty() const { return data_.empty(); }
    size_t sizeBytes() const { return descriptor_.sizeBytes(); }

    // Metadata
    const std::string& sourcePath() const { return sourcePath_; }
    void setSourcePath(const std::string& path) { sourcePath_ = path; }

    // Clone/Copy
    Image clone() const { return Image(data_.clone()); }
    void copyTo(Image& dst) const { data_.copyTo(dst.data_); }

    // ROI operations
    Image roi(int x, int y, int width, int height) const;
    Image roi(const cv::Rect& rect) const;

    // Channel operations
    Result<Image> splitChannel(int channel) const;
    Result<Image> extractChannels(const std::vector<int>& channelIndices) const;
    static Result<Image> merge(const std::vector<Image>& channels);

    // Type conversion
    Result<Image> convertDepth(int newDepth) const;
    Result<Image> toGray() const;
    Result<Image> toBGR() const;
    Result<Image> toRGBA() const;

    // Validation
    bool isContinuous() const { return data_.isContinuous(); }
    bool isValid() const { return !empty() && descriptor_.isValid(); }

    // Raw data access
    void* data() { return data_.data; }
    const void* data() const { return data_.data; }
    size_t step() const { return data_.step; }
};

// ============================================================================
// Image Sequence - For multi-image operations (e.g., focus stacking)
// ============================================================================

class CV_CORE_API ImageSequence {
private:
    std::vector<Image> images_;

public:
    ImageSequence() = default;
    explicit ImageSequence(size_t count) : images_(count) {}
    explicit ImageSequence(const std::vector<Image>& images) : images_(images) {}
    explicit ImageSequence(std::vector<Image>&& images) : images_(std::move(images)) {}

    // Factory
    static Result<ImageSequence> fromFiles(const std::vector<std::string>& paths);

    // Element access
    Image& operator[](size_t idx) { return images_[idx]; }
    const Image& operator[](size_t idx) const { return images_[idx]; }
    Image& at(size_t idx) { return images_.at(idx); }
    const Image& at(size_t idx) const { return images_.at(idx); }

    // Capacity
    size_t size() const { return images_.size(); }
    bool empty() const { return images_.empty(); }
    void clear() { images_.clear(); }
    void reserve(size_t n) { images_.reserve(n); }
    void resize(size_t n) { images_.resize(n); }

    // Modifiers
    void push_back(const Image& img) { images_.push_back(img); }
    void push_back(Image&& img) { images_.push_back(std::move(img)); }
    void pop_back() { images_.pop_back(); }

    // Iterators
    auto begin() { return images_.begin(); }
    auto end() { return images_.end(); }
    auto begin() const { return images_.begin(); }
    auto end() const { return images_.end(); }

    // Validation
    bool allSameSize() const;
    bool allSameFormat() const;
    ImageDescriptor commonDescriptor() const;

    // Batch operations
    Result<ImageSequence> convertDepth(int newDepth) const;
    Result<std::vector<ImageDescriptor>> getDescriptors() const;
};

// ============================================================================
// Image I/O Operations
// ============================================================================

namespace io {

struct ReadOptions {
    int flags = cv::IMREAD_UNCHANGED;
    bool async = false;
    size_t bufferSize = 0;  // 0 = auto
};

struct WriteOptions {
    std::vector<int> params;
    bool async = false;
    CompressionType compression = CompressionType::None;
};

CV_CORE_API Result<Image> readImage(const std::string& path,
                                     const ReadOptions& options = {});

CV_CORE_API Error writeImage(const std::string& path, const Image& image,
                              const WriteOptions& options = {});

CV_CORE_API Result<ImageSequence> readImageSequence(const std::vector<std::string>& paths,
                                                      const ReadOptions& options = {});

// Custom format support
CV_CORE_API Result<Image> readCustomFormat(const std::string& path);
CV_CORE_API Error writeCustomFormat(const std::string& path, const Image& image,
                                     CompressionType compression);

} // namespace io

} // namespace cvcore
