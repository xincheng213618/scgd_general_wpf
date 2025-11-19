#pragma once

#include <string>
#include <opencv2/opencv.hpp>

#include "custom_structs.h"
#include "common.h"

#ifdef OPENCV_EXPORTS
#define COLORVISIONCORE_API __declspec(dllexport)
#else
#define COLORVISIONCORE_API __declspec(dllimport)
#endif

enum FocusAlgorithm {
    Variance = 0,
    StandardDeviation = 1,
    Tenengrad = 2,
    Laplacian = 3,
    VarianceOfLaplacian = 4,
    EnergyOfGradient = 5,
    SpatialFrequency = 6
    // CalResol �Ƚϸ��ӣ�ͨ����Ҫ�ض�ͼ�������ﲻ��Ϊͨ�öԽ��㷨
};

// ����������ö��
enum class StitchingErrorCode {
    SUCCESS = 0,          // �ɹ�
    EMPTY_INPUT = -1,     // ����Ϊ��
    FILE_NOT_FOUND = -2,  // �ļ�δ�ҵ�
    DIFFERENT_DIMENSIONS = -3, // �ߴ粻ͬ
    DIFFERENT_TYPE = -4,  // ���Ͳ�ͬ
    NO_VALID_IMAGES = -5 // û����Ч��ͼ��
};

extern "C" COLORVISIONCORE_API int M_ExtractChannel(HImage img, HImage* outImage, int channel);
extern "C" COLORVISIONCORE_API int M_PseudoColor(HImage img, HImage* outImage, uint min, uint max, cv::ColormapTypes types = cv::ColormapTypes::COLORMAP_JET, int channel = -1);

extern "C" COLORVISIONCORE_API int M_AutoLevelsAdjust(HImage img, HImage* outImage);

extern "C" COLORVISIONCORE_API int M_AutomaticColorAdjustment(HImage img, HImage* outImage);

extern "C" COLORVISIONCORE_API int M_AutomaticToneAdjustment(HImage img, HImage* outImage);

extern "C" COLORVISIONCORE_API int M_DrawPoiImage(HImage img, HImage* outImage, int radius, int* point, int pointCount, int thickness);

extern "C" COLORVISIONCORE_API int M_ConvertImage(HImage img, uchar** rowGrayPixels, int* length, int* scaleFactor, int targetPixelsX = 512, int targetPixelsY = 512);

extern "C" COLORVISIONCORE_API double M_CalArtculation(HImage img, FocusAlgorithm type, int roi_x, int roi_y, int roi_width, int roi_height);

extern "C" COLORVISIONCORE_API int M_GetWhiteBalance(HImage img, HImage* outImage, double redBalance, double greenBalance, double blueBalance);

extern "C" COLORVISIONCORE_API int M_ApplyGammaCorrection(HImage img, HImage* outImage, double gamma);

extern "C" COLORVISIONCORE_API int M_AdjustBrightnessContrast(HImage img, HImage* outImage, double alpha, double beta);

extern "C" COLORVISIONCORE_API int M_InvertImage(HImage img, HImage* outImage);

extern "C" COLORVISIONCORE_API int M_Threshold(HImage img, HImage* outImage, double thresh, double maxval, int type);

extern "C" COLORVISIONCORE_API int M_FindLuminousArea(HImage img,const char* config, char** result);

extern "C" COLORVISIONCORE_API int M_ConvertGray32Float(HImage img, HImage* outImage);

extern "C" COLORVISIONCORE_API int M_StitchImages(const char* config, HImage* outImage);

extern "C" COLORVISIONCORE_API int M_Fusion(const char* fusionjson, HImage* outImage);

extern "C" COLORVISIONCORE_API int M_RemoveMoire(HImage img, HImage* outImage);

extern "C" COLORVISIONCORE_API int M_ApplyGaussianBlur(HImage img, HImage* outImage, int kernelSize, double sigma);

extern "C" COLORVISIONCORE_API int M_ApplyMedianBlur(HImage img, HImage* outImage, int kernelSize);

extern "C" COLORVISIONCORE_API int M_ApplySharpen(HImage img, HImage* outImage);

extern "C" COLORVISIONCORE_API int M_ApplyCannyEdgeDetection(HImage img, HImage* outImage, double threshold1, double threshold2);

extern "C" COLORVISIONCORE_API int M_ApplyHistogramEqualization(HImage img, HImage* outImage);

extern "C" COLORVISIONCORE_API int FreeResult(char* result) {
    delete[] result;
    return 0;
}

extern "C" COLORVISIONCORE_API int M_CalSFR(
    HImage img,
    double del,
    int roi_x, int roi_y, int roi_width, int roi_height,
    double* freq,   // 输出：频率数组
    double* sfr,    // 输出：SFR 数组
    int    maxLen,  // 输入：数组容量
    int* outLen,  // 输出：实际长度
    double* mtf10_norm,
    double* mtf50_norm,
    double* mtf10_cypix,
    double* mtf50_cypix);

// Multi-channel SFR calculation for RGB + L channels
// For 3-channel images: outputs R, G, B, L (4 channels)
// For single-channel images: outputs only L (1 channel)
// L is calculated as: Y = 0.213*R + 0.715*G + 0.072*B
extern "C" COLORVISIONCORE_API int M_CalSFRMultiChannel(
    HImage img,
    double del,
    int roi_x, int roi_y, int roi_width, int roi_height,
    double* freq,           // 输出：频率数组（所有通道共享）
    double* sfr_r,          // 输出：R通道 SFR（3通道时有效）
    double* sfr_g,          // 输出：G通道 SFR（3通道时有效）
    double* sfr_b,          // 输出：B通道 SFR（3通道时有效）
    double* sfr_l,          // 输出：L通道 SFR（总是输出）
    int    maxLen,          // 输入：数组容量
    int* outLen,            // 输出：实际长度
    int* channelCount,      // 输出：有效通道数（1或4）
    double* mtf10_norm_r, double* mtf50_norm_r, double* mtf10_cypix_r, double* mtf50_cypix_r,
    double* mtf10_norm_g, double* mtf50_norm_g, double* mtf10_cypix_g, double* mtf50_cypix_g,
    double* mtf10_norm_b, double* mtf50_norm_b, double* mtf10_cypix_b, double* mtf50_cypix_b,
    double* mtf10_norm_l, double* mtf50_norm_l, double* mtf10_cypix_l, double* mtf50_cypix_l);


