#pragma once

#include <opencv2/core/core.hpp>  
#include <opencv2/highgui/highgui.hpp>
#include <opencv2/imgproc/imgproc.hpp>

#include "cudamath.h"
using namespace cv;

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

void process_image(Mat& img)
{
	img = gfocus(img);
}

Mat Sum3(std::vector<Mat> mats) {
	Mat result = Mat::zeros(mats[0].size(), CV_64FC1);
	for (size_t i = 0; i < mats.size(); i++)
	{
		result += mats[i];
	}
	return result;
}

Mat Fusion(std::vector<Mat> imgs, int STEP) {


	clock_t start, end;
	size_t P;
	int M = 0, N = 0, channels = 0;
	P = imgs.size();
	start = clock();
	for (size_t i = 0; i < P; i++)
	{
		M = imgs[i].rows;
		N = imgs[i].cols;
		channels = imgs[i].channels();
	}
	double* img1, * img2, * img3, * img4, * img5, * img6, * img7, * img8;
	cudaMalloc((void**)&img1, M * N * sizeof(double));
	cudaMalloc((void**)&img2, M * N * sizeof(double));
	cudaMalloc((void**)&img3, M * N * sizeof(double));
	cudaMalloc((void**)&img4, M * N * sizeof(double));
	cudaMalloc((void**)&img5, M * N * sizeof(double));
	cudaMalloc((void**)&img6, M * N * sizeof(double));
	cudaMalloc((void**)&img7, M * N * sizeof(double));
	cudaMalloc((void**)&img8, M * N * sizeof(double));

	end = clock();
	std::cout << "CUDA 申请内存花费了" << (double)(end - start) / CLOCKS_PER_SEC << "秒" << std::endl;
	start = clock();


	std::vector<std::thread> threads;

	std::vector<Mat> imagesR, imagesG, imagesB;
	if (channels == 3) {
		for (size_t i = 0; i < P; i++)
		{
			std::vector<cv::Mat> channels;
			cv::split(imgs[i], channels);
			imagesR.push_back(channels[0]);
			imagesG.push_back(channels[1]);
			imagesB.push_back(channels[2]);
			threads.emplace_back([i, &imgs]() {
				cvtColor(imgs[i], imgs[i], COLOR_BGR2GRAY);
				imgs[i].convertTo(imgs[i], CV_64FC1, 1.0 / 255.0);
				imgs[i] = gfocus(imgs[i]);
				});
		}

	}
	else {
		for (size_t i = 0; i < P; i++)
		{
			imagesG.push_back(imgs[i]);
			threads.emplace_back([i, &imgs]() {
				cvtColor(imgs[i], imgs[i], COLOR_BGR2GRAY);
				imgs[i].convertTo(imgs[i], CV_64FC1, 1.0 / 255.0);
				imgs[i] = gfocus(imgs[i]);
				});
		}
	}		
	for (auto& t : threads)
	{
		t.join();
	}
	threads.clear();

	if (STEP > 0 && P > 2 * STEP + 1) {
		end = clock();
		std::cout << "00时间" << (double)(end - start) / CLOCKS_PER_SEC << "秒" << std::endl;
		Mat Ymax(M, N, CV_64FC1);
		Mat I(M, N, CV_64FC1);
		end = clock();
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
		end = clock();
		std::cout << "01时间" << (double)(end - start) / CLOCKS_PER_SEC << "秒" << std::endl;
		Mat s2(M, N, CV_64FC1);
		Mat u(M, N, CV_64FC1);
		Mat A(M, N, CV_64FC1);


		Mat y1t(M, N, CV_64FC1);
		Mat y2t(M, N, CV_64FC1);
		Mat y3t(M, N, CV_64FC1);
		Mat Ic(M, N, CV_64FC1);

		for (int row = 0; row < M; row++)
		{
			double* y1tptr = y1t.ptr<double>(row);
			double* y2tptr = y2t.ptr<double>(row);
			double* y3tptr = y3t.ptr<double>(row);
			double* ptr = I.ptr<double>(row);
			double* Icptr = Ic.ptr<double>(row);


			for (int col = 0; col < N; col++)
			{
				if (ptr[col] <= STEP)
				{
					Icptr[col] = STEP + 1;
				}
				else if (ptr[col] >= (int)P - STEP) {
					Icptr[col] = (int)P - STEP;
				}
				else {
					Icptr[col] = ptr[col];
				}

				y1tptr[col] = imgs[Icptr[col] - STEP - 1].at<double>(row, col);
				y2tptr[col] = imgs[Icptr[col] - 1].at<double>(row, col);
				y3tptr[col] = imgs[Icptr[col] + STEP - 1].at<double>(row, col);
			}
		}
		end = clock();
		std::cout << "02时间" << (double)(end - start) / CLOCKS_PER_SEC << "秒" << std::endl;

		cudaMemcpy(img1, I.data, M * N * sizeof(double), cudaMemcpyHostToDevice);
		cudaMemcpy(img2, y1t.data, M * N * sizeof(double), cudaMemcpyHostToDevice);
		cudaMemcpy(img3, y2t.data, M * N * sizeof(double), cudaMemcpyHostToDevice);
		cudaMemcpy(img4, y3t.data, M * N * sizeof(double), cudaMemcpyHostToDevice);
		cudaMemcpy(img5, Ic.data, M * N * sizeof(double), cudaMemcpyHostToDevice);
		cudaMemcpy(img6, s2.data, M * N * sizeof(double), cudaMemcpyHostToDevice);
		cudaMemcpy(img7, u.data, M * N * sizeof(double), cudaMemcpyHostToDevice);
		cudaMemcpy(img8, A.data, M * N * sizeof(double), cudaMemcpyHostToDevice);

		dim3 blockDim(32, 32);
		dim3 gridDim((N + blockDim.x - 1) / blockDim.x, (M + blockDim.y - 1) / blockDim.y);

		// Launch the kernel
		kernel << <gridDim, blockDim >> > (img1, img2, img3, img4, img5, img6, img7, img8,M, N, STEP);

		cudaMemcpy(s2.data, img6, M* N * sizeof(double), cudaMemcpyDeviceToHost);
		cudaMemcpy(u.data, img7, M* N * sizeof(double), cudaMemcpyDeviceToHost);
		cudaMemcpy(A.data, img8, M* N * sizeof(double), cudaMemcpyDeviceToHost);
		I.release();


		Mat err = Mat::zeros(M, N, CV_64FC1);
		cudaMemcpy(img2, err.data, M * N * sizeof(double), cudaMemcpyHostToDevice);
		cudaMemcpy(img3, A.data, M * N * sizeof(double), cudaMemcpyHostToDevice);
		cudaMemcpy(img4, u.data, M * N * sizeof(double), cudaMemcpyHostToDevice);
		cudaMemcpy(img5, s2.data, M * N * sizeof(double), cudaMemcpyHostToDevice);
		cudaMemcpy(img6, Ymax.data, M * N * sizeof(double), cudaMemcpyHostToDevice);

		for (size_t p = 0; p < P; p++)
		{
			cudaMemcpy(img1, imgs[p].data, M * N * sizeof(double), cudaMemcpyHostToDevice);

			dim3 blockSize(32, 32);
			dim3 gridSize((N + blockSize.x - 1) / blockSize.x, (M + blockSize.y - 1) / blockSize.y);

			calculate_err << <gridSize, blockSize >> > (img1, img2, img3, img4, img5,img6, M, N,p);

			//fm(:, : , p) = fm(:, : , p). / fmax;
			cudaMemcpy(imgs[p].data,img1, M* N * sizeof(double), cudaMemcpyDeviceToHost);
		}

		cudaMemcpy(err.data, img2, M* N * sizeof(double), cudaMemcpyDeviceToHost);
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
		cudaMemcpy(img1, S.data, M* N * sizeof(double), cudaMemcpyHostToDevice);
		cudaMemcpy(img2, inv_psnr.data, M* N * sizeof(double), cudaMemcpyHostToDevice);

		dim3 blockSize(32, 32);
		dim3 gridSize((N + blockSize.x - 1) / blockSize.x, (M + blockSize.y - 1) / blockSize.y);
		calculate_S << <gridSize, blockSize >> > (img1, img2, M, N);

		cudaMemcpy(S.data, img1, M* N * sizeof(double), cudaMemcpyDeviceToHost);

		inv_psnr.release();

		//phi = 0.5 * (1 + tanh(opts.alpha * (S - opts.sth))) / opts.alpha;
		Mat phi = Mat::zeros(M, N, CV_64FC1);

		cudaMemcpy(img2, S.data, M* N * sizeof(double), cudaMemcpyHostToDevice);
		cudaMemcpy(img1, phi.data, M* N * sizeof(double), cudaMemcpyHostToDevice);
		int block_size = 16;
		dim3 dim_block(block_size, block_size);
		dim3 dim_grid((N + block_size - 1) / block_size, (M + block_size - 1) / block_size);
		compute << <dim_grid, dim_block >> > (img1, img2, M, N);
		cudaMemcpy(phi.data, img1, M* N * sizeof(double), cudaMemcpyDeviceToHost);

		phi.convertTo(phi, CV_32FC1);
		cv::medianBlur(phi, phi, 3);
		phi.convertTo(phi, CV_64FC1);

		cudaMemcpy(img2, phi.data, M* N * sizeof(double), cudaMemcpyHostToDevice);
		for (size_t p = 0; p < P; p++)
		{
			cudaMemcpy(img1, imgs[p].data, M* N * sizeof(double), cudaMemcpyHostToDevice);

			int block_size = 256;
			int num_blocks = (M + block_size - 1) / block_size;
			tanh_kernel << <num_blocks, block_size >> > (img1, img2, M, N);
			cudaMemcpy(imgs[p].data, img1, M* N * sizeof(double), cudaMemcpyDeviceToHost);
		}
		phi.release();
	}
	Mat result;
	Mat fmn = Sum3(imgs);
	if (channels == 3) {

		for (size_t i = 0; i < imgs.size(); i++)
		{
			threads.emplace_back([i, &imgs,&imagesR,&imagesG,&imagesB]() {
				imagesR[i].convertTo(imagesR[i], CV_64FC1);
				imagesG[i].convertTo(imagesG[i], CV_64FC1);
				imagesB[i].convertTo(imagesB[i], CV_64FC1);

				imagesR[i] = imagesR[i].mul(imgs[i]);
				imagesG[i] = imagesG[i].mul(imgs[i]);
				imagesB[i] = imagesB[i].mul(imgs[i]);
				});
		}
		for (auto& t : threads)
		{
			t.join();
		}
		threads.clear();

		Mat imagesR1 = Sum3(imagesR) / fmn;
		Mat imagesG1 = Sum3(imagesG) / fmn;
		Mat imagesB1 = Sum3(imagesB) / fmn;

		imagesR1.convertTo(imagesR1, CV_8UC1);
		imagesG1.convertTo(imagesG1, CV_8UC1);
		imagesB1.convertTo(imagesB1, CV_8UC1);

		Mat mergesrc[3] = { imagesR1, imagesG1,imagesB1 };
		cv::merge(mergesrc, 3, result);

	}
	else {
		for (size_t i = 0; i < imgs.size(); i++)
		{
			imagesG[i].convertTo(imagesG[i], CV_64FC3);
			imagesG[i] = imagesG[i].mul(imgs[i]);
		}

		Mat imagesG1 = Sum3(imagesG) / fmn;
		imagesG1.convertTo(result, CV_8UC1);
	}
	end = clock();
	std::cout << "算法花费了" << (double)(end - start) / CLOCKS_PER_SEC << "秒" << std::endl;
	imagesR.clear();
	imagesB.clear();
	imagesG.clear();
	fmn.release();
	return result;
}


