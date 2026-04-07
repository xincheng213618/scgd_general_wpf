#pragma once
#include <math.h>
#include "cuda_runtime.h"

#define HOD __host__ __device__

// ============================================================================
// Math Utility Functions
// ============================================================================

inline HOD float dev_atan2f(float y, float x)
{
    return atan2f(y, x);
}

inline HOD float dev_sqrt(float x)
{
    return sqrtf(x);
}

inline HOD float dev_cos(float x)
{
    return cosf(x);
}

inline HOD float dev_pow(float x, float y)
{
    return powf(x, y);
}

inline HOD float dev_acos(float x)
{
    return acosf(x);
}

inline HOD float dev_tanh(float x)
{
    return tanhf(x);
}

inline HOD double dev_log(double x)
{
    return log(x);
}

inline HOD double dev_exp(double x)
{
    return exp(x);
}

inline HOD double dev_abs(double x)
{
    return fabs(x);
}

// ============================================================================
// Kernel 1: Focus Measure (gfocus) - Box Filter Based
// ============================================================================
__global__ void gfocus_kernel(const double* src, double* dst, double* buffer, int M, int N, int KERNEL_SIZE)
{
    int col = blockIdx.x * blockDim.x + threadIdx.x;
    int row = blockIdx.y * blockDim.y + threadIdx.y;

    if (row < M && col < N) {
        // First pass: box filter for averaging (U)
        double sum = 0.0;
        int half_k = KERNEL_SIZE / 2;
        int count = 0;

        for (int r = -half_k; r <= half_k; ++r) {
            for (int c = -half_k; c <= half_k; ++c) {
                int cur_r = row + r;
                int cur_c = col + c;
                if (cur_r >= 0 && cur_r < M && cur_c >= 0 && cur_c < N) {
                    sum += src[cur_r * N + cur_c];
                    count++;
                }
            }
        }
        double u = sum / count;
        buffer[row * N + col] = u;

        // Second pass: compute focus measure
        sum = 0.0;
        count = 0;
        for (int r = -half_k; r <= half_k; ++r) {
            for (int c = -half_k; c <= half_k; ++c) {
                int cur_r = row + r;
                int cur_c = col + c;
                if (cur_r >= 0 && cur_r < M && cur_c >= 0 && cur_c < N) {
                    double diff = src[cur_r * N + cur_c] - buffer[cur_r * N + cur_c];
                    sum += diff * diff;
                    count++;
                }
            }
        }
        dst[row * N + col] = sum / count;
    }
}

// ============================================================================
// Kernel 2: Find Max Focus and Prepare Data for Curve Fitting
// ============================================================================
__global__ void find_max_and_prepare_kernel(
    double* fms, double* ymax, int* I,
    double* y1t, double* y2t, double* y3t, int* Ic,
    int M, int N, int P, int STEP)
{
    int col = blockIdx.x * blockDim.x + threadIdx.x;
    int row = blockIdx.y * blockDim.y + threadIdx.y;
    int idx = row * N + col;

    if (row < M && col < N) {
        // Find max focus value across all images
        double max_val = -1.0;
        int max_idx = 0;
        for (int p = 0; p < P; ++p) {
            double current_val = fms[p * M * N + idx];
            if (current_val > max_val) {
                max_val = current_val;
                max_idx = p;
            }
        }
        ymax[idx] = max_val;
        I[idx] = max_idx + 1;  // 1-based index

        // Calculate Ic with boundary handling
        int ic_val = max_idx + 1;
        if (ic_val <= STEP) {
            ic_val = STEP + 1;
        }
        else if (ic_val > P - STEP) {
            ic_val = P - STEP;
        }
        Ic[idx] = ic_val;

        // Store y values for curve fitting (1-based to 0-based conversion)
        y1t[idx] = fms[(ic_val - STEP - 1) * M * N + idx];
        y2t[idx] = fms[(ic_val - 1) * M * N + idx];
        y3t[idx] = fms[(ic_val + STEP - 1) * M * N + idx];
    }
}

// ============================================================================
// Kernel 3: Curve Fitting to get Gaussian parameters (A, u, s2)
// ============================================================================
__global__ void curve_fitting_kernel(
    int* I_ptr, double* y1t_ptr, double* y2t_ptr, double* y3t_ptr,
    int* Ic_ptr, double* s2_ptr, double* u_ptr, double* A_ptr,
    int M, int N, int STEP)
{
    int row = blockIdx.y * blockDim.y + threadIdx.y;
    int col = blockIdx.x * blockDim.x + threadIdx.x;
    int idx = row * N + col;

    if (row < M && col < N) {
        double x1, x2, x3;
        double y1, y2, y3;
        double a, b, c;

        // Calculate x coordinates
        x1 = (double)Ic_ptr[idx] - STEP;
        x2 = (double)Ic_ptr[idx];
        x3 = (double)Ic_ptr[idx] + STEP;

        // Log transform of y values
        y2 = log(y2t_ptr[idx]);

        if (I_ptr[idx] <= STEP) {
            y1 = log(y3t_ptr[idx]);
        }
        else {
            y1 = log(y1t_ptr[idx]);
        }
        y3 = y1;

        // Quadratic curve fitting: y = a + b*x + c*x^2
        double denom = (x1 * x1 - x2 * x2) * (x2 - x3) - (x2 * x2 - x3 * x3) * (x1 - x2);
        c = ((y1 - y2) * (x2 - x3) - (y2 - y3) * (x1 - x2)) / denom;

        if (isinf(c) || isnan(c) || c == 0) {
            c = 0.00001;
        }

        b = ((y2 - y3) - c * (x2 - x3) * (x2 + x3)) / (x2 - x3);
        if (isinf(b) || isnan(b) || b == 0) {
            b = 0.00001;
        }

        // Convert to Gaussian parameters
        s2_ptr[idx] = -1.0 / (2.0 * c);
        u_ptr[idx] = b * s2_ptr[idx];
        a = y1 - b * x1 - c * x1 * x1;
        A_ptr[idx] = exp(a + (u_ptr[idx] * u_ptr[idx]) / (2.0 * s2_ptr[idx]));
    }
}

// ============================================================================
// Kernel 4: Calculate Error and Normalize Focus Maps
// ============================================================================
__global__ void calculate_err_kernel(
    double* imgs, double* err, double* A, double* u, double* s2,
    double* Ymax, int M, int N, int p)
{
    int row = blockIdx.y * blockDim.y + threadIdx.y;
    int col = blockIdx.x * blockIdx.x + threadIdx.x;
    int idx = row * N + col;

    if (row < M && col < N) {
        double val = imgs[idx] - A[idx] * exp((-pow((p + 1 - u[idx]), 2) / (2 * s2[idx])));
        atomicAdd(&err[idx], fabs(val));
        imgs[idx] = imgs[idx] / Ymax[idx];
    }
}

// ============================================================================
// Kernel 5: Calculate S (PSNR-based weight)
// ============================================================================
__global__ void calculate_S_kernel(double* S, double* inv_psnr, int M, int N)
{
    int row = blockIdx.y * blockDim.y + threadIdx.y;
    int col = blockIdx.x * blockDim.x + threadIdx.x;
    int idx = row * N + col;

    if (row < M && col < N) {
        S[idx] = 20.0 * log10(1.0 / inv_psnr[idx]);
    }
}

// ============================================================================
// Kernel 6: Compute Phi (adaptive weight)
// ============================================================================
__global__ void compute_phi_kernel(double* phi, double* S, int M, int N)
{
    int row = blockIdx.y * blockDim.y + threadIdx.y;
    int col = blockIdx.x * blockDim.x + threadIdx.x;
    int idx = row * N + col;

    if (row < M && col < N) {
        phi[idx] = 0.5 * (1.0 + tanh(0.2 * (S[idx] - 13.0))) / 0.2;
    }
}

// ============================================================================
// Kernel 7: Apply Tanh Weights to Focus Maps
// ============================================================================
__global__ void tanh_weight_kernel(double* imgs, double* phi, int M, int N)
{
    int idx = blockIdx.x * blockDim.x + threadIdx.x;
    int total = M * N;

    if (idx < total) {
        imgs[idx] = 0.5 + 0.5 * tanh(phi[idx] * (imgs[idx] - 1.0));
    }
}

// ============================================================================
// Kernel 8: Box Filter (for inv_psnr calculation on GPU)
// ============================================================================
__global__ void box_filter_kernel(
    const double* src, double* dst, int M, int N, int kernel_size)
{
    int col = blockIdx.x * blockDim.x + threadIdx.x;
    int row = blockIdx.y * blockDim.y + threadIdx.y;

    if (row < M && col < N) {
        double sum = 0.0;
        int half_k = kernel_size / 2;
        int count = 0;

        for (int r = -half_k; r <= half_k; ++r) {
            for (int c = -half_k; c <= half_k; ++c) {
                int cur_r = row + r;
                int cur_c = col + c;
                if (cur_r >= 0 && cur_r < M && cur_c >= 0 && cur_c < N) {
                    sum += src[cur_r * N + cur_c];
                    count++;
                }
            }
        }
        dst[row * N + col] = sum / count;
    }
}

// ============================================================================
// Kernel 9: Element-wise Division (err / (P * ymax))
// ============================================================================
__global__ void element_divide_kernel(
    const double* err, const double* ymax, double* dst,
    int M, int N, int P)
{
    int idx = blockIdx.x * blockDim.x + threadIdx.x;
    int total = M * N;

    if (idx < total) {
        dst[idx] = err[idx] / (P * ymax[idx]);
    }
}

// ============================================================================
// Kernel 10: Reduction Sum (for fmn calculation)
// ============================================================================
__global__ void sum_images_kernel(
    double** images, double* output, int M, int N, int P)
{
    int col = blockIdx.x * blockDim.x + threadIdx.x;
    int row = blockIdx.y * blockDim.y + threadIdx.y;

    if (row < M && col < N) {
        int idx = row * N + col;
        double sum = 0.0;
        for (int p = 0; p < P; ++p) {
            sum += images[p][idx];
        }
        output[idx] = sum;
    }
}

// ============================================================================
// Kernel 11: Weighted Channel Fusion (for color images)
// ============================================================================
__global__ void weighted_channel_fusion_kernel(
    const double* channel, const double* weights, double* output,
    int M, int N, int P)
{
    int col = blockIdx.x * blockDim.x + threadIdx.x;
    int row = blockIdx.y * blockDim.y + threadIdx.y;

    if (row < M && col < N) {
        int idx = row * N + col;
        double sum = 0.0;
        for (int p = 0; p < P; ++p) {
            sum += channel[p * M * N + idx] * weights[p * M * N + idx];
        }
        output[idx] = sum;
    }
}

// ============================================================================
// Kernel 12: Final Division and Conversion (divide by fmn)
// ============================================================================
__global__ void final_divide_kernel(
    const double* numerator, const double* fmn,
    unsigned char* output, int M, int N)
{
    int idx = blockIdx.x * blockDim.x + threadIdx.x;
    int total = M * N;

    if (idx < total) {
        double val = numerator[idx] / fmn[idx];
        // Clamp to [0, 255]
        val = fmax(0.0, fmin(255.0, val));
        output[idx] = (unsigned char)val;
    }
}

// ============================================================================
// Kernel 13: Grayscale Fusion
// ============================================================================
__global__ void grayscale_fusion_kernel(
    double** images, double** weights, double* output,
    int M, int N, int P)
{
    int col = blockIdx.x * blockDim.x + threadIdx.x;
    int row = blockIdx.y * blockDim.y + threadIdx.y;

    if (row < M && col < N) {
        int idx = row * N + col;
        double weighted_sum = 0.0;
        double weight_sum = 0.0;

        for (int p = 0; p < P; ++p) {
            weighted_sum += images[p][idx] * weights[p][idx];
            weight_sum += weights[p][idx];
        }

        output[idx] = (weight_sum > 0) ? (weighted_sum / weight_sum) : 0;
    }
}

// ============================================================================
// Kernel 14: Median Filter (3x3, simplified for GPU)
// ============================================================================
__global__ void median_filter_3x3_kernel(
    const double* src, double* dst, int M, int N)
{
    int col = blockIdx.x * blockDim.x + threadIdx.x;
    int row = blockIdx.y * blockDim.y + threadIdx.y;

    if (row < M && col < N) {
        int idx = row * N + col;

        // Collect 3x3 neighborhood
        double window[9];
        int count = 0;

        for (int r = -1; r <= 1; ++r) {
            for (int c = -1; c <= 1; ++c) {
                int cur_r = row + r;
                int cur_c = col + c;
                if (cur_r >= 0 && cur_r < M && cur_c >= 0 && cur_c < N) {
                    window[count++] = src[cur_r * N + cur_c];
                }
                else {
                    window[count++] = src[idx];  // Use center for border
                }
            }
        }

        // Simple bubble sort for median
        for (int i = 0; i < 8; ++i) {
            for (int j = i + 1; j < 9; ++j) {
                if (window[i] > window[j]) {
                    double temp = window[i];
                    window[i] = window[j];
                    window[j] = temp;
                }
            }
        }

        dst[idx] = window[4];  // Median is at index 4
    }
}

// ============================================================================
// Legacy Kernel Names (for backward compatibility)
// ============================================================================

__global__ void calculate_err(double* imgs, double* err, double* A, double* u, double* s2,
    double* Ymax, int M, int N, int p)
{
    int row = blockIdx.y * blockDim.y + threadIdx.y;
    int col = blockIdx.x * blockDim.x + threadIdx.x;
    int idx = row * N + col;

    if (row < M && col < N) {
        double val = imgs[idx] - A[idx] * exp((-pow((p + 1 - u[idx]), 2) / (2 * s2[idx])));
        atomicAdd(&err[idx], fabs(val));
        imgs[idx] = imgs[idx] / Ymax[idx];
    }
}

__global__ void find_max(double* imgs, double* Ymax, double* I, int M, int N, int p)
{
    int row = blockIdx.y * blockDim.y + threadIdx.y;
    int col = blockIdx.x * blockDim.x + threadIdx.x;
    int idx = row * N + col;

    if (row < M && col < N) {
        if (imgs[idx] > Ymax[idx]) {
            Ymax[idx] = imgs[idx];
            I[idx] = p + 1;
        }
    }
}
