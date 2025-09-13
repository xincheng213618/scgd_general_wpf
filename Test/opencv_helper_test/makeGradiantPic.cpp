#include "function.h"

//int width：图像的宽度
//int height：图像的高度
//int R：图像红色通道的值
//int G：图像绿色通道的值
//int B: 图像蓝色通道的值
//const char*filePath：保存的图像的名称位置
//int flag:保存哪种类型的图片
//int direction：渐变方向
//000：制作黑白渐变图（左右渐变）
//001：制作上下渐变图（上下渐变）
//002：最作左上到右下渐变图（45度）
//004:制作左下到右上渐变图（45度）
//100:制作红色渐变图
//200：制作绿色渐变图
//300：制作蓝色渐变图
//400: 制作竖彩图
//500：制作斜彩图
//1000：制作自定义图片
int makeGradiantPic(int width, int height, int colorNum, const char *filePath, int flag)
{
	Mat srcImg(height, width, CV_8UC4, Scalar(0, 0, 0, 255));;
	float singleHeight = float(height) / float(colorNum);
	float singleWidth = float(width) / float(colorNum);
	uchar pixel = 0;
	switch (flag)
	{
	case 000:
		for (int i = 0; i < height; i++)
		{
			for (int j = 0; j < width; j++)
			{
				int level = int(j / singleWidth);
				pixel = (uchar)(255.0* level / (float)(colorNum - 1));
				srcImg.at<Vec<uchar, 4>>(i, j) = Scalar(pixel, pixel, pixel, 255);
			}
		}; break;
	case 001:

		for (int j = 0; j < width; j++)
		{
			for (int i = 0; i < height; i++)
			{
				int level = int(i / singleHeight);
				pixel = (uchar)(255.0* level / (float)(colorNum - 1));
				srcImg.at<Vec<uchar, 4>>(i, j) = Scalar(pixel, pixel, pixel, 255);
			}
		}; break;
	case 100:
		for (int i = 0; i < height; i++)
		{
			for (int j = 0; j < width; j++)
			{
				int level = int(j / singleWidth);
				pixel = (uchar)(255.0* level / (float)(colorNum - 1));
				srcImg.at<Vec<uchar, 4>>(i, j) = Scalar(0, 0, pixel, 255);
			}
		}; break;
	case 101:

		for (int j = 0; j < width; j++)
		{
			for (int i = 0; i < height; i++)
			{
				int level = int(i / singleHeight);
				pixel = (uchar)(255.0* level / (float)(colorNum - 1));
				srcImg.at<Vec<uchar, 4>>(i, j) = Scalar(0, 0, pixel, 255);
			}
		}; break;
	case 200:
		for (int i = 0; i < height; i++)
		{
			for (int j = 0; j < width; j++)
			{
				int level = int(j / singleWidth);
				pixel = (uchar)(255.0* level / (float)(colorNum - 1));
				srcImg.at<Vec<uchar, 4>>(i, j) = Scalar(0, pixel, 0, 255);
			}
		}; break;
	case 201:

		for (int j = 0; j < width; j++)
		{
			for (int i = 0; i < height; i++)
			{
				int level = int(i / singleHeight);
				pixel = (uchar)(255.0* level / (float)(colorNum - 1));
				srcImg.at<Vec<uchar, 4>>(i, j) = Scalar(0, pixel, 0, 255);
			}
		}; break;
	case 300:
		for (int i = 0; i < height; i++)
		{
			for (int j = 0; j < width; j++)
			{
				int level = int(j / singleWidth);
				pixel = (uchar)(255.0* level / (float)(colorNum - 1));
				srcImg.at<Vec<uchar, 4>>(i, j) = Scalar(pixel, 0, 0, 255);
			}
		}; break;
	case 301:

		for (int j = 0; j < width; j++)
		{
			for (int i = 0; i < height; i++)
			{
				int level = int(i / singleHeight);
				pixel = (uchar)(255.0* level / (float)(colorNum - 1));
				srcImg.at<Vec<uchar, 4>>(i, j) = Scalar(pixel, 0, 0, 255);
			}
		}; break;
	case 400:

		for (int j = 0; j < width; j++)
		{
			for (int i = 0; i < height; i++)
			{
				int level = int(i / singleHeight);
				pixel = (uchar)(255.0*j / width);
				switch (level)
				{
					//灰阶渐变
				case 0:	srcImg.at<Vec<uchar, 4>>(i, j) = Scalar(pixel, pixel, pixel, 255); break;
					//黄色渐变
				case 1: srcImg.at<Vec<uchar, 4>>(i, j) = Scalar(0, pixel, pixel, 255); break;
					//青色渐变
				case 2: srcImg.at<Vec<uchar, 4>>(i, j) = Scalar(pixel, pixel, 0, 255); break;
					//绿色渐变
				case 3: srcImg.at<Vec<uchar, 4>>(i, j) = Scalar(0, pixel, 0, 255); break;
					//洋红渐变
				case 4: srcImg.at<Vec<uchar, 4>>(i, j) = Scalar(pixel, 0, pixel, 255); break;
					//红色渐变
				case 5: srcImg.at<Vec<uchar, 4>>(i, j) = Scalar(0, 0, pixel, 255); break;
					//蓝色渐变
				case 6: srcImg.at<Vec<uchar, 4>>(i, j) = Scalar(pixel, 0, 0, 255); break;
					//纯黑
				case 7: srcImg.at<Vec<uchar, 4>>(i, j) = Scalar(0, 0, 0, 255); break;

				default:
					break;
				}
			}
		}; break;
		//500制作斜彩图
	case 500:
		for (int j = 0; j < width; j++)
		{
			for (int i = 0; i < height; i++)
			{
				int level = int(i / singleHeight);
				pixel = (uchar)(255.0*j / width);
				if (i%int(singleHeight) == 0)
				{
					line(srcImg, Point(0, level*singleHeight), Point(width, level*singleHeight), Scalar(0, 0, 0), 1, 8);
				}
				switch (level)
				{
					//白
				case 0:	srcImg.at<Vec<uchar, 4>>(i, j) = Scalar(255, 255, 255, 255); break;
					//黄
				case 1: srcImg.at<Vec<uchar, 4>>(i, j) = Scalar(0, 255, 255, 255); break;
					//洋红
				case 2: srcImg.at<Vec<uchar, 4>>(i, j) = Scalar(255, 0, 255, 255); break;
					//红
				case 3: srcImg.at<Vec<uchar, 4>>(i, j) = Scalar(0, 0, 255, 255); break;
					//青
				case 4: srcImg.at<Vec<uchar, 4>>(i, j) = Scalar(255, 255, 0, 255); break;
					//绿
				case 5: srcImg.at<Vec<uchar, 4>>(i, j) = Scalar(0, 255, 0, 255); break;
					//蓝
				case 6: srcImg.at<Vec<uchar, 4>>(i, j) = Scalar(255, 0, 0, 255); break;
					//白
				case 7: srcImg.at<Vec<uchar, 4>>(i, j) = Scalar(255, 255, 255, 255); break;
					//蓝
				case 8: srcImg.at<Vec<uchar, 4>>(i, j) = Scalar(255, 0, 0, 255); break;
					//绿
				case 9: srcImg.at<Vec<uchar, 4>>(i, j) = Scalar(0, 255, 0, 255); break;
					//青
				case 10: srcImg.at<Vec<uchar, 4>>(i, j) = Scalar(255, 255, 0, 255); break;
					//红
				case 11: srcImg.at<Vec<uchar, 4>>(i, j) = Scalar(0, 0, 255, 255); break;
					//洋红
				case 12: srcImg.at<Vec<uchar, 4>>(i, j) = Scalar(255, 0, 255, 255); break;
					//黄
				case 13: srcImg.at<Vec<uchar, 4>>(i, j) = Scalar(0, 255, 255, 255); break;
					//白
				case 14:	srcImg.at<Vec<uchar, 4>>(i, j) = Scalar(255, 255, 255, 255); break;

				default:
					break;
				}
			}
		}
	default:
		break;
	}
	Mat M = getRotationMatrix2D(Point2f((width - 1)*0.5, (height - 1)*0.5), 45, ((width + height)*sqrt(2) / (2 * height)));
	warpAffine(srcImg, srcImg, M, Size(width, height));

	/*namedWindow("srcImg", WINDOW_NORMAL);
	imshow("srcImg", srcImg);*/
	imwrite(filePath, srcImg);
	return 0;

}
