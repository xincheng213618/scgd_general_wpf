#include "pch.h"
#include "export.h"
#include "algorithm.h"
#include <opencv2/opencv.hpp>
#include <nlohmann\json.hpp>

using json = nlohmann::json;

static void MatToHImage(cv::Mat& mat, HImage* outImage)
{
	///���ﲻ����Ļ����ֲ��ڴ�������н���֮�����
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
	outImage->depth = bitsPerElement; // ����ÿ����λ��
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
	// ���ַ������� JSON ����
	json j = json::parse(sss);

	// ��� JSON �����Ƿ�������
	if (!j.is_array()) {
		// ������
		return -1;
	}
	std::vector<cv::Mat> imgs;
	std::vector<std::string> files = j.get<std::vector<std::string>>();

	for (const auto& file : files) {
		cv::Mat img = cv::imread(file);
		if (img.empty()) {
			// ������
			return -1;
		}
		imgs.push_back(img);
	}

	cv::Mat out = fusion(imgs,2);
	duration = std::chrono::duration_cast<std::chrono::microseconds>(end - start);
	std::cout << "fusionִ��ʱ��: " << duration.count() / 1000.0 << " ����" << std::endl;
	
	MatToHImage(out, outImage);
	duration = std::chrono::duration_cast<std::chrono::microseconds>(end - start);
	std::cout << "MatToHImage: " << duration.count() / 1000.0 << " ����" << std::endl;
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
	///���ﲻ����Ļ����ֲ��ڴ�������н���֮�����
	MatToHImage(mat, outImage);
	return 0;
}




void FreeHImageData(unsigned char* data)
{
	// ʹ�� delete[] ���ͷ��� new[] ������ڴ�
	delete[] data;
}



int ReadGhostImage(const char* FilePath, int singleLedPixelNum, int* LED_pixel_X , int* LED_pixel_Y, int singleGhostPixelNum, int* Ghost_pixel_X, int* Ghost_pixel_Y, HImage* outImage)
{
	cv::Mat mat = cv::imread(FilePath, cv::ImreadModes::IMREAD_UNCHANGED);
	if (mat.empty())
		return -1;

	// ȷ��ͼ����CV_32FC1����
	if (mat.type() != CV_32FC1) {
		return -2; // ����������������ת��ͼ������
	}

	// ת��Ϊ8λͼ��
	double minVal, maxVal;
	cv::minMaxLoc(mat, &minVal, &maxVal); // �ҵ�ͼ�����С���������ֵ
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



