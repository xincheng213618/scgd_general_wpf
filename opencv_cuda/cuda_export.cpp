#include "cuda_runtime.h"
#include "device_launch_parameters.h"

#include "pch.h"
#include "cuda_export.h"
#include <opencv2/opencv.hpp>
#include <nlohmann\json.hpp>
#include "Fusion.h"

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

	cv::Mat out = Fusion(imgs, 2);
	duration = std::chrono::duration_cast<std::chrono::microseconds>(end - start);
	std::cout << "fusion执行时间: " << duration.count() / 1000.0 << " 毫秒" << std::endl;

	MatToHImage(out, outImage);
	duration = std::chrono::duration_cast<std::chrono::microseconds>(end - start);
	std::cout << "MatToHImage: " << duration.count() / 1000.0 << " 毫秒" << std::endl;
	return 0;
}