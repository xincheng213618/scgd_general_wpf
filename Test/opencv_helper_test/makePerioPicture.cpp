#include "function.h"


//int width：图像的宽度
//int height：图像的高度
//int xPoint：水平方向多少列个点，或线
//int yPoint：垂直平方向多少行个点，或线
//int R:圆的半径或线的宽度
//double backgroundColor[]：背景色
// double drawColor[]：绘图色
//const char*filePath：保存的图像的名称位置
//int flag:保存哪种类型的图片
//00: 棋盘格（黑白黑白）
//01：棋盘格（白黑白黑）
//10：点阵图（黑底白点阵图）
//11：点阵图（白底黑点阵图）
//12: 点阵图（9点点阵图（HUD,VR测试Chart，Xpoint=10,ypoint=10））
//13: 白屏黑点阵图（9点点阵图（HUD,VR测试Chart）Xpoint=10,ypoint=10）
//20: 网格图（黑底白线）
//21: 网格图（黑底白线带边框）
//22: 网格图（白底黑线）
//23：网格图（白底黑线带边框）
//30: 线对图（黑底白线横向线对）
//31：线对图（黑底白线纵向线对）
//32横竖线对图交叉图，白背景黑线对白点
//33 VID每隔5个点做个白点
//34 VID每隔5个点做一个白点，中间做一个直径为5的圆
//40：制作同心圆（中心区域圆）
//41：制作同心圆（中心区域圆加十字）
//42：制作同心圆环（不同视场）

int makePerioPicture(int width, int height, int xPoint, int yPoint, int R,double backgroundColor[],double drawColor[], const char *filePath, int flag)
{
	//背景颜色
	double B0 = backgroundColor[0];
	double G0 = backgroundColor[1];
	double R0 = backgroundColor[2];


	//绘图颜色
	double B1 = drawColor[0];
	double G1 = drawColor[1];
	double R1 = drawColor[2];
	Mat srcImg(height, width, CV_8UC3, Scalar(B0, G0,R0));  //创建一个高2000，宽1200的灰度图
	Mat makImg;
	//每一单元格宽,Xpoint为水平方向多少个点
	double step_x = static_cast<double>(width) / xPoint;

	//每个单元格高，yPoint表示垂直方向多少个点
	double step_y = static_cast<double>(height) / yPoint;
	//圆形绘画
	int i, j;

	//绘制棋盘格（黑白黑白）
	if (flag == 00)
	{
		makImg = srcImg;
		for (int i = 0; i < height; i++)
		{						// 遍历所有像素点
			for (int j = 0; j < width; j++)
			{
				if (int(i / step_y) % 2 == 0)
				{             //如果是奇数行
					if (int(j / step_x) % 2 != 0)
					{
						makImg.at<Vec3b>(i, j)[0] = B1;
						makImg.at<Vec3b>(i, j)[1] = G1;
						makImg.at<Vec3b>(i, j)[2] = R1;
					}
				}
				if (int(i / step_y) % 2 != 0)
				{            //如果是偶数行
					if (int(j / step_x) % 2 == 0)
					{
						makImg.at<Vec3b>(i, j)[0] = B1;
						makImg.at<Vec3b>(i, j)[1] = G1;
						makImg.at<Vec3b>(i, j)[2] = B1;
					}
				}
			}
		}
	}

	//绘制棋盘格（白黑白黑）
	else if (flag == 01)
	{
		makImg = srcImg;
		for (int i = 0; i < height; i++)
		{						// 遍历所有像素点
			for (int j = 0; j < width; j++)
			{
				if (int(i / step_y) % 2 == 0)
				{             //如果是奇数行
					if (int(j / step_x) % 2 == 0)
					{
						makImg.at<Vec3b>(i, j)[0] = B1;
						makImg.at<Vec3b>(i, j)[1] = G1;//
						makImg.at<Vec3b>(i, j)[2] = R1;
					}
				}
				if (int(i / step_y) % 2 != 0)
				{            //如果是偶数行
					if (int(j / step_x) % 2 != 0)
					{
						makImg.at<Vec3b>(i, j)[0] = B1;
						makImg.at<Vec3b>(i, j)[1] = G1;
						makImg.at<Vec3b>(i, j)[2] = R1;
					}
				}
			}
		}
	}
	//绘制点阵图(黑底白点)	
	//xpoint 水平方向点的间隔
	//ypoint 垂直方向点的间隔
	//R点的长宽或半径
	

	else if (flag == 10)
	{
		makImg = srcImg;
		int xNum = width / xPoint;//水平方向点的个数
		int yNum = height / yPoint;//垂直方向点的个数
		for (i = 0; i <= xNum; i++)
		{
			for (j = 0; j <= yNum; j++)
			{
				int point_x = xPoint * (i + 1) - xPoint / 2;//圆心坐标x值			
				int point_y = yPoint * (j + 1) - yPoint / 2;//圆心坐标y值	
				/*circle(makImg, Point(point_x, point_y), R, Scalar(255, 255, 255), -1, 8);*/
				//方形点
				if (R % 2 == 0)
				{
					rectangle(makImg, Point(point_x, point_y), Point(point_x + R / 2, point_y + R / 2), Scalar(B1, G1, R1), -1, 8, 0);
				}
				else
				{
					rectangle(makImg, Point(point_x - R / 2, point_y - R / 2), Point(point_x + R / 2, point_y + R / 2), Scalar(0, 0, 0), -1, 8, 0);
				}
				
			}
		}
	}
	//绘制点阵图(白底黑点)	
	//xpoint 水平方向点的间隔
	//ypoint 垂直方向点的间隔
	//R点的长宽或半径
	else if (flag == 11)
	{
		makImg = ~srcImg;
		int xNum = width / xPoint;//水平方向点的个数
		int yNum = height / yPoint;//垂直方向点的个数
		for (i = 0; i <= xNum; i++)
		{
			for (j = 0; j <= yNum; j++)
			{
				int point_x = xPoint * (i + 1) - xPoint / 2;//圆心坐标x值			
				int point_y = yPoint * (j + 1) - yPoint / 2;//圆心坐标y值	
				//圆点
				//circle(makImg, Point(point_x, point_y), R, Scalar(0, 0, 0), -1, 8);
				//方形点
				if (R % 2 == 0)
				{
					rectangle(makImg, Point(point_x, point_y), Point(point_x + R / 2, point_y + R / 2), Scalar(B1, G1, R1), -1, 8, 0);
				}
				else
				{
					rectangle(makImg, Point(point_x - R / 2, point_y - R / 2), Point(point_x + R / 2, point_y + R / 2), Scalar(0, 0, 0), -1, 8, 0);
				}
			}
		}
	}
	//点阵图（白背景黑点 9点点阵图（HUD,VR测试Chart））
	else if (flag == 12)
	{
		makImg = ~srcImg;


		for (i = step_x; i <= width; i += (4 * step_x))
		{
			for (j = step_y; j < height; j += (4 * step_y))
			{
				circle(makImg, Point(i, j), R, Scalar(0, 0, 0), -1, 8);
			}
		}
	}
	else if (flag == 121)
	{
	makImg = srcImg;

	for (i = step_x - 1; i <= width; i += (4 * step_x))
	{
		for (j = step_y - 1; j < height; j += (4 * step_y))
		{
			int width = 55;
			int height = 49;
			rectangle(makImg, Point(i - width / 2, j - height / 2), Point(i + width / 2 + 1, j + height / 2 + 1), Scalar(B1, G1, R1), -1, 8);
		}
	}
	}

	//点阵图（黑背景白点9点点阵图（HUD,VR测试Chart））	
	else if (flag == 13)
	{
	
		makImg = srcImg;


		for (i = step_x-1; i <= width; i += (4 * step_x))
		{
			for (j = step_y-1; j < height; j += (4 * step_y))
			{
			
				circle(makImg, Point(i, j), R, Scalar(B1, G1, R1), -1, 8);				
				
			}
		}
	}
	//绘制中间标准9点方框图1/10位置
	else if (flag == 131)
	{	
	makImg = srcImg;


	for (i = step_x - 1; i <= width; i += (4 * step_x))
	{
		for (j = step_y - 1; j < height; j += (4 * step_y))
		{
			int width = 75;
			int height = 65;
			
			/*int width = 55;
			int height = 49;*/		
			rectangle(makImg, Point(i - width / 2, j - height / 2), Point(i + width / 2 + 1, j + height / 2 + 1), Scalar(B1, G1, R1), -1, 8);
		}
	}
	}


	//制作9点均匀性图（均匀分布9点方框图）
	else if (flag == 132)
	{
	makImg = srcImg;

	for (i = step_x /2- 1; i <= width; i += step_x)
	{
		for (j = step_y/2 - 1; j < height; j += step_y)
		{
			int width = 75;
			int height = 65;

			/*int width = 55;
			int height = 49;*/
			rectangle(makImg, Point(i - width / 2, j - height / 2), Point(i + width / 2 + 1, j + height / 2 + 1), Scalar(B1, G1, R1), -1, 8);
		}
	}
	}
	
	//制作9点均匀性图（均匀分布9点方框图）2/10位置

	else if (flag == 133)
	{
	makImg = srcImg;


	for (i = 2*step_x - 1; i <= width; i += (3 * step_x))
	{
		for (j = 2*step_y - 1; j < height; j += (3 * step_y))
		{
			int width = 75;
			int height = 65;
			/*
			int width = 55;
			int height = 49;*/
			rectangle(makImg, Point(i - width / 2, j - height / 2), Point(i + width / 2 + 1, j + height / 2 + 1), Scalar(B1, G1, R1), -1, 8);
		}
	}
	}

	//int width：图像的宽度
	//int height：图像的高度
	//int xPoint：水平方向多少列个点，或线
	//int yPoint：垂直平方向多少行个点，或线
	//int R:圆的半径或线的宽度
	//double backgroundColor[]：背景色
	// double drawColor[]：绘图色
	//const char*filePath：保存的图像的名称位置
	//int flag:保存哪种类型的图片
	//00: 棋盘格（黑白黑白）
	//01：棋盘格（白黑白黑）
	//10：点阵图（黑底白点阵图）
	//11：点阵图（白底黑点阵图）
	//12: 点阵图（9点点阵图（HUD,VR测试Chart，Xpoint=10,ypoint=10））
	//13: 白屏黑点阵图（9点点阵图（HUD,VR测试Chart）Xpoint=10,ypoint=10）
	//20: 网格图（黑底白线）
	//21: 网格图（黑底白线带边框）
	//22: 网格图（白底黑线）
	//23：网格图（白底黑线带边框）
	//30: 线对图（黑底白线横向线对）
	//31：线对图（黑底白线纵向线对）
	//32横竖线对图交叉图，白背景黑线对白点
	//33 VID每隔5个点做个白点
	//34 VID每隔5个点做一个白点，中间做一个直径为5的圆
	//40：制作同心圆（中心区域圆）
	//41：制作同心圆（中心区域圆加十字）
	//42：制作同心圆环（不同视场）

	//用于VR鬼影测试这块，黑背景白点点阵图，用于鬼影测试
	else if (flag == 14)
	{
		makImg = srcImg;
		double degree[8] = { 0, PI / 4, PI / 2, 3 * PI / 4, PI, 5 * PI / 4, 3 * PI / 2, 7 * PI / 4 };
		float field[5] = { 0,0.3,0.5,0.7,0.9 };
		for (int i = 0; i < ((sizeof(field)) / (sizeof(field[0]))); i++)
		{
			for (int k = 0; k < ((sizeof(degree)) / (sizeof(degree[0]))); k++)
			{
				int xpoint = int(width / 2 * (1 + field[i] * cos(degree[k])));
				int ypoint = int(height / 2 * (1 - field[i] * sin(degree[k])));
				circle(makImg, Point(xpoint, ypoint), R, Scalar(255, 255, 255), -1, 8);
			}
		}
	}
	//
		//用于VR畸变测试这块
	else if (flag == 15)
	{
		makImg = srcImg;
		double degree[8] = { 0, PI / 4, PI / 2, 3 * PI / 4, PI, 5 * PI / 4, 3 * PI / 2, 7 * PI / 4 };
		float field[10] = { 0,0.1,0.2,0.3,0.4,0.5,0.6,0.7,0.8,0.9 };
		for (int i = 0; i < ((sizeof(field)) / (sizeof(field[0]))); i++)
		{
			for (int k = 0; k < ((sizeof(degree)) / (sizeof(degree[0]))); k++)
			{
				int xpoint = int(width / 2 * (1 + field[i] * cos(degree[k])));
				int ypoint = int(height / 2 * (1 - field[i] * sin(degree[k])));
				circle(makImg, Point(xpoint, ypoint), R, Scalar(255, 255, 255), -1, 8);
			}
		}
	}
	//用于VR AA测试这块左右两个
	else if (flag == 16)
	{
		makImg = srcImg;
		double degree[2] = { 0,   PI, };
		float field[1] = { 0.6 };
		for (int i = 0; i < ((sizeof(field)) / (sizeof(field[0]))); i++)
		{
			for (int k = 0; k < ((sizeof(degree)) / (sizeof(degree[0]))); k++)
			{
				int xpoint = int(width / 2 * (1 + field[i] * cos(degree[k])));
				int ypoint = int(height / 2 * (1 - field[i] * sin(degree[k])));
				circle(makImg, Point(xpoint, ypoint), R, Scalar(255, 255, 255), -1, 8);
			}
		}
	}
	//用于VR 视觉定位，左右两个圆形
	else if (flag == 17)
	{
		makImg = ~srcImg;
		double degree[2] = { 0,   PI, };
		float field[1] = { 0.6 };
		for (int i = 0; i < ((sizeof(field)) / (sizeof(field[0]))); i++)
		{
			for (int k = 0; k < ((sizeof(degree)) / (sizeof(degree[0]))); k++)
			{
				int xpoint = int(width / 2 * (1 + field[i] * cos(degree[k])));
				int ypoint = int(height / 2 * (1 - field[i] * sin(degree[k])));
				circle(makImg, Point(xpoint, ypoint), R, Scalar(0, 0, 0), -1, 8);
			}
		}
	}
	//绘制矩形框
	else if (flag == 18)
	{
	makImg = srcImg;

	for (i = step_x; i <= width; i += (4 * step_x))
	{
		for (j = step_y; j < height; j += (4 * step_y))
		{
			int width = 80;
			int height = 40;		
			rectangle(makImg, Point(i - width / 2, j - height / 2), Point(i + width / 2, j + width / 2), Scalar(B1, G1, R1), -1, 8);

		}
	}
	}

	//绘制网格图(黑背景，白网格线)
	else if (flag == 20)
	{
		makImg = srcImg;
		for (i = 1; i <= xPoint; i++)
		{
			for (j = 1; j <= yPoint; j++)
			{
				line(makImg, Point(0, height*i / (xPoint + 1)-R/2), Point(width, height*i / (xPoint + 1)-R/2), Scalar(B1, G1, R1), R);
				line(makImg, Point(width*j / (yPoint + 1)-R/2, 0), Point(width*j / (yPoint + 1)-R/2, height), Scalar(B1, G1, R1), R);
			}
		}
	}
	//绘制网格图(黑背景，白网格线,带边框)
	else if (flag == 21)
	{
		makImg = srcImg;
		for (i = 0; i <= xPoint+1; i++)
		{
			for (j = 0; j <= yPoint+1; j++)
			{
				line(makImg, Point(0, height * i / (xPoint + 1) - R/2), Point(width, height * i / (xPoint + 1) - R / 2), Scalar(0, 255, 0), R);
				line(makImg, Point(width * j / (yPoint + 1) - R/2 , 0), Point(width * j / (yPoint + 1) - R / 2 ,  height), Scalar(0, 255, 0), R);
			}
		}
	}
	/*	line(makImg, Point(0, R-1), Point(width, R-1), Scalar(255, 255, 255), R);*/

	//绘制网格图（白背景，黑网格线）
	else if (flag == 22)
	{
		makImg = ~srcImg;
		for (i = 1; i <= xPoint; i++)
		{
			for (j = 1; j <= yPoint; j++)
			{

				line(makImg, Point(0, height*i / (xPoint + 1)), Point(width, height*i / (xPoint + 1)), Scalar(0, 0, 0), R);
				line(makImg, Point(width*j / (xPoint + 1), 0), Point(width*j / (xPoint + 1), height), Scalar(0, 0, 0), R);
			}
		}
	}

	//绘制网格图（白背景，黑网格线，带边框）
	else if (flag == 23)
	{
		makImg = ~srcImg;
		for (i = 0; i <= xPoint + 1; i++)
		{
			for (j = 0; j <= yPoint + 1; j++)
			{
				line(makImg, Point(0, height*i / (xPoint + 1)), Point(width, height*i / (xPoint + 1)), Scalar(0, 0, 0), R);
				line(makImg, Point(width*j / (xPoint + 1), 0), Point(width*j / (xPoint + 1), height), Scalar(0, 0, 0), R);
			}
		}
	}
	//30: 线对图（黑底白线横向线对）
	else if (flag == 30)
	{
		makImg = srcImg;
		for (i = 0; i < height; i++)
		{
			for (j = 0; j < width; j++)
			{
				if (int(i / R) % 2 == 0)
				{
					makImg.at<Vec3b>(i, j)[0] = B1;
					makImg.at<Vec3b>(i, j)[1] = G1;
					makImg.at<Vec3b>(i, j)[2] = R1;
				}
			}
		}
	}
	//31 线对图（黑底白线纵向线对）
	else if (flag == 31)
	{
		makImg = srcImg;
		for (i = 0; i < height; i++)
		{
			for (j = 0; j < width; j++)
			{
				if (int(j / R) % 2 == 0)
				{
					makImg.at<Vec3b>(i, j)[0] = B1;
					makImg.at<Vec3b>(i, j)[1] = G1;
					makImg.at<Vec3b>(i, j)[2] = R1;
				}
			}
		}
	}
	//32横竖线对图交叉图，白背景黑线对白点
	else if (flag == 32)
	{
		makImg = Mat(height, width, CV_8UC3, Scalar(255, 255, 255));
		for (i = 0; i < height; i++)
		{
			for (j = 0; j < width; j++)
			{
				if (int(i / R) % 2 == 0)
				{
					makImg.at<Vec3b>(i, j)[0] = 0;
					makImg.at<Vec3b>(i, j)[1] = 0;
					makImg.at<Vec3b>(i, j)[2] = 0;
				}
				if (int(j / R) % 2 == 0)
				{
					makImg.at<Vec3b>(i, j)[0] = 0;
					makImg.at<Vec3b>(i, j)[1] = 0;
					makImg.at<Vec3b>(i, j)[2] = 0;
				}
			}
		}
	}
	//33 VID每隔5个点做个白点
	else if (flag == 33)
	{
		makImg = Mat(height, width, CV_8UC3, Scalar(0, 0, 0));
		for (i = 0; i < height; i++)
		{
			for (j = 0; j < width; j++)
			{
				if (int(i / R) % 5 == 0 && int(j / R) % 5 == 0)
				{
					makImg.at<Vec3b>(i, j)[0] = 0;
					makImg.at<Vec3b>(i, j)[1] = 255;
					makImg.at<Vec3b>(i, j)[2] = 0;
				}
			}
		}
	}
	//34 VID每隔5个点做一个白点，中间做一个直径为5的圆
	else if (flag == 34)
	{
		makImg = Mat(height, width, CV_8UC3, Scalar(0, 0, 0));
		for (i = 0; i < height; i++)
		{
			for (j = 0; j < width; j++)
			{
				if (int(i / R) % 6 == 0 && int(j / R) % 6 == 0)
				{
					makImg.at<Vec3b>(i, j)[0] = 255;
					makImg.at<Vec3b>(i, j)[1] = 255;
					makImg.at<Vec3b>(i, j)[2] = 255;
				}
			}
		}
		int xcenter = width / 2;
		int ycenter = height / 2;
		circle(makImg, Point(xcenter, ycenter), 2, Scalar(255, 255, 255), -1, 0);
	}
	//40 中心圆（黑底白圆无圆心）
	else if (flag == 40)
	{
		makImg = srcImg;
		int xcenter = width / 2;
		int ycenter = height / 2;
		circle(makImg, Point(xcenter, ycenter), R, Scalar(255, 255, 255), -1, 0);

	}
	else if (flag == 401)
	{
		makImg = srcImg;
		int xcenter = width / 2;
		int ycenter = height / 2;
		circle(makImg, Point(xcenter, ycenter), R, Scalar(255, 255, 255), -1, 0);
		int linewidth = 1;
		for (i = 0; i < height; i++)
		{
			for (j = 0; j < width; j++)
			{
				if (int(i / linewidth) % 2 == 0)
				{
					makImg.at<Vec3b>(i, j)[0] = 0;
					makImg.at<Vec3b>(i, j)[1] = 0;
					makImg.at<Vec3b>(i, j)[2] = 0;
				}
			}
		}
	}
	else if (flag == 402)
	{
		makImg = srcImg;
		int xcenter = width / 2;
		int ycenter = height / 2;
		circle(makImg, Point(xcenter, ycenter), R, Scalar(255, 255, 255), -1, 0);
		line(makImg, Point(0, ycenter), Point(width, ycenter), Scalar(0, 0, 0), 1, 8, 0);
		line(makImg, Point(0, ycenter - 4), Point(width, ycenter - 4), Scalar(0, 0, 0), 2, 8, 0);
		line(makImg, Point(0, ycenter + 4), Point(width, ycenter + 4), Scalar(0, 0, 0), 3, 8, 0);
		line(makImg, Point(0, ycenter - 6), Point(width, ycenter + 4), Scalar(0, 0, 0), 5, 8, 0);
		line(makImg, Point(0, ycenter + 6), Point(width, ycenter + 4), Scalar(0, 0, 0), 4, 8, 0);
	}
	//41 中心圆（黑底白圆带圆心）
	else if (flag == 41)
	{
		makImg = srcImg;
		int xcenter = width / 2;
		int ycenter = height / 2;
		circle(makImg, Point(xcenter, ycenter), R, Scalar(255, 255, 255), 4, 0);
		circle(makImg, Point(xcenter, ycenter), 2, Scalar(255, 255, 255), 2);
	}
	//42 中心圆（白底黑圆无圆心）
	else if (flag == 42)
	{
		makImg = ~srcImg;
		int xcenter = width / 2;
		int ycenter = height / 2;
		circle(makImg, Point(xcenter, ycenter), R, Scalar(0, 0, 0), 4, 0);
	}
	//43 中心圆（白底黑圆有圆心）
	else if (flag == 43)
	{
		makImg = ~srcImg;
		int xcenter = width / 2;
		int ycenter = height / 2;
		circle(makImg, Point(xcenter, ycenter), R, Scalar(0, 0, 0), 4, 0);
		circle(makImg, Point(xcenter, ycenter), 2, Scalar(0, 0, 0), 2);
	}
	//黑底白圆带十字中心
	else if (flag == 44)
	{
		makImg = srcImg;
		int xcenter = width / 2;
		int ycenter = height / 2;
		circle(makImg, Point(xcenter + 50, ycenter), R, Scalar(255, 255, 255), 2, 0);
		/*circle(makImg, Point(xcenter, ycenter), 2, Scalar(255, 255, 255), 2);*/
		line(makImg, Point(xcenter - (xPoint / 2), ycenter), Point(xcenter + (xPoint / 2), ycenter), Scalar(255, 255, 255), 1);
		line(makImg, Point(xcenter + 50, ycenter - (yPoint / 2)), Point(xcenter + 50, ycenter + (yPoint / 2)), Scalar(255, 255, 255), 1);
	}
	//白底黑圆带十字中心
	else if (flag == 45)
	{
		makImg = ~srcImg;
		int xcenter = width / 2;
		int ycenter = height / 2;
		circle(makImg, Point(xcenter, ycenter), R, Scalar(0, 0, 0), 4, 0);
		circle(makImg, Point(xcenter, ycenter), 2, Scalar(0, 0, 0), 2);
		line(makImg, Point(xcenter - (xPoint / 2), ycenter), Point(xcenter + (xPoint / 2), ycenter), Scalar(0, 0, 0), 1);
		line(makImg, Point(xcenter, ycenter - (yPoint / 2)), Point(xcenter, ycenter + (yPoint / 2)), Scalar(0, 0, 0), 1);
	}
	//黑底白圆同心圆
	else if (flag == 46)
	{
		makImg = srcImg;
		int xcenter = width / 2;
		int ycenter = height / 2;
		circle(makImg, Point(xcenter, ycenter), 2, Scalar(255, 255, 255), 2);
		for (int i = 1; i <= xPoint; i++)
		{
			circle(makImg, Point(xcenter, ycenter), i*R, Scalar(255, 255, 255), 4, 0);
		}
	}
	//黑底白圆同心圆带十字
	else if (flag == 47)
	{
		makImg = srcImg;
		int xcenter = width / 2;
		int ycenter = height / 2;
		circle(makImg, Point(xcenter, ycenter), 2, Scalar(255, 255, 255), 2);
		line(makImg, Point(0, ycenter), Point(width, ycenter), Scalar(255, 255, 255), 2);
		line(makImg, Point(xcenter, 0), Point(xcenter, height), Scalar(255, 255, 255), 2);
		for (int i = 1; i <= xPoint; i++)
		{
			circle(makImg, Point(xcenter, ycenter), i*R, Scalar(255, 255, 255), 4, 0);
		}
	}
	//白底黑圆同心圆
	else if (flag == 48)
	{
		makImg = ~srcImg;
		int xcenter = width / 2;
		int ycenter = height / 2;
		circle(makImg, Point(xcenter, ycenter), 2, Scalar(0, 0, 0), 2);
		for (int i = 1; i <= xPoint; i++)
		{
			circle(makImg, Point(xcenter, ycenter), i*R, Scalar(0, 0, 0), 4, 0);
		}
	}
	//白底黑圆同心圆带十字
	else if (flag == 49)
	{
		makImg = ~srcImg;
		int xcenter = width / 2;
		int ycenter = height / 2;
		circle(makImg, Point(xcenter, ycenter), 2, Scalar(0, 0, 0), 2);
		line(makImg, Point(0, ycenter), Point(width, ycenter), Scalar(0, 0, 0), 2);
		line(makImg, Point(xcenter, 0), Point(xcenter, height), Scalar(0, 0, 0), 2);
		for (int i = 1; i <= xPoint; i++)
		{
			circle(makImg, Point(xcenter, ycenter), i*R, Scalar(0, 0, 0), 4, 0);
		}
	}

	else
	{
		cout << "错误类型为：制作图片flag编号错误" << endl;
		return -1;
	}
	/*namedWindow("makImg", WINDOW_NORMAL);*/
	/*imshow("makImg", makImg);*/
	imwrite(filePath, makImg);
	return 0;
}
