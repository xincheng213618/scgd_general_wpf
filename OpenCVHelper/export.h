#pragma once

#include <string>
#include <opencv2/opencv.hpp>


#ifdef OPENCV_EXPORTS
#define COLORVISIONCORE_API __declspec(dllexport)
#else
#define OPENCV_API __declspec(dllimport)
#endif

typedef struct HImage
{
    int rows;
    int cols;
    int channels;
    int depth; //Bpp
    int type()  const {
        int cv_depth = CV_8U;
        switch (depth) {
        case 8:
            cv_depth = CV_8U;
            break;
        case 16:
            cv_depth = CV_16U;
            break;
        case 32:
            cv_depth = CV_32F;
            break;
        case 64:
            cv_depth = CV_64F;
            break;
        default:
            break;
        }
        return CV_MAKETYPE(cv_depth, channels);
    }
    int elemSize() const { return  ((((((((depth) & ((1 << 3) - 1)) + (((channels)-1) << 3))) & ((512 - 1) << 3)) >> 3) + 1) * ((0x28442211 >> (((((depth) & ((1 << 3) - 1)) + (((channels)-1) << 3))) & ((1 << 3) - 1)) * 4) & 15)); }
    unsigned char* pData;
}HImage;

extern "C" COLORVISIONCORE_API void FreeHImageData(unsigned char* data);

extern "C" COLORVISIONCORE_API int ReadGhostImage(const char* FilePath, int singleLedPixelNum, int* LED_pixel_X, int* LED_pixel_Y, int singleGhostPixelNum, int* Ghost_pixel_X, int* Ghost_pixel_Y, HImage * outImage);

extern "C" COLORVISIONCORE_API int CM_PseudoColor(HImage img, HImage * outImage, uint min , uint max, cv::ColormapTypes types = cv::ColormapTypes::COLORMAP_JET);

extern "C" COLORVISIONCORE_API double CalArtculation(int nw, int nh, char* data);

extern "C" COLORVISIONCORE_API double CalArtculationROI(int nw, int nh, char* data,int x ,int y ,int width, int height);

extern "C" COLORVISIONCORE_API int CM_AutoLevelsAdjust(HImage img, HImage* outImage);

extern "C" COLORVISIONCORE_API int CM_AutomaticColorAdjustment(HImage img, HImage* outImage);

extern "C" COLORVISIONCORE_API int CM_AutomaticToneAdjustment(HImage img, HImage* outImage);
