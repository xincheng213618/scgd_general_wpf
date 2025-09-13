#include "function.h"


//width���������
//width�������߶�
//double x��ˮƽ�ӳ�ռ��0�ӳ���0.3�ӳ���0.5�ӳ���0.707�ӳ���0.85�ӳ�
//double y����ֱ�ӳ�ռ��0�ӳ���0.3�ӳ���0.5�ӳ���0.707�ӳ���0.85�ӳ�
//int R:б����ߴ�
//double angle ��ת�Ƕ�
//const char* picFile����ͼƬ����·��
//int SFRchartflag:SFRchartflag=0��ʾ���̸�SFRchartflag=1��ʾ�����
//int flag  ͼƬ����  0����Ϊ��ɫ��1����Ϊ��ɫ


int makeSFRpic(const char* picFile, int width, int height, double x, double y, int R, double angle,int SFRchartflag, int flag)
{
	Mat srcImg(height, width, CV_8UC3, Scalar(255, 0, 255));
	Mat makeImg;
	//srcImg.setTo(0);
	if (SFRchartflag == 0)
	{
		if (x == 0)
		{
			RotatedRect cent_rect(cv::Point(width / 2, height / 2), Size(R, R), angle);
			DrawRotatedRect(srcImg, cent_rect, cv::Scalar(0, 255, 0), -1, 16);//������ɫ�޸�
		}
		else
		{
			//��������б����
			RotatedRect cent_rect(cv::Point(width / 2, height / 2), Size(R, R), angle);
			DrawRotatedRect(srcImg, cent_rect, cv::Scalar(255, 255, 255), -1, 16);
			//���ƶ�Ӧ�ӳ��Ľ�б����
			RotatedRect LeftUpRect(cv::Point((1 - x) * width / 2, (1 - y) * height / 2), Size(R, R), angle);
			RotatedRect RightDownRect(cv::Point((1 + x) * width / 2, (1 + y) * height / 2), Size(R, R), angle);
			RotatedRect LeftDownRect(cv::Point((1 - x) * width / 2, (1 + y) * height / 2), Size(R, R), angle);
			RotatedRect RightUprect(cv::Point((1 + x) * width / 2, (1 - y) * height / 2), Size(R, R), angle);
			DrawRotatedRect(srcImg, LeftUpRect, cv::Scalar(255, 255, 255), -1, 16);
			DrawRotatedRect(srcImg, RightDownRect, cv::Scalar(255, 255, 255), -1, 16);
			DrawRotatedRect(srcImg, LeftDownRect, cv::Scalar(255, 255, 255), -1, 16);
			DrawRotatedRect(srcImg, RightUprect, cv::Scalar(255, 255, 255), -1, 16);

			//����0.3��Ӧ�ӳ��Ľ�б����
			RotatedRect LeftUpRect_0(cv::Point((1 - 0.3) * width / 2, (1 - 0.3) * height / 2), Size(R, R), angle);
			RotatedRect RightDownRect_0(cv::Point((1 + 0.3) * width / 2, (1 + 0.3) * height / 2), Size(R, R), angle);
			RotatedRect LeftDownRect_0(cv::Point((1 - 0.3) * width / 2, (1 + 0.3) * height / 2), Size(R, R), angle);
			RotatedRect RightUprect_0(cv::Point((1 + 0.3) * width / 2, (1 - 0.3) * height / 2), Size(R, R), angle);
			DrawRotatedRect(srcImg, LeftUpRect_0, cv::Scalar(255, 255, 255), -1, 16);
			DrawRotatedRect(srcImg, RightDownRect_0, cv::Scalar(255, 255, 255), -1, 16);
			DrawRotatedRect(srcImg, LeftDownRect_0, cv::Scalar(255, 255, 255), -1, 16);
			DrawRotatedRect(srcImg, RightUprect_0, cv::Scalar(255, 255, 255), -1, 16);

			//����0.6��Ӧ�ӳ��Ľ�б����
		/*	RotatedRect LeftUpRect_1(cv::Point((1 - 0.6) * width / 2, (1 - 0.6) * height / 2), Size(R, R), angle);
			RotatedRect RightDownRect_1(cv::Point((1 + 0.6) * width / 2, (1 + 0.6) * height / 2), Size(R, R), angle);
			RotatedRect LeftDownRect_1(cv::Point((1 - 0.6) * width / 2, (1 + 0.6) * height / 2), Size(R, R), angle);
			RotatedRect RightUprect_1(cv::Point((1 + 0.6) * width / 2, (1 - 0.6) * height / 2), Size(R, R), angle);
			DrawRotatedRect(srcImg, LeftUpRect_1, cv::Scalar(255, 255, 255), -1, 16);
			DrawRotatedRect(srcImg, RightDownRect_1, cv::Scalar(255, 255, 255), -1, 16);
			DrawRotatedRect(srcImg, LeftDownRect_1, cv::Scalar(255, 255, 255), -1, 16);
			DrawRotatedRect(srcImg, RightUprect_1, cv::Scalar(255, 255, 255), -1, 16);*/

			//������������0.6�ӳ�б����
			RotatedRect LeftRect_2(cv::Point((1 - 0.6) * width / 2, height / 2), Size(R, R), angle);
			RotatedRect RightRect_2(cv::Point((1 + 0.6) * width / 2, height / 2), Size(R, R), angle);
			RotatedRect DownRect_2(cv::Point(width / 2, (1 + 0.6) * height / 2), Size(R, R), angle);
			RotatedRect Uprect_2(cv::Point(width / 2, (1 - 0.6) * height / 2), Size(R, R), angle);
			DrawRotatedRect(srcImg, LeftRect_2, cv::Scalar(255, 255, 255), -1, 16);
			DrawRotatedRect(srcImg, RightRect_2, cv::Scalar(255, 255, 255), -1, 16);
			DrawRotatedRect(srcImg, DownRect_2, cv::Scalar(255, 255, 255), -1, 16);
			DrawRotatedRect(srcImg, Uprect_2, cv::Scalar(255, 255, 255), -1, 16);

			////�������ı���ͼ
			//ellipse(srcImg, Point(width / 2, height / 2), Size(int(R), int(R)), 0, angle, angle + 90, cv::Scalar(255, 255, 255), -1, cv::LINE_AA, 0);
			//ellipse(srcImg, Point(width / 2, height / 2), Size(int(R), int(R)), 0, angle + 180, angle + 270, cv::Scalar(255, 255, 255), -1, cv::LINE_AA, 0);
			//
			////���Ʊ�Ե�ӳ�����ͼ
			////����
			//ellipse(srcImg, Point((1 - x)*width / 2, (1 - y)*height / 2), Size(int(R), int(R)), 0, angle, angle + 90, cv::Scalar(255, 255, 255), -1, cv::LINE_AA, 0);
			//ellipse(srcImg, Point((1 - x)*width / 2, (1 - y)*height / 2), Size(int(R), int(R)), 0, angle + 180, angle + 270, cv::Scalar(255, 255, 255), -1, cv::LINE_AA, 0);
			////����
			//ellipse(srcImg, Point((1 - x)*width / 2, (1 + y)*height / 2), Size(int(R), int(R)), 0, angle, angle + 90, cv::Scalar(255, 255, 255), -1, cv::LINE_AA, 0);
			//ellipse(srcImg, Point((1 - x)*width / 2, (1 + y)*height / 2), Size(int(R), int(R)), 0, angle + 180, angle + 270, cv::Scalar(255, 255, 255), -1, cv::LINE_AA, 0);
			////����
			//ellipse(srcImg, Point((1 + x)*width / 2, (1 - y)* height / 2), Size(int(R), int(R)), 0, angle, angle + 90, cv::Scalar(255, 255, 255), -1, cv::LINE_AA, 0);
			//ellipse(srcImg, Point((1 + x)*width / 2, (1 - y)* height / 2), Size(int(R), int(R)), 0, angle + 180, angle + 270, cv::Scalar(255, 255, 255), -1, cv::LINE_AA, 0);
			////����
			//ellipse(srcImg, Point((1 + x)*width / 2, (1 + y)*height / 2), Size(int(R), int(R)), 0, angle, angle + 90, cv::Scalar(255, 255, 255), -1, cv::LINE_AA, 0);
			//ellipse(srcImg, Point((1 + x)*width / 2, (1 + y)*height / 2), Size(int(R), int(R)), 0, angle + 180, angle + 270, cv::Scalar(255, 255, 255), -1, cv::LINE_AA, 0);
		}
	}
	else if (SFRchartflag == 1)	
	{
		if (x == 0)
		{
			ellipse(srcImg, Point(width / 2, height / 2), Size(int(R), int(R)), 0, angle, angle + 90, cv::Scalar(255, 255, 255), -1, cv::LINE_AA, 0);
			ellipse(srcImg, Point(width / 2, height / 2), Size(int(R), int(R)), 0, angle + 180, angle + 270, cv::Scalar(255, 255, 255), -1, cv::LINE_AA, 0);
		}
		else
		{		

			//�������ı���ͼ
			ellipse(srcImg, Point(width / 2, height / 2), Size(int(R), int(R)), 0, angle, angle + 90, cv::Scalar(255, 255, 255), -1, cv::LINE_AA, 0);
			ellipse(srcImg, Point(width / 2, height / 2), Size(int(R), int(R)), 0, angle + 180, angle + 270, cv::Scalar(255, 255, 255), -1, cv::LINE_AA, 0);
			
			//���Ʊ�Ե��Ӧ�ӳ�����ͼ
			//����
			ellipse(srcImg, Point((1 - x)*width / 2, (1 - y)*height / 2), Size(int(R), int(R)), 0, angle, angle + 90, cv::Scalar(255, 255, 255), -1, cv::LINE_AA, 0);
			ellipse(srcImg, Point((1 - x)*width / 2, (1 - y)*height / 2), Size(int(R), int(R)), 0, angle + 180, angle + 270, cv::Scalar(255, 255, 255), -1, cv::LINE_AA, 0);
			//����
			ellipse(srcImg, Point((1 - x)*width / 2, (1 + y)*height / 2), Size(int(R), int(R)), 0, angle, angle + 90, cv::Scalar(255, 255, 255), -1, cv::LINE_AA, 0);
			ellipse(srcImg, Point((1 - x)*width / 2, (1 + y)*height / 2), Size(int(R), int(R)), 0, angle + 180, angle + 270, cv::Scalar(255, 255, 255), -1, cv::LINE_AA, 0);
			//����
			ellipse(srcImg, Point((1 + x)*width / 2, (1 - y)* height / 2), Size(int(R), int(R)), 0, angle, angle + 90, cv::Scalar(255, 255, 255), -1, cv::LINE_AA, 0);
			ellipse(srcImg, Point((1 + x)*width / 2, (1 - y)* height / 2), Size(int(R), int(R)), 0, angle + 180, angle + 270, cv::Scalar(255, 255, 255), -1, cv::LINE_AA, 0);
			//����
			ellipse(srcImg, Point((1 + x)*width / 2, (1 + y)*height / 2), Size(int(R), int(R)), 0, angle, angle + 90, cv::Scalar(255, 255, 255), -1, cv::LINE_AA, 0);
			ellipse(srcImg, Point((1 + x)*width / 2, (1 + y)*height / 2), Size(int(R), int(R)), 0, angle + 180, angle + 270, cv::Scalar(255, 255, 255), -1, cv::LINE_AA, 0);

			//����0.3�ӳ��ĽǶ�Ӧ�ӳ�����ͼ
			//����
			ellipse(srcImg, Point((1 - 0.3) * width / 2, (1 - 0.3) * height / 2), Size(int(R), int(R)), 0, angle, angle + 90, cv::Scalar(255, 255, 255), -1, cv::LINE_AA, 0);
			ellipse(srcImg, Point((1 - 0.3) * width / 2, (1 - 0.3) * height / 2), Size(int(R), int(R)), 0, angle + 180, angle + 270, cv::Scalar(255, 255, 255), -1, cv::LINE_AA, 0);
			//����
			ellipse(srcImg, Point((1 - 0.3) * width / 2, (1 + 0.3) * height / 2), Size(int(R), int(R)), 0, angle, angle + 90, cv::Scalar(255, 255, 255), -1, cv::LINE_AA, 0);
			ellipse(srcImg, Point((1 - 0.3) * width / 2, (1 + 0.3) * height / 2), Size(int(R), int(R)), 0, angle + 180, angle + 270, cv::Scalar(255, 255, 255), -1, cv::LINE_AA, 0);
			//����
			ellipse(srcImg, Point((1 + 0.3) * width / 2, (1 - 0.3) * height / 2), Size(int(R), int(R)), 0, angle, angle + 90, cv::Scalar(255, 255, 255), -1, cv::LINE_AA, 0);
			ellipse(srcImg, Point((1 + 0.3) * width / 2, (1 - 0.3) * height / 2), Size(int(R), int(R)), 0, angle + 180, angle + 270, cv::Scalar(255, 255, 255), -1, cv::LINE_AA, 0);
			//����
			ellipse(srcImg, Point((1 + 0.3) * width / 2, (1 + 0.3) * height / 2), Size(int(R), int(R)), 0, angle, angle + 90, cv::Scalar(255, 255, 255), -1, cv::LINE_AA, 0);
			ellipse(srcImg, Point((1 + 0.3) * width / 2, (1 + 0.3) * height / 2), Size(int(R), int(R)), 0, angle + 180, angle + 270, cv::Scalar(255, 255, 255), -1, cv::LINE_AA, 0);

			////����0.6�ӳ��Ľ��ӳ�����ͼ
			////����
			//ellipse(srcImg, Point((1 - 0.6)* width / 2, (1 - 0.6)* height / 2), Size(int(R), int(R)), 0, angle, angle + 90, cv::Scalar(255, 255, 255), -1, cv::LINE_AA, 0);
			//ellipse(srcImg, Point((1 - 0.6)* width / 2, (1 - 0.6)* height / 2), Size(int(R), int(R)), 0, angle + 180, angle + 270, cv::Scalar(255, 255, 255), -1, cv::LINE_AA, 0);
			////����
			//ellipse(srcImg, Point((1 - 0.6)* width / 2, (1 + 0.6)* height / 2), Size(int(R), int(R)), 0, angle, angle + 90, cv::Scalar(255, 255, 255), -1, cv::LINE_AA, 0);
			//ellipse(srcImg, Point((1 - 0.6)* width / 2, (1 + 0.6)* height / 2), Size(int(R), int(R)), 0, angle + 180, angle + 270, cv::Scalar(255, 255, 255), -1, cv::LINE_AA, 0);
			////����
			//ellipse(srcImg, Point((1 + 0.6)* width / 2, (1 - 0.6)* height / 2), Size(int(R), int(R)), 0, angle, angle + 90, cv::Scalar(255, 255, 255), -1, cv::LINE_AA, 0);
			//ellipse(srcImg, Point((1 + 0.6)* width / 2, (1 - 0.6)* height / 2), Size(int(R), int(R)), 0, angle + 180, angle + 270, cv::Scalar(255, 255, 255), -1, cv::LINE_AA, 0);
			////����
			//ellipse(srcImg, Point((1 + 0.6)* width / 2, (1 + 0.6)* height / 2), Size(int(R), int(R)), 0, angle, angle + 90, cv::Scalar(255, 255, 255), -1, cv::LINE_AA, 0);
			//ellipse(srcImg, Point((1 + 0.6)* width / 2, (1 + 0.6)* height / 2), Size(int(R), int(R)), 0, angle + 180, angle + 270, cv::Scalar(255, 255, 255), -1, cv::LINE_AA, 0);


			//����0.6�ӳ������ӳ�����ͼ
			//��
			int dialmeter;
			dialmeter = sqrt(pow(width, 2) + pow(height, 2));
			ellipse(srcImg, Point(width/2- 0.6* dialmeter / 2, height / 2), Size(int(R), int(R)), 0, angle, angle + 90, cv::Scalar(255, 255, 255), -1, cv::LINE_AA, 0);
			ellipse(srcImg, Point(width/2- 0.6* dialmeter / 2, height / 2), Size(int(R), int(R)), 0, angle + 180, angle + 270, cv::Scalar(255, 255, 255), -1, cv::LINE_AA, 0);
			//��
			ellipse(srcImg, Point(width / 2, height/2 + 0.6* dialmeter / 2), Size(int(R), int(R)), 0, angle, angle + 90, cv::Scalar(255, 255, 255), -1, cv::LINE_AA, 0);
			ellipse(srcImg, Point(width / 2, height/2 + 0.6* dialmeter / 2), Size(int(R), int(R)), 0, angle + 180, angle + 270, cv::Scalar(255, 255, 255), -1, cv::LINE_AA, 0);
			//��
			ellipse(srcImg, Point(width / 2, height/2 - 0.6* dialmeter / 2), Size(int(R), int(R)), 0, angle, angle + 90, cv::Scalar(255, 255, 255), -1, cv::LINE_AA, 0);
			ellipse(srcImg, Point(width / 2, height/2 - 0.6* dialmeter / 2), Size(int(R), int(R)), 0, angle + 180, angle + 270, cv::Scalar(255, 255, 255), -1, cv::LINE_AA, 0);
			//��
			ellipse(srcImg, Point(width/2 + 0.6* dialmeter / 2, height / 2), Size(int(R), int(R)), 0, angle, angle + 90, cv::Scalar(255, 255, 255), -1, cv::LINE_AA, 0);
			ellipse(srcImg, Point(width/2 + 0.6* dialmeter / 2, height / 2), Size(int(R), int(R)), 0, angle + 180, angle + 270, cv::Scalar(255, 255, 255), -1, cv::LINE_AA, 0);
		}
	}
	if (flag == 0)
	{
		makeImg = srcImg;
	}
	else if (flag == 1)
	{
		makeImg = ~srcImg;
	}
	imshow("makeImg", makeImg);
	imwrite(picFile, makeImg);
	return 0;
}
