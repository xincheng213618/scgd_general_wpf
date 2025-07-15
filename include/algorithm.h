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
