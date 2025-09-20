#include "function.h"
//00:中心区域斜方块旋转

//00:黑底白格中心区域斜方块旋转
//01:白底黑格中心区域斜方块旋转
int makechessPicture(int width, int height, double angle, int R, const char *filePath, int flag)
{
	Mat srcImg(height, width, CV_8UC3, Scalar(0, 0, 0, 255));
	Mat makImg;
	Mat dstImage;
	int Xcent = int(width / 2);
	int Ycent = int(height / 2);
	Rect rect;
	rect.x = Xcent - R / 2;
	rect.y = Ycent - R / 2;
	rect.width = R;
	rect.height = R;
	makImg = srcImg;
	rectangle(makImg, rect, Scalar(255, 255, 255), FILLED, LINE_8, 0);
	Point2f center(Xcent, Ycent);
	Rotate(makImg, dstImage, angle, center, 1);
	//namedWindow("makImg", WINDOW_NORMAL);
	//imshow("makImg", makImg);
	if (flag == 00)
	{
		namedWindow("dstImage", WINDOW_NORMAL);
		imshow("dstImage", dstImage);
		imwrite(filePath, dstImage);
	}
	else if (flag == 01)
	{
		namedWindow("dstImage", WINDOW_NORMAL);
		imshow("dstImage", ~dstImage);
		imwrite(filePath, ~dstImage);
	}

	else
	{
		cout << "错误类型为：制作图片flag编号错误" << endl;
		return -1;
	}
	return 0;
}
