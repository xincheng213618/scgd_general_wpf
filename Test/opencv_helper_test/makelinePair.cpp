//#include "function.h"
///*
//int width:生成图像的宽
//int height:生成图像的高
//int R：生成图像线对的长
//int linethickness：生成线对的宽
//double xfield：生成多少水平视场的线对，比如0.5视场
//double yfield：生成多少垂直视场的线对，比如0.5视场
//const char *filePath：生成图片保存路径
//int lineType:
//	0:特定的中心和四角MTF线对图
//	1：特定位置的四线对
//	2：生成多视场的四线对图
//int flag:生成图的背景是黑背景还是白背景，0黑背景，1白背景。
//*/


//int makelinePair(int width, int height, int R, int linethickness, double xfield, double yfield, const char *filePath, int lineType, int flag)
//{
//	//创建行数为height,列数为width
//	Mat srcImg(height, width, CV_8UC3);
//	Mat makeImg;
//	srcImg.setTo(0);
//	//x_cent水平位置，y_cent垂直位置
//	int x_cent = width / 2;
//	int y_cent = height / 2;
//	int i, j;
//	if (lineType == 0)
//	{
//		for (i = 0; i < R; i++)
//		{
//			for (j = 0; j < R; j++)
//			{
//				//横线对
//				if (int(i / linethickness) % 2 == 0)
//				{
//					//0视场
//					srcImg.at<Vec3b>(y_cent - R + i, x_cent - R + j)[0] = 255;
//					srcImg.at<Vec3b>(y_cent - R + i, x_cent - R + j)[1] = 255;
//					srcImg.at<Vec3b>(y_cent - R + i, x_cent - R + j)[2] = 255;
//					////xfield，yfield
//					//左上   前面是行，后面是列数
//					srcImg.at<Vec3b>(y_cent*(1 - yfield) - R + i, x_cent*(1 - xfield) + j)[0] = 255;
//					srcImg.at<Vec3b>(y_cent*(1 - yfield) - R + i, x_cent*(1 - xfield) + j)[1] = 255;
//					srcImg.at<Vec3b>(y_cent*(1 - yfield) - R + i, x_cent*(1 - xfield) + j)[2] = 255;
//
//					//右上
//					srcImg.at<Vec3b>(y_cent*(1 - yfield) - R + i, x_cent*(xfield + 1) - R + j)[0] = 255;
//					srcImg.at<Vec3b>(y_cent*(1 - yfield) - R + i, x_cent*(xfield + 1) - R + j)[1] = 255;
//					srcImg.at<Vec3b>(y_cent*(1 - yfield) - R + i, x_cent*(xfield + 1) - R + j)[2] = 255;
//					//
//					//左下
//					srcImg.at<Vec3b>(y_cent*(yfield + 1) + i, x_cent*(1 - xfield) + j)[0] = 255;
//					srcImg.at<Vec3b>(y_cent*(yfield + 1) + i, x_cent*(1 - xfield) + j)[1] = 255;
//					srcImg.at<Vec3b>(y_cent*(yfield + 1) + i, x_cent*(1 - xfield) + j)[2] = 255;
//
//					//右下
//					srcImg.at<Vec3b>(y_cent*(yfield + 1) + i, x_cent*(xfield + 1) - R + j)[0] = 255;
//					srcImg.at<Vec3b>(y_cent*(yfield + 1) + i, x_cent*(xfield + 1) - R + j)[1] = 255;
//					srcImg.at<Vec3b>(y_cent*(yfield + 1) + i, x_cent*(xfield + 1) - R + j)[2] = 255;
//
//				}
//				//竖线对
//				if (int(j / linethickness) % 2 == 0)
//				{
//					//0视场
//					srcImg.at<Vec3b>(y_cent + i, x_cent + j)[0] = 255;
//					srcImg.at<Vec3b>(y_cent + i, x_cent + j)[1] = 255;
//					srcImg.at<Vec3b>(y_cent + i, x_cent + j)[2] = 255;
//
//					//xfield，yfield
//					//左上
//					srcImg.at<Vec3b>(y_cent*(1 - yfield) + i, x_cent*(1 - xfield) - R + j)[0] = 255;
//					srcImg.at<Vec3b>(y_cent*(1 - yfield) + i, x_cent*(1 - xfield) - R + j)[1] = 255;
//					srcImg.at<Vec3b>(y_cent*(1 - yfield) + i, x_cent*(1 - xfield) - R + j)[2] = 255;
//					//右上					
//					srcImg.at<Vec3b>(y_cent*(1 - yfield) + i, x_cent*(xfield + 1) + j)[0] = 255;
//					srcImg.at<Vec3b>(y_cent*(1 - yfield) + i, x_cent*(xfield + 1) + j)[1] = 255;
//					srcImg.at<Vec3b>(y_cent*(1 - yfield) + i, x_cent*(xfield + 1) + j)[2] = 255;
//					//左下
//					srcImg.at<Vec3b>(y_cent*(yfield + 1) - R + i, x_cent*(1 - xfield) - R + j)[0] = 255;
//					srcImg.at<Vec3b>(y_cent*(yfield + 1) - R + i, x_cent*(1 - xfield) - R + j)[1] = 255;
//					srcImg.at<Vec3b>(y_cent*(yfield + 1) - R + i, x_cent*(1 - xfield) - R + j)[2] = 255;
//					//右下
//					srcImg.at<Vec3b>(y_cent*(yfield + 1) - R + i, x_cent*(xfield + 1) + j)[0] = 255;
//					srcImg.at<Vec3b>(y_cent*(yfield + 1) - R + i, x_cent*(xfield + 1) + j)[1] = 255;
//					srcImg.at<Vec3b>(y_cent*(yfield + 1) - R + i, x_cent*(xfield + 1) + j)[2] = 255;
//				}
//			}
//		}
//	}
//	else if (lineType == 1)
//	{
//		double backgroundcolor[3] = { 0,0,0 };
//		double linecolor[3] = { 0,255,0 };
//		fourLinePair(srcImg, srcImg, width, height, R, linethickness, backgroundcolor[3], linecolor[3],width / 2 * (1 - xfield), height / 2 * (1 - yfield), 0);
//	}
//	//绘制不同视场线对图
//	else if (lineType == 2)
//	{
//		double backgroundcolor[3] = { 0,0,0 };
//		double linecolor[3] = { 0,255,0 };		
//		/*fourLinePair(srcImg, srcImg, width, height, R, linethickness, backgroundcolor, linecolor[3], width / 2, height / 2, 0);*/
//		double degree[8] = { 0, PI / 4, PI / 2, 3 * PI / 4, PI, 5 * PI / 4, 3 * PI / 2, 7 * PI / 4 };
//		float field[5] = { 0,0.3,0.5,0.7,0.9 };
//		for (int i = 0; i < ((sizeof(field)) / (sizeof(field[0]))); i++)
//		{
//			for (int k = 0; k < ((sizeof(degree)) / (sizeof(degree[0]))); k++)
//			{
//				int xpoint = int(width / 2 * (1 + field[i] * cos(degree[k])));
//				int ypoint = int(height / 2 * (1 - field[i] * sin(degree[k])));
//				fourLinePair(srcImg, srcImg, width, height, R, linethickness, xpoint, ypoint, 0);
//			}
//		}
//	}
//	//黑底白图
//	if (flag == 0)
//	{
//		makeImg = srcImg;
//	}
//	//白底黑图
//	else if (flag == 1)
//	{
//		makeImg = ~srcImg;
//	}
//	/*imshow("makeImg", makeImg);*/
//	imwrite(filePath, makeImg);
//	return 0;
//}
