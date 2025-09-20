#include "function.h"

void Rotate(const cv::Mat &srcImage, cv::Mat &dstImage, double angle, cv::Point2f center, double scale)
{
	cv::Mat M = cv::getRotationMatrix2D(center, angle, scale);//计算旋转的仿射变换矩阵 
	cv::warpAffine(srcImage, dstImage, M, cv::Size(srcImage.cols, srcImage.rows));//仿射变换  
}
