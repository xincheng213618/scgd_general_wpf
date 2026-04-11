#pragma once
#include <string>
#include <opencv2/opencv.hpp>

#include "custom_structs.h"

#ifdef OPENCVCUDA_EXPORTS

#define COLORVISIONCORE_API __declspec(dllexport)
#else
#define COLORVISIONCORE_API __declspec(dllimport)
#endif

extern "C" COLORVISIONCORE_API void M_FreeHImageData(unsigned char* data);
extern "C" COLORVISIONCORE_API int M_Fusion(const char* fusionjson, HImage* outImage);
extern "C" COLORVISIONCORE_API int CM_Fusion(const char* fusionjson, HImage* outImage);
extern "C" COLORVISIONCORE_API int CM_Fusion_Async(const char* fusionjson, HImage* outImage);
extern "C" COLORVISIONCORE_API int CM_Fusion_Batch(const char* batchjson, HImage* outImages, int* outCount);
