#include "function.h"

//������Ե�������ͼ��
//int width:ͼ����
//int height��ͼ��߶�
//int R����İ뾶
//double pointcolor[]:�����ɫpointcolor
//double backgroundcolor[]:��Ե����㱳����ɫ
// const char* filePath:���ݱ���·��
//int flag����־λ,Ϊ0����׼�ı�Ե9��λ��,Ϊ1�����׸�����������9��ͼ

int make_9point_DistortionChart(int width, int height, int R, double backgroundcolor[], double pointcolor[],const char* filePath, int flag)
{
	
	//������ɫ
	double B0 = backgroundcolor[0];
	double G0 = backgroundcolor[1];
	double R0 = backgroundcolor[2];


	//�����ɫ
	double B1 = pointcolor[0];
	double G1 = pointcolor[1];
	double R1 = pointcolor[2];
	
	//�׸��������
	int firstX = 90;
	int firstY = 20;

	//���Ʊ���Ϊ��B1.G1,R1����ͼ��
	Mat srcImg(height, width, CV_8UC3, Scalar(B0, G0, R0));  //����һ����Ϊheight,��Ϊwidth�ģ�3ͨ��ͼ��
	int stepX = (width - 2 * R) / 2;
	int stepy= (height - 2 * R) / 2;
	int stepX1 = width / 2 - firstX;
	int stepY1 = height / 2 - firstY;
	if (flag == 0)
	{
		for (int i = 0; i <= 2; i++)
		{
			for (int j = 0; j <= 2; j++)
			{
				int point_xcenter = int(i * stepX + R);
				int point_ycenter = int(j * stepy + R);
				circle(srcImg, Point(point_xcenter, point_ycenter), R, Scalar(B1, G1, R1), -1, 8);
			}
		}
	}
	else if (flag == 1)
	{
		for (int i = 0; i <= 2; i++)
		{
			for (int j = 0; j <= 2; j++)
			{
				int point_xcenter = int(i * stepX1 + firstX);
				int point_ycenter = int(j * stepY1 + firstY);
				circle(srcImg, Point(point_xcenter, point_ycenter), R, Scalar(B1, G1, R1), -1, 8);
			}
		}
	}	
	else
	{
	cout << "��������Ϊ������ͼƬflag��Ŵ���" << endl;
	return -1;
	}
	imwrite(filePath, srcImg);
	return 0;
}
