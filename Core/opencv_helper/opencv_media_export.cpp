#include "pch.h"
#include "Windows.h"
#include "opencv_media_export.h"
#include "algorithm.h"
#include <opencv2/opencv.hpp>
#include <nlohmann\json.hpp>
#include <string>
#include <locale>
#include <codecvt>
#include <combaseapi.h>

using json = nlohmann::json;


COLORVISIONCORE_API void M_FreeHImageData(unsigned char* data)
{

}


COLORVISIONCORE_API double M_CalArtculation(HImage img, FocusAlgorithm type, int roi_x, int roi_y, int roi_width, int roi_height)
{

	// 1. 将 HImage 数据包装成 cv::Mat，无数据拷贝
	cv::Mat full_mat(img.rows, img.cols, img.type(), img.pData);
	if (full_mat.empty() || full_mat.data == nullptr) {
		return -1.0; // 无效图像
	}

	// 2. 根据ROI参数确定处理区域
	cv::Rect roi(roi_x, roi_y, roi_width, roi_height);
	bool use_roi = (roi.width > 0 && roi.height > 0 && (roi & cv::Rect(0, 0, full_mat.cols, full_mat.rows)) == roi);

	cv::Mat mat = use_roi ? full_mat(roi) : full_mat;

	// 3. 转换为灰度图进行计算
	cv::Mat gray_mat;
	if (mat.channels() == 3 || mat.channels() == 4)
	{
		cv::cvtColor(mat, gray_mat, cv::COLOR_BGRA2GRAY);
	}
	else
	{
		gray_mat = mat;
	}

	double value = -1.0;
	cv::Mat mean, stddev;
	cv::Mat laplacian_mat;
	cv::Mat grad_x, grad_y;
	cv::Mat abs_grad_x, abs_grad_y;
	cv::Mat gradient_mat;

	switch (type)
	{
	case Variance:
		cv::meanStdDev(gray_mat, mean, stddev);
		value = stddev.at<double>(0, 0) * stddev.at<double>(0, 0);
		break; // 必须加 break!

	case StandardDeviation:
		cv::meanStdDev(gray_mat, mean, stddev);
		value = stddev.at<double>(0, 0);
		break;

	case Tenengrad:
		cv::Sobel(gray_mat, grad_x, CV_64F, 1, 0, 3);
		cv::Sobel(gray_mat, grad_y, CV_64F, 0, 1, 3);
		cv::pow(grad_x, 2, grad_x);
		cv::pow(grad_y, 2, grad_y);
		cv::add(grad_x, grad_y, gradient_mat);
		cv::sqrt(gradient_mat, gradient_mat);
		value = cv::mean(gradient_mat)[0];
		break;

	case Laplacian:
		cv::Laplacian(gray_mat, laplacian_mat, CV_8UC1);
		value = cv::mean(cv::abs(laplacian_mat))[0];
		break;

	case VarianceOfLaplacian: // 推荐！非常鲁棒的算法
		cv::Laplacian(gray_mat, laplacian_mat, CV_8UC1);
		cv::meanStdDev(laplacian_mat, mean, stddev);
		value = stddev.at<double>(0, 0) * stddev.at<double>(0, 0);
		break;

	case EnergyOfGradient:
		// 通过减去移位后的自身来计算梯度
		gray_mat.convertTo(gradient_mat, CV_64F);
		cv::subtract(gradient_mat(cv::Rect(1, 0, gradient_mat.cols - 1, gradient_mat.rows)), gradient_mat(cv::Rect(0, 0, gradient_mat.cols - 1, gradient_mat.rows)), grad_x);
		cv::subtract(gradient_mat(cv::Rect(0, 1, gradient_mat.cols, gradient_mat.rows - 1)), gradient_mat(cv::Rect(0, 0, gradient_mat.cols, gradient_mat.rows - 1)), grad_y);
		cv::pow(grad_x, 2, grad_x);
		cv::pow(grad_y, 2, grad_y);
		value = cv::mean(grad_x)[0] + cv::mean(grad_y)[0];
		break;

	case SpatialFrequency:
	{
		// 确保 gray_mat 不为空且至少有2x2的大小
		if (gray_mat.rows < 2 || gray_mat.cols < 2) {
			value = 0; // 图像太小无法计算
			break;
		}

		double RF = 0, CF = 0;
		cv::Mat diff_x, diff_y;

		// --- Row frequency (行频) ---
		// 计算水平方向相邻像素的差值
		cv::subtract(gray_mat.colRange(1, gray_mat.cols), gray_mat.colRange(0, gray_mat.cols - 1), diff_x, cv::noArray(), CV_64F);
		// 对差值求平方
		cv::pow(diff_x, 2, diff_x);
		// 先求均值，再对均值结果开方
		RF = std::sqrt(cv::mean(diff_x)[0]);

		// --- Column frequency (列频) ---
		// 计算垂直方向相邻像素的差值
		cv::subtract(gray_mat.rowRange(1, gray_mat.rows), gray_mat.rowRange(0, gray_mat.rows - 1), diff_y, cv::noArray(), CV_64F);
		// 对差值求平方
		cv::pow(diff_y, 2, diff_y);
		// 先求均值，再对均值结果开方 (修正点)
		CF = std::sqrt(cv::mean(diff_y)[0]);

		// 最终空间频率是行频和列频的平方和的平方根
		value = std::sqrt(RF * RF + CF * CF);
		break;
	}
	default: // 默认行为
		cv::meanStdDev(gray_mat, mean, stddev);
		value = stddev.at<double>(0, 0) * stddev.at<double>(0, 0); // 默认使用方差
		break;
	}

	return value;
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
	return MatToHImage(out, outImage);
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

	if (mat.depth() == CV_16U) {

		///2025.02.07 16为图像做直方图时，不做直方图均衡化，这会导致图像变形，这个效果可以通过调节Gammma实现
		//// 应用自适应直方图均衡化
		//cv::Ptr<cv::CLAHE> clahe = cv::createCLAHE();
		////这里设置高了，四个角会被扩散掉，
		//clahe->setClipLimit(1.0); // 设置对比度限制
		//clahe->apply(image, image);

		cv::normalize(mat, mat, 0, 255, cv::NORM_MINMAX, CV_8U);
	}

	if (mat.depth() == CV_32F) {
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
	if (mat.empty() || !config || !result) {
		return -1;
	}

	json j = json::parse(config);
	int threshold = j.at("Threshold").get<int>();
	bool useRotatedRect = false;
	if (j.contains("UseRotatedRect")) {
		useRotatedRect = j.at("UseRotatedRect").get<bool>();
	}

	json outputJson;
	int ret = 0;

	if (useRotatedRect) {
		std::vector<cv::Point2f> corners;
		ret = findLuminousAreaCorners(mat, corners, threshold);
		if (ret == 0 && corners.size() == 4) {
			outputJson["Corners"] = {
				{corners[0].x, corners[0].y},
				{corners[1].x, corners[1].y},
				{corners[2].x, corners[2].y},
				{corners[3].x, corners[3].y}
			};
		}
		else {
			return -2;
		}
	}
	else {
		cv::Rect LuminousArea;
		ret = findLuminousArea(mat, LuminousArea, threshold);
		if (ret == 0) {
			outputJson["X"] = LuminousArea.x;
			outputJson["Y"] = LuminousArea.y;
			outputJson["Width"] = LuminousArea.width;
			outputJson["Height"] = LuminousArea.height;
		}
		else {
			return -2;
		}
	}

	std::string output = outputJson.dump();
	size_t length = output.length() + 1;
	*result = new char[length];
	if (!*result) {
		return -3; // 错误：内存分配失败
	}
	std::strcpy(*result, output.c_str());
	return static_cast<int>(length);
}


StitchingErrorCode stitchImages(const std::vector<std::string>& image_files, cv::Mat& result) {
	if (image_files.empty()) {
		return StitchingErrorCode::EMPTY_INPUT;
	}

	std::vector<cv::Mat> images;

	for (const auto& file : image_files) {
		std::string ss = UTF8ToGB(file.c_str());
		cv::Mat img = cv::imread(ss, cv::IMREAD_UNCHANGED);
		if (img.empty()) {
			return StitchingErrorCode::FILE_NOT_FOUND;
		}

		if (images.empty()) {
			// 读取第一张图像以获取参考尺寸和类型
			int ref_height = img.rows;
			int ref_width = img.cols;
			int ref_type = img.type(); // 获取图像类型，例如 CV_8UC1 表示灰度图像

			// 检查后续图像的尺寸和类型是否与第一张图像相同
			for (size_t i = 1; i < image_files.size(); ++i) {
				std::string ss = UTF8ToGB(image_files[i].c_str());
				cv::Mat img = cv::imread(ss, cv::IMREAD_UNCHANGED);
				if (img.empty() || img.rows != ref_height || img.cols != ref_width || img.type() != ref_type) {
					return StitchingErrorCode::DIFFERENT_DIMENSIONS;
				}
			}
		}

		images.push_back(img);
	}

	size_t num_images = images.size();
	if (num_images == 0) {
		return StitchingErrorCode::NO_VALID_IMAGES;
	}

	// 使用最后一张图像作为底图
	cv::Mat last_image = images.back();
	int result_height = last_image.rows;
	int result_width = last_image.cols;

	result.create(result_height, result_width, last_image.type());

	if (result.empty()) {
		return StitchingErrorCode::NO_VALID_IMAGES;
	}
	last_image.copyTo(result);

	size_t width = result_width / num_images;
	for (int i = 0; i < num_images -1; ++i) {
		cv::Mat part = images[i](cv::Rect(i* (int)width, 0, (int)width, result_height));
		part.copyTo(result(cv::Rect(i * (int)width, 0, (int)width, result_height)));
	}

	return StitchingErrorCode::SUCCESS;
}

std::string GbkToUtf8(const char* src_str)
{
	int len = MultiByteToWideChar(CP_ACP, 0, src_str, -1, NULL, 0);
	wchar_t* wstr = new wchar_t[len + 1];
	memset(wstr, 0, len + 1);
	MultiByteToWideChar(CP_ACP, 0, src_str, -1, wstr, len);
	len = WideCharToMultiByte(CP_UTF8, 0, wstr, -1, NULL, 0, NULL, NULL);
	char* str = new char[len + 1];
	memset(str, 0, len + 1);
	WideCharToMultiByte(CP_UTF8, 0, wstr, -1, str, len, NULL, NULL);
	std::string strTemp = str;
	if (wstr) delete[] wstr;
	if (str) delete[] str;
	return strTemp;
}

COLORVISIONCORE_API int M_StitchImages(const char* config, HImage* outImage)
{
	if (!config) {
		return -1;
	}
	json j = json::parse(GbkToUtf8(config));

	const auto& image_files = j.at("ImageFiles").get<std::vector<std::string>>();
	if (image_files.empty()) {
		return -1;
	}
	cv::Mat result;

	StitchingErrorCode code = stitchImages(image_files, result);

	if (code == StitchingErrorCode::SUCCESS && !result.empty()) {
		MatToHImage(result, outImage);
	}
	return static_cast<int>(code);
}



COLORVISIONCORE_API int M_ConvertGray32Float(HImage img, HImage* outImage)
{
	cv::Mat mat(img.rows, img.cols, img.type(), img.pData);

	// 检查图像深度是否为CV_32FC1
	if (mat.depth() != CV_32FC1) {
		return -1; // 图像不是32位浮点类型
	}

	// 找到图像中的最小值和最大值
	double minVal, maxVal;
	cv::minMaxLoc(mat, &minVal, &maxVal);

	// 如果minVal为0且maxVal为1，则不需要缩放和偏移，直接转换
	if (minVal >= 0.0 && maxVal <= 5.0) {
		cv::Mat outMat(img.rows, img.cols, CV_16UC1);
		mat.convertTo(outMat, CV_16UC1, 65535);
		// 将OpenCV的Mat对象转换回HImage对象
		MatToHImage(outMat, outImage);
	}
	else {
		// 计算比例因子和标量值
		float scale = 65535 / (maxVal - minVal);
		float delta = -minVal * scale;

		// 创建输出图像矩阵
		cv::Mat outMat(img.rows, img.cols, CV_16UC1);

		// 将32位浮点图像转换为16位灰度图像
		mat.convertTo(outMat, CV_16UC1, scale, delta);
		// 将OpenCV的Mat对象转换回HImage对象
		MatToHImage(outMat, outImage);
	}


	return 0;
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


COLORVISIONCORE_API int M_Fusion(const char* fusionjson, HImage* outImage)
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

	std::vector<std::string> files = j.get<std::vector<std::string>>();
	if (files.empty()) {
		std::cerr << "Error: No files provided in JSON array." << std::endl;
		return -1;
	}
	std::vector<cv::Mat> imgs(files.size());
	std::vector<std::thread> threads;
	std::vector<bool> read_success(files.size(), false); // To track success of each thread

	for (size_t i = 0; i < files.size(); ++i) {
		threads.emplace_back([i, &files, &imgs, &read_success]() {
			imgs[i] = cv::imread(files[i]);
			if (!imgs[i].empty()) {
				read_success[i] = true;
			}
			});
	}

	// Wait for all reading threads to complete
	for (auto& t : threads) {
		t.join();
	}

	cv::Mat out = fusion(imgs, 2);
	duration = std::chrono::duration_cast<std::chrono::microseconds>(end - start);
	std::cout << "fusion执行时间: " << duration.count() / 1000.0 << " 毫秒" << std::endl;

	MatToHImage(out, outImage);
	duration = std::chrono::duration_cast<std::chrono::microseconds>(end - start);
	std::cout << "MatToHImage: " << duration.count() / 1000.0 << " 毫秒" << std::endl;
	return 0;
}