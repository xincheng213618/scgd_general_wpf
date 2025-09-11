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
	///���ﲻ����Ļ����ֲ��ڴ�������н���֮�����
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
	// �����µĿ�Ⱥ͸߶�
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
/// ��ֵ��
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
		return -3; // �����ڴ����ʧ��
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