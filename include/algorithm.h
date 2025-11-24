#pragma once

#pragma warning(disable:4305 4244)

#include <opencv2/opencv.hpp>

/// <summary>
/// Ħ�����˳�
/// </summary>
/// <param name="image"></param>
/// <returns></returns>
cv::Mat removeMoire(const cv::Mat& image);
/// <summary>
/// Ѱ�ҷ���ȥ
/// </summary>
/// <param name="src"></param>
/// <param name="largestRect"></param>
/// <param name="threshold"></param>
/// <returns></returns>
int findLuminousArea(cv::Mat& src, cv::Rect& largestRect,int threshold);
int findLuminousAreaCorners(cv::Mat& src, std::vector<cv::Point2f>& points, int threshold);

/// <summary>
/// ������
/// </summary>
/// <param name="image"></param>
/// <param name="rows"></param>
/// <param name="cols"></param>
void LampBeadDetection(cv::Mat image, int rows, int cols);

/// <summary>
/// ����poi��ע��
/// </summary>
/// <param name="img"></param>
/// <param name="dst"></param>
/// <param name="radius"></param>
/// <param name="points"></param>
/// <param name="pointCount"></param>
/// <param name="thickness"></param>
/// <returns></returns>
int drawPoiImage(cv::Mat& img, cv::Mat& dst, int radius, int* points, int pointCount,int thickness);

/// <summary>
/// α��ɫ
/// </summary>
/// <param name="image"></param>
/// <param name="min1"></param>
/// <param name="max1"></param>
/// <param name="types"></param>
/// <returns></returns>
int pseudoColor(cv::Mat& image, uint min1, uint max1, cv::ColormapTypes types);

/// <summary>
///�Զ��Աȶȵ���
/// </summary>
/// <param name="src"></param>
/// <param name="dst"></param>
void autoLevelsAdjust(cv::Mat& src, cv::Mat& dst);

/// <summary>
/// �Զ���ɫ����
/// </summary>
/// <param name="image"></param>
void automaticColorAdjustment(cv::Mat& image);

/// <summary>
/// �Զ�ɫ������
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

//��ƽ��
void AdjustWhiteBalance(const cv::Mat& src, cv::Mat& dst, double redBalance, double greenBalance, double blueBalance);

/// <summary>
///�Զ�ɫ��
/// </summary>
void ApplyGammaCorrection(const cv::Mat& src, cv::Mat& dst, double gamma);

/// <summary>
/// �������ȺͶԱȶ�
/// </summary>
/// <param name="src"></param>
/// <param name="dst"></param>
/// <param name="alpha"></param>
/// <param name="beta"></param>
void AdjustBrightnessContrast(const cv::Mat& src, cv::Mat& dst, double alpha, double beta);

/// <summary>
/// 高斯模糊
/// </summary>
/// <param name="src"></param>
/// <param name="dst"></param>
/// <param name="kernelSize">核大小(必须为奇数)</param>
/// <param name="sigma">标准差</param>
void ApplyGaussianBlur(const cv::Mat& src, cv::Mat& dst, int kernelSize, double sigma);

/// <summary>
/// 中值滤波
/// </summary>
/// <param name="src"></param>
/// <param name="dst"></param>
/// <param name="kernelSize">核大小(必须为奇数)</param>
void ApplyMedianBlur(const cv::Mat& src, cv::Mat& dst, int kernelSize);

/// <summary>
/// 锐化
/// </summary>
/// <param name="src"></param>
/// <param name="dst"></param>
void ApplySharpen(const cv::Mat& src, cv::Mat& dst);

/// <summary>
/// Canny边缘检测
/// </summary>
/// <param name="src"></param>
/// <param name="dst"></param>
/// <param name="threshold1">第一个阈值</param>
/// <param name="threshold2">第二个阈值</param>
void ApplyCannyEdgeDetection(const cv::Mat& src, cv::Mat& dst, double threshold1, double threshold2);

/// <summary>
/// 直方图均衡化 (仅用于灰度图像)
/// </summary>
/// <param name="src"></param>
/// <param name="dst"></param>
void ApplyHistogramEqualization(const cv::Mat& src, cv::Mat& dst);

/// <summary>
/// 寻找灯珠 (Find Light Beads)
/// </summary>
/// <param name="src">输入图像</param>
/// <param name="centers">输出：检测到的灯珠中心点坐标</param>
/// <param name="blackCenters">输出：缺失的灯珠位置坐标</param>
/// <param name="threshold">二值化阈值</param>
/// <param name="minSize">最小灯珠尺寸</param>
/// <param name="maxSize">最大灯珠尺寸</param>
/// <param name="rows">预期灯珠行数(用于计算缺失点)</param>
/// <param name="cols">预期灯珠列数(用于计算缺失点)</param>
/// <returns>0表示成功，负数表示错误</returns>
int findLightBeads(
    cv::Mat& src, 
    std::vector<cv::Point>& centers, 
    std::vector<cv::Point>& blackCenters,
    int threshold = 20,
    int minSize = 2,
    int maxSize = 20,
    int rows = 650,
    int cols = 850
);
