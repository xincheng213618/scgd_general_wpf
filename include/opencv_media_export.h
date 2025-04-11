#pragma once

#include <string>
#include <opencv2/opencv.hpp>

#include "custom_structs.h"
#include "Utilcom.h"

#ifdef OPENCV_EXPORTS
#define COLORVISIONCORE_API __declspec(dllexport)
#else
#define COLORVISIONCORE_API __declspec(dllimport)
#endif

enum EvaFunc
{
    Variance = 0,
    Tenengrad = 1,
    Laplace,
    CalResol,
};

// 定义错误代码枚举
enum class StitchingErrorCode {
    SUCCESS = 0,          // 成功
    EMPTY_INPUT = -1,     // 输入为空
    FILE_NOT_FOUND = -2,  // 文件未找到
    DIFFERENT_DIMENSIONS = -3, // 尺寸不同
    DIFFERENT_TYPE = -4,  // 类型不同
    NO_VALID_IMAGES = -5 // 没有有效的图像
};



extern "C" COLORVISIONCORE_API void M_FreeHImageData(unsigned char* data);


extern "C" COLORVISIONCORE_API int M_ExtractChannel(HImage img, HImage* outImage, int channel);
extern "C" COLORVISIONCORE_API int M_PseudoColor(HImage img, HImage* outImage, uint min, uint max, cv::ColormapTypes types = cv::ColormapTypes::COLORMAP_JET, int channel = -1);

extern "C" COLORVISIONCORE_API int M_AutoLevelsAdjust(HImage img, HImage* outImage);

extern "C" COLORVISIONCORE_API int M_AutomaticColorAdjustment(HImage img, HImage* outImage);

extern "C" COLORVISIONCORE_API int M_AutomaticToneAdjustment(HImage img, HImage* outImage);

extern "C" COLORVISIONCORE_API int M_DrawPoiImage(HImage img, HImage* outImage, int radius, int* point, int pointCount, int thickness);

extern "C" COLORVISIONCORE_API int M_ConvertImage(HImage img, uchar** rowGrayPixels, int* length, int* scaleFactor, int targetPixelsX = 512, int targetPixelsY = 512);

extern "C" COLORVISIONCORE_API double M_CalArtculation(HImage img, EvaFunc type);

extern "C" COLORVISIONCORE_API int M_GetWhiteBalance(HImage img, HImage* outImage, double redBalance, double greenBalance, double blueBalance);

extern "C" COLORVISIONCORE_API int M_ApplyGammaCorrection(HImage img, HImage* outImage, double gamma);

extern "C" COLORVISIONCORE_API int M_AdjustBrightnessContrast(HImage img, HImage* outImage, double alpha, double beta);

extern "C" COLORVISIONCORE_API int M_InvertImage(HImage img, HImage* outImage);

extern "C" COLORVISIONCORE_API int M_Threshold(HImage img, HImage* outImage, double thresh, double maxval, int type);

extern "C" COLORVISIONCORE_API int M_FindLuminousArea(HImage img,const char* config, char** result);

extern "C" COLORVISIONCORE_API int M_ConvertGray32Float(HImage img, HImage* outImage);

extern "C" COLORVISIONCORE_API int M_StitchImages(const char* config, HImage* outImage);


extern "C" COLORVISIONCORE_API int FreeResult(char* result) {
    delete[] result;
    return 0;
}



