// OpenCVHelper_test.cpp : 此文件包含 "main" 函数。程序执行将在此处开始并结束。
//



#include <iostream>
#include <chrono>

#include <opencv.hpp>
#include "../OpenCVHelper/Customfile.h"

int main()
{
	std::chrono::steady_clock::time_point start, end;
	std::chrono::microseconds duration;
	start = std::chrono::high_resolution_clock::now();

	cv::Mat test1 = cv::imread("D:\\C#\\1.tif");

	end = std::chrono::high_resolution_clock::now();
	duration = std::chrono::duration_cast<std::chrono::microseconds>(end - start);
	std::cout << "原生读 执行时间: " << duration.count() / 1000.0 << " 毫秒" << std::endl;

	start = std::chrono::high_resolution_clock::now();

	cv::imwrite("D:\\C#\\2.tif", test1);

	end = std::chrono::high_resolution_clock::now();
	duration = std::chrono::duration_cast<std::chrono::microseconds>(end - start);
	std::cout << "原生写 执行时间: " << duration.count() / 1000.0 << " 毫秒" << std::endl;

	start = std::chrono::high_resolution_clock::now();

	CVWrite("D:\\C#\\3.custom", test1);

	end = std::chrono::high_resolution_clock::now();
	duration = std::chrono::duration_cast<std::chrono::microseconds>(end - start);
	std::cout << "二进制写 执行时间: " << duration.count() / 1000.0 << " 毫秒" << std::endl;


	start = std::chrono::high_resolution_clock::now();

	cv::Mat test2 = CVRead("D:\\C#\\3.custom");

	end = std::chrono::high_resolution_clock::now();
	duration = std::chrono::duration_cast<std::chrono::microseconds>(end - start);
	std::cout << "二进制读 执行时间: " << duration.count() / 1000.0 << " 毫秒" << std::endl;


	//cv::imshow("tif读", test1);
	//cv::imshow("二进制读", test2);
	cv::waitKey(0);
}

