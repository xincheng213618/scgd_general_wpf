#include "function.h"

//制作边缘畸变测试图卡
//int width:图像宽度
//int height：图像高度
//int xLenth：方形水平长
//nt yLenth:方形垂直宽 
//double pointcolor[]:方形颜色pointcolor
//double backgroundcolor[]:背景颜色
// const char* filePath:数据保存路径
//int flag：标志位

int makeGhostSquare(int width, int height, int xLenth,int yLenth, double backgroundcolor[], double Drawcolor[], const char* filePath, int flag)
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
	int xCent = width/2;
	int yCent = height/2;	
	/*double xfield = 0.8;
	double yfield = 0.8;
	int xLeftup = width / 2 *(1 - xfield);
	int yLeftup = height / 2 * (1 -yfield);
	int xRightup = width / 2 * (1 + xfield);
	int yRightup = height / 2 * (1 - yfield);
	int xLeftDown = width/ 2 * (1 - xfield);
	int yLeftDown = height / 2 * (1 + yfield);
	int xRightDown = width / 2 * (1 + xfield);
	int yRightDown = height / 2 * (1 + yfield);*/

	rectangle(srcImg, Point(xCent- xLenth/2, yCent- yLenth/2), cv::Point(xCent + xLenth / 2, yCent + yLenth / 2), Scalar(B1, G1, R1), -1); // -1表示填充整个矩形
	//rectangle(srcImg, Point(xLeftup - xLenth / 2, yLeftup - yLenth / 2), cv::Point(xLeftup + xLenth / 2, yLeftup + yLenth / 2), Scalar(B1, G1, R1), -1); // -1表示填充整个矩形
	//rectangle(srcImg, Point(xRightup - xLenth / 2, yRightup - yLenth / 2), cv::Point(xRightup + xLenth / 2, yRightup + yLenth / 2), Scalar(B1, G1, R1), -1); // -1表示填充整个矩形
	//rectangle(srcImg, Point(xLeftDown - xLenth / 2, yLeftDown - yLenth / 2), cv::Point(xLeftDown + xLenth / 2, yLeftDown + yLenth / 2), Scalar(B1, G1, R1), -1); // -1表示填充整个矩形
	//rectangle(srcImg, Point(xRightDown - xLenth / 2, yRightDown - yLenth / 2), cv::Point(xRightDown + xLenth / 2, yRightDown + yLenth / 2), Scalar(B1, G1, R1), -1); // -1表示填充整个矩形

	imwrite(filePath, srcImg);
	return 0;
}
