#include "function.h"

int logoDetect(const char*input_path, int width, int height)
{
	Mat srcImg = imread(input_path);
	Mat grayImg;
	cvtColor(srcImg, grayImg, COLOR_BGR2GRAY);
	//ͼƬȥ��
	Mat meanBlurImg;
	medianBlur(grayImg, meanBlurImg, 3);
	GaussianBlur(meanBlurImg, meanBlurImg, Size(17, 17), 10, 20);


	//ͼƬ��ֵ��

	Mat BwImg;
	//threshold(meanBlurImg, BwImg, 82, 255, THRESH_BINARY);
	adaptiveThreshold(meanBlurImg, BwImg, 255, 1, 0, 16, 20);
	/*bilateralFilter(meanBlurImg, BwImg, 5, 12.5, 12.5);*/
	//Mat kernel1 = (Mat_<char>(3, 3) << -1, -1, -1, -1, 8, -1, -1, -1, -1);    // ������˹����
	////Mat kernel2 = (Mat_<char>(3, 3) << -1, -1, -1, -1, 9, -1, -1, -1, -1);  // ����Աȶ�
	////Mat kernel3 = (Mat_<char>(3, 3) << -1, -1, -1, -1, 8, -1, -1,-1, -1);// ������˹����2
	//Mat dst, dst2, dst3;
	//filter2D(meanBlurImg, BwImg, -1, kernel1);
	/*filter2D(srcImg, dst2, -1, kernel2);
	filter2D(srcImg, dst3, -1, kernel3, Point(-1, -1), 50.0);*/
	/*Canny(meanBlurImg, BwImg, 5, 35, 3, false);*/

	////�������
	//vector<vector<Point>>contours;
	//vector<Vec4i>hierarchy;

	////����ROI������������
	//findContours(RoiBlurImg, contours, hierarchy, RETR_LIST, CHAIN_APPROX_NONE);
	//logRecording(logName, "������ȡ�ɹ�");

	////��������ͼ
	//Mat dstImage = Mat::zeros(ROI.size(), CV_8UC3);
	//vector<vector<Point>> contours_poly(contours.size());
	//vector<Rect>boundRect(contours.size());
	//vector<Point2f>center(contours.size());
	//vector<float>radius(contours.size());

	//double minR = 10000, maxR = 0;
	//double minC = 10000, maxC = 0;

	////���������������е����Բ
	//for (int i = 0; i < contours.size(); i++)
	//{
	//	approxPolyDP(Mat(contours[i]), contours_poly[i], 3, true);
	//	minEnclosingCircle(contours_poly[i], center[i], radius[i]);
	//	Scalar color = Scalar(rand() % 255, rand() % 255, rand() % 255);
	//	drawContours(dstImage, contours, i, color, 1, 8, hierarchy);
	//}
	//for (int i = 0; i < contours.size(); i++)
	//{
	//	Scalar color = Scalar(rand() % 255, rand() % 255, rand() % 255);

	//	//��������Ҫ��������������Բ����Բ������
	//	if ((radius[i] >= Minradius1) && (radius[i] <= Maxradius1))
	//	{
	//		if ((center[i].x > centerXMin) && (center[i].x < centerXMax) && (center[i].y > centerYMin) && (center[i].y < centerYMax))
	//		{
	//			circle(dstImage, center[i], (int)radius[i], color, 2, 8, 0);

	//			string s = to_string((int)radius[i]);
	//			putText(dstImage, s, center[i], CV_FONT_HERSHEY_SIMPLEX, 1, Scalar(0, 0, 255), 2, 8);

	//			if (minR > center[i].y)
	//			{
	//				minR = center[i].y;
	//			}
	//			if (maxR < center[i].y)
	//			{
	//				maxR = center[i].y;
	//			}
	//			if (minC > center[i].x)
	//			{
	//				minC = center[i].x;
	//			}
	//			if (maxC < center[i].x)
	//			{
	//				maxC = center[i].x;
	//			}
	//		}
	//	}
	//}
	/*cout << "thresh=" << thresh << endl;*/
	return 0;
}
