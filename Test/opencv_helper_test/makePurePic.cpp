#include "function.h"

//int width��ͼ��Ŀ��
//int height��ͼ��ĸ߶�
//int R��ͼ���ɫͨ����ֵ
//int G��ͼ����ɫͨ����ֵ
//int B: ͼ����ɫͨ����ֵ
//const char*filePath�������ͼ�������λ��
//int flag:�����������͵�ͼƬ//000����������ͼƬ
//001����������128ͼƬ
//002����������64ͼƬ
//003����������32ͼƬ
//004����������16ͼƬ
//005�������ҽ�L3ͼƬ
//006����������ͼƬ
//100����������ͼƬ
//200����������ͼƬ
//300����������ͼƬ
//1000�������Զ���ͼƬ
int makePurePic(int width, int height, int R, int G, int B, const char *filePath, int flag)
{
	//Mat srcImg;
	//switch (flag)
	//{
	//	//��������ͼƬ
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
