#pragma once
#include <opencv2/core.hpp>
typedef struct HImage
{
    int rows;
    int cols;
    int channels;
    int depth; //Bpp
    int stride;
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