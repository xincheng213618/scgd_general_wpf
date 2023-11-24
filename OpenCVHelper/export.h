#pragma once

#include <string>
#include <opencv2/opencv.hpp>


#ifdef OPENCV_EXPORTS
#define COLORVISIONCORE_API __declspec(dllexport)
#else
#define OPENCV_API __declspec(dllimport)
#endif

extern "C" COLORVISIONCORE_API double CalArtculation(int nw, int nh, char* data);
extern "C" COLORVISIONCORE_API double CalArtculationROI(int nw, int nh, char* data,int x ,int y ,int width, int height);

