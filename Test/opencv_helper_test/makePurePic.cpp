#include "function.h"

//int width：图像的宽度
//int height：图像的高度
//int R：图像红色通道的值
//int G：图像绿色通道的值
//int B: 图像蓝色通道的值
//const char*filePath：保存的图像的名称位置
//int flag:保存哪种类型的图片//000：制作纯白图片
//001：制作纯灰128图片
//002：制作纯灰64图片
//003：制作纯灰32图片
//004：制作纯灰16图片
//005：制作灰阶L3图片
//006：制作纯黑图片
//100：制作纯红图片
//200：制作纯绿图片
//300：制作纯蓝图片
//1000：制作自定义图片
int makePurePic(int width, int height, int R, int G, int B, const char *filePath, int flag)
{
	//Mat srcImg;
	//switch (flag)
	//{
	//	//制作纯白图片
	//case 000:
	//	Mat srcImg(height, width, CV_8UC3,Scalar(255,255,255));	
	//	break;
	//case 001:
	//	Mat srcImg(height, width, CV_8UC3, Scalar(128, 128, 128));
	//	break;
	//case 002:
	//	Mat srcImg(height, width, CV_8UC3, Scalar(64, 64, 64));
	//	break;
	//case 003:
	//	Mat srcImg(height, width, CV_8UC3, Scalar(32, 32, 32));
	//	break;
	//case 004:
	//	Mat srcImg(height, width, CV_8UC3, Scalar(16, 16, 16));
	//	break;
	//case 005:
	//	Mat srcImg(height, width, CV_8UC3, Scalar(3, 3, 3));
	//	break;
	//case 006:
	//	Mat srcImg(height, width, CV_8UC3, Scalar(0, 0, 0));
	//	break;
	//case 100:
	//	Mat srcImg(height, width, CV_8UC3, Scalar(0, 0, 255));
	//	break;
	//case 200:
	//	Mat srcImg(height, width, CV_8UC3, Scalar(0, 255,0));
	//	break;
	//case 300:
	//	Mat srcImg(height, width, CV_8UC3, Scalar(255, 0, 0));
	//	break;
	//case 1000:
	Mat srcImg(height, width, CV_8UC3, Scalar(B, G, R));
	//	break;
	//default:
	//	return -1;
	//	break;
	//}
	/*imshow("srcImg", srcImg);
	namedWindow("srcImg", WINDOW_NORMAL);*/
	imwrite(filePath, srcImg);
	return 0;
}
