#include "function.h"

//制作VID测试图卡
//int width:图像宽度
//int height：图像高度
//int xStep：水平间隔（点阵）,棋盘格水平宽度
//int yStep: 垂直间隔（点阵），棋盘格垂直宽度 
// int DrawSize:为线的时候，表示线宽,为点的时候，表示边长
//double pointcolor[]:方形颜色pointcolor
//double backgroundcolor[]:背景颜色
// const char* filePath:数据保存路径
//int flag：标志位0为点阵，1为横线对，2为竖线对，3为横竖线对，4为棋盘格。

int makeVidPointChart(int width, int height,double xField, double yField, int xStep, int yStep, int DrawSize, double backgroundColor[], double drawColor[], const char* filePath, int flag)
{
	//背景颜色
	double B0 = backgroundColor[0];
	double G0 = backgroundColor[1];
	double R0 = backgroundColor[2];

	//绘图颜色
	double B1 = drawColor[0];
	double G1 = drawColor[1];
	double R1 = drawColor[2];
	
	//生成背景图
	Mat srcImg(height, width, CV_8UC3, Scalar(B0, G0, R0));  //创建一个高2000，宽1200的灰度图
	
	//计算需要绘图区的大小
	int x_LeftUp_Point = int(width/2 - width/2 * xField);
	int y_LeftUp_Point = int(height/2 - height/2 * yField);
	int x_RightDown_Point = int(width/2 + width/2 * xField);
	int y_RightDown_Point = int(height/2 + height/2 * yField);
	int RoiWidth = x_RightDown_Point - x_LeftUp_Point;
	int RoiHeight = y_RightDown_Point - y_LeftUp_Point;	
	int xNum = RoiWidth / xStep;//水平方向点的个数,垂直线的个数
	int yNum = RoiHeight / yStep;//垂直方向点的个数，水平线的个数
	if (flag == 0)
	{
		for (int i = 0; i <= xNum; i++)
		{
			for (int j = 0; j <= yNum; j++)
			{
				int point_x = xStep * (i + 1) - xStep / 2 + x_LeftUp_Point;//点的中心坐标x值			
				int point_y = yStep * (j + 1) - yStep / 2 + y_LeftUp_Point;//点的中心坐标y值				
				/*circle(makImg, Point(point_x, point_y), R, Scalar(255, 255, 255), -1, 8);*/
				//方形点
				if (DrawSize % 2 == 0)
				{
					rectangle(srcImg, Point(point_x, point_y), Point(point_x + DrawSize / 2, point_y + DrawSize / 2), Scalar(B1, G1, R1), -1, 8, 0);
				}
				else
				{
					rectangle(srcImg, Point(point_x - DrawSize / 2, point_y - DrawSize / 2), Point(point_x + DrawSize / 2, point_y + DrawSize / 2), Scalar(B1, G1, R1), -1, 8, 0);
				}
			}
		}
	}
	else if (flag==1)
	{
		for (int i = 0; i < RoiHeight; i++)
		{
			for (int j = 0; j < RoiWidth; j++)
			{
				if (int(i /DrawSize) % 2 == 0)
				{
					srcImg.at<Vec3b>(y_LeftUp_Point+i, x_LeftUp_Point+j)[0] = B1;
					srcImg.at<Vec3b>(y_LeftUp_Point + i, x_LeftUp_Point + j)[1] = G1;
					srcImg.at<Vec3b>(y_LeftUp_Point + i, x_LeftUp_Point + j)[2] = R1;
				}
			}
		}
	}
	else if (flag==2)
	{
		for (int i = 0; i < RoiHeight; i++)
		{
			for (int j = 0; j < RoiWidth; j++)
			{
				if (int(j / DrawSize) % 2 == 0)
				{
					srcImg.at<Vec3b>(y_LeftUp_Point + i, x_LeftUp_Point + j)[0] = B1;
					srcImg.at<Vec3b>(y_LeftUp_Point + i, x_LeftUp_Point + j)[1] = G1;
					srcImg.at<Vec3b>(y_LeftUp_Point + i, x_LeftUp_Point + j)[2] = R1;
				}
			}
		}
	}
	else if (flag == 4)
	{
		for (int i = 0; i < RoiHeight; i++)
		{						// 遍历所有像素点
			for (int j = 0; j < RoiWidth; j++)
			{
				if (int(i / yStep) % 2 == 0)
				{             //如果是奇数行
					if (int(j / xStep) % 2 != 0)
					{
						srcImg.at<Vec3b>(y_LeftUp_Point + i, x_LeftUp_Point + j)[0] = B1;
						srcImg.at<Vec3b>(y_LeftUp_Point + i, x_LeftUp_Point + j)[1] = G1;
						srcImg.at<Vec3b>(y_LeftUp_Point + i, x_LeftUp_Point + j)[2] = R1;
					}
				}
				if (int(i / yStep) % 2 != 0)
				{            //如果是偶数行
					if (int(j / xStep) % 2 == 0)
					{
						srcImg.at<Vec3b>(y_LeftUp_Point + i, x_LeftUp_Point + j)[0] = B1;
						srcImg.at<Vec3b>(y_LeftUp_Point + i, x_LeftUp_Point + j)[1] = G1;
						srcImg.at<Vec3b>(y_LeftUp_Point + i, x_LeftUp_Point + j)[2] = B1;
					}
				}
			}
		}
	}
	else
	{
		cout << "错误类型为：制作图片flag编号错误" << endl;
		return -1;
	}
	imwrite(filePath, srcImg);
	waitKey(0);
	return 0;
}
