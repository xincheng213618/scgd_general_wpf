#include"function.h"

//Mat srcImage: 输入图像
//Mat dstImage：输出图像
//int width:图像宽度
//int height：图像高度
//int line_Lenth:线的长度
//int line_Thickness：线的宽度
//double xpoint:线对中心的水平视场图像坐标
//double ypoint:线对中心的垂直视场图像坐标
// double lineColor:线的颜色
//int flag：标志位
//制作特定位置的四线对
int fourLinePair(Mat srcImage, int width, int height, int line_Lenth, int line_Thickness, double lineColor[],int xpoint, int ypoint, int flag)
{
	//背景颜色
	double B1 = lineColor[0];
	double G1 = lineColor[1];
	double R1 = lineColor[2];
	//创建行数为height,列数为width
	Mat srcImg = srcImage;
	int x_cent = width / 2;
	int y_cent = height / 2;
	int i, j;
	for (i = 0; i < line_Lenth; i++)
	{
		for (j = 0; j < line_Lenth; j++)
		{
			//横线对
			if (int(i / line_Thickness) % 2 == 0)
			{
				//特定位置线对图
				
				//左上
				srcImg.at<Vec3b>(ypoint - line_Lenth + i, xpoint - line_Lenth + j)[0] = B1;
				srcImg.at<Vec3b>(ypoint - line_Lenth + i, xpoint - line_Lenth + j)[1] = G1;
				srcImg.at<Vec3b>(ypoint - line_Lenth + i, xpoint - line_Lenth + j)[2] = R1;

				//右下
				srcImg.at<Vec3b>(ypoint + i, xpoint + j)[0] = B1;
				srcImg.at<Vec3b>(ypoint + i, xpoint + j)[1] = G1;
				srcImg.at<Vec3b>(ypoint + i, xpoint + j)[2] = R1;
			}
			//竖线对
			if (int(j / line_Thickness) % 2 == 0)
			{
				//右上
				srcImg.at<Vec3b>(ypoint - line_Lenth + i, xpoint + j)[0] = B1;
				srcImg.at<Vec3b>(ypoint - line_Lenth + i, xpoint + j)[1] = G1;
				srcImg.at<Vec3b>(ypoint - line_Lenth + i, xpoint + j)[2] = R1;
				//左下
				srcImg.at<Vec3b>(ypoint + i, xpoint - line_Lenth + j)[0] = B1;
				srcImg.at<Vec3b>(ypoint + i, xpoint - line_Lenth + j)[1] = G1;
				srcImg.at<Vec3b>(ypoint + i, xpoint - line_Lenth + j)[2] = R1;
			}
		}
	}	
	return 0;
}
