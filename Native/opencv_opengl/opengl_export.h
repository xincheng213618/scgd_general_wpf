#pragma once
#include <string>
#include <opencv2/opencv.hpp>

#include "custom_structs.h"

#ifdef OPENCVCUDA_EXPORTS

#define COLORVISIONCORE_API __declspec(dllexport)
#else
#define COLORVISIONCORE_API __declspec(dllimport)
#endif

