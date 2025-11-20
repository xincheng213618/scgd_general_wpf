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

#include <sfr/general.h>
#include <sfr/slanted.h>
#include <sfr/cylinder.h>

using json = nlohmann::json;


COLORVISIONCORE_API void M_FreeHImageData(unsigned char* data)
{

}

COLORVISIONCORE_API int M_CalSFR(
	HImage img,
	double del,
	int roi_x, int roi_y, int roi_width, int roi_height,
	double* freq,
	double* sfr,
	int    maxLen,
	int* outLen,
	double* mtf10_norm,
	double* mtf50_norm,
	double* mtf10_cypix,
	double* mtf50_cypix)
{
	if (!freq || !sfr || !outLen ||
		!mtf10_norm || !mtf50_norm || !mtf10_cypix || !mtf50_cypix) {
		return -1;
	}

	cv::Mat mat(img.rows, img.cols, img.type(), img.pData);
	if (mat.empty()) return -2;

	cv::Rect roi(roi_x, roi_y, roi_width, roi_height);
	bool use_roi = (roi.width > 0 && roi.height > 0 && (roi & cv::Rect(0, 0, mat.cols, mat.rows)) == roi);
    mat = use_roi ? mat(roi) : mat;

	auto res = sfr::CalSFR(mat, del, /*npol=*/5, /*nbin=*/4);

	int N = static_cast<int>(res.freq.size());
	if (N == 0) {
		*outLen = 0;
		return -3;
	}

	if (N > maxLen) {
		*outLen = N;      // 通知调用方需要的长度
		return -4;
	}

	for (int i = 0; i < N; ++i) {
		freq[i] = res.freq[i];
		sfr[i] = res.sfr[i];
	}

	*outLen = N;
	*mtf10_norm = res.mtf10_norm;
	*mtf50_norm = res.mtf50_norm;
	*mtf10_cypix = res.mtf10_cypix;
	*mtf50_cypix = res.mtf50_cypix;

	return 0;
}

COLORVISIONCORE_API int M_CalSFRMultiChannel(
	HImage img,
	double del,
	RoiRect roi,
	double* freq,
	double* sfr_r,
	double* sfr_g,
	double* sfr_b,
	double* sfr_l,
	int    maxLen,
	int* outLen,
	int* channelCount,
	double* mtf10_norm_r, double* mtf50_norm_r, double* mtf10_cypix_r, double* mtf50_cypix_r,
	double* mtf10_norm_g, double* mtf50_norm_g, double* mtf10_cypix_g, double* mtf50_cypix_g,
	double* mtf10_norm_b, double* mtf50_norm_b, double* mtf10_cypix_b, double* mtf50_cypix_b,
	double* mtf10_norm_l, double* mtf50_norm_l, double* mtf10_cypix_l, double* mtf50_cypix_l)
{
	// Validate pointers
	if (!freq || !sfr_l || !outLen || !channelCount ||
		!mtf10_norm_l || !mtf50_norm_l || !mtf10_cypix_l || !mtf50_cypix_l) {
		return -1;
	}

	cv::Mat mat(img.rows, img.cols, img.type(), img.pData);
	if (mat.empty()) return -2;

	// Apply ROI if specified
	cv::Rect mroi(roi.x, roi.y, roi.width, roi.height);
	bool use_roi = (mroi.width > 0 && mroi.height > 0 && (mroi & cv::Rect(0, 0, mat.cols, mat.rows)) == mroi);
	mat = use_roi ? mat(mroi) : mat;

	// Determine if this is a 3-channel (RGB) or single-channel image
	bool isRGB = (mat.channels() == 3 || mat.channels() == 4);
	
	if (isRGB) {
		// For RGB images: calculate SFR for R, G, B, and L channels
		*channelCount = 4;
		
		// Validate RGB output pointers
		if (!sfr_r || !sfr_g || !sfr_b ||
			!mtf10_norm_r || !mtf50_norm_r || !mtf10_cypix_r || !mtf50_cypix_r ||
			!mtf10_norm_g || !mtf50_norm_g || !mtf10_cypix_g || !mtf50_cypix_g ||
			!mtf10_norm_b || !mtf50_norm_b || !mtf10_cypix_b || !mtf50_cypix_b) {
			return -1;
		}
		
		// Convert to BGR if BGRA

		// Split into channels

		std::vector<cv::Mat> channels;
		cv::split(mat, channels);

		// Helper function to convert BGR to L using custom weights: Y = 0.213*R + 0.715*G + 0.072*B
		cv::Mat luminance(mat.size(), CV_64FC1);
		for (int y = 0; y < mat.rows; ++y) {
			const cv::Vec3b* bgr_row = mat.ptr<cv::Vec3b>(y);

			uchar* r_row = channels[2].ptr<uchar>(y);
			uchar* g_row = channels[1].ptr<uchar>(y);
			uchar* b_row = channels[0].ptr<uchar>(y);

			double* lum_row = luminance.ptr<double>(y);

			for (int x = 0; x < mat.cols; ++x) {
				double r = r_row[x];
				double g = g_row[x];
				double b = b_row[x];
				// L = 0.213*R + 0.715*G + 0.072*B
				lum_row[x] = 0.213 * r + 0.715 * g + 0.072 * b;
			}
		}
		// Calculate SFR for each channel: B, G, R (OpenCV order)
		auto res_l = sfr::CalSFR(luminance, del, 5, 4);
		auto res_r = sfr::CalSFR(channels[2], del, 5, 4, res_l.vslope);
		auto res_g = sfr::CalSFR(channels[1], del, 5, 4, res_l.vslope);
		auto res_b = sfr::CalSFR(channels[0], del, 5, 4, res_l.vslope);
		
		// Verify all results have data
		int N = static_cast<int>(res_r.freq.size());
		if (N == 0 || res_g.freq.size() != N || res_b.freq.size() != N || res_l.freq.size() != N) {
			*outLen = 0;
			return -3;
		}
		
		if (N > maxLen) {
			*outLen = N;
			return -4;
		}
		
		// Copy results
		for (int i = 0; i < N; ++i) {
			freq[i] = res_r.freq[i];  // Use R's freq (all should be same)
			sfr_r[i] = res_r.sfr[i];
			sfr_g[i] = res_g.sfr[i];
			sfr_b[i] = res_b.sfr[i];
			sfr_l[i] = res_l.sfr[i];
		}
		
		*outLen = N;
		
		// R channel MTF values
		*mtf10_norm_r = res_r.mtf10_norm;
		*mtf50_norm_r = res_r.mtf50_norm;
		*mtf10_cypix_r = res_r.mtf10_cypix;
		*mtf50_cypix_r = res_r.mtf50_cypix;
		
		// G channel MTF values
		*mtf10_norm_g = res_g.mtf10_norm;
		*mtf50_norm_g = res_g.mtf50_norm;
		*mtf10_cypix_g = res_g.mtf10_cypix;
		*mtf50_cypix_g = res_g.mtf50_cypix;
		
		// B channel MTF values
		*mtf10_norm_b = res_b.mtf10_norm;
		*mtf50_norm_b = res_b.mtf50_norm;
		*mtf10_cypix_b = res_b.mtf10_cypix;
		*mtf50_cypix_b = res_b.mtf50_cypix;
		
		// L channel MTF values
		*mtf10_norm_l = res_l.mtf10_norm;
		*mtf50_norm_l = res_l.mtf50_norm;
		*mtf10_cypix_l = res_l.mtf10_cypix;
		*mtf50_cypix_l = res_l.mtf50_cypix;
	}
	else {
		// For single-channel images: only calculate L channel
		*channelCount = 1;
		
		auto res_l = sfr::CalSFR(mat, del, 5, 4);
		
		int N = static_cast<int>(res_l.freq.size());
		if (N == 0) {
			*outLen = 0;
			return -3;
		}
		
		if (N > maxLen) {
			*outLen = N;
			return -4;
		}
		
		// Copy results
		for (int i = 0; i < N; ++i) {
			freq[i] = res_l.freq[i];
			sfr_l[i] = res_l.sfr[i];
		}
		
		*outLen = N;
		
		// L channel MTF values
		*mtf10_norm_l = res_l.mtf10_norm;
		*mtf50_norm_l = res_l.mtf50_norm;
		*mtf10_cypix_l = res_l.mtf10_cypix;
		*mtf50_cypix_l = res_l.mtf50_cypix;
	}

	return 0;
}



COLORVISIONCORE_API double M_CalArtculation(HImage img, FocusAlgorithm type, int roi_x, int roi_y, int roi_width, int roi_height)
{

	// 1. �� HImage ���ݰ�װ�� cv::Mat�������ݿ���
	cv::Mat full_mat(img.rows, img.cols, img.type(), img.pData);
	if (full_mat.empty() || full_mat.data == nullptr) {
		return -1.0; // ��Чͼ��
	}

	// 2. ����ROI����ȷ����������
	cv::Rect roi(roi_x, roi_y, roi_width, roi_height);
	bool use_roi = (roi.width > 0 && roi.height > 0 && (roi & cv::Rect(0, 0, full_mat.cols, full_mat.rows)) == roi);

	cv::Mat mat = use_roi ? full_mat(roi) : full_mat;

	// 3. ת��Ϊ�Ҷ�ͼ���м���
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
		break; // ����� break!

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

	case VarianceOfLaplacian: // �Ƽ����ǳ�³�����㷨
		cv::Laplacian(gray_mat, laplacian_mat, CV_8UC1);
		cv::meanStdDev(laplacian_mat, mean, stddev);
		value = stddev.at<double>(0, 0) * stddev.at<double>(0, 0);
		break;

	case EnergyOfGradient:
		// ͨ����ȥ��λ��������������ݶ�
		gray_mat.convertTo(gradient_mat, CV_64F);
		cv::subtract(gradient_mat(cv::Rect(1, 0, gradient_mat.cols - 1, gradient_mat.rows)), gradient_mat(cv::Rect(0, 0, gradient_mat.cols - 1, gradient_mat.rows)), grad_x);
		cv::subtract(gradient_mat(cv::Rect(0, 1, gradient_mat.cols, gradient_mat.rows - 1)), gradient_mat(cv::Rect(0, 0, gradient_mat.cols, gradient_mat.rows - 1)), grad_y);
		cv::pow(grad_x, 2, grad_x);
		cv::pow(grad_y, 2, grad_y);
		value = cv::mean(grad_x)[0] + cv::mean(grad_y)[0];
		break;

	case SpatialFrequency:
	{
		// ȷ�� gray_mat ��Ϊ����������2x2�Ĵ�С
		if (gray_mat.rows < 2 || gray_mat.cols < 2) {
			value = 0; // ͼ��̫С�޷�����
			break;
		}

		double RF = 0, CF = 0;
		cv::Mat diff_x, diff_y;

		// --- Row frequency (��Ƶ) ---
		// ����ˮƽ�����������صĲ�ֵ
		cv::subtract(gray_mat.colRange(1, gray_mat.cols), gray_mat.colRange(0, gray_mat.cols - 1), diff_x, cv::noArray(), CV_64F);
		// �Բ�ֵ��ƽ��
		cv::pow(diff_x, 2, diff_x);
		// �����ֵ���ٶԾ�ֵ�������
		RF = std::sqrt(cv::mean(diff_x)[0]);

		// --- Column frequency (��Ƶ) ---
		// ���㴹ֱ�����������صĲ�ֵ
		cv::subtract(gray_mat.rowRange(1, gray_mat.rows), gray_mat.rowRange(0, gray_mat.rows - 1), diff_y, cv::noArray(), CV_64F);
		// �Բ�ֵ��ƽ��
		cv::pow(diff_y, 2, diff_y);
		// �����ֵ���ٶԾ�ֵ������� (������)
		CF = std::sqrt(cv::mean(diff_y)[0]);

		// ���տռ�Ƶ������Ƶ����Ƶ��ƽ���͵�ƽ����
		value = std::sqrt(RF * RF + CF * CF);
		break;
	}
	default: // Ĭ����Ϊ
		cv::meanStdDev(gray_mat, mean, stddev);
		value = stddev.at<double>(0, 0) * stddev.at<double>(0, 0); // Ĭ��ʹ�÷���
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