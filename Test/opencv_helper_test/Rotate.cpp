#include "function.h"

void Rotate(const cv::Mat &srcImage, cv::Mat &dstImage, double angle, cv::Point2f center, double scale)
{
	cv::Mat M = cv::getRotationMatrix2D(center, angle, scale);//������ת�ķ���任���� 
	cv::warpAffine(srcImage, dstImage, M, cv::Size(srcImage.cols, srcImage.rows));//����任  
}
