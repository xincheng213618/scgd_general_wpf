#include "pch.h"
#include "opencv_media_export.h"
#include "algorithm.h"
#include <opencv2/opencv.hpp>
#include <nlohmann\json.hpp>

using json = nlohmann::json;

std::vector<std::pair<int, cv::Mat>> MediaList;

static void MatToHImage(cv::Mat& mat, HImage* outImage)
{
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
	MediaList.push_back(std::make_pair(reinterpret_cast<int>(outImage->pData), mat));
}

COLORVISIONCORE_API void M_FreeHImageData(unsigned char* data)
{
	auto it = std::find_if(MediaList.begin(), MediaList.end(),
		[data](const std::pair<int, cv::Mat>& pair) {
			return pair.first == reinterpret_cast<int>(data);
		});
	if (it != MediaList.end()) {
		it->second.release();
		// 从缓存中移除
		MediaList.erase(it);
	}
}


COLORVISIONCORE_API int M_PseudoColor(HImage img, HImage* outImage, uint min, uint max, cv::ColormapTypes types)
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
	pseudoColor(mat, min, max, types);
	///这里不分配的话，局部内存会在运行结束之后清空
	MatToHImage(mat, outImage);
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
