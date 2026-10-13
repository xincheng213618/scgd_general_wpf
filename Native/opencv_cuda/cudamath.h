#pragma once
#include <cuda_runtime.h>
#include <cmath>

// All spatial filters use BORDER_REPLICATE, matching the CPU implementation.
__global__ void fusion_gray(const unsigned char* src, float* gray, int total, int channels) {
    const int i = blockIdx.x * blockDim.x + threadIdx.x;
    if (i >= total) return;
    // OpenCV's 8-bit BGR2GRAY fixed-point coefficients (15 fractional bits).
    const int value = channels == 1 ? src[i]
        : (3735 * src[3 * size_t(i)] + 19235 * src[3 * size_t(i) + 1]
            + 9798 * src[3 * size_t(i) + 2] + 16384) >> 15;
    gray[i] = value * (1.0f / 255.0f);
}

__global__ void fusion_box_horizontal(const float* src, float* dst, int width, int height, int window) {
    const int x = blockIdx.x * blockDim.x + threadIdx.x;
    const int y = blockIdx.y * blockDim.y + threadIdx.y;
    if (x >= width || y >= height) return;
    float sum = 0.0f;
    for (int dx = -window / 2; dx <= window / 2; ++dx)
        sum += src[y * width + min(max(x + dx, 0), width - 1)];
    dst[y * width + x] = sum / window;
}

// A vertical box pass can produce either the mean, squared residual, or phi.
// Keeping the nonlinear transform after the second pass preserves the filter.
template<int Transform>
__global__ void fusion_box_vertical(const float* src, const float* gray, float* dst,
    int width, int height, int window, float alpha = 0.2f, float threshold = 13.0f) {
    const int x = blockIdx.x * blockDim.x + threadIdx.x;
    const int y = blockIdx.y * blockDim.y + threadIdx.y;
    if (x >= width || y >= height) return;
    const int i = y * width + x;
    float sum = 0.0f;
    for (int dy = -window / 2; dy <= window / 2; ++dy)
        sum += src[min(max(y + dy, 0), height - 1) * width + x];
    float value = sum / window;
    if constexpr (Transform == 1) {
        const float residual = gray[i] - value;
        value = residual * residual;
    }
    if constexpr (Transform == 2) {
        const float selectivity = -8.685889638f * logf(fmaxf(value, 1e-6f));
        value = 0.5f * (1.0f + tanhf(alpha * (selectivity - threshold))) / alpha;
    }
    dst[i] = value;
}

__global__ void fusion_fit_normalize(float* focus, float* inversePsnr, int total, int count, int step) {
    const int i = blockIdx.x * blockDim.x + threadIdx.x;
    if (i >= total) return;
    float maximum = focus[i];
    int peak = 1;
    for (int p = 1; p < count; ++p) {
        const float value = focus[size_t(p) * total + i];
        if (value > maximum) { maximum = value; peak = p + 1; }
    }
    maximum = fmaxf(maximum, 1e-6f);
    const int center = min(max(peak, step + 1), count - step);
    const int side = peak <= step ? center + step : center - step;
    const float centerValue = fmaxf(focus[size_t(center - 1) * total + i], 1e-6f);
    // Centered form of the inherited symmetric-endpoint fit: no cancellation
    // in u/A and no positive exponential for a valid Gaussian peak.
    const float curvature = (logf(fmaxf(focus[size_t(side - 1) * total + i], 1e-6f)) - logf(centerValue)) / (step * step);
    const bool validPeak = curvature < 0.0f;
    float error = 0.0f;
    for (int p = 0; p < count; ++p) {
        float& value = focus[size_t(p) * total + i];
        if (validPeak) {
            const float delta = float(p + 1 - center);
            error += fabsf(value - centerValue * expf(curvature * delta * delta));
        }
        value /= maximum;
    }
    inversePsnr[i] = validPeak ? error / (count * maximum) : 1.0f;
}

__global__ void fusion_median(const float* src, float* dst, int width, int height) {
    const int x = blockIdx.x * blockDim.x + threadIdx.x;
    const int y = blockIdx.y * blockDim.y + threadIdx.y;
    if (x >= width || y >= height) return;
    float values[9];
    int k = 0;
    for (int dy = -1; dy <= 1; ++dy)
        for (int dx = -1; dx <= 1; ++dx)
            values[k++] = src[min(max(y + dy, 0), height - 1) * width + min(max(x + dx, 0), width - 1)];
    // Fixed-size insertion sort; no data-dependent global accesses.
    for (int a = 1; a < 9; ++a) {
        const float value = values[a];
        int b = a;
        while (b > 0 && values[b - 1] > value) { values[b] = values[b - 1]; --b; }
        values[b] = value;
    }
    dst[y * width + x] = values[4];
}

__global__ void fusion_output(const unsigned char* images, const float* focus, const float* phi,
    unsigned char* output, int total, int count, int channels, bool adaptive) {
    const int i = blockIdx.x * blockDim.x + threadIdx.x;
    if (i >= total) return;
    float sum = 0.0f, color[3] = {}, unweighted[3] = {};
    const float selectivity = adaptive ? phi[i] : 0.0f;
    for (int p = 0; p < count; ++p) {
        float weight = focus[size_t(p) * total + i];
        if (adaptive) weight = 0.5f + 0.5f * tanhf(selectivity * (weight - 1.0f));
        sum += weight;
        for (int c = 0; c < channels; ++c) {
            const float value = images[(size_t(p) * total + i) * channels + c];
            color[c] += value * weight;
            unweighted[c] += value;
        }
    }
    for (int c = 0; c < channels; ++c) {
        const float value = sum > 1e-6f ? color[c] / sum : unweighted[c] / count;
        output[size_t(i) * channels + c] = static_cast<unsigned char>(__float2int_rn(fminf(255.0f, fmaxf(0.0f, value))));
    }
}
