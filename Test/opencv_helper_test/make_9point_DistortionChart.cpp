#include "function.h"

//制作边缘畸变测试图卡
//int width:图像宽度
//int height：图像高度
//int R：点的半径
//double pointcolor[]:点的颜色pointcolor
//double backgroundcolor[]:边缘畸变点背景颜色
// const char* filePath:数据保存路径
//int flag：标志位,为0，标准的边缘9点位置,为1按照首个点的坐标绘制9点图

int make_9point_DistortionChart(int width, int height, int R, double backgroundcolor[], double pointcolor[],const char* filePath, int flag)
{
	
	//背景颜色
	double B0 = backgroundcolor[0];
	double G0 = backgroundcolor[1];
	double R0 = backgroundcolor[2];


	//点的颜色
	double B1 = pointcolor[0];
	double G1 = pointcolor[1];
	double R1 = pointcolor[2];
	
	//首个点的坐标
	int firstX = 90;
	int firstY = 20;

	//绘制背景为（B1.G1,R1）的图像
	Mat srcImg(height, width, CV_8UC3, Scalar(B0, G0, R0));  //创建一个高为height,宽为width的，3通道图像
	int stepX = (width - 2 * R) / 2;
	int stepy= (height - 2 * R) / 2;
	int stepX1 = width / 2 - firstX;
	int stepY1 = height / 2 - firstY;
	if (flag == 0)
	{
		for (int i = 0; i <= 2; i++)
		{
			for (int j = 0; j <= 2; j++)
			{
				int point_xcenter = int(i * stepX + R);
				int point_ycenter = int(j * stepy + R);
				circle(srcImg, Point(point_xcenter, point_ycenter), R, Scalar(B1, G1, R1), -1, 8);
			}
		}
	}
	else if (flag == 1)
	{
		for (int i = 0; i <= 2; i++)
		{
			for (int j = 0; j <= 2; j++)
			{
				int point_xcenter = int(i * stepX1 + firstX);
				int point_ycenter = int(j * stepY1 + firstY);
				circle(srcImg, Point(point_xcenter, point_ycenter), R, Scalar(B1, G1, R1), -1, 8);
			}
		}
	}	
	else
	{
	cout << "错误类型为：制作图片flag编号错误" << endl;
	return -1;
	}
	imwrite(filePath, srcImg);
	return 0;
}
