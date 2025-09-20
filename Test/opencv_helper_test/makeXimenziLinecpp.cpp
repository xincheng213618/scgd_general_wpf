#include "function.h"

int makeXimenziLine(const char* picFile, int width, int height, double x, double y, int R, double angle, int flag)
{
	Mat srcImg(height, width, CV_8UC3);
	Mat makeImg;
	srcImg.setTo(0);
	for (int i = 0; i < (360 / (2 * angle)); i++)
	{
		ellipse(srcImg, Point(width / 2, height / 2), Size(int(R), int(R)), 0, 2 * angle*i, 2 * angle*i + angle, cv::Scalar(255, 255, 255), -1, cv::LINE_AA, 0);
	}
	   	
	if (flag == 0)
	{
		makeImg = srcImg;
	}
	else if (flag == 1)
	{
		makeImg = ~srcImg;
	}
	//imshow("makeImg", makeImg);
	imwrite(picFile, makeImg);
	return 0;
}
