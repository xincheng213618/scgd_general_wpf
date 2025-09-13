#include "function.h"

//����VID����ͼ��
//int width:ͼ����
//int height��ͼ��߶�
//int xStep��ˮƽ���������,���̸�ˮƽ���
//int yStep: ��ֱ��������󣩣����̸�ֱ��� 
// int DrawSize:Ϊ�ߵ�ʱ�򣬱�ʾ�߿�,Ϊ���ʱ�򣬱�ʾ�߳�
//double pointcolor[]:������ɫpointcolor
//double backgroundcolor[]:������ɫ
// const char* filePath:���ݱ���·��
//int flag����־λ0Ϊ����1Ϊ���߶ԣ�2Ϊ���߶ԣ�3Ϊ�����߶ԣ�4Ϊ���̸�

int makeVidPointChart(int width, int height,double xField, double yField, int xStep, int yStep, int DrawSize, double backgroundColor[], double drawColor[], const char* filePath, int flag)
{
	//������ɫ
	double B0 = backgroundColor[0];
	double G0 = backgroundColor[1];
	double R0 = backgroundColor[2];

	//��ͼ��ɫ
	double B1 = drawColor[0];
	double G1 = drawColor[1];
	double R1 = drawColor[2];
	
	//���ɱ���ͼ
	Mat srcImg(height, width, CV_8UC3, Scalar(B0, G0, R0));  //����һ����2000����1200�ĻҶ�ͼ
	
	//������Ҫ��ͼ���Ĵ�С
	int x_LeftUp_Point = int(width/2 - width/2 * xField);
	int y_LeftUp_Point = int(height/2 - height/2 * yField);
	int x_RightDown_Point = int(width/2 + width/2 * xField);
	int y_RightDown_Point = int(height/2 + height/2 * yField);
	int RoiWidth = x_RightDown_Point - x_LeftUp_Point;
	int RoiHeight = y_RightDown_Point - y_LeftUp_Point;	
	int xNum = RoiWidth / xStep;//ˮƽ�����ĸ���,��ֱ�ߵĸ���
	int yNum = RoiHeight / yStep;//��ֱ�����ĸ�����ˮƽ�ߵĸ���
	if (flag == 0)
	{
		for (int i = 0; i <= xNum; i++)
		{
			for (int j = 0; j <= yNum; j++)
			{
				int point_x = xStep * (i + 1) - xStep / 2 + x_LeftUp_Point;//�����������xֵ			
				int point_y = yStep * (j + 1) - yStep / 2 + y_LeftUp_Point;//�����������yֵ				
				/*circle(makImg, Point(point_x, point_y), R, Scalar(255, 255, 255), -1, 8);*/
				//���ε�
				if (DrawSize % 2 == 0)
				{
					rectangle(srcImg, Point(point_x, point_y), Point(point_x + DrawSize / 2, point_y + DrawSize / 2), Scalar(B1, G1, R1), -1, 8, 0);
				}
				else
				{
					rectangle(srcImg, Point(point_x - DrawSize / 2, point_y - DrawSize / 2), Point(point_x + DrawSize / 2, point_y + DrawSize / 2), Scalar(B1, G1, R1), -1, 8, 0);
				}
			}
		}
	}
	else if (flag==1)
	{
		for (int i = 0; i < RoiHeight; i++)
		{
			for (int j = 0; j < RoiWidth; j++)
			{
				if (int(i /DrawSize) % 2 == 0)
				{
					srcImg.at<Vec3b>(y_LeftUp_Point+i, x_LeftUp_Point+j)[0] = B1;
					srcImg.at<Vec3b>(y_LeftUp_Point + i, x_LeftUp_Point + j)[1] = G1;
					srcImg.at<Vec3b>(y_LeftUp_Point + i, x_LeftUp_Point + j)[2] = R1;
				}
			}
		}
	}
	else if (flag==2)
	{
		for (int i = 0; i < RoiHeight; i++)
		{
			for (int j = 0; j < RoiWidth; j++)
			{
				if (int(j / DrawSize) % 2 == 0)
				{
					srcImg.at<Vec3b>(y_LeftUp_Point + i, x_LeftUp_Point + j)[0] = B1;
					srcImg.at<Vec3b>(y_LeftUp_Point + i, x_LeftUp_Point + j)[1] = G1;
					srcImg.at<Vec3b>(y_LeftUp_Point + i, x_LeftUp_Point + j)[2] = R1;
				}
			}
		}
	}
	else if (flag == 4)
	{
		for (int i = 0; i < RoiHeight; i++)
		{						// �����������ص�
			for (int j = 0; j < RoiWidth; j++)
			{
				if (int(i / yStep) % 2 == 0)
				{             //�����������
					if (int(j / xStep) % 2 != 0)
					{
						srcImg.at<Vec3b>(y_LeftUp_Point + i, x_LeftUp_Point + j)[0] = B1;
						srcImg.at<Vec3b>(y_LeftUp_Point + i, x_LeftUp_Point + j)[1] = G1;
						srcImg.at<Vec3b>(y_LeftUp_Point + i, x_LeftUp_Point + j)[2] = R1;
					}
				}
				if (int(i / yStep) % 2 != 0)
				{            //�����ż����
					if (int(j / xStep) % 2 == 0)
					{
						srcImg.at<Vec3b>(y_LeftUp_Point + i, x_LeftUp_Point + j)[0] = B1;
						srcImg.at<Vec3b>(y_LeftUp_Point + i, x_LeftUp_Point + j)[1] = G1;
						srcImg.at<Vec3b>(y_LeftUp_Point + i, x_LeftUp_Point + j)[2] = B1;
					}
				}
			}
		}
	}
	else
	{
		cout << "��������Ϊ������ͼƬflag��Ŵ���" << endl;
		return -1;
	}
	imwrite(filePath, srcImg);
	waitKey(0);
	return 0;
}
