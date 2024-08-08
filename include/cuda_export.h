#pragma once
#include <string>
#include <opencv2/opencv.hpp>

#include "custom_structs.h"

#ifdef OPENCVCUDA_EXPORTS

#define COLORVISIONCORE_API __declspec(dllexport)
#else
#define COLORVISIONCORE_API __declspec(dllimport)
#endif


extern "C" COLORVISIONCORE_API int CM_Fusion(const char* fusionjson, HImage* outImage);
