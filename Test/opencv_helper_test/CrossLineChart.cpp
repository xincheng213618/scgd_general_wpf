#include "function.h"

//制作不同视场的十字靶标
//Mat srcImage：输出图像
//int width：图像宽
//int height：图像高
//int Xpoint：交叉点的X坐标
//int Ypoint：交叉点的Y坐标
//int xLength：水平线的长度
//int yLength：垂直线的长度
//int lineThinkness:线的宽度
//double DrawColor[]:十字线的颜色

int CrossLineChart(Mat srcImage, int width, int height, int Xpoint, int Ypoint,int xLength, int yLength, int lineThinkness, double DrawColor[])
{	
	//绘制的线的颜色
	double B1 = DrawColor[0];
	double G1 = DrawColor[1];
	double R1 = DrawColor[2];
	line(srcImage, Point(Xpoint- xLength / 2, Ypoint), Point(Xpoint + xLength / 2, Ypoint), Scalar(B1, G1, R1), lineThinkness, 8, 0);
	line(srcImage, Point(Xpoint, Ypoint - yLength / 2), Point(Xpoint, Ypoint + yLength / 2), Scalar(B1, G1, R1), lineThinkness, 8, 0);	
	return 0;
}