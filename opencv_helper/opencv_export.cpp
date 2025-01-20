#include "pch.h"
#include "opencv_export.h"
#include "algorithm.h"
#include <opencv2/opencv.hpp>
#include <nlohmann\json.hpp>

using json = nlohmann::json;

static void MatToHImage(cv::Mat& mat, HImage* outImage)
{
	///这里不分配的话，局部内存会在运行结束之后清空
	outImage->pData = new unsigned char[mat.total() * mat.elemSize()];
	memcpy(outImage->pData, mat.data, mat.total() * mat.elemSize());

	outImage->rows = mat.rows;
	outImage->cols = mat.cols;
	outImage->channels = mat.channels();
	int bitsPerElement = 0;

	switch (mat.depth()) {
	case CV_8U:
	case CV_8S:
		bitsPerElement = 8;
		break;
	case CV_16U:
	case CV_16S:
		bitsPerElement = 16;
		break;
	case CV_32S:
	case CV_32F:
		bitsPerElement = 32;
		break;
	case CV_64F:
		bitsPerElement = 64;
		break;
	default:
		break;
	}
	outImage->depth = bitsPerElement; // 设置每像素位数
	outImage->stride = (int)mat.step; // 设置图像的步长
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

COLORVISIONCORE_API int CM_Fusion(const char* fusionjson, HImage* outImage)
{
	std::chrono::steady_clock::time_point start, end;
	std::chrono::microseconds duration;
	start = std::chrono::high_resolution_clock::now();

	std::string sss = fusionjson;;
	// 从字符串解析 JSON 对象
	json j = json::parse(sss);

	// 检查 JSON 对象是否是数组
	if (!j.is_array()) {
		// 错误处理
		return -1;
	}
	std::vector<cv::Mat> imgs;
	std::vector<std::string> files = j.get<std::vector<std::string>>();

	for (const auto& file : files) {
		cv::Mat img = cv::imread(file);
		if (img.empty()) {
			// 错误处理
			return -1;
		}
		imgs.push_back(img);
	}

	cv::Mat out = fusion(imgs,2);
	duration = std::chrono::duration_cast<std::chrono::microseconds>(end - start);
	std::cout << "fusion执行时间: " << duration.count() / 1000.0 << " 毫秒" << std::endl;
	
	MatToHImage(out, outImage);
	duration = std::chrono::duration_cast<std::chrono::microseconds>(end - start);
	std::cout << "MatToHImage: " << duration.count() / 1000.0 << " 毫秒" << std::endl;
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




void FreeHImageData(unsigned char* data)
{
	// 使用 delete[] 来释放由 new[] 分配的内存
	delete[] data;
}



int ReadGhostImage(const char* FilePath, int singleLedPixelNum, int* LED_pixel_X , int* LED_pixel_Y, int singleGhostPixelNum, int* Ghost_pixel_X, int* Ghost_pixel_Y, HImage* outImage)
{
	cv::Mat mat = cv::imread(FilePath, cv::ImreadModes::IMREAD_UNCHANGED);
	if (mat.empty())
		return -1;


	// 转换为8位图像
	double minVal, maxVal;
	cv::minMaxLoc(mat, &minVal, &maxVal); // 找到图像的最小和最大像素值
	cv::Mat scaledMat;
	mat.convertTo(scaledMat, CV_8UC1, 255.0 / (maxVal - minVal), -minVal * 255.0 / (maxVal - minVal));
	cv::cvtColor(scaledMat, scaledMat, cv::COLOR_GRAY2BGR);

	std::vector<std::vector<cv::Point>> paintContours;

	for (size_t i = 0; i < singleLedPixelNum; i++)
	{
		std::vector<cv::Point> lists;
		lists.push_back(cv::Point(LED_pixel_X[i], LED_pixel_Y[i]));
		paintContours.push_back(lists);
	}

	cv::drawContours(scaledMat, paintContours, -1, cv::Scalar(0, 255,0), -1, 8, cv::noArray(), INT_MAX, cv::Point());


	//paintContours.clear();
	std::vector<std::vector<cv::Point>> paintContours1;


	for (size_t i = 0; i < singleGhostPixelNum; i++)
	{
		std::vector<cv::Point> lists;
		lists.push_back(cv::Point(Ghost_pixel_X[i], Ghost_pixel_Y[i]));
		paintContours1.push_back(lists);
	}
	cv::drawContours(scaledMat, paintContours1, -1, cv::Scalar(0,0, 255), -1, 8, cv::noArray(), INT_MAX, cv::Point());
	//paintContours.clear();

	MatToHImage(scaledMat, outImage);
	return 0;
}

int GhostImage(HImage img, HImage* outImage, int singleLedPixelNum, int* LED_pixel_X, int* LED_pixel_Y, int singleGhostPixelNum, int* Ghost_pixel_X, int* Ghost_pixel_Y)
{
	cv::Mat mat(img.rows, img.cols, img.type(), img.pData);

	if (mat.empty())
		return -1;

	// 转换为8位图像
	double minVal, maxVal;
	cv::minMaxLoc(mat, &minVal, &maxVal); // 找到图像的最小和最大像素值
	cv::Mat scaledMat;
	mat.convertTo(scaledMat, CV_8UC1, 255.0 / (maxVal - minVal), -minVal * 255.0 / (maxVal - minVal));
	cv::cvtColor(scaledMat, scaledMat, cv::COLOR_GRAY2BGR);

	std::vector<std::vector<cv::Point>> paintContours;

	for (size_t i = 0; i < singleLedPixelNum; i++)
	{
		std::vector<cv::Point> lists;
		lists.push_back(cv::Point(LED_pixel_X[i], LED_pixel_Y[i]));
		paintContours.push_back(lists);
	}

	cv::drawContours(scaledMat, paintContours, -1, cv::Scalar(0, 255, 0), -1, 8, cv::noArray(), INT_MAX, cv::Point());


	//paintContours.clear();
	std::vector<std::vector<cv::Point>> paintContours1;


	for (size_t i = 0; i < singleGhostPixelNum; i++)
	{
		std::vector<cv::Point> lists;
		lists.push_back(cv::Point(Ghost_pixel_X[i], Ghost_pixel_Y[i]));
		paintContours1.push_back(lists);
	}
	cv::drawContours(scaledMat, paintContours1, -1, cv::Scalar(0, 0, 255), -1, 8, cv::noArray(), INT_MAX, cv::Point());
	//paintContours.clear();

	MatToHImage(scaledMat, outImage);
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




