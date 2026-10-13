// Fusion.cpp 
//2021.12.9  Xin & Shen qian Update
//具体参数传入图像Mat，输出mat
#include "pch.h"
#include "algorithm.h"

#include <iostream>  
#include <opencv2/core/core.hpp>  
#include <opencv2/opencv.hpp>
#include <opencv2/highgui/highgui.hpp>
#include <opencv2/imgproc/imgproc.hpp>
#include <vector>
#include <algorithm>
#include <cmath>

using namespace std;
using namespace cv;

namespace
{
	constexpr float kFocusScale = 1.0f / 255.0f;
	constexpr float kEpsilon = 1e-6f;
	constexpr float kPsnrScale = -8.685889638f;

	inline float clampPositive(float value)
	{
		return std::max(value, kEpsilon);
	}
}


void showimages(vector<Mat> sample) {
	Mat temp;
	for (size_t i = 0; i < sample.size(); ++i)
	{
		sample[i].copyTo(temp);
		putText(temp, std::to_string(i+1) + "/" + std::to_string(sample.size()), Point(50, 60), FONT_HERSHEY_SIMPLEX, 1, Scalar(255, 23, 0), 4, 8);//在图片上写文字
		imshow("Result", temp);
		waitKey(50);
	}
	temp.release();
}

Mat gfocus(const Mat& im, int window) {
	CV_Assert(im.type() == CV_32FC1);

	Mat average;
	Mat focusMeasure;
	boxFilter(im, average, CV_32FC1, Size(window, window), Point(-1, -1), true, BORDER_REPLICATE);
	subtract(im, average, focusMeasure);
	multiply(focusMeasure, focusMeasure, focusMeasure);
	boxFilter(focusMeasure, focusMeasure, CV_32FC1, Size(window, window), Point(-1, -1), true, BORDER_REPLICATE);
	return focusMeasure;
}

Mat fusion(const vector<Mat>& imgs, const FusionOptions& options) {
	if (imgs.empty() || !options.valid()) {
		return Mat();
	}

	const int P = static_cast<int>(imgs.size());
	const int M = imgs[0].rows;
	const int N = imgs[0].cols;
	const int channels = imgs[0].channels();
	const int depth = imgs[0].depth();
	const int STEP = options.focusStep;

	if (P == 1) {
		return imgs[0].clone();
	}

	if (M == 0 || N == 0 || depth != CV_8U || (channels != 1 && channels != 3)) {
		return Mat();
	}

	for (int i = 1; i < P; ++i)
	{
		if (imgs[i].empty() || imgs[i].rows != M || imgs[i].cols != N || imgs[i].channels() != channels || imgs[i].depth() != depth)
		{
			return Mat();
		}
	}

	vector<Mat> focusMaps(P);
	for (int i = 0; i < P; ++i)
	{
		Mat gray;
		if (channels == 3) {
			cvtColor(imgs[i], gray, COLOR_BGR2GRAY);
		}
		else {
			gray = imgs[i];
		}

		gray.convertTo(gray, CV_32FC1, kFocusScale);
		focusMaps[i] = gfocus(gray, options.focusWindow);
	}

	if (P >= 2 * STEP + 1) {
#pragma region Compute Smeasure
		Mat invPsnr(M, N, CV_32FC1);
		cv::parallel_for_(Range(0, M), [&](const Range& range) {
			vector<float*> focusRows(P);
			for (int row = range.start; row < range.end; ++row) {
				for (int p = 0; p < P; ++p) focusRows[p] = focusMaps[p].ptr<float>(row);
				float* inverseRow = invPsnr.ptr<float>(row);
				for (int col = 0; col < N; ++col) {
					float maximum = focusRows[0][col];
					int peak = 1;
					for (int p = 1; p < P; ++p) {
						if (focusRows[p][col] > maximum) { maximum = focusRows[p][col]; peak = p + 1; }
					}
					maximum = clampPositive(maximum);
					const int center = std::clamp(peak, STEP + 1, P - STEP);
					const int side = peak <= STEP ? center + STEP : center - STEP;
					const float centerValue = clampPositive(focusRows[center - 1][col]);
					// The inherited fit mirrors its endpoints (y3 == y1). In centered
					// coordinates, u=center, A=centerValue, c=(log(side)-log(center))/step^2.
					const float curvature = (std::log(clampPositive(focusRows[side - 1][col])) - std::log(centerValue)) / (STEP * STEP);
					const bool validPeak = curvature < 0.0f;
					float error = 0.0f;
					for (int p = 0; p < P; ++p) {
						float& focus = focusRows[p][col];
						if (validPeak) {
							const float delta = static_cast<float>(p + 1 - center);
							error += std::abs(focus - centerValue * std::exp(curvature * delta * delta));
						}
						focus /= maximum;
					}
					// A non-peak cannot describe a Gaussian focus profile. Assign zero-dB
					// confidence instead of a negative variance and an overflowing exp.
					inverseRow[col] = validPeak ? error / (P * maximum) : 1.0f;
				}
			}
		});
		blur(invPsnr, invPsnr, Size(options.consistencyWindow, options.consistencyWindow), Point(-1, -1), BORDER_REPLICATE);
		max(invPsnr, Scalar(kEpsilon), invPsnr);

		Mat S;
		log(invPsnr, S);
		S *= kPsnrScale;

		Mat phi(M, N, CV_32FC1);
		cv::parallel_for_(Range(0, M), [&](const Range& range) {
			for (int row = range.start; row < range.end; ++row)
			{
				const float* sRow = S.ptr<float>(row);
				float* phiRow = phi.ptr<float>(row);
				for (int col = 0; col < N; ++col)
				{
					phiRow[col] = 0.5f * (1.0f + std::tanh(options.alpha * (sRow[col] - options.threshold))) / options.alpha;
				}
			}
		});
		medianBlur(phi, phi, 3);
#pragma endregion
		cv::parallel_for_(Range(0, M), [&](const Range& range) {
			vector<float*> focusRows(P);

			for (int row = range.start; row < range.end; ++row)
			{
				for (int p = 0; p < P; ++p)
				{
					focusRows[p] = focusMaps[p].ptr<float>(row);
				}

				const float* phiRow = phi.ptr<float>(row);
				for (int col = 0; col < N; ++col)
				{
					const float phiValue = phiRow[col];
					for (int p = 0; p < P; ++p)
					{
						focusRows[p][col] = 0.5f + 0.5f * std::tanh(phiValue * (focusRows[p][col] - 1.0f));
					}
				}
			}
		});
	}

	if (channels == 3) {
		Mat result(M, N, CV_8UC3);
		cv::parallel_for_(Range(0, M), [&](const Range& range) {
			vector<const float*> focusRows(P);
			vector<const Vec3b*> sourceRows(P);

			for (int row = range.start; row < range.end; ++row)
			{
				for (int p = 0; p < P; ++p)
				{
					focusRows[p] = focusMaps[p].ptr<float>(row);
					sourceRows[p] = imgs[p].ptr<Vec3b>(row);
				}

				Vec3b* dstRow = result.ptr<Vec3b>(row);
				for (int col = 0; col < N; ++col)
				{
					float weightSum = 0.0f;
					float blue = 0.0f;
					float green = 0.0f;
					float red = 0.0f;

					for (int p = 0; p < P; ++p)
					{
						const float weight = focusRows[p][col];
						const Vec3b& pixel = sourceRows[p][col];
						weightSum += weight;
						blue += pixel[0] * weight;
						green += pixel[1] * weight;
						red += pixel[2] * weight;
					}

					// A textureless stack has no focus evidence: retain its mean intensity.
					if (weightSum <= kEpsilon) {
						blue = green = red = 0.0f;
						for (int p = 0; p < P; ++p) {
							const Vec3b& pixel = sourceRows[p][col];
							blue += pixel[0]; green += pixel[1]; red += pixel[2];
						}
						weightSum = static_cast<float>(P);
					}
					const float invWeight = 1.0f / weightSum;
					dstRow[col] = Vec3b(
						saturate_cast<uchar>(blue * invWeight),
						saturate_cast<uchar>(green * invWeight),
						saturate_cast<uchar>(red * invWeight));
				}
			}
		});
		return result;
	}

	Mat result(M, N, CV_8UC1);
	cv::parallel_for_(Range(0, M), [&](const Range& range) {
		vector<const float*> focusRows(P);
		vector<const uchar*> sourceRows(P);

		for (int row = range.start; row < range.end; ++row)
		{
			for (int p = 0; p < P; ++p)
			{
				focusRows[p] = focusMaps[p].ptr<float>(row);
				sourceRows[p] = imgs[p].ptr<uchar>(row);
			}

			uchar* dstRow = result.ptr<uchar>(row);
			for (int col = 0; col < N; ++col)
			{
				float weightSum = 0.0f;
				float value = 0.0f;

				for (int p = 0; p < P; ++p)
				{
					const float weight = focusRows[p][col];
					weightSum += weight;
					value += sourceRows[p][col] * weight;
				}

				if (weightSum <= kEpsilon) {
					value = 0.0f;
					for (int p = 0; p < P; ++p) value += sourceRows[p][col];
					weightSum = static_cast<float>(P);
				}
				dstRow[col] = saturate_cast<uchar>(value / weightSum);
			}
		}
	});
	return result;
}






//int main(int argc, char* argv[])
//{
//	std::cout << "景深融合" << endl;
//	clock_t start, end;
//	vector<Mat> imgs;
//
//	size_t P;
//	P = image_name.size();
//	for (size_t i = 0; i < P; i++)
//	{
//		imgs.push_back(imread(image_name[i]));
//	}
//
//	start = clock();
//
//
//	waitKey(1);
//	Mat result = fusion(imgs,2);
//	end = clock();
//	cout << "花费了" << (double)(end - start) / CLOCKS_PER_SEC << "秒" << endl;
//	imshow("Result", result);
//
//	result.release();
//	imgs.clear();
//	waitKey();
//}
