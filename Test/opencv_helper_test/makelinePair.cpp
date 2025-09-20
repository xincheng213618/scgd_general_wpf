//#include "function.h"
///*
//int width:����ͼ��Ŀ�
//int height:����ͼ��ĸ�
//int R������ͼ���߶Եĳ�
//int linethickness�������߶ԵĿ�
//double xfield�����ɶ���ˮƽ�ӳ����߶ԣ�����0.5�ӳ�
//double yfield�����ɶ��ٴ�ֱ�ӳ����߶ԣ�����0.5�ӳ�
//const char *filePath������ͼƬ����·��
//int lineType:
//	0:�ض������ĺ��Ľ�MTF�߶�ͼ
//	1���ض�λ�õ����߶�
//	2�����ɶ��ӳ������߶�ͼ
//int flag:����ͼ�ı����Ǻڱ������ǰױ�����0�ڱ�����1�ױ�����
//*/


//int makelinePair(int width, int height, int R, int linethickness, double xfield, double yfield, const char *filePath, int lineType, int flag)
//{
//	//��������Ϊheight,����Ϊwidth
//	Mat srcImg(height, width, CV_8UC3);
//	Mat makeImg;
//	srcImg.setTo(0);
//	//x_centˮƽλ�ã�y_cent��ֱλ��
//	int x_cent = width / 2;
//	int y_cent = height / 2;
//	int i, j;
//	if (lineType == 0)
//	{
//		for (i = 0; i < R; i++)
//		{
//			for (j = 0; j < R; j++)
//			{
//				//���߶�
//				if (int(i / linethickness) % 2 == 0)
//				{
//					//0�ӳ�
//					srcImg.at<Vec3b>(y_cent - R + i, x_cent - R + j)[0] = 255;
//					srcImg.at<Vec3b>(y_cent - R + i, x_cent - R + j)[1] = 255;
//					srcImg.at<Vec3b>(y_cent - R + i, x_cent - R + j)[2] = 255;
//					////xfield��yfield
//					//����   ǰ�����У�����������
//					srcImg.at<Vec3b>(y_cent*(1 - yfield) - R + i, x_cent*(1 - xfield) + j)[0] = 255;
//					srcImg.at<Vec3b>(y_cent*(1 - yfield) - R + i, x_cent*(1 - xfield) + j)[1] = 255;
//					srcImg.at<Vec3b>(y_cent*(1 - yfield) - R + i, x_cent*(1 - xfield) + j)[2] = 255;
//
//					//����
//					srcImg.at<Vec3b>(y_cent*(1 - yfield) - R + i, x_cent*(xfield + 1) - R + j)[0] = 255;
//					srcImg.at<Vec3b>(y_cent*(1 - yfield) - R + i, x_cent*(xfield + 1) - R + j)[1] = 255;
//					srcImg.at<Vec3b>(y_cent*(1 - yfield) - R + i, x_cent*(xfield + 1) - R + j)[2] = 255;
//					//
//					//����
//					srcImg.at<Vec3b>(y_cent*(yfield + 1) + i, x_cent*(1 - xfield) + j)[0] = 255;
//					srcImg.at<Vec3b>(y_cent*(yfield + 1) + i, x_cent*(1 - xfield) + j)[1] = 255;
//					srcImg.at<Vec3b>(y_cent*(yfield + 1) + i, x_cent*(1 - xfield) + j)[2] = 255;
//
//					//����
//					srcImg.at<Vec3b>(y_cent*(yfield + 1) + i, x_cent*(xfield + 1) - R + j)[0] = 255;
//					srcImg.at<Vec3b>(y_cent*(yfield + 1) + i, x_cent*(xfield + 1) - R + j)[1] = 255;
//					srcImg.at<Vec3b>(y_cent*(yfield + 1) + i, x_cent*(xfield + 1) - R + j)[2] = 255;
//
//				}
//				//���߶�
//				if (int(j / linethickness) % 2 == 0)
//				{
//					//0�ӳ�
//					srcImg.at<Vec3b>(y_cent + i, x_cent + j)[0] = 255;
//					srcImg.at<Vec3b>(y_cent + i, x_cent + j)[1] = 255;
//					srcImg.at<Vec3b>(y_cent + i, x_cent + j)[2] = 255;
//
//					//xfield��yfield
//					//����
//					srcImg.at<Vec3b>(y_cent*(1 - yfield) + i, x_cent*(1 - xfield) - R + j)[0] = 255;
//					srcImg.at<Vec3b>(y_cent*(1 - yfield) + i, x_cent*(1 - xfield) - R + j)[1] = 255;
//					srcImg.at<Vec3b>(y_cent*(1 - yfield) + i, x_cent*(1 - xfield) - R + j)[2] = 255;
//					//����					
//					srcImg.at<Vec3b>(y_cent*(1 - yfield) + i, x_cent*(xfield + 1) + j)[0] = 255;
//					srcImg.at<Vec3b>(y_cent*(1 - yfield) + i, x_cent*(xfield + 1) + j)[1] = 255;
//					srcImg.at<Vec3b>(y_cent*(1 - yfield) + i, x_cent*(xfield + 1) + j)[2] = 255;
//					//����
//					srcImg.at<Vec3b>(y_cent*(yfield + 1) - R + i, x_cent*(1 - xfield) - R + j)[0] = 255;
//					srcImg.at<Vec3b>(y_cent*(yfield + 1) - R + i, x_cent*(1 - xfield) - R + j)[1] = 255;
//					srcImg.at<Vec3b>(y_cent*(yfield + 1) - R + i, x_cent*(1 - xfield) - R + j)[2] = 255;
//					//����
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
//	//���Ʋ�ͬ�ӳ��߶�ͼ
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
//	//�ڵװ�ͼ
//	if (flag == 0)
//	{
//		makeImg = srcImg;
//	}
//	//�׵׺�ͼ
//	else if (flag == 1)
//	{
//		makeImg = ~srcImg;
//	}
//	/*imshow("makeImg", makeImg);*/
//	imwrite(filePath, makeImg);
//	return 0;
//}
