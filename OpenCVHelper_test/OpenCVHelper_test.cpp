// OpenCVHelper_test.cpp : 此文件包含 "main" 函数。程序执行将在此处开始并结束。
//



#include <chrono>
#include <iostream>
#include <opencv.hpp>
#include "../ColorVisionCore/Customfile.h"


cv::Mat ToMat(HImage img)
{
	cv::Mat mat(img.rows, img.cols, img.type(), img.pData);
	return mat;
}
HImage ToHImage(cv::Mat mat)
{
	HImage img;
	img.cols = mat.cols;
	img.rows = mat.rows;
	img.depth = mat.depth();
	img.channels = mat.channels();
	img.pData = (char*)mat.data;
	return img;
}

int main()
{
	std::chrono::steady_clock::time_point start, end;
	std::chrono::microseconds duration;
	start = std::chrono::high_resolution_clock::now();

	cv::Mat test1 = cv::imread("D:\\C#\\1.tif",cv::ImreadModes::IMREAD_UNCHANGED);

	end = std::chrono::high_resolution_clock::now();
	duration = std::chrono::duration_cast<std::chrono::microseconds>(end - start);
	std::cout << "原生读 执行时间: " << duration.count() / 1000.0 << " 毫秒" << std::endl;

	start = std::chrono::high_resolution_clock::now();

	//cv::imwrite("D:\\C#\\2.tif", test1);

	//end = std::chrono::high_resolution_clock::now();
	//duration = std::chrono::duration_cast<std::chrono::microseconds>(end - start);
	//std::cout << "原生写 执行时间: " << duration.count() / 1000.0 << " 毫秒" << std::endl;
	//start = std::chrono::high_resolution_clock::now();

	HImage hImage = ToHImage(test1);
	cv::Mat test3 = ToMat(hImage);

	CVWrite("D:\\C#\\3.custom", hImage);

	end = std::chrono::high_resolution_clock::now();
	duration = std::chrono::duration_cast<std::chrono::microseconds>(end - start);
	std::cout << "二进制写 执行时间: " << duration.count() / 1000.0 << " 毫秒" << std::endl;


	start = std::chrono::high_resolution_clock::now();
	HImage hImage1;
	int i = CVRead("D:\\C#\\3.custom", &hImage1);
	cv::Mat test2 = ToMat(hImage1);

	end = std::chrono::high_resolution_clock::now();
	duration = std::chrono::duration_cast<std::chrono::microseconds>(end - start);
	std::cout << "二进制读 执行时间: " << duration.count() / 1000.0 << " 毫秒" << std::endl;


	cv::imshow("tif读", test1);
	cv::imshow("二进制读", test2);
	cv::waitKey(0);
}

