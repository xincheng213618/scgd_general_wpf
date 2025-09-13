#include "function.h"

//制作边缘畸变测试图卡
//int width:图像宽度
//int height：图像高度
//int xLenth：水平长
//int yLenth: 垂直宽 
// int linethickness:线宽
//double pointcolor[]:方形颜色pointcolor
//double backgroundcolor[]:背景颜色
// const char* filePath:数据保存路径
//int flag：标志位

int makeBinocularFusionChart(int width, int height, int xLenth, int yLenth,int linethickness, double backgroundcolor[], double Drawcolor[], const char* filePath, int flag)
{

	//背景颜色
	double B0 = backgroundcolor[0];
	double G0 = backgroundcolor[1];
	double R0 = backgroundcolor[2];


	//点的颜色
	double B1 = Drawcolor[0];
	double G1 = Drawcolor[1];
	double R1 = Drawcolor[2];



	//绘制背景为（B1.G1,R1）的图像
	Mat srcImg(height, width, CV_8UC3, Scalar(B0, G0, R0));  //创建一个高为height,宽为width的，3通道图像
	int xcent = width / 2;
	int ycent = height / 2;
	for (int i = 0; i <=2; i++)
	{
		for (int j = 0; j <=2; j++)
		{
			line(srcImg, Point(0,ycent+yLenth*(i-1)), Point(width, ycent + yLenth * (i - 1)), Scalar(B1, G1, R1), linethickness, LINE_4);
			line(srcImg, Point(xcent+xLenth*(j-1) , 0), Point(xcent + xLenth * (j - 1),height), Scalar(B1, G1, R1), linethickness, LINE_4);
		}
	}
	//imshow("srcImg", srcImg);
	//waitKey(0);
	imwrite(filePath, srcImg);	
	return 0;
}








