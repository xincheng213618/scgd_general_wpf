#pragma once

#pragma warning(disable:4305 4244)

#include <opencv2/opencv.hpp>
int findLuminousArea(cv::Mat& src, cv::Rect& largestRect,int threshold);

int drawPoiImage(cv::Mat& img, cv::Mat& dst, int radius, int* points, int pointCount,int thickness);

/// <summary>
/// 伪彩色
/// </summary>
/// <param name="image"></param>
/// <param name="min1"></param>
/// <param name="max1"></param>
/// <param name="types"></param>
/// <returns></returns>
int pseudoColor(cv::Mat& image, uint min1, uint max1, cv::ColormapTypes types);

/// <summary>
///自动对比度调整
/// </summary>
/// <param name="src"></param>
/// <param name="dst"></param>
void autoLevelsAdjust(cv::Mat& src, cv::Mat& dst);

/// <summary>
/// 自动颜色调整
/// </summary>
/// <param name="image"></param>
void automaticColorAdjustment(cv::Mat& image);

/// <summary>
/// 自动色调调整
/// </summary>
/// <param name="image"></param>
/// <param name="clip_hist_percent"></param>
void automaticToneAdjustment(cv::Mat& image, double clip_hist_percent = 1);

/// <summary>
/// 
/// </summary>
/// <param name="imgs"></param>
/// <param name="STEP"></param>
/// <returns></returns>
cv::Mat fusion(std::vector<cv::Mat> imgs, int STEP);

int extractChannel(cv::Mat& input, cv::Mat& dst, int channel);

//白平衡
void AdjustWhiteBalance(const cv::Mat& src, cv::Mat& dst, double redBalance, double greenBalance, double blueBalance);

/// <summary>
///自动色阶
/// </summary>
void ApplyGammaCorrection(const cv::Mat& src, cv::Mat& dst, double gamma);

/// <summary>
/// 调整亮度和对比度
/// </summary>
/// <param name="src"></param>
/// <param name="dst"></param>
/// <param name="alpha"></param>
/// <param name="beta"></param>
void AdjustBrightnessContrast(const cv::Mat& src, cv::Mat& dst, double alpha, double beta);
