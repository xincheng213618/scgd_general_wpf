#include "function.h"

//int width��ͼ��Ŀ��
//int height��ͼ��ĸ߶�
//int R��ͼ���ɫͨ����ֵ
//int G��ͼ����ɫͨ����ֵ
//int B: ͼ����ɫͨ����ֵ
//const char*filePath�������ͼ�������λ��
//int flag:�����������͵�ͼƬ
//int direction�����䷽��
//000�������ڰ׽���ͼ�����ҽ��䣩
//001���������½���ͼ�����½��䣩
//002���������ϵ����½���ͼ��45�ȣ�
//004:�������µ����Ͻ���ͼ��45�ȣ�
//100:������ɫ����ͼ
//200��������ɫ����ͼ
//300��������ɫ����ͼ
//400: ��������ͼ
//500������б��ͼ
//1000�������Զ���ͼƬ
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
					//�ҽ׽���
				case 0:	srcImg.at<Vec<uchar, 4>>(i, j) = Scalar(pixel, pixel, pixel, 255); break;
					//��ɫ����
				case 1: srcImg.at<Vec<uchar, 4>>(i, j) = Scalar(0, pixel, pixel, 255); break;
					//��ɫ����
				case 2: srcImg.at<Vec<uchar, 4>>(i, j) = Scalar(pixel, pixel, 0, 255); break;
					//��ɫ����
				case 3: srcImg.at<Vec<uchar, 4>>(i, j) = Scalar(0, pixel, 0, 255); break;
					//��콥��
				case 4: srcImg.at<Vec<uchar, 4>>(i, j) = Scalar(pixel, 0, pixel, 255); break;
					//��ɫ����
				case 5: srcImg.at<Vec<uchar, 4>>(i, j) = Scalar(0, 0, pixel, 255); break;
					//��ɫ����
				case 6: srcImg.at<Vec<uchar, 4>>(i, j) = Scalar(pixel, 0, 0, 255); break;
					//����
				case 7: srcImg.at<Vec<uchar, 4>>(i, j) = Scalar(0, 0, 0, 255); break;

				default:
					break;
				}
			}
		}; break;
		//500����б��ͼ
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
					//��
				case 0:	srcImg.at<Vec<uchar, 4>>(i, j) = Scalar(255, 255, 255, 255); break;
					//��
				case 1: srcImg.at<Vec<uchar, 4>>(i, j) = Scalar(0, 255, 255, 255); break;
					//���
				case 2: srcImg.at<Vec<uchar, 4>>(i, j) = Scalar(255, 0, 255, 255); break;
					//��
				case 3: srcImg.at<Vec<uchar, 4>>(i, j) = Scalar(0, 0, 255, 255); break;
					//��
				case 4: srcImg.at<Vec<uchar, 4>>(i, j) = Scalar(255, 255, 0, 255); break;
					//��
				case 5: srcImg.at<Vec<uchar, 4>>(i, j) = Scalar(0, 255, 0, 255); break;
					//��
				case 6: srcImg.at<Vec<uchar, 4>>(i, j) = Scalar(255, 0, 0, 255); break;
					//��
				case 7: srcImg.at<Vec<uchar, 4>>(i, j) = Scalar(255, 255, 255, 255); break;
					//��
				case 8: srcImg.at<Vec<uchar, 4>>(i, j) = Scalar(255, 0, 0, 255); break;
					//��
				case 9: srcImg.at<Vec<uchar, 4>>(i, j) = Scalar(0, 255, 0, 255); break;
					//��
				case 10: srcImg.at<Vec<uchar, 4>>(i, j) = Scalar(255, 255, 0, 255); break;
					//��
				case 11: srcImg.at<Vec<uchar, 4>>(i, j) = Scalar(0, 0, 255, 255); break;
					//���
				case 12: srcImg.at<Vec<uchar, 4>>(i, j) = Scalar(255, 0, 255, 255); break;
					//��
				case 13: srcImg.at<Vec<uchar, 4>>(i, j) = Scalar(0, 255, 255, 255); break;
					//��
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
