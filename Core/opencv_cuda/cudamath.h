#pragma once
#include <math.h>
#include "cuda_runtime.h"


#define HOD __host__ __device__


inline HOD float dev_atan2f(float  y, float  x)
{
	return atan2(y, x);
}
inline HOD float dev_sqrt(float  x)
{
	return sqrt(x);
}
inline HOD float dev_cos(float  x)
{
	return cos(x);
}
inline HOD float dev_pow(float x, float y)
{
	return pow(x, y);
}

inline HOD float dev_acos(float x)
{
	return acos(x);
}

inline HOD float dev_tanh(float x)
{
	return tanh(x);
}

// New kernel to calculate focus measure (gfocus) on the GPU
__global__ void gfocus_kernel(const double* src, double* dst, double* buffer, int M, int N, int KERNEL_SIZE)
{
    int col = blockIdx.x * blockDim.x + threadIdx.x;
    int row = blockIdx.y * blockDim.y + threadIdx.y;

    if (row < M && col < N) {
        // Simple box filter for averaging
        double sum = 0.0;
        int count = 0;
        for (int r = -KERNEL_SIZE / 2; r <= KERNEL_SIZE / 2; ++r) {
            for (int c = -KERNEL_SIZE / 2; c <= KERNEL_SIZE / 2; ++c) {
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

        // Second pass for the focus measure
        double diff = src[row * N + col] - u;
        double fm = diff * diff;

        sum = 0.0;
        count = 0;
        for (int r = -KERNEL_SIZE / 2; r <= KERNEL_SIZE / 2; ++r) {
            for (int c = -KERNEL_SIZE / 2; c <= KERNEL_SIZE / 2; ++c) {
                int cur_r = row + r;
                int cur_c = col + c;
                if (cur_r >= 0 && cur_r < M && cur_c >= 0 && cur_c < N) {
                    double d = src[cur_r * N + cur_c] - buffer[cur_r * N + cur_c];
                    sum += d * d;
                    count++;
                }
            }
        }
        dst[row * N + col] = sum / count;
    }
}


// Consolidated kernel for finding max focus and preparing for the next step
__global__ void find_max_and_prepare_kernel(double* fms, double* ymax, int* I, double* y1t, double* y2t, double* y3t, int* Ic, int M, int N, int P, int STEP)
{
    int col = blockIdx.x * blockDim.x + threadIdx.x;
    int row = blockIdx.y * blockDim.y + threadIdx.y;
    int idx = row * N + col;

    if (row < M && col < N) {
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
        I[idx] = max_idx + 1;

        int ic_val = max_idx + 1;
        if (ic_val <= STEP) {
            ic_val = STEP + 1;
        }
        else if (ic_val > P - STEP) {
            ic_val = P - STEP;
        }
        Ic[idx] = ic_val;

        // Indices are 0-based for array access, but Ic stores 1-based values
        y1t[idx] = fms[(ic_val - STEP - 1) * M * N + idx];
        y2t[idx] = fms[(ic_val - 1) * M * N + idx];
        y3t[idx] = fms[(ic_val + STEP - 1) * M * N + idx];
    }
}




__global__ void tanh_kernel(double* imgs, double* phi, int M, int N)
{
    int row = blockIdx.x * blockDim.x + threadIdx.x;
    if (row < M)
    {
        double* ptr = imgs + row * N;
        double* ptr1 = phi + row * N;
        for (int col = 0; col < N; col++)
        {
            ptr[col] = 0.5 + 0.5 * tanh(ptr1[col] * (ptr[col] - 1));
        }
    }
}
__global__ void find_max(double* imgs, double* Ymax, double* I, int M, int N, int p)
{
    int row = blockIdx.y * blockDim.y + threadIdx.y;
    int col = blockIdx.x * blockDim.x + threadIdx.x;

    if (row < M && col < N)
    {
        double* ptr = imgs + row * N + col;
        double* ptr1 = Ymax + row * N + col;
        double* ptr2 = I + row * N + col;

        if (*ptr > *ptr1)
        {
            *ptr1 = *ptr;
            *ptr2 = p + 1;
        }
    }
}

__global__ void compute(double* phi, double* S, int M, int N)
{
    int row = blockIdx.y * blockDim.y + threadIdx.y;
    int col = blockIdx.x * blockDim.x + threadIdx.x;
    if (row < M && col < N)
    {
        double val = 0.5 * (1 + tanh(0.2 * (S[row * N + col] - 13))) / 0.2;
        phi[row * N + col] = val;
    }
}

__global__ void calculate_S(double* S, double* inv_psnr, int M, int N)
{
    int row = blockIdx.y * blockDim.y + threadIdx.y;
    int col = blockIdx.x * blockDim.x + threadIdx.x;
    if (row < M && col < N)
    {
        S[row * N + col] = 20 * log10(1 / inv_psnr[row * N + col]);
    }
}

__global__ void calculate_err(double* imgs, double* err, double* A, double* u, double* s2, double* Ymax, int M, int N, int p)
{
    int row = blockIdx.y * blockDim.y + threadIdx.y;
    int col = blockIdx.x * blockDim.x + threadIdx.x;
    if (row < M && col < N)
    {
        double val = imgs[row * N + col] - A[row * N + col] * exp((-pow((p + 1 - u[row * N + col]), 2) / (2 * s2[row * N + col])));
        err[row * N + col] += abs(val);

        imgs[row * N + col] = imgs[row * N + col] / Ymax[row * N + col];
    }
}
// CORRECTED kernel function
__global__ void kernel(int* I_ptr, double* y1t_ptr, double* y2t_ptr, double* y3t_ptr, int* Ic_ptr, double* s2_ptr, double* u_ptr, double* A_ptr, int M, int N, double STEP) {
    int row = blockIdx.y * blockDim.y + threadIdx.y;
    int col = blockIdx.x * blockDim.x + threadIdx.x;
    if (row < M && col < N)
    {
        double x1, x2, x3;
        double y1, y2, y3;
        double a, b, c;

        // Ic_ptr now correctly points to integer data
        x1 = (double)Ic_ptr[row * N + col] - STEP;
        x2 = (double)Ic_ptr[row * N + col];
        x3 = (double)Ic_ptr[row * N + col] + STEP;

        y2 = log(y2t_ptr[row * N + col]);

        // I_ptr now correctly points to integer data
        if (I_ptr[row * N + col] <= STEP) {
            y1 = log(y3t_ptr[row * N + col]);
        }
        else {
            y1 = log(y1t_ptr[row * N + col]);
        }
        y3 = y1;

        c = ((y1 - y2) * (x2 - x3) - (y2 - y3) * (x1 - x2)) / ((x1 * x1 - x2 * x2) * (x2 - x3) - (x2 * x2 - x3 * x3) * (x1 - x2));
        if (isinf(c) || isnan(c) || c == 0) {
            c = 0.00001;
        }
        b = ((y2 - y3) - c * (x2 - x3) * (x2 + x3)) / (x2 - x3);
        if (isinf(b) || isnan(b) || b == 0) {
            b = 0.00001;
        }

        s2_ptr[row * N + col] = -1.0 / (2.0 * c);
        u_ptr[row * N + col] = b * s2_ptr[row * N + col];
        a = y1 - b * x1 - c * x1 * x1;
        A_ptr[row * N + col] = exp(a + (u_ptr[row * N + col] * u_ptr[row * N + col]) / (2.0 * s2_ptr[row * N + col]));
    }
}


