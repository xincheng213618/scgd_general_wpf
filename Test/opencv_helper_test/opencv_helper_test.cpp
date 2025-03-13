// OpenCVHelper_test.cpp : 此文件包含 "main" 函数。程序执行将在此处开始并结束。
//
#include <chrono>
#include <iostream>
#include <opencv.hpp>
#include <stack>
#include <algorithm.h>

int main()
{
    int rows = 650;
    int cols = 850;
	std::chrono::steady_clock::time_point start, end;
	std::chrono::microseconds duration;

	cv::Mat image = cv::imread("C:\\Users\\17917\\Desktop\\20241104142506_1_src.tif",cv::ImreadModes::IMREAD_UNCHANGED);

	if (image.empty()) {
		std::cerr << "无法读取图像文件！" << std::endl;
		return -1;
	}
    start = std::chrono::high_resolution_clock::now();


	end = std::chrono::high_resolution_clock::now();
	duration = std::chrono::duration_cast<std::chrono::microseconds>(end - start);
	std::cout << ": " << duration.count() / 1000.0 << " 毫秒" << std::endl;


	cv::waitKey(0);
}

