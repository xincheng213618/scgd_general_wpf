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

// ��װ�ĺ��������ڴ� cv::Mat ���� HImage
// ��������µ��ڴ棬��������Ȩת�Ƹ�������
// ���� 0 ��ʾ�ɹ���������ʾʧ��
static int MatToHImage(const cv::Mat& mat, HImage* outImage)
{
	if (outImage == nullptr) {
		return -1; // ���ָ����Ч
	}
	if (mat.empty()) {
		return -2; // ���� Mat Ϊ��
	}

	// Ϊ��ȷ�� memcpy �İ�ȫ��������Ҫһ�������ڴ�� Mat��
	// ������� mat �����������ģ����ǾͿ�¡һ�ݡ�
	cv::Mat continuousMat;
	if (mat.isContinuous()) {
		continuousMat = mat; // ֱ��ʹ�ã��޶��⿪��
	}
	else {
		continuousMat = mat.clone(); // ��¡Ϊ�����ڴ�
	}

	// 1. �����ġ������С������ COM �����ڴ�
	size_t totalBytes = continuousMat.total() * continuousMat.elemSize();
	unsigned char* pAllocatedData = static_cast<unsigned char*>(CoTaskMemAlloc(totalBytes));
	if (pAllocatedData == nullptr) {
		return -3; // �ڴ����ʧ��
	}

	// 2. �����ġ���ͼ�����ݿ������·�����ڴ���
	memcpy(pAllocatedData, continuousMat.data, totalBytes);

	// 3. �����ġ���� HImage �ṹ�壬���ڴ�����Ȩ����������
	outImage->rows = continuousMat.rows;
	outImage->cols = continuousMat.cols;
	outImage->channels = continuousMat.channels();
	outImage->stride = static_cast<int>(continuousMat.step);
	outImage->depth = static_cast<int>(continuousMat.elemSize1()) * 8;
	outImage->pData = pAllocatedData; // ָ���·�����ڴ�
	return 0; // �ɹ�
}