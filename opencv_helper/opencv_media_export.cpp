#include "pch.h"
#include "opencv_media_export.h"
#include "algorithm.h"
#include <opencv2/opencv.hpp>
#include <nlohmann\json.hpp>

using json = nlohmann::json;

std::vector<std::pair<uintptr_t, cv::Mat>> MediaList;

std::mutex mediaListMutex; // 定义一个互斥锁

static void MatToHImage(cv::Mat& mat, HImage* outImage)
{
	std::lock_guard<std::mutex> lock(mediaListMutex); // 加锁


	outImage->rows = mat.rows;
	outImage->cols = mat.cols;
	outImage->channels = mat.channels();
	outImage->pData = mat.data;
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
	MediaList.push_back(std::make_pair(reinterpret_cast<uintptr_t>(outImage->pData), mat));
}

COLORVISIONCORE_API double M_CalArtculation(HImage img, EvaFunc type) {

	cv::Mat mat(img.rows, img.cols, img.type(), img.pData);
	if (mat.channels() == 3)
	{
		cv::cvtColor(mat, mat, cv::ColorConversionCodes::COLOR_BGR2GRAY);
	}
	cv::Mat TempMen;
	cv::Mat TempStd;
	double value = -1;
	switch (type)
	{
	case Variance:
		cv::meanStdDev(mat, TempMen, TempStd);
		value = TempStd.at<double>(0, 0);
	case Tenengrad:
		//cv::Sobel(tempImg, imgSobel, CV_8UC1, dx, dy, ksize);
		//convertScaleAbs(imgSobel, imgSobel, 1, 0);
		//articulation = mean(imgSobel)[0];
		break;
	case Laplace:
		cv::Laplacian(mat, TempStd, CV_8UC1);
		value = cv::mean(TempStd)[0];
	case CalResol:
		break;
	default:
		cv::meanStdDev(mat, TempMen, TempStd);
		value = TempStd.at<double>(0, 0);
	}

	return value;
}

COLORVISIONCORE_API void M_FreeHImageData(unsigned char* data)
{
	uintptr_t address = reinterpret_cast<uintptr_t>(data);
	std::lock_guard<std::mutex> lock(mediaListMutex); // 加锁

	std::vector<std::pair<uintptr_t, cv::Mat>>::iterator it = std::find_if(
		MediaList.begin(), MediaList.end(),
		[address](const std::pair<uintptr_t, cv::Mat>& pair) {
			return pair.first == address;
		});

	if (it != MediaList.end()) {
		it->second.release();
		// 从缓存中移除
		MediaList.erase(it);
	}
}


COLORVISIONCORE_API int M_PseudoColor(HImage img, HImage* outImage, uint min, uint max, cv::ColormapTypes types, int channel)
{
	cv::Mat mat(img.rows, img.cols, img.type(), img.pData);

	if (mat.empty())
		return -1;

	cv::Mat out = mat.clone();
	if (out.channels() != 1) {
		if (channel >= 0 && channel < mat.channels()) {
			std::vector<cv::Mat> channels;
			cv::split(mat, channels);
			out = channels[channel];
		}
		else {
			// Default to converting to grayscale if no valid channel is specified
			cv::cvtColor(mat, out, cv::COLOR_BGR2GRAY);
		}
	}
	pseudoColor(out, min, max, types);
	///这里不分配的话，局部内存会在运行结束之后清空
	MatToHImage(out, outImage);
	return 0;
}

COLORVISIONCORE_API int M_AutoLevelsAdjust(HImage img, HImage* outImage)
{
	cv::Mat mat(img.rows, img.cols, img.type(), img.pData);

	if (mat.empty())
		return -1;
	if (mat.channels() ==1 ) {
		return -1;
	}
	cv::Mat out = mat.clone();
	if (out.depth() == CV_16U) {
		cv::normalize(out, out, 0, 255, cv::NORM_MINMAX, CV_8U);
	}
	cv::Mat outMat;
	autoLevelsAdjust(out, outMat);
	out.release();
	MatToHImage(outMat, outImage);
	return 0;
}

COLORVISIONCORE_API int M_AutomaticColorAdjustment(HImage img, HImage* outImage)
{
	cv::Mat mat(img.rows, img.cols, img.type(), img.pData);
	if (mat.empty())
		return -1;
	if (mat.channels() == 1) {
		return -1;
	}
	cv::Mat out = mat.clone();

	if (out.depth() == CV_16U) {
		cv::normalize(out, out, 0, 255, cv::NORM_MINMAX, CV_8U);
	}
	automaticColorAdjustment(out);
	MatToHImage(out, outImage);
	return 0;
}

COLORVISIONCORE_API int M_AutomaticToneAdjustment(HImage img, HImage* outImage)
{
	cv::Mat mat(img.rows, img.cols, img.type(), img.pData);
	if (mat.empty())
		return -1;
	if (mat.channels() == 1) {
		return -1;
	}
	cv::Mat out = mat.clone();
	if (mat.depth() == CV_16U) {
		cv::normalize(out, out, 0, 255, cv::NORM_MINMAX, CV_8U);
	}
	automaticToneAdjustment(out, 1);
	MatToHImage(out, outImage);
	return 0;
}

COLORVISIONCORE_API int M_DrawPoiImage(HImage img, HImage* outImage,int radius, int* point , int pointCount, int thickness)
{
	cv::Mat mat(img.rows, img.cols, img.type(), img.pData);
	if (mat.empty())
		return -1;
	if (mat.channels() != 3) {
		if (mat.channels() == 1) {
			// 将单通道图像转换为三通道
			cv::cvtColor(mat, mat, cv::COLOR_GRAY2BGR);
		}
	}

	cv::Mat out = mat.clone();
	drawPoiImage(out, out, radius, point, pointCount, thickness);
	MatToHImage(out, outImage);
	return 0;
}

int FindClosestFactor(int value, const int* allowedFactors, int size = 13)
{
	int closestFactor = allowedFactors[0];
	for (int i = 1; i < size; ++i)
	{
		if (std::abs(value - allowedFactors[i]) < std::abs(value - closestFactor))
		{
			closestFactor = allowedFactors[i];
		}
	}
	return closestFactor;
}

COLORVISIONCORE_API int M_ConvertImage(HImage img, uchar** rowGrayPixels, int* length, int* scaleFactout,int targetPixelsX, int targetPixelsY)
{
	cv::Mat mat(img.rows, img.cols, img.type(), img.pData);
	if (mat.empty())
		return -1;
	// 如果是彩色图像，转换为灰度图
	if (mat.channels() == 3 || mat.channels() == 4)  // 判断是否为彩色图（BGR 或 BGRA）
	{
		cv::cvtColor(mat, mat, cv::COLOR_BGR2GRAY); // 转换为灰度图
	}
	else
	{
		cv::normalize(mat, mat, 0, 255, cv::NORM_MINMAX, CV_8U);
	}

	// 目标分辨率设置
	int targetPixels = targetPixelsX * targetPixelsY; // 目标像素数（可以调整）
	int originalWidth = mat.cols;
	int originalHeight = mat.rows;

	// 计算初始比例因子
	double initialScaleFactor = std::sqrt((double)originalWidth * originalHeight / targetPixels);

	// 确保比例因子是 1、2、4、8 等倍数
	int allowedFactors[] = { 1, 2, 4, 8, 16, 32, 64, 128, 256, 512, 1024, 2048 };
	int scaleFactor = FindClosestFactor((int)std::round(initialScaleFactor), allowedFactors);
	// 计算新的宽度和高度
	int newWidth = originalWidth / scaleFactor;
	int newHeight = originalHeight / scaleFactor;

	// 分配内存给 rowGrayPixels
	*length = newWidth * newHeight;
	*rowGrayPixels = new uchar[*length];

	// 并行处理图像像素缩放
#pragma omp parallel for
	for (int y = 0; y < newHeight; ++y)
	{
		uchar* row = *rowGrayPixels + y * newWidth;
		for (int x = 0; x < newWidth; ++x)
		{
			int oldX = x * scaleFactor;
			int oldY = y * scaleFactor;
			int oldIndex = oldY * mat.cols + oldX;

			// 将像素值存储到 rowGrayPixels
			row[x] = mat.data[oldIndex];
		}
	}


	return 0;
}

COLORVISIONCORE_API int M_ExtractChannel(HImage img, HImage* outImage, int channel)
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


COLORVISIONCORE_API int M_GetWhiteBalance(HImage img, HImage* outImage, double redBalance, double greenBalance, double blueBalance)
{
	cv::Mat mat(img.rows, img.cols, img.type(), img.pData);
	cv::Mat dst;

	AdjustWhiteBalance(mat,dst, redBalance, greenBalance, blueBalance);

	MatToHImage(dst, outImage);
	return 0;
}

COLORVISIONCORE_API int M_ApplyGammaCorrection(HImage img, HImage* outImage, double gamma)
{
	cv::Mat mat(img.rows, img.cols, img.type(), img.pData);
	cv::Mat dst;

	ApplyGammaCorrection(mat, dst, gamma);

	MatToHImage(dst, outImage);
	return 0;
}

COLORVISIONCORE_API int M_AdjustBrightnessContrast(HImage img, HImage* outImage, double alpha, double beta)
{
	cv::Mat mat(img.rows, img.cols, img.type(), img.pData);

	cv::Mat dst;
	AdjustBrightnessContrast(mat, dst, alpha, beta);

	MatToHImage(dst, outImage);
	return 0;
}

/// <summary>
/// 反相
/// </summary>
/// <param name="img"></param>
/// <param name="outImage"></param>
/// <returns></returns>
COLORVISIONCORE_API int M_InvertImage(HImage img, HImage* outImage)
{
	cv::Mat mat(img.rows, img.cols, img.type(), img.pData);

	cv::Mat dst;
	cv::bitwise_not(mat, dst);

	MatToHImage(dst, outImage);
	return 0;
}

/// <summary>
/// 二值化
/// </summary>
/// <param name="img"></param>
/// <param name="outImage"></param>
/// <returns></returns>
COLORVISIONCORE_API int M_Threshold(HImage img, HImage* outImage, double thresh, double maxval, int type)
{
	cv::Mat mat(img.rows, img.cols, img.type(), img.pData);

	cv::Mat dst;
	cv::threshold(mat, dst, thresh, maxval, type);

	MatToHImage(dst, outImage);
	return 0;
}

COLORVISIONCORE_API int M_FindLuminousArea(HImage img, const char* config, char** result)
{
	cv::Mat mat(img.rows, img.cols, img.type(), img.pData);
	// 检查输入图像是否为空
	if (mat.empty()) {
		return -1;
	}

	if (!config || !result) {
		return -1; 
	}        
	json j = json::parse(config);
	int threshold = j.at("Threshold").get<int>();


	cv::Rect LuminousArea;
	
	findLuminousArea(mat, LuminousArea, threshold);

	json outputJson;
	outputJson["X"] = LuminousArea.x;
	outputJson["Y"] = LuminousArea.y;
	outputJson["Width"] = LuminousArea.width;
	outputJson["Height"] = LuminousArea.height;

	std::string output = outputJson.dump();
	size_t length = output.length() + 1;
	*result = new char[length];
	if (!*result) {
		return -2; // 错误：内存分配失败
	}
	std::strcpy(*result, output.c_str());
	return static_cast<int>(length);
}

COLORVISIONCORE_API int M_ConvertGray32Float(HImage img, HImage* outImage)
{
	cv::Mat mat(img.rows, img.cols, img.type(), img.pData);

	if (mat.depth() == CV_32FC1) {
		cv::Mat outMat(img.rows, img.cols, CV_16UC1);

		// 找到图像中的最小值和最大值
		double minVal, maxVal;
		cv::minMaxLoc(mat, &minVal, &maxVal);

		// 计算比例因子和标量值
		float scale = 65535 / (maxVal - minVal);
		float delta = -minVal * scale;

		// 将32位浮点图像转换为16位灰度图像
		mat.convertTo(outMat, CV_16UC1, scale, delta);

		MatToHImage(outMat, outImage);


		return 0;
	}
	return -1;
}

COLORVISIONCORE_API int M_CvtColor(HImage img, HImage* outImage, double thresh, double maxval, int type)
{
	cv::Mat mat(img.rows, img.cols, img.type(), img.pData);

	cv::Mat dst;
	cv::cvtColor(mat, dst, cv::COLOR_RGBA2GRAY);

	MatToHImage(dst, outImage);
	return 0;
}
COLORVISIONCORE_API int M_RemoveMoire(HImage img, HImage* outImage)
{
	cv::Mat mat(img.rows, img.cols, img.type(), img.pData);
	cv::Mat dst = removeMoire(mat);
	MatToHImage(dst, outImage);
	return 0;
}






