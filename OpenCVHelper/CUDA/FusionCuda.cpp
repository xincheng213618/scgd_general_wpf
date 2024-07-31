#include "cuda_runtime.h"
#include "device_launch_parameters.h"
#include <iostream>
#include <stdio.h>

#include <opencv2/core/core.hpp>  
#include <opencv2/highgui/highgui.hpp>
#include <opencv2/imgproc/imgproc.hpp>
#include <vector>
#include <algorithm>
#include "Fusion.h"

constexpr size_t MAXSIZE = 20;

__global__ void addKernel(int* const c, const int* const b, const int* const a)
{
	int i = threadIdx.x;
	c[i] = a[i] + b[i];
}
