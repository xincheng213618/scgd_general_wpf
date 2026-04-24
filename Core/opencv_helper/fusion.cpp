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
	constexpr float kCurveFallback = 1e-5f;
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

Mat gfocus(const Mat& im) {
	CV_Assert(im.type() == CV_32FC1);

	Mat average;
	Mat focusMeasure;
	boxFilter(im, average, CV_32FC1, Size(5, 5), Point(-1, -1), true, BORDER_REPLICATE);
	subtract(im, average, focusMeasure);
	multiply(focusMeasure, focusMeasure, focusMeasure);
	boxFilter(focusMeasure, focusMeasure, CV_32FC1, Size(5, 5), Point(-1, -1), true, BORDER_REPLICATE);
	return focusMeasure;
}

Mat fusion(const vector<Mat>& imgs, int STEP) {
	if (imgs.empty()) {
		return Mat();
	}

	const int P = static_cast<int>(imgs.size());
	const int M = imgs[0].rows;
	const int N = imgs[0].cols;
	const int channels = imgs[0].channels();
	const int depth = imgs[0].depth();

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
		focusMaps[i] = gfocus(gray);
	}

	if (STEP > 0 && P > 2 * STEP + 1) {
#pragma region Compute Smeasure
		Mat Ymax = Mat::zeros(M, N, CV_32FC1);
		Mat s2(M, N, CV_32FC1);
		Mat u(M, N, CV_32FC1);
		Mat A(M, N, CV_32FC1);

		cv::parallel_for_(Range(0, M), [&](const Range& range) {
			vector<const float*> focusRows(P);

			for (int row = range.start; row < range.end; ++row)
			{
				for (int p = 0; p < P; ++p)
				{
					focusRows[p] = focusMaps[p].ptr<float>(row);
				}

				float* ymaxRow = Ymax.ptr<float>(row);
				float* s2Row = s2.ptr<float>(row);
				float* uRow = u.ptr<float>(row);
				float* aRow = A.ptr<float>(row);

				for (int col = 0; col < N; ++col)
				{
					float maxValue = focusRows[0][col];
					int maxIndex = 0;

					for (int p = 1; p < P; ++p)
					{
						const float currentValue = focusRows[p][col];
						if (currentValue > maxValue)
						{
							maxValue = currentValue;
							maxIndex = p;
						}
					}

					ymaxRow[col] = clampPositive(maxValue);

					const int sourceIndex = maxIndex + 1;
					const int centerIndex = std::clamp(sourceIndex, STEP + 1, P - STEP);
					const float x1 = static_cast<float>(centerIndex - STEP);
					const float x2 = static_cast<float>(centerIndex);
					const float x3 = static_cast<float>(centerIndex + STEP);

					const float y1t = clampPositive(focusRows[centerIndex - STEP - 1][col]);
					const float y2t = clampPositive(focusRows[centerIndex - 1][col]);
					const float y3t = clampPositive(focusRows[centerIndex + STEP - 1][col]);

					const float y2 = std::log(y2t);
					const float y1 = std::log(sourceIndex <= STEP ? y3t : y1t);
					const float y3 = y1;
					const float x1Squared = x1 * x1;
					const float x2Squared = x2 * x2;
					const float x3Squared = x3 * x3;
					const float denominator = (x1Squared - x2Squared) * (x2 - x3) - (x2Squared - x3Squared) * (x1 - x2);

					float c = ((y1 - y2) * (x2 - x3) - (y2 - y3) * (x1 - x2)) / denominator;
					if (!std::isfinite(c) || c == 0.0f)
					{
						c = kCurveFallback;
					}

					float b = ((y2 - y3) - c * (x2 - x3) * (x2 + x3)) / (x2 - x3);
					if (!std::isfinite(b) || b == 0.0f)
					{
						b = kCurveFallback;
					}

					float s2Value = -1.0f / (2.0f * c);
					if (!std::isfinite(s2Value) || std::abs(s2Value) < kEpsilon)
					{
						s2Value = -1.0f / (2.0f * kCurveFallback);
					}

					const float uValue = b * s2Value;
					const float aValue = y1 - b * x1 - c * x1Squared;
					float amplitude = std::exp(aValue + (uValue * uValue) / (2.0f * s2Value));
					if (!std::isfinite(amplitude))
					{
						amplitude = 0.0f;
					}

					s2Row[col] = s2Value;
					uRow[col] = uValue;
					aRow[col] = amplitude;
				}
			}
		});

		Mat err = Mat::zeros(M, N, CV_32FC1);
		cv::parallel_for_(Range(0, M), [&](const Range& range) {
			vector<float*> focusRows(P);

			for (int row = range.start; row < range.end; ++row)
			{
				for (int p = 0; p < P; ++p)
				{
					focusRows[p] = focusMaps[p].ptr<float>(row);
				}

				const float* ymaxRow = Ymax.ptr<float>(row);
				const float* s2Row = s2.ptr<float>(row);
				const float* uRow = u.ptr<float>(row);
				const float* aRow = A.ptr<float>(row);
				float* errRow = err.ptr<float>(row);

				for (int col = 0; col < N; ++col)
				{
					const float ymaxValue = clampPositive(ymaxRow[col]);
					const float s2Value = s2Row[col];
					const float uValue = uRow[col];
					const float amplitude = aRow[col];
					const float denominator = 2.0f * s2Value;
					float errorValue = 0.0f;

					for (int p = 0; p < P; ++p)
					{
						const float sourceValue = focusRows[p][col];
						const float delta = static_cast<float>(p + 1) - uValue;
						const float fittedValue = amplitude * std::exp(-(delta * delta) / denominator);
						errorValue += std::abs(sourceValue - fittedValue);
						focusRows[p][col] = sourceValue / ymaxValue;
					}

					errRow[col] = errorValue;
				}
			}
		});

		Mat invPsnr;
		Mat psnrDenominator = Ymax * static_cast<float>(P);
		divide(err, psnrDenominator, invPsnr);
		blur(invPsnr, invPsnr, Size(3, 3), Point(-1, -1), BORDER_REPLICATE);
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
					phiRow[col] = 0.5f * (1.0f + std::tanh(0.2f * (sRow[col] - 13.0f))) / 0.2f;
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

					const float invWeight = 1.0f / clampPositive(weightSum);
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

				dstRow[col] = saturate_cast<uchar>(value / clampPositive(weightSum));
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
