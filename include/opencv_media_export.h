#pragma once

#include <string>
#include <opencv2/opencv.hpp>

#include "custom_structs.h"

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



