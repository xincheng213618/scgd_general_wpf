#pragma once

#include <string>
#include <opencv2/opencv.hpp>

#include "custom_structs.h"

#ifdef OPENCV_EXPORTS
#define COLORVISIONCORE_API __declspec(dllexport)
#else
#define COLORVISIONCORE_API __declspec(dllimport)
#endif

extern "C" COLORVISIONCORE_API int CM_PseudoColor(HImage img, HImage * outImage, uint min , uint max, cv::ColormapTypes types = cv::ColormapTypes::COLORMAP_JET);

extern "C" COLORVISIONCORE_API double CalArtculation(int nw, int nh, char* data);

extern "C" COLORVISIONCORE_API double CalArtculationROI(int nw, int nh, char* data,int x ,int y ,int width, int height);

extern "C" COLORVISIONCORE_API int CM_AutoLevelsAdjust(HImage img, HImage* outImage);

extern "C" COLORVISIONCORE_API int CM_AutomaticColorAdjustment(HImage img, HImage* outImage);

extern "C" COLORVISIONCORE_API int CM_AutomaticToneAdjustment(HImage img, HImage* outImage);




