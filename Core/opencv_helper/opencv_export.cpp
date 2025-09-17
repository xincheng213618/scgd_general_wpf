#include "pch.h"
#include "opencv_export.h"
#include "algorithm.h"
#include <opencv2/opencv.hpp>
#include <nlohmann\json.hpp>

using json = nlohmann::json;


void FreeHImageData(unsigned char* data)
{
	// 使用 delete[] 来释放由 new[] 分配的内存
	delete[] data;
}

int CM_AutoLevelsAdjust(HImage img, HImage* outImage)
{
	cv::Mat mat(img.rows, img.cols, img.type(), img.pData);
	if (mat.empty())
		return -1;
	if (mat.channels() != 3) {
		return -1;
	}
	if (mat.depth() == CV_16U) {
		cv::normalize(mat, mat, 0, 255, cv::NORM_MINMAX, CV_8U);
	}
	cv::Mat outMat;
	autoLevelsAdjust(mat, outMat);
	MatToHImage(outMat, outImage);
	return 0;
}

COLORVISIONCORE_API int CM_AutomaticColorAdjustment(HImage img, HImage* outImage)
{
	cv::Mat mat(img.rows, img.cols, img.type(), img.pData);
	if (mat.empty())
		return -1;
	if (mat.channels() != 3) {
		return -1;
	}
	if (mat.depth() == CV_16U) {
		cv::normalize(mat, mat, 0, 255, cv::NORM_MINMAX, CV_8U);
	}
	automaticColorAdjustment(mat);
	MatToHImage(mat, outImage);
	return 0;
}

COLORVISIONCORE_API int CM_AutomaticToneAdjustment(HImage img, HImage* outImage)
{
	cv::Mat mat(img.rows, img.cols, img.type(), img.pData);
	if (mat.empty())
		return -1;
	if (mat.channels() != 3) {
		return -1;
	}
	if (mat.depth() == CV_16U) {
		cv::normalize(mat, mat, 0, 255, cv::NORM_MINMAX, CV_8U);
	}
	automaticToneAdjustment(mat,1);
	MatToHImage(mat, outImage);
	return 0;
}

COLORVISIONCORE_API int CM_ExtractChannel(HImage img, HImage* outImage, int channel)
{
	cv::Mat mat(img.rows, img.cols, img.type(), img.pData);
	if (mat.empty())
		return -1;
	cv::Mat outMat;
	int i = extractChannel(mat, outMat, channel);
	if (i != 0)
		return i;
	MatToHImage(outMat, outImage);
	return 0;	
}

COLORVISIONCORE_API int CM_PseudoColor(HImage img, HImage* outImage, uint min, uint max, cv::ColormapTypes types)
{
	cv::Mat mat(img.rows, img.cols, img.type(), img.pData);

	if (mat.empty())
		return -1;

	if (mat.channels() != 1) {
		cv::cvtColor(mat, mat, cv::COLOR_BGR2GRAY);
	}
	if (mat.depth() == CV_16U) {
		cv::normalize(mat, mat, 0, 255, cv::NORM_MINMAX, CV_8U);
	}
	pseudoColor(mat, min,max, types);
	///这里不分配的话，局部内存会在运行结束之后清空
	MatToHImage(mat, outImage);
	return 0;
}


double CalArtculation(int nw,int nh,char* data) {
	cv::Mat img =cv::Mat(nh, nw, CV_8UC1, data);
	//cv::Mat gray;

	//cv::cvtColor(img, gray, cv::ColorConversionCodes::COLOR_RGB2GRAY);
	cv::Mat TempMen;
	cv::Mat TempStd;
	cv::meanStdDev(img, TempMen, TempStd);

	double value = TempStd.at<double>(0, 0);
	return value;
}

double CalArtculationROI(int nw, int nh, char* data, int x, int y, int width, int height) {
	cv::Mat img = cv::Mat(nh, nw, CV_8UC1, data);
	cv::Mat m_roi = img(cv::Rect(x, y, width, height));

	//cv::Mat gray;

	//cv::cvtColor(m_roi, gray, cv::ColorConversionCodes::COLOR_RGB2GRAY);
	cv::Mat TempMen;
	cv::Mat TempStd;
	cv::meanStdDev(m_roi, TempMen, TempStd);

	double value = TempStd.at<double>(0, 0);
	return value;
}




