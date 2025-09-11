#pragma once
#include <opencv2/core.hpp>
#include <combaseapi.h>
typedef struct HImage
{
    int rows;
    int cols;
    int channels;
    int depth; //Bpp
    int stride;
    int type()  const {
        int cv_depth = CV_8U;
        switch (depth) {
        case 8:
            cv_depth = CV_8U;
            break;
        case 16:
            cv_depth = CV_16U;
            break;
        case 32:
            cv_depth = CV_32F;
            break;
        case 64:
            cv_depth = CV_64F;
            break;
        default:
            break;
        }
        return CV_MAKETYPE(cv_depth, channels);
    }
    int elemSize() const { return  ((((((((depth) & ((1 << 3) - 1)) + (((channels)-1) << 3))) & ((512 - 1) << 3)) >> 3) + 1) * ((0x28442211 >> (((((depth) & ((1 << 3) - 1)) + (((channels)-1) << 3))) & ((1 << 3) - 1)) * 4) & 15)); }
    unsigned char* pData;
}HImage;

// 封装的函数，用于从 cv::Mat 创建 HImage
// 它会分配新的内存，并将所有权转移给调用者
// 返回 0 表示成功，负数表示失败
static int MatToHImage(const cv::Mat& mat, HImage* outImage)
{
	if (outImage == nullptr) {
		return -1; // 输出指针无效
	}
	if (mat.empty()) {
		return -2; // 输入 Mat 为空
	}

	// 为了确保 memcpy 的安全，我们需要一个连续内存的 Mat。
	// 如果输入 mat 本身不是连续的，我们就克隆一份。
	cv::Mat continuousMat;
	if (mat.isContinuous()) {
		continuousMat = mat; // 直接使用，无额外开销
	}
	else {
		continuousMat = mat.clone(); // 克隆为连续内存
	}

	// 1. 【核心】计算大小并分配 COM 任务内存
	size_t totalBytes = continuousMat.total() * continuousMat.elemSize();
	unsigned char* pAllocatedData = static_cast<unsigned char*>(CoTaskMemAlloc(totalBytes));
	if (pAllocatedData == nullptr) {
		return -3; // 内存分配失败
	}

	// 2. 【核心】将图像数据拷贝到新分配的内存中
	memcpy(pAllocatedData, continuousMat.data, totalBytes);

	// 3. 【核心】填充 HImage 结构体，将内存所有权交给调用者
	outImage->rows = continuousMat.rows;
	outImage->cols = continuousMat.cols;
	outImage->channels = continuousMat.channels();
	outImage->stride = static_cast<int>(continuousMat.step);
	outImage->depth = static_cast<int>(continuousMat.elemSize1()) * 8;
	outImage->pData = pAllocatedData; // 指向新分配的内存
	return 0; // 成功
}