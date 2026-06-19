#pragma once

#include <opencv2/core.hpp>
#include <combaseapi.h>
#include <cstdint>
#include <cstring>
#include <limits>

#pragma pack(push, 1)
typedef struct _RoiRect {
    int32_t x;
    int32_t y;
    int32_t width;
    int32_t height;
} RoiRect;
#pragma pack(pop)

static inline int HImageDepthToCvDepth(int depth) noexcept
{
    switch (depth) {
    case 8:
        return CV_8U;
    case 16:
        return CV_16U;
    case 32:
        return CV_32F;
    case 64:
        return CV_64F;
    default:
        return -1;
    }
}

static inline int CvDepthToHImageDepth(int cvDepth) noexcept
{
    switch (cvDepth) {
    case CV_8U:
        return 8;
    case CV_16U:
        return 16;
    case CV_32F:
        return 32;
    case CV_64F:
        return 64;
    default:
        return -1;
    }
}

static inline bool HImageIsValidChannelCount(int channels) noexcept
{
    return channels > 0 && channels <= CV_CN_MAX;
}

static inline bool HImageTryMultiplySize(size_t left, size_t right, size_t* result) noexcept
{
    if (result == nullptr) {
        return false;
    }

    if (left != 0 && right > (std::numeric_limits<size_t>::max)() / left) {
        *result = 0;
        return false;
    }

    *result = left * right;
    return true;
}

static inline bool HImageTryGetType(int depth, int channels, int* outType) noexcept
{
    if (outType == nullptr || !HImageIsValidChannelCount(channels)) {
        return false;
    }

    int cvDepth = HImageDepthToCvDepth(depth);
    if (cvDepth < 0) {
        return false;
    }

    *outType = CV_MAKETYPE(cvDepth, channels);
    return true;
}

static inline bool HImageTryGetRowBytes(int cols, int channels, int depth, size_t* rowBytes, int* outType = nullptr) noexcept
{
    if (rowBytes == nullptr || cols <= 0) {
        return false;
    }

    int type = 0;
    if (!HImageTryGetType(depth, channels, &type)) {
        return false;
    }

    size_t bytes = 0;
    if (!HImageTryMultiplySize(static_cast<size_t>(cols), CV_ELEM_SIZE(type), &bytes) || bytes == 0) {
        return false;
    }

    *rowBytes = bytes;
    if (outType != nullptr) {
        *outType = type;
    }
    return true;
}

static inline bool HImageTryGetSpanBytes(int rows, size_t stride, size_t rowBytes, size_t* spanBytes) noexcept
{
    if (spanBytes == nullptr || rows <= 0 || rowBytes == 0 || stride < rowBytes) {
        return false;
    }

    size_t skippedRows = static_cast<size_t>(rows - 1);
    size_t prefixBytes = 0;
    if (!HImageTryMultiplySize(skippedRows, stride, &prefixBytes)) {
        return false;
    }

    if (prefixBytes > (std::numeric_limits<size_t>::max)() - rowBytes) {
        *spanBytes = 0;
        return false;
    }

    *spanBytes = prefixBytes + rowBytes;
    return true;
}

typedef struct HImage
{
    int rows;
    int cols;
    int channels;
    int depth;
    int stride;
    bool isDispose = false;
    int type() const
    {
        int cvType = -1;
        return HImageTryGetType(depth, channels, &cvType) ? cvType : -1;
    }
    int elemSize() const
    {
        int cvType = -1;
        return HImageTryGetType(depth, channels, &cvType) ? static_cast<int>(CV_ELEM_SIZE(cvType)) : 0;
    }
    unsigned char* pData;
} HImage;

static cv::Mat HImageToMatView(const HImage& img)
{
    if (img.pData == nullptr || img.rows <= 0 || img.cols <= 0 || img.stride < 0) {
        return cv::Mat();
    }

    int type = 0;
    size_t rowBytes = 0;
    if (!HImageTryGetRowBytes(img.cols, img.channels, img.depth, &rowBytes, &type)) {
        return cv::Mat();
    }

    const size_t step = img.stride > 0 ? static_cast<size_t>(img.stride) : rowBytes;
    size_t spanBytes = 0;
    if (!HImageTryGetSpanBytes(img.rows, step, rowBytes, &spanBytes)) {
        return cv::Mat();
    }

    return cv::Mat(img.rows, img.cols, type, img.pData, step);
}

static int MatToHImage(const cv::Mat& mat, HImage* outImage)
{
    if (outImage == nullptr) {
        return -1;
    }
    *outImage = HImage{};

    if (mat.empty()) {
        return -2;
    }

    const int hDepth = CvDepthToHImageDepth(mat.depth());
    if (hDepth < 0 || !HImageIsValidChannelCount(mat.channels())) {
        return -4;
    }

    cv::Mat continuousMat;
    if (mat.isContinuous()) {
        continuousMat = mat;
    }
    else {
        continuousMat = mat.clone();
    }
    if (continuousMat.empty() || !continuousMat.isContinuous()) {
        return -5;
    }

    if (continuousMat.step > static_cast<size_t>((std::numeric_limits<int>::max)())) {
        return -6;
    }

    size_t totalBytes = 0;
    if (!HImageTryMultiplySize(continuousMat.total(), continuousMat.elemSize(), &totalBytes) || totalBytes == 0) {
        return -7;
    }

    unsigned char* pAllocatedData = static_cast<unsigned char*>(CoTaskMemAlloc(totalBytes));
    if (pAllocatedData == nullptr) {
        return -3;
    }

    std::memcpy(pAllocatedData, continuousMat.data, totalBytes);

    outImage->rows = continuousMat.rows;
    outImage->cols = continuousMat.cols;
    outImage->channels = continuousMat.channels();
    outImage->stride = static_cast<int>(continuousMat.step);
    outImage->depth = hDepth;
    outImage->isDispose = false;
    outImage->pData = pAllocatedData;
    return 0;
}
