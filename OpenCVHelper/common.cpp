#include "pch.h"
#include "common.h"
#include "Customfile.h"

InitialFrame initialFrame = NULL;
UpdateFrame updateFrame = NULL;

OPENCV_API void SetInitialFrame(InitialFrame fn)
{
	initialFrame = fn;
}

OPENCV_API void SetUpdateFrame(UpdateFrame fn)
{
	updateFrame = fn;
}

OPENCV_API int ReadCVFile(char* FilePath)
{
	cv::Mat mat = CVRead(FilePath);
	if (!mat.empty()) {
		return initialFrame(mat.data, mat.rows, mat.cols, mat.channels());
	}
	return -1;
}
