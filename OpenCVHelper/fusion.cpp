// Fusion.cpp 
//2021.12.9  Xin & Shen qian Update
//具体参数传入图像Mat，输出mat
#include "pch.h"
#include "algorithm.h"

#include <iostream>  
#include <opencv2/core/core.hpp>  
#include <opencv2/highgui/highgui.hpp>
#include <opencv2/imgproc/imgproc.hpp>
#include <vector>
#include <algorithm>

using namespace std;
using namespace cv;


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

Mat gfocus(Mat im) {
	Mat FM;
	Mat averageFilter(5, 5, CV_64FC1, Scalar(0)), U;
	averageFilter = cv::Scalar::all(1.0 / (5 * 5));
	filter2D(im, U, -1, averageFilter, Point(-1, -1), 0, BORDER_REPLICATE);
	//FM = (im - U) ^ 2; Matlab的.* 不能直接乘，点乘和矩阵相乘的区别
	FM = im - U;
	FM = FM.mul(FM);
	filter2D(FM, FM, -1, averageFilter, Point(-1, -1), 0, BORDER_REPLICATE);
	return FM;
}

Mat sum3(vector<Mat> mats) {
	Mat result = Mat::zeros(mats[0].size(), CV_64FC1);
	for (size_t i = 0; i < mats.size(); i++)
	{
		result += mats[i];
	}
	return result;
}

Mat fusion(vector<Mat> imgs,int STEP) {

	size_t P;
	int M = 0, N = 0 , channels = 0;
	P = imgs.size();
	for (size_t i = 0; i < P; i++)
	{
		M = imgs[i].rows;
		N = imgs[i].cols;
		channels = imgs[i].channels();
	}
	vector<Mat> imagesR, imagesG, imagesB;
	if (channels == 3) {
		for (size_t i = 0; i < P; i++)
		{	
			std::vector<cv::Mat> channels;
			cv::split(imgs[i], channels);
			imagesR.push_back(channels[0]);
			imagesG.push_back(channels[1]);
			imagesB.push_back(channels[2]);
			cvtColor(imgs[i], imgs[i], COLOR_BGR2GRAY);
			imgs[i].convertTo(imgs[i], CV_64FC1, 1.0 / 255.0);
			imgs[i] = gfocus(imgs[i]);
		}
	}else{
		for (size_t i = 0; i < P; i++)
		{
			imagesG.push_back(imgs[i]);
			cvtColor(imgs[i], imgs[i], COLOR_BGR2GRAY);
			imgs[i].convertTo(imgs[i], CV_64FC1, 1.0 / 255.0);
			imgs[i] = gfocus(imgs[i]);
		}
	}
	if (STEP > 0 && P > 2 * STEP + 1) {
#pragma region  Compute Smeasure

		Mat Ymax(M, N, CV_64FC1);
		Mat I(M, N, CV_64FC1);
		for (size_t i = 0; i < P; i++)
		{
			for (int row = 0; row < M; row++)
			{
				double* ptr = imgs[i].ptr<double>(row);
				double* ptr1 = Ymax.ptr<double>(row);
				double* ptr2 = I.ptr<double>(row);
				for (int col = 0; col < N; col++)
				{
					if (ptr[col] > ptr1[col])
					{
						ptr1[col] = ptr[col];
						ptr2[col] = i + 1;
					}
				}
			}
		}
		Mat x1(M, N, CV_64FC1);
		Mat x2(M, N, CV_64FC1);
		Mat x3(M, N, CV_64FC1);
		Mat y1(M, N, CV_64FC1);
		Mat y2(M, N, CV_64FC1);
		Mat y3(M, N, CV_64FC1);

		Mat c(M, N, CV_64FC1);
		Mat b(M, N, CV_64FC1);
		Mat s2(M, N, CV_64FC1);
		Mat u(M, N, CV_64FC1);
		Mat a(M, N, CV_64FC1);
		Mat A(M, N, CV_64FC1);

		size_t i = 0;
		double Icptr;
		for (int row = 0; row < M; row++)
		{
			double* ptr = I.ptr<double>(row);
			double* x1ptr = x1.ptr<double>(row);
			double* x2ptr = x2.ptr<double>(row);
			double* x3ptr = x3.ptr<double>(row);
			double* y1ptr = y1.ptr<double>(row);
			double* y2ptr = y2.ptr<double>(row);
			double* y3ptr = y3.ptr<double>(row);
			double* cptr = c.ptr<double>(row);
			double* bptr = b.ptr<double>(row);
			double* s2ptr = s2.ptr<double>(row);
			double* uptr = u.ptr<double>(row);
			double* aptr = a.ptr<double>(row);
			double* Aptr = A.ptr<double>(row);


			for (int col = 0; col < N; col++)
			{
				if (ptr[col] <= STEP)
				{
					Icptr = STEP + 1;
				}
				else if (ptr[col] >= (int)P - STEP) {
					Icptr = (int)P - STEP;
				}
				else {
					Icptr = ptr[col];
				}

				x1ptr[col] = Icptr - STEP;
				x2ptr[col] = Icptr;
				x3ptr[col] = Icptr + STEP;

				double y1t = imgs[Icptr - STEP - 1].at<double>(row, col);
				double y2t = imgs[Icptr - 1].at<double>(row, col);
				double y3t = imgs[Icptr + STEP - 1].at<double>(row, col);

				y2ptr[col] = log(y2t);

				if (ptr[col] <= STEP) {

					y1ptr[col] = log(y3t);
				}
				else {
					y1ptr[col] = log(y1t);
				}
				y3ptr[col] = y1ptr[col];


				cptr[col] = ((y1ptr[col] - y2ptr[col]) * (x2ptr[col] - x3ptr[col]) - (y2ptr[col] - y3ptr[col]) * (x1ptr[col] - x2ptr[col])) / ((x1ptr[col] * x1ptr[col] - x2ptr[col] * x2ptr[col]) * (x2ptr[col] - x3ptr[col]) - (x2ptr[col] * x2ptr[col] - x3ptr[col] * x3ptr[col]) * (x1ptr[col] - x2ptr[col]));
				if (cvIsInf(cptr[col]) || cvIsNaN(cptr[col]) || cptr[col] == 0)
				{
					cptr[col] = 0.00001;
				}
				bptr[col] = ((y2ptr[col] - y3ptr[col]) - cptr[col] * (x2ptr[col] - x3ptr[col]) * (x2ptr[col] + x3ptr[col])) / (x2ptr[col] - x3ptr[col]);
				if (cvIsInf(bptr[col]) || cvIsNaN(bptr[col]) || bptr[col] == 0)
				{
					bptr[col] = 0.00001;
				}

				s2ptr[col] = -1 / (2 * cptr[col]);
				uptr[col] = bptr[col] * s2ptr[col];
				aptr[col] = y1ptr[col] - bptr[col] * x1ptr[col] - cptr[col] * x1ptr[col] * x1ptr[col];
				Aptr[col] = exp(aptr[col] + (uptr[col] * uptr[col]) / (2 * s2ptr[col]));
				i++;
			}
		}
		x1.release();
		x2.release();
		x3.release();
		y1.release();
		y2.release();
		y3.release();
		I.release();
		c.release();
		b.release();
		a.release();

		Mat err = Mat::zeros(M, N, CV_64FC1);
		for (size_t p = 0; p < P; p++)
		{
			for (int row = 0; row < M; row++)
			{
				double* ptr = imgs[p].ptr<double>(row);
				double* ptr1 = err.ptr<double>(row);
				double* ptr2 = A.ptr<double>(row);
				double* uptr = u.ptr<double>(row);
				double* s2ptr = s2.ptr<double>(row);

				for (size_t col = 0; col < N; col++)
				{
					ptr1[col] += abs(ptr[col] - ptr2[col] * exp((-pow((p + 1 - uptr[col]), 2) / (2 * s2ptr[col]))));
				}
			}
			//fm(:, : , p) = fm(:, : , p). / fmax;
			imgs[p] = imgs[p] / Ymax;
		}
		A.release();
		s2.release();
		u.release();

		//h = fspecial('average', opts.nhsize);
		//inv_psnr = imfilter(err. / (P * fmax), h, 'replicate');

		Mat averageFilter(3, 3, CV_64FC1, Scalar(0)), inv_psnr;
		averageFilter = cv::Scalar::all(1.0 / (3 * 3));
		filter2D(err / (P * Ymax), inv_psnr, -1, averageFilter, Point(-1, -1), 0, BORDER_REPLICATE);
		err.release();
		Ymax.release();

		//S = 20 * log10(1. / inv_psnr);
		Mat S = Mat::zeros(M, N, CV_64FC1);
		for (int row = 0; row < M; row++)
		{
			double* ptr = S.ptr<double>(row);
			double* ptr1 = inv_psnr.ptr<double>(row);
			for (int col = 0; col < N; col++)
			{
				ptr[col] = 20 * log10(1 / ptr1[col]);
			}
		}
		inv_psnr.release();

		//phi = 0.5 * (1 + tanh(opts.alpha * (S - opts.sth))) / opts.alpha;
		Mat phi = Mat::zeros(M, N, CV_64FC1);
		for (int row = 0; row < M; row++)
		{
			double* ptr = phi.ptr<double>(row);
			double* ptr1 = S.ptr<double>(row);
			for (int col = 0; col < N; col++)
			{
				ptr[col] = 0.5 * (1 + tanh(0.2 * (ptr1[col] - 13))) / 0.2;
			}
		}
		S.release();
		phi.convertTo(phi, CV_32FC1);
		cv::medianBlur(phi, phi, 3);
		phi.convertTo(phi, CV_64FC1);
#pragma endregion
		for (size_t p = 0; p < P; p++)
		{
			for (int row = 0; row < M; row++)
			{
				double* ptr = imgs[p].ptr<double>(row);
				double* ptr1 = phi.ptr<double>(row);
				for (int col = 0; col < N; col++)
				{
					ptr[col] = 0.5 + 0.5 * tanh(ptr1[col] * (ptr[col] - 1));
				}
			}
		}
		phi.release();
	}




	Mat result;
	Mat fmn = sum3(imgs);
	if (channels == 3) {

		for (size_t i = 0; i < imgs.size(); i++)
		{
			imagesR[i].convertTo(imagesR[i], CV_64FC1);
			imagesG[i].convertTo(imagesG[i], CV_64FC1);
			imagesB[i].convertTo(imagesB[i], CV_64FC1);

			imagesR[i] = imagesR[i].mul(imgs[i]);
			imagesG[i] = imagesG[i].mul(imgs[i]);
			imagesB[i] = imagesB[i].mul(imgs[i]);
		}
		Mat imagesR1 = sum3(imagesR) / fmn;
		Mat imagesG1 = sum3(imagesG) / fmn;
		Mat imagesB1 = sum3(imagesB) / fmn;

		imagesR1.convertTo(imagesR1, CV_8UC1);
		imagesG1.convertTo(imagesG1, CV_8UC1);
		imagesB1.convertTo(imagesB1, CV_8UC1);

		Mat mergesrc[3] = { imagesR1, imagesG1,imagesB1 };
		merge(mergesrc, 3, result);
	}
	else {
		for (size_t i = 0; i < imgs.size(); i++)
		{
			imagesG[i].convertTo(imagesG[i], CV_64FC3);
			imagesG[i] = imagesG[i].mul(imgs[i]);
		}

		Mat imagesG1 = sum3(imagesG) / fmn;
		imagesG1.convertTo(result, CV_8UC1);
	}
	imagesR.clear();
	imagesB.clear();
	imagesG.clear();
	fmn.release();
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
