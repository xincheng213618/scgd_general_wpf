#include "function.h"

//00:��������б������ת


int makechessPicture_SFR(int width, int height, double angle, int R, const char *filePath, int flag)
{

	Mat srcImg(height, width, CV_8UC4, Scalar(0, 0, 0, 255));
	Mat makImg;
	Mat dstImage;

	//�������̸񣨺ڰ׺ڰף�
	makImg = srcImg;
	for (int i = 0; i < height; i++)
	{						// �����������ص�
		for (int j = 0; j < width; j++)
		{
			if (int(i / R) % 2 == 0)
			{             //�����������
				if (int(j / R) % 2 != 0)
				{
					makImg.at<Vec3b>(i, j)[0] = 255;
					makImg.at<Vec3b>(i, j)[1] = 255;
					makImg.at<Vec3b>(i, j)[2] = 255;
				}
			}
			if (int(i / R) % 2 != 0)
			{            //�����ż����
				if (int(j / R) % 2 == 0)
				{
					makImg.at<Vec3b>(i, j)[0] = 255;
					makImg.at<Vec3b>(i, j)[1] = 255;
					makImg.at<Vec3b>(i, j)[2] = 255;
				}
			}
		}
	}
	/*int Xcent = width;
	int Ycent = height;
	Point2f center(Xcent, Ycent);
	Rotate(makImg, dstImage, angle, center, 1);*/
	namedWindow("makImg", WINDOW_NORMAL);
	imshow("makImg", makImg);
	imwrite(filePath, makImg);



	/*namedWindow("dstImage", WINDOW_NORMAL);
	imshow("dstImage", dstImage);
	imwrite(filePath, dstImage);*/
	//Mat Img_ROI = dstImage(Rect(Xcent/2, Ycent/2, width, height));
	//namedWindow("Img_ROI", WINDOW_NORMAL);
	//imshow("Img_ROI", Img_ROI);
	///*imwrite(filePath, Img_ROI);*/
	return 0;
}
