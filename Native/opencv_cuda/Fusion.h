#pragma once

#include <opencv2/core.hpp>
#include <cuda_runtime.h>
#include <cstring>
#include <limits>
#include <stdexcept>
#include <vector>
#include "fusion_options.h"
#include "cudamath.h"

namespace fusion_cuda {
inline void check(cudaError_t error) {
    if (error != cudaSuccess) throw std::runtime_error(cudaGetErrorString(error));
}

// Per-call ownership: no shared workspace or default-stream dependencies.
struct Resources {
    cudaStream_t stream = nullptr;
    cudaEvent_t copied[2] = {};
    unsigned char* pinned[2] = {};
    unsigned char *images = nullptr, *output = nullptr;
    float *focus = nullptr, *scratch = nullptr;
    ~Resources() {
        if (stream) cudaStreamSynchronize(stream);
        for (int i = 0; i < 2; ++i) {
            if (copied[i]) cudaEventDestroy(copied[i]);
            if (pinned[i]) cudaFreeHost(pinned[i]);
        }
        if (images) cudaFree(images);
        if (output) cudaFree(output);
        if (focus) cudaFree(focus);
        if (scratch) cudaFree(scratch);
        if (stream) cudaStreamDestroy(stream);
    }
};
}

inline cv::Mat Fusion(const std::vector<cv::Mat>& imgs, const FusionOptions& options) {
    using fusion_cuda::check;
    if (imgs.empty() || !options.valid()) return {};
    const int width = imgs[0].cols, height = imgs[0].rows, channels = imgs[0].channels();
    if (imgs[0].empty() || imgs[0].depth() != CV_8U || (channels != 1 && channels != 3)) return {};
    for (const auto& img : imgs)
        if (img.size() != imgs[0].size() || img.type() != imgs[0].type()) return {};
    if (imgs.size() == 1) return imgs[0].clone();
    const size_t pixels = imgs[0].total();
    if (pixels > size_t(std::numeric_limits<int>::max()) || imgs.size() > size_t(std::numeric_limits<int>::max())
        || imgs.size() > std::numeric_limits<size_t>::max() / pixels / sizeof(float)) return {};
    const int total = static_cast<int>(pixels), count = static_cast<int>(imgs.size());
    const size_t imageBytes = pixels * channels, rowBytes = size_t(width) * channels;
    const bool adaptive = count >= 2 * options.focusStep + 1;
    fusion_cuda::Resources r;
    check(cudaStreamCreateWithFlags(&r.stream, cudaStreamNonBlocking));
    check(cudaMalloc(&r.images, imageBytes * count));
    check(cudaMalloc(&r.output, imageBytes));
    check(cudaMalloc(&r.focus, pixels * count * sizeof(float)));
    check(cudaMalloc(&r.scratch, pixels * 4 * sizeof(float)));
    for (int b = 0; b < 2; ++b) {
        check(cudaMallocHost(&r.pinned[b], imageBytes));
        check(cudaEventCreateWithFlags(&r.copied[b], cudaEventDisableTiming));
    }
    float* gray = r.scratch;
    float* temp = gray + pixels;
    float* work = temp + pixels;
    float* phi = work + pixels;
    const dim3 block(32, 8), grid((width + 31) / 32, (height + 7) / 8);
    const int blocks = (total + 255LL) / 256;
    for (int p = 0; p < count; ++p) {
        const int b = p % 2;
        if (p >= 2) check(cudaEventSynchronize(r.copied[b]));
        if (imgs[p].isContinuous()) std::memcpy(r.pinned[b], imgs[p].data, imageBytes);
        else for (int y = 0; y < height; ++y) std::memcpy(r.pinned[b] + y * rowBytes, imgs[p].ptr(y), rowBytes);
        auto* input = r.images + size_t(p) * imageBytes;
        check(cudaMemcpyAsync(input, r.pinned[b], imageBytes, cudaMemcpyHostToDevice, r.stream));
        check(cudaEventRecord(r.copied[b], r.stream));
        fusion_gray<<<blocks, 256, 0, r.stream>>>(input, gray, total, channels);
        fusion_box_horizontal<<<grid, block, 0, r.stream>>>(gray, temp, width, height, options.focusWindow);
        fusion_box_vertical<1><<<grid, block, 0, r.stream>>>(temp, gray, work, width, height, options.focusWindow);
        fusion_box_horizontal<<<grid, block, 0, r.stream>>>(work, temp, width, height, options.focusWindow);
        fusion_box_vertical<0><<<grid, block, 0, r.stream>>>(temp, nullptr, r.focus + size_t(p) * pixels, width, height, options.focusWindow);
        check(cudaGetLastError());
    }
    if (adaptive) {
        fusion_fit_normalize<<<blocks, 256, 0, r.stream>>>(r.focus, work, total, count, options.focusStep);
        fusion_box_horizontal<<<grid, block, 0, r.stream>>>(work, temp, width, height, options.consistencyWindow);
        fusion_box_vertical<2><<<grid, block, 0, r.stream>>>(temp, nullptr, gray, width, height,
            options.consistencyWindow, options.alpha, options.threshold);
        fusion_median<<<grid, block, 0, r.stream>>>(gray, phi, width, height);
    }
    // Each pixel computes weights, denominator and all output channels in one pass.
    fusion_output<<<blocks, 256, 0, r.stream>>>(r.images, r.focus, phi, r.output, total, count, channels, adaptive);
    check(cudaGetLastError());
    cv::Mat result(height, width, imgs[0].type());
    check(cudaMemcpyAsync(result.data, r.output, imageBytes, cudaMemcpyDeviceToHost, r.stream));
    check(cudaStreamSynchronize(r.stream));
    return result;
}
