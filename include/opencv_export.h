#pragma once

#include <string>
#include <opencv2/opencv.hpp>

#include "custom_structs.h"

#ifdef OPENCV_EXPORTS
#define COLORVISIONCORE_API __declspec(dllexport)
#else
#define COLORVISIONCORE_API __declspec(dllimport)
#endif


extern "C" COLORVISIONCORE_API int ReadGhostImage(const char* FilePath, int singleLedPixelNum, int* LED_pixel_X, int* LED_pixel_Y, int singleGhostPixelNum, int* Ghost_pixel_X, int* Ghost_pixel_Y, HImage * outImage);
extern "C" COLORVISIONCORE_API int GhostImage(HImage img, HImage* outImage, int singleLedPixelNum, int* LED_pixel_X, int* LED_pixel_Y, int singleGhostPixelNum, int* Ghost_pixel_X, int* Ghost_pixel_Y);

extern "C" COLORVISIONCORE_API int CM_PseudoColor(HImage img, HImage * outImage, uint min , uint max, cv::ColormapTypes types = cv::ColormapTypes::COLORMAP_JET);

extern "C" COLORVISIONCORE_API double CalArtculation(int nw, int nh, char* data);

extern "C" COLORVISIONCORE_API double CalArtculationROI(int nw, int nh, char* data,int x ,int y ,int width, int height);

extern "C" COLORVISIONCORE_API int CM_AutoLevelsAdjust(HImage img, HImage* outImage);

extern "C" COLORVISIONCORE_API int CM_AutomaticColorAdjustment(HImage img, HImage* outImage);

extern "C" COLORVISIONCORE_API int CM_AutomaticToneAdjustment(HImage img, HImage* outImage);




