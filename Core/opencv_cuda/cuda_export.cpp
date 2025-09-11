#include "Windows.h"
#include "cuda_runtime.h"
#include "device_launch_parameters.h"

#include "pch.h"
#include "cuda_export.h"
#include <opencv2/opencv.hpp>
#include <nlohmann\json.hpp>
#include "Fusion.h"

using json = nlohmann::json;


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

	cv::Mat out = Fusion(imgs, 2);
	duration = std::chrono::duration_cast<std::chrono::microseconds>(end - start);
	std::cout << "fusion执行时间: " << duration.count() / 1000.0 << " 毫秒" << std::endl;

	MatToHImage(out, outImage);
	duration = std::chrono::duration_cast<std::chrono::microseconds>(end - start);
	std::cout << "MatToHImage: " << duration.count() / 1000.0 << " 毫秒" << std::endl;
	return 0;
}