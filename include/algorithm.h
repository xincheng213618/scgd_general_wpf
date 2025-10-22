#pragma once

#pragma warning(disable:4305 4244)

#include <opencv2/opencv.hpp>

/// <summary>
/// Ä¦¶ûÎÆÂË³ı
/// </summary>
/// <param name="image"></param>
/// <returns></returns>
cv::Mat removeMoire(const cv::Mat& image);
/// <summary>
/// Ñ°ÕÒ·¢¹âÈ¥
/// </summary>
/// <param name="src"></param>
/// <param name="largestRect"></param>
/// <param name="threshold"></param>
/// <returns></returns>
int findLuminousArea(cv::Mat& src, cv::Rect& largestRect,int threshold);
int findLuminousAreaCorners(cv::Mat& src, std::vector<cv::Point2f>& points, int threshold);

/// <summary>
/// µÆÖé¼ì²â
/// </summary>
/// <param name="image"></param>
/// <param name="rows"></param>
/// <param name="cols"></param>
void LampBeadDetection(cv::Mat image, int rows, int cols);

/// <summary>
/// »æÖÆpoi¹Ø×¢µã
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
/// Î±²ÊÉ«
/// </summary>
/// <param name="image"></param>
/// <param name="min1"></param>
/// <param name="max1"></param>
/// <param name="types"></param>
/// <returns></returns>
int pseudoColor(cv::Mat& image, uint min1, uint max1, cv::ColormapTypes types);

/// <summary>
///×Ô¶¯¶Ô±È¶Èµ÷Õû
/// </summary>
/// <param name="src"></param>
/// <param name="dst"></param>
void autoLevelsAdjust(cv::Mat& src, cv::Mat& dst);

/// <summary>
/// ×Ô¶¯ÑÕÉ«µ÷Õû
/// </summary>
/// <param name="image"></param>
void automaticColorAdjustment(cv::Mat& image);

/// <summary>
/// ×Ô¶¯É«µ÷µ÷Õû
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

//°×Æ½ºâ
void AdjustWhiteBalance(const cv::Mat& src, cv::Mat& dst, double redBalance, double greenBalance, double blueBalance);

/// <summary>
///×Ô¶¯É«½×
/// </summary>
void ApplyGammaCorrection(const cv::Mat& src, cv::Mat& dst, double gamma);

/// <summary>
/// µ÷ÕûÁÁ¶ÈºÍ¶Ô±È¶È
/// </summary>
/// <param name="src"></param>
/// <param name="dst"></param>
/// <param name="alpha"></param>
/// <param name="beta"></param>
void AdjustBrightnessContrast(const cv::Mat& src, cv::Mat& dst, double alpha, double beta);

/// <summary>
/// é«˜æ–¯æ¨¡ç³Š
/// </summary>
/// <param name="src"></param>
/// <param name="dst"></param>
/// <param name="kernelSize">æ ¸å¤§å°(å¿…é¡»ä¸ºå¥‡æ•°)</param>
/// <param name="sigma">æ ‡å‡†å·®</param>
void ApplyGaussianBlur(const cv::Mat& src, cv::Mat& dst, int kernelSize, double sigma);

/// <summary>
/// ä¸­å€¼æ»¤æ³¢
/// </summary>
/// <param name="src"></param>
/// <param name="dst"></param>
/// <param name="kernelSize">æ ¸å¤§å°(å¿…é¡»ä¸ºå¥‡æ•°)</param>
void ApplyMedianBlur(const cv::Mat& src, cv::Mat& dst, int kernelSize);

/// <summary>
/// é”åŒ–
/// </summary>
/// <param name="src"></param>
/// <param name="dst"></param>
void ApplySharpen(const cv::Mat& src, cv::Mat& dst);

/// <summary>
/// Cannyè¾¹ç¼˜æ£€æµ‹
/// </summary>
/// <param name="src"></param>
/// <param name="dst"></param>
/// <param name="threshold1">ç¬¬ä¸€ä¸ªé˜ˆå€¼</param>
/// <param name="threshold2">ç¬¬äºŒä¸ªé˜ˆå€¼</param>
void ApplyCannyEdgeDetection(const cv::Mat& src, cv::Mat& dst, double threshold1, double threshold2);

/// <summary>
/// ç›´æ–¹å›¾å‡è¡¡åŒ– (ä»…ç”¨äºç°åº¦å›¾åƒ)
/// </summary>
/// <param name="src"></param>
/// <param name="dst"></param>
void ApplyHistogramEqualization(const cv::Mat& src, cv::Mat& dst);
