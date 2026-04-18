#include "pch.h"
#include "Windows.h"
#include "opencv_media_export.h"
#include "algorithm.h"
#include <opencv2/opencv.hpp>
#include <nlohmann\json.hpp>
#include <string>
#include <locale>
#include <codecvt>
#include <cmath>
#include <combaseapi.h>

using json = nlohmann::json;

namespace
{
cv::Mat CreateMatView(const HImage& img)
{
	if (img.pData == nullptr || img.rows <= 0 || img.cols <= 0 || img.channels <= 0) {
		return cv::Mat();
	}

	switch (img.depth) {
	case 8:
	case 16:
	case 32:
	case 64:
		break;
	default:
		return cv::Mat();
	}

	const size_t step = img.stride > 0 ? static_cast<size_t>(img.stride) : 0;
	return cv::Mat(img.rows, img.cols, img.type(), img.pData, step);
}

cv::Mat ClipToRoi(const cv::Mat& mat, const RoiRect& roi)
{
	if (mat.empty()) {
		return mat;
	}

	const cv::Rect imageRect(0, 0, mat.cols, mat.rows);
	const cv::Rect requested(roi.x, roi.y, roi.width, roi.height);
	const cv::Rect clipped = requested & imageRect;

	if (clipped.width > 0 && clipped.height > 0) {
		return mat(clipped);
	}

	return mat;
}

bool TryBuildGrayFocusInput(const HImage& img, const RoiRect& roi, cv::Mat& grayMat)
{
	cv::Mat mat = ClipToRoi(CreateMatView(img), roi);
	if (mat.empty() || mat.data == nullptr) {
		return false;
	}

	switch (mat.channels())
	{
	case 1:
		grayMat = mat;
		break;
	case 3:
		cv::cvtColor(mat, grayMat, cv::COLOR_BGR2GRAY);
		break;
	case 4:
		cv::cvtColor(mat, grayMat, cv::COLOR_BGRA2GRAY);
		break;
	default:
		cv::extractChannel(mat, grayMat, 0);
		break;
	}

	if (grayMat.empty()) {
		return false;
	}

	switch (grayMat.depth())
	{
	case CV_8U:
		grayMat.convertTo(grayMat, CV_32F, 1.0 / 255.0);
		break;
	case CV_16U:
		grayMat.convertTo(grayMat, CV_32F, 1.0 / 65535.0);
		break;
	case CV_32F:
		break;
	case CV_64F:
		grayMat.convertTo(grayMat, CV_32F);
		break;
	default:
		return false;
	}

	cv::patchNaNs(grayMat, 0.0f);
	return true;
}

inline double Square(double value)
{
	return value * value;
}
}


COLORVISIONCORE_API void M_FreeHImageData(unsigned char* data)
{
	if (data != nullptr) {
		CoTaskMemFree(data);
	}
}

COLORVISIONCORE_API double M_CalArtculation(HImage img, FocusAlgorithm type, RoiRect roi)
{
	try {
		cv::Mat gray_mat;
		if (!TryBuildGrayFocusInput(img, roi, gray_mat)) {
			return -1.0;
		}

		double value = -1.0;
		cv::Mat mean;
		cv::Mat stddev;
		cv::Mat laplacian_mat;
		cv::Mat grad_x;
		cv::Mat grad_y;
		cv::Mat gradient_mat;

		switch (type)
		{
		case Variance:
			cv::meanStdDev(gray_mat, mean, stddev);
			value = Square(stddev.at<double>(0, 0));
			break;

		case StandardDeviation:
			cv::meanStdDev(gray_mat, mean, stddev);
			value = stddev.at<double>(0, 0);
			break;

		case Tenengrad:
			if (gray_mat.rows < 2 || gray_mat.cols < 2) {
				return 0.0;
			}
			cv::Sobel(gray_mat, grad_x, CV_32F, 1, 0, 3);
			cv::Sobel(gray_mat, grad_y, CV_32F, 0, 1, 3);
			cv::magnitude(grad_x, grad_y, gradient_mat);
			value = cv::mean(gradient_mat)[0];
			break;

		case Laplacian:
			if (gray_mat.rows < 2 || gray_mat.cols < 2) {
				return 0.0;
			}
			cv::Laplacian(gray_mat, laplacian_mat, CV_32F, 3);
			cv::absdiff(laplacian_mat, cv::Scalar::all(0), gradient_mat);
			value = cv::mean(gradient_mat)[0];
			break;

		case VarianceOfLaplacian:
			if (gray_mat.rows < 2 || gray_mat.cols < 2) {
				return 0.0;
			}
			cv::Laplacian(gray_mat, laplacian_mat, CV_32F, 3);
			cv::meanStdDev(laplacian_mat, mean, stddev);
			value = Square(stddev.at<double>(0, 0));
			break;

		case EnergyOfGradient:
			if (gray_mat.rows < 2 || gray_mat.cols < 2) {
				return 0.0;
			}
			cv::subtract(gray_mat(cv::Rect(1, 0, gray_mat.cols - 1, gray_mat.rows)), gray_mat(cv::Rect(0, 0, gray_mat.cols - 1, gray_mat.rows)), grad_x, cv::noArray(), CV_32F);
			cv::subtract(gray_mat(cv::Rect(0, 1, gray_mat.cols, gray_mat.rows - 1)), gray_mat(cv::Rect(0, 0, gray_mat.cols, gray_mat.rows - 1)), grad_y, cv::noArray(), CV_32F);
			cv::multiply(grad_x, grad_x, grad_x);
			cv::multiply(grad_y, grad_y, grad_y);
			value = cv::mean(grad_x)[0] + cv::mean(grad_y)[0];
			break;

		case SpatialFrequency:
		{
			if (gray_mat.rows < 2 || gray_mat.cols < 2) {
				return 0.0;
			}

			double RF = 0.0;
			double CF = 0.0;
			cv::Mat diff_x;
			cv::Mat diff_y;

			cv::subtract(gray_mat.colRange(1, gray_mat.cols), gray_mat.colRange(0, gray_mat.cols - 1), diff_x, cv::noArray(), CV_32F);
			cv::subtract(gray_mat.rowRange(1, gray_mat.rows), gray_mat.rowRange(0, gray_mat.rows - 1), diff_y, cv::noArray(), CV_32F);
			cv::multiply(diff_x, diff_x, diff_x);
			cv::multiply(diff_y, diff_y, diff_y);
			RF = std::sqrt(cv::mean(diff_x)[0]);
			CF = std::sqrt(cv::mean(diff_y)[0]);
			value = std::sqrt(RF * RF + CF * CF);
			break;
		}
		default:
			cv::meanStdDev(gray_mat, mean, stddev);
			value = Square(stddev.at<double>(0, 0));
			break;
		}

		return std::isfinite(value) ? value : -1.0;
	}
	catch (const cv::Exception&) {
		return -1.0;
	}
}

	COLORVISIONCORE_API int FreeResult(char* result) {
		delete[] result;
		return 0;
	}



COLORVISIONCORE_API int M_PseudoColor(HImage img, HImage* outImage, uint min, uint max, cv::ColormapTypes types, int channel)
{
	// 构造 Mat 头，不拷贝数据
	cv::Mat mat(img.rows, img.cols, img.type(), img.pData);

	if (mat.empty())
		return -1;

	cv::Mat out;

	// 优化通道提取
	if (mat.channels() != 1) {
		if (channel >= 0 && channel < mat.channels()) {
			cv::extractChannel(mat, out, channel);
		}
		else {
			cv::cvtColor(mat, out, cv::COLOR_BGR2GRAY);
		}
	}
	else {
		out = mat.clone();
	}

	// 执行伪彩色变换
	pseudoColor(out, min, max, types);

	// 转回 HImage (假设 MatToHImage 负责数据拷贝或接管)
	return MatToHImage(out, outImage);
}

COLORVISIONCORE_API int M_PseudoColorAutoRange(HImage img, HImage* outImage, uint min, uint max, cv::ColormapTypes types, int channel, uint dataMin, uint dataMax)
{
	cv::Mat mat(img.rows, img.cols, img.type(), img.pData);

	if (mat.empty())
		return -1;

	cv::Mat out;

	if (mat.channels() != 1) {
		if (channel >= 0 && channel < mat.channels()) {
			cv::extractChannel(mat, out, channel);
		}
		else {
			cv::cvtColor(mat, out, cv::COLOR_BGR2GRAY);
		}
	}
	else {
		out = mat.clone();
	}

	pseudoColorAutoRange(out, min, max, types, dataMin, dataMax);

	return MatToHImage(out, outImage);
}

COLORVISIONCORE_API int M_GetMinMax(HImage img, uint* outMin, uint* outMax, int channel)
{
	cv::Mat mat(img.rows, img.cols, img.type(), img.pData);

	if (mat.empty())
		return -1;

	cv::Mat gray;

	if (mat.channels() != 1) {
		if (channel >= 0 && channel < mat.channels()) {
			cv::extractChannel(mat, gray, channel);
		}
		else {
			cv::cvtColor(mat, gray, cv::COLOR_BGR2GRAY);
		}
	}
	else {
		gray = mat;
	}

	double minVal, maxVal;
	cv::minMaxLoc(gray, &minVal, &maxVal);

	*outMin = (uint)std::max(minVal, 0.0);
	*outMax = (uint)std::max(maxVal, 0.0);

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
			// ����ͨ��ͼ��ת��Ϊ��ͨ��
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
	// ����ǲ�ɫͼ��ת��Ϊ�Ҷ�ͼ
	if (mat.channels() == 3 || mat.channels() == 4)  // �ж��Ƿ�Ϊ��ɫͼ��BGR �� BGRA��
	{
		cv::cvtColor(mat, mat, cv::COLOR_BGR2GRAY); // ת��Ϊ�Ҷ�ͼ
	}
	else
	{
		cv::normalize(mat, mat, 0, 255, cv::NORM_MINMAX, CV_8U);
	}

	if (mat.depth() == CV_16U) {

		///2025.02.07 16Ϊͼ����ֱ��ͼʱ������ֱ��ͼ���⻯����ᵼ��ͼ����Σ����Ч������ͨ������Gammmaʵ��
		//// Ӧ������Ӧֱ��ͼ���⻯
		//cv::Ptr<cv::CLAHE> clahe = cv::createCLAHE();
		////�������ø��ˣ��ĸ��ǻᱻ��ɢ����
		//clahe->setClipLimit(1.0); // ���öԱȶ�����
		//clahe->apply(image, image);

		cv::normalize(mat, mat, 0, 255, cv::NORM_MINMAX, CV_8U);
	}

	if (mat.depth() == CV_32F) {
		cv::normalize(mat, mat, 0, 255, cv::NORM_MINMAX, CV_8U);
	}


	// Ŀ��ֱ�������
	int targetPixels = targetPixelsX * targetPixelsY; // Ŀ�������������Ե�����
	int originalWidth = mat.cols;
	int originalHeight = mat.rows;

	// �����ʼ��������
	double initialScaleFactor = std::sqrt((double)originalWidth * originalHeight / targetPixels);

	// ȷ������������ 1��2��4��8 �ȱ���
	int allowedFactors[] = { 1, 2, 4, 8, 16, 32, 64, 128, 256, 512, 1024, 2048 };
	int scaleFactor = FindClosestFactor((int)std::round(initialScaleFactor), allowedFactors);
	// �����µĿ��Ⱥ͸߶�
	int newWidth = originalWidth / scaleFactor;
	int newHeight = originalHeight / scaleFactor;

	// �����ڴ�� rowGrayPixels
	*length = newWidth * newHeight;
	*rowGrayPixels = new uchar[*length];

	// ���д���ͼ����������
#pragma omp parallel for
	for (int y = 0; y < newHeight; ++y)
	{
		uchar* row = *rowGrayPixels + y * newWidth;
		for (int x = 0; x < newWidth; ++x)
		{
			int oldX = x * scaleFactor;
			int oldY = y * scaleFactor;
			int oldIndex = oldY * mat.cols + oldX;

			// ������ֵ�洢�� rowGrayPixels
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
	cv::extractChannel(mat, outMat, channel);
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
/// ����
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
/// 
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

COLORVISIONCORE_API int M_FindLuminousArea(HImage img, RoiRect roi, const char* config, char** result)
{
	cv::Mat mat(img.rows, img.cols, img.type(), img.pData);
	if (mat.empty() || !config || !result) {
		return -1;
	}

	cv::Rect mroi(roi.x, roi.y, roi.width, roi.height);
	bool use_roi = (mroi.width > 0 && mroi.height > 0 && (mroi & cv::Rect(0, 0, mat.cols, mat.rows)) == mroi);
	mat = use_roi ? mat(mroi) : mat;

	json j = json::parse(config);
	int threshold = -1;
	if (j.contains("Threshold")) {
		threshold = j.at("Threshold").get<int>();
	}
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
		return -3; // �����ڴ����ʧ��
	}
	std::strcpy(*result, output.c_str());
	return static_cast<int>(length);
}

COLORVISIONCORE_API int M_FindLightBeads(HImage img, RoiRect roi, const char* config, char** result)
{
	cv::Mat mat(img.rows, img.cols, img.type(), img.pData);
	if (mat.empty() || !config || !result) {
		return -1;
	}

	// Validate and apply ROI
	cv::Rect mroi(roi.x, roi.y, roi.width, roi.height);
	cv::Rect imageRect(0, 0, mat.cols, mat.rows);
	bool hasValidRoi = (mroi.width > 0 && mroi.height > 0);
	bool roiWithinBounds = hasValidRoi && ((mroi & imageRect) == mroi);
	bool use_roi = hasValidRoi && roiWithinBounds;
	
	mat = use_roi ? mat(mroi) : mat;

	// 解析 JSON 配置
	json j = json::parse(config);
	int threshold = j.value("Threshold", 20);
	int minSize = j.value("MinSize", 2);
	int maxSize = j.value("MaxSize", 20);
	int rows = j.value("Rows", 650);
	int cols = j.value("Cols", 850);

	std::vector<cv::Point> centers;
	std::vector<cv::Point> blackCenters;

	int ret = findLightBeads(mat, centers, blackCenters, threshold, minSize, maxSize, rows, cols);
	if (ret != 0) {
		return -2;
	}

	// 构建 JSON 输出
	json outputJson;
	
	// 灯珠中心点
	json centersArray = json::array();
	for (const auto& center : centers) {
		centersArray.push_back({ center.x, center.y });
	}
	outputJson["Centers"] = centersArray;
	outputJson["CenterCount"] = centers.size();

	// 缺失的灯珠
	json blackCentersArray = json::array();
	for (const auto& blackCenter : blackCenters) {
		blackCentersArray.push_back({ blackCenter.x, blackCenter.y });
	}
	outputJson["BlackCenters"] = blackCentersArray;
	outputJson["BlackCenterCount"] = blackCenters.size();

	// 预期数量 (使用 size_t 避免整数溢出)
	size_t expectedCount = static_cast<size_t>(rows) * static_cast<size_t>(cols);
	size_t actualCount = centers.size();
	size_t missingCount = (expectedCount > actualCount) ? (expectedCount - actualCount) : 0;
	
	outputJson["ExpectedCount"] = expectedCount;
	outputJson["MissingCount"] = missingCount;

	std::string output = outputJson.dump();
	size_t length = output.length() + 1;
	*result = new char[length];
	if (!*result) {
		return -3;
	}
	std::strcpy(*result, output.c_str());
	return static_cast<int>(length);
}

COLORVISIONCORE_API int M_DetectKeyRegions(HImage img, RoiRect roi, const char* config, char** result)
{
	cv::Mat mat(img.rows, img.cols, img.type(), img.pData);
	if (mat.empty() || !config || !result) {
		return -1;
	}

	// 应用ROI
	cv::Rect mroi(roi.x, roi.y, roi.width, roi.height);
	bool use_roi = (mroi.width > 0 && mroi.height > 0 && (mroi & cv::Rect(0, 0, mat.cols, mat.rows)) == mroi);
	cv::Mat workMat = use_roi ? mat(mroi) : mat;

	// 解析JSON配置
	json j = json::parse(config);
	int threshold = j.value("Threshold", -1);
	int minArea = j.value("MinArea", 500);
	int maxArea = j.value("MaxArea", 0);
	double marginRatio = j.value("MarginRatio", 0.05);

	std::vector<cv::Rect> keyRects;
	int ret = detectKeyRegions(workMat, keyRects, threshold, minArea, maxArea, marginRatio);
	if (ret != 0 || keyRects.empty()) {
		return -2;
	}

	// 构建JSON输出
	json outputJson;
	json rectsArray = json::array();
	for (const auto& r : keyRects) {
		json rectObj;
		rectObj["X"] = r.x + (use_roi ? roi.x : 0);
		rectObj["Y"] = r.y + (use_roi ? roi.y : 0);
		rectObj["Width"] = r.width;
		rectObj["Height"] = r.height;
		rectsArray.push_back(rectObj);
	}
	outputJson["KeyRegions"] = rectsArray;
	outputJson["Count"] = keyRects.size();

	std::string output = outputJson.dump();
	size_t length = output.length() + 1;
	*result = new char[length];
	if (!*result) {
		return -3;
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
			// ��ȡ��һ��ͼ���Ի�ȡ�ο��ߴ������
			int ref_height = img.rows;
			int ref_width = img.cols;
			int ref_type = img.type(); // ��ȡͼ�����ͣ����� CV_8UC1 ��ʾ�Ҷ�ͼ��

			// ������ͼ��ĳߴ�������Ƿ����һ��ͼ����ͬ
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

	// ʹ�����һ��ͼ����Ϊ��ͼ
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

	// ���ͼ������Ƿ�ΪCV_32FC1
	if (mat.depth() != CV_32FC1) {
		return -1; // ͼ����32λ��������
	}

	// �ҵ�ͼ���е���Сֵ�����ֵ
	double minVal, maxVal;
	cv::minMaxLoc(mat, &minVal, &maxVal);

	// ���minValΪ0��maxValΪ1������Ҫ���ź�ƫ�ƣ�ֱ��ת��
	if (minVal >= 0.0 && maxVal <= 5.0) {
		cv::Mat outMat(img.rows, img.cols, CV_16UC1);
		mat.convertTo(outMat, CV_16UC1, 65535);
		// ��OpenCV��Mat����ת����HImage����
		MatToHImage(outMat, outImage);
	}
	else {
		// ����������Ӻͱ���ֵ
		float scale = 65535 / (maxVal - minVal);
		float delta = -minVal * scale;

		// �������ͼ�����
		cv::Mat outMat(img.rows, img.cols, CV_16UC1);

		// ��32λ����ͼ��ת��Ϊ16λ�Ҷ�ͼ��
		mat.convertTo(outMat, CV_16UC1, scale, delta);
		// ��OpenCV��Mat����ת����HImage����
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

COLORVISIONCORE_API int M_ApplyGaussianBlur(HImage img, HImage* outImage, int kernelSize, double sigma)
{
	cv::Mat mat(img.rows, img.cols, img.type(), img.pData);
	cv::Mat dst;
	ApplyGaussianBlur(mat, dst, kernelSize, sigma);
	MatToHImage(dst, outImage);
	return 0;
}

COLORVISIONCORE_API int M_ApplyMedianBlur(HImage img, HImage* outImage, int kernelSize)
{
	cv::Mat mat(img.rows, img.cols, img.type(), img.pData);
	cv::Mat dst;
	ApplyMedianBlur(mat, dst, kernelSize);
	MatToHImage(dst, outImage);
	return 0;
}

COLORVISIONCORE_API int M_ApplySharpen(HImage img, HImage* outImage)
{
	cv::Mat mat(img.rows, img.cols, img.type(), img.pData);
	cv::Mat dst;
	ApplySharpen(mat, dst);
	MatToHImage(dst, outImage);
	return 0;
}

COLORVISIONCORE_API int M_ApplyCannyEdgeDetection(HImage img, HImage* outImage, double threshold1, double threshold2)
{
	cv::Mat mat(img.rows, img.cols, img.type(), img.pData);
	cv::Mat dst;
	ApplyCannyEdgeDetection(mat, dst, threshold1, threshold2);
	MatToHImage(dst, outImage);
	return 0;
}

COLORVISIONCORE_API int M_ApplyHistogramEqualization(HImage img, HImage* outImage)
{
	cv::Mat mat(img.rows, img.cols, img.type(), img.pData);
	cv::Mat dst;
	ApplyHistogramEqualization(mat, dst);
	MatToHImage(dst, outImage);
	return 0;
}

COLORVISIONCORE_API int M_Fusion(const char* fusionjson, HImage* outImage)
{
	std::chrono::steady_clock::time_point start, end;
	std::chrono::microseconds duration;
	start = std::chrono::high_resolution_clock::now();

	std::string sss = fusionjson;;
	// ���ַ������� JSON ����
	json j = json::parse(sss);

	// ��� JSON �����Ƿ�������
	if (!j.is_array()) {
		// ������
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
	std::cout << "fusionִ��ʱ��: " << duration.count() / 1000.0 << " ����" << std::endl;

	MatToHImage(out, outImage);
	duration = std::chrono::duration_cast<std::chrono::microseconds>(end - start);
	std::cout << "MatToHImage: " << duration.count() / 1000.0 << " ����" << std::endl;
	return 0;
}