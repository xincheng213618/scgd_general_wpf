#include "pch.h"
#include "common.h"
#include "custom_file.h"
#include <atltime.h>

InitialFrame initialFrame = NULL;
UpdateFrame updateFrame = NULL;

COLORVISIONCORE_API void SetInitialFrame(InitialFrame fn)
{
	initialFrame = fn;
}

COLORVISIONCORE_API void SetUpdateFrame(UpdateFrame fn)
{
	updateFrame = fn;
}

COLORVISIONCORE_API int ReadCVFile(char* FilePath)
{
	cv::Mat mat = CVRead(FilePath);
	if (!mat.empty()) {
		return initialFrame(mat.data, mat.rows, mat.cols, mat.channels());
	}
	return -1;
}

std::string UTF8ToGB(const char* str)
{
	std::string result;
	WCHAR* strSrc;
	LPSTR szRes;

	int i = MultiByteToWideChar(CP_UTF8, 0, str, -1, NULL, 0);
	strSrc = new WCHAR[i + 1];
	MultiByteToWideChar(CP_UTF8, 0, str, -1, strSrc, i);

	i = WideCharToMultiByte(CP_ACP, 0, strSrc, -1, NULL, 0, NULL, NULL);
	szRes = new CHAR[i + 1];
	WideCharToMultiByte(CP_ACP, 0, strSrc, -1, szRes, i, NULL, NULL);

	result = szRes;
	delete[] strSrc;
	delete[] szRes;
	return result;
}

COLORVISIONCORE_API int ReadVideoTest(char* FilePath)
{
	cv::Mat frame;
	cv::VideoCapture cap = cv::VideoCapture("D:\\1.mp4");

	if (!cap.isOpened()) {
		return -1;
	}

	for (;;) {
		
		cap >> frame;
		if (frame.empty()) {
			break;
		}
		initialFrame(frame.data, frame.rows, frame.cols, frame.channels());
		cv::waitKey(30);
	}
	return 0;
}
