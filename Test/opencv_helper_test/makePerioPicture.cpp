#include "function.h"


//int width��ͼ��Ŀ��
//int height��ͼ��ĸ߶�
//int xPoint��ˮƽ��������и��㣬����
//int yPoint����ֱƽ��������и��㣬����
//int R:Բ�İ뾶���ߵĿ��
//double backgroundColor[]������ɫ
// double drawColor[]����ͼɫ
//const char*filePath�������ͼ�������λ��
//int flag:�����������͵�ͼƬ
//00: ���̸񣨺ڰ׺ڰף�
//01�����̸񣨰׺ڰ׺ڣ�
//10������ͼ���ڵװ׵���ͼ��
//11������ͼ���׵׺ڵ���ͼ��
//12: ����ͼ��9�����ͼ��HUD,VR����Chart��Xpoint=10,ypoint=10����
//13: �����ڵ���ͼ��9�����ͼ��HUD,VR����Chart��Xpoint=10,ypoint=10��
//20: ����ͼ���ڵװ��ߣ�
//21: ����ͼ���ڵװ��ߴ��߿�
//22: ����ͼ���׵׺��ߣ�
//23������ͼ���׵׺��ߴ��߿�
//30: �߶�ͼ���ڵװ��ߺ����߶ԣ�
//31���߶�ͼ���ڵװ��������߶ԣ�
//32�����߶�ͼ����ͼ���ױ������߶԰׵�
//33 VIDÿ��5���������׵�
//34 VIDÿ��5������һ���׵㣬�м���һ��ֱ��Ϊ5��Բ
//40������ͬ��Բ����������Բ��
//41������ͬ��Բ����������Բ��ʮ�֣�
//42������ͬ��Բ������ͬ�ӳ���

int makePerioPicture(int width, int height, int xPoint, int yPoint, int R,double backgroundColor[],double drawColor[], const char *filePath, int flag)
{
	//������ɫ
	double B0 = backgroundColor[0];
	double G0 = backgroundColor[1];
	double R0 = backgroundColor[2];


	//��ͼ��ɫ
	double B1 = drawColor[0];
	double G1 = drawColor[1];
	double R1 = drawColor[2];
	Mat srcImg(height, width, CV_8UC3, Scalar(B0, G0,R0));  //����һ����2000����1200�ĻҶ�ͼ
	Mat makImg;
	//ÿһ��Ԫ���,XpointΪˮƽ������ٸ���
	double step_x = static_cast<double>(width) / xPoint;

	//ÿ����Ԫ��ߣ�yPoint��ʾ��ֱ������ٸ���
	double step_y = static_cast<double>(height) / yPoint;
	//Բ�λ滭
	int i, j;

	//�������̸񣨺ڰ׺ڰף�
	if (flag == 00)
	{
		makImg = srcImg;
		for (int i = 0; i < height; i++)
		{						// �����������ص�
			for (int j = 0; j < width; j++)
			{
				if (int(i / step_y) % 2 == 0)
				{             //�����������
					if (int(j / step_x) % 2 != 0)
					{
						makImg.at<Vec3b>(i, j)[0] = B1;
						makImg.at<Vec3b>(i, j)[1] = G1;
						makImg.at<Vec3b>(i, j)[2] = R1;
					}
				}
				if (int(i / step_y) % 2 != 0)
				{            //�����ż����
					if (int(j / step_x) % 2 == 0)
					{
						makImg.at<Vec3b>(i, j)[0] = B1;
						makImg.at<Vec3b>(i, j)[1] = G1;
						makImg.at<Vec3b>(i, j)[2] = B1;
					}
				}
			}
		}
	}

	//�������̸񣨰׺ڰ׺ڣ�
	else if (flag == 01)
	{
		makImg = srcImg;
		for (int i = 0; i < height; i++)
		{						// �����������ص�
			for (int j = 0; j < width; j++)
			{
				if (int(i / step_y) % 2 == 0)
				{             //�����������
					if (int(j / step_x) % 2 == 0)
					{
						makImg.at<Vec3b>(i, j)[0] = B1;
						makImg.at<Vec3b>(i, j)[1] = G1;//
						makImg.at<Vec3b>(i, j)[2] = R1;
					}
				}
				if (int(i / step_y) % 2 != 0)
				{            //�����ż����
					if (int(j / step_x) % 2 != 0)
					{
						makImg.at<Vec3b>(i, j)[0] = B1;
						makImg.at<Vec3b>(i, j)[1] = G1;
						makImg.at<Vec3b>(i, j)[2] = R1;
					}
				}
			}
		}
	}
	//���Ƶ���ͼ(�ڵװ׵�)	
	//xpoint ˮƽ�����ļ��
	//ypoint ��ֱ�����ļ��
	//R��ĳ����뾶
	

	else if (flag == 10)
	{
		makImg = srcImg;
		int xNum = width / xPoint;//ˮƽ�����ĸ���
		int yNum = height / yPoint;//��ֱ�����ĸ���
		for (i = 0; i <= xNum; i++)
		{
			for (j = 0; j <= yNum; j++)
			{
				int point_x = xPoint * (i + 1) - xPoint / 2;//Բ������xֵ			
				int point_y = yPoint * (j + 1) - yPoint / 2;//Բ������yֵ	
				/*circle(makImg, Point(point_x, point_y), R, Scalar(255, 255, 255), -1, 8);*/
				//���ε�
				if (R % 2 == 0)
				{
					rectangle(makImg, Point(point_x, point_y), Point(point_x + R / 2, point_y + R / 2), Scalar(B1, G1, R1), -1, 8, 0);
				}
				else
				{
					rectangle(makImg, Point(point_x - R / 2, point_y - R / 2), Point(point_x + R / 2, point_y + R / 2), Scalar(0, 0, 0), -1, 8, 0);
				}
				
			}
		}
	}
	//���Ƶ���ͼ(�׵׺ڵ�)	
	//xpoint ˮƽ�����ļ��
	//ypoint ��ֱ�����ļ��
	//R��ĳ����뾶
	else if (flag == 11)
	{
		makImg = ~srcImg;
		int xNum = width / xPoint;//ˮƽ�����ĸ���
		int yNum = height / yPoint;//��ֱ�����ĸ���
		for (i = 0; i <= xNum; i++)
		{
			for (j = 0; j <= yNum; j++)
			{
				int point_x = xPoint * (i + 1) - xPoint / 2;//Բ������xֵ			
				int point_y = yPoint * (j + 1) - yPoint / 2;//Բ������yֵ	
				//Բ��
				//circle(makImg, Point(point_x, point_y), R, Scalar(0, 0, 0), -1, 8);
				//���ε�
				if (R % 2 == 0)
				{
					rectangle(makImg, Point(point_x, point_y), Point(point_x + R / 2, point_y + R / 2), Scalar(B1, G1, R1), -1, 8, 0);
				}
				else
				{
					rectangle(makImg, Point(point_x - R / 2, point_y - R / 2), Point(point_x + R / 2, point_y + R / 2), Scalar(0, 0, 0), -1, 8, 0);
				}
			}
		}
	}
	//����ͼ���ױ����ڵ� 9�����ͼ��HUD,VR����Chart����
	else if (flag == 12)
	{
		makImg = ~srcImg;


		for (i = step_x; i <= width; i += (4 * step_x))
		{
			for (j = step_y; j < height; j += (4 * step_y))
			{
				circle(makImg, Point(i, j), R, Scalar(0, 0, 0), -1, 8);
			}
		}
	}
	else if (flag == 121)
	{
	makImg = srcImg;

	for (i = step_x - 1; i <= width; i += (4 * step_x))
	{
		for (j = step_y - 1; j < height; j += (4 * step_y))
		{
			int width = 55;
			int height = 49;
			rectangle(makImg, Point(i - width / 2, j - height / 2), Point(i + width / 2 + 1, j + height / 2 + 1), Scalar(B1, G1, R1), -1, 8);
		}
	}
	}

	//����ͼ���ڱ����׵�9�����ͼ��HUD,VR����Chart����	
	else if (flag == 13)
	{
	
		makImg = srcImg;


		for (i = step_x-1; i <= width; i += (4 * step_x))
		{
			for (j = step_y-1; j < height; j += (4 * step_y))
			{
			
				circle(makImg, Point(i, j), R, Scalar(B1, G1, R1), -1, 8);				
				
			}
		}
	}
	//�����м��׼9�㷽��ͼ1/10λ��
	else if (flag == 131)
	{	
	makImg = srcImg;


	for (i = step_x - 1; i <= width; i += (4 * step_x))
	{
		for (j = step_y - 1; j < height; j += (4 * step_y))
		{
			int width = 75;
			int height = 65;
			
			/*int width = 55;
			int height = 49;*/		
			rectangle(makImg, Point(i - width / 2, j - height / 2), Point(i + width / 2 + 1, j + height / 2 + 1), Scalar(B1, G1, R1), -1, 8);
		}
	}
	}


	//����9�������ͼ�����ȷֲ�9�㷽��ͼ��
	else if (flag == 132)
	{
	makImg = srcImg;

	for (i = step_x /2- 1; i <= width; i += step_x)
	{
		for (j = step_y/2 - 1; j < height; j += step_y)
		{
			int width = 75;
			int height = 65;

			/*int width = 55;
			int height = 49;*/
			rectangle(makImg, Point(i - width / 2, j - height / 2), Point(i + width / 2 + 1, j + height / 2 + 1), Scalar(B1, G1, R1), -1, 8);
		}
	}
	}
	
	//����9�������ͼ�����ȷֲ�9�㷽��ͼ��2/10λ��

	else if (flag == 133)
	{
	makImg = srcImg;


	for (i = 2*step_x - 1; i <= width; i += (3 * step_x))
	{
		for (j = 2*step_y - 1; j < height; j += (3 * step_y))
		{
			int width = 75;
			int height = 65;
			/*
			int width = 55;
			int height = 49;*/
			rectangle(makImg, Point(i - width / 2, j - height / 2), Point(i + width / 2 + 1, j + height / 2 + 1), Scalar(B1, G1, R1), -1, 8);
		}
	}
	}

	//int width��ͼ��Ŀ��
	//int height��ͼ��ĸ߶�
	//int xPoint��ˮƽ��������и��㣬����
	//int yPoint����ֱƽ��������и��㣬����
	//int R:Բ�İ뾶���ߵĿ��
	//double backgroundColor[]������ɫ
	// double drawColor[]����ͼɫ
	//const char*filePath�������ͼ�������λ��
	//int flag:�����������͵�ͼƬ
	//00: ���̸񣨺ڰ׺ڰף�
	//01�����̸񣨰׺ڰ׺ڣ�
	//10������ͼ���ڵװ׵���ͼ��
	//11������ͼ���׵׺ڵ���ͼ��
	//12: ����ͼ��9�����ͼ��HUD,VR����Chart��Xpoint=10,ypoint=10����
	//13: �����ڵ���ͼ��9�����ͼ��HUD,VR����Chart��Xpoint=10,ypoint=10��
	//20: ����ͼ���ڵװ��ߣ�
	//21: ����ͼ���ڵװ��ߴ��߿�
	//22: ����ͼ���׵׺��ߣ�
	//23������ͼ���׵׺��ߴ��߿�
	//30: �߶�ͼ���ڵװ��ߺ����߶ԣ�
	//31���߶�ͼ���ڵװ��������߶ԣ�
	//32�����߶�ͼ����ͼ���ױ������߶԰׵�
	//33 VIDÿ��5���������׵�
	//34 VIDÿ��5������һ���׵㣬�м���һ��ֱ��Ϊ5��Բ
	//40������ͬ��Բ����������Բ��
	//41������ͬ��Բ����������Բ��ʮ�֣�
	//42������ͬ��Բ������ͬ�ӳ���

	//����VR��Ӱ������飬�ڱ����׵����ͼ�����ڹ�Ӱ����
	else if (flag == 14)
	{
		makImg = srcImg;
		double degree[8] = { 0, PI / 4, PI / 2, 3 * PI / 4, PI, 5 * PI / 4, 3 * PI / 2, 7 * PI / 4 };
		float field[5] = { 0,0.3,0.5,0.7,0.9 };
		for (int i = 0; i < ((sizeof(field)) / (sizeof(field[0]))); i++)
		{
			for (int k = 0; k < ((sizeof(degree)) / (sizeof(degree[0]))); k++)
			{
				int xpoint = int(width / 2 * (1 + field[i] * cos(degree[k])));
				int ypoint = int(height / 2 * (1 - field[i] * sin(degree[k])));
				circle(makImg, Point(xpoint, ypoint), R, Scalar(255, 255, 255), -1, 8);
			}
		}
	}
	//
		//����VR����������
	else if (flag == 15)
	{
		makImg = srcImg;
		double degree[8] = { 0, PI / 4, PI / 2, 3 * PI / 4, PI, 5 * PI / 4, 3 * PI / 2, 7 * PI / 4 };
		float field[10] = { 0,0.1,0.2,0.3,0.4,0.5,0.6,0.7,0.8,0.9 };
		for (int i = 0; i < ((sizeof(field)) / (sizeof(field[0]))); i++)
		{
			for (int k = 0; k < ((sizeof(degree)) / (sizeof(degree[0]))); k++)
			{
				int xpoint = int(width / 2 * (1 + field[i] * cos(degree[k])));
				int ypoint = int(height / 2 * (1 - field[i] * sin(degree[k])));
				circle(makImg, Point(xpoint, ypoint), R, Scalar(255, 255, 255), -1, 8);
			}
		}
	}
	//����VR AA���������������
	else if (flag == 16)
	{
		makImg = srcImg;
		double degree[2] = { 0,   PI, };
		float field[1] = { 0.6 };
		for (int i = 0; i < ((sizeof(field)) / (sizeof(field[0]))); i++)
		{
			for (int k = 0; k < ((sizeof(degree)) / (sizeof(degree[0]))); k++)
			{
				int xpoint = int(width / 2 * (1 + field[i] * cos(degree[k])));
				int ypoint = int(height / 2 * (1 - field[i] * sin(degree[k])));
				circle(makImg, Point(xpoint, ypoint), R, Scalar(255, 255, 255), -1, 8);
			}
		}
	}
	//����VR �Ӿ���λ����������Բ��
	else if (flag == 17)
	{
		makImg = ~srcImg;
		double degree[2] = { 0,   PI, };
		float field[1] = { 0.6 };
		for (int i = 0; i < ((sizeof(field)) / (sizeof(field[0]))); i++)
		{
			for (int k = 0; k < ((sizeof(degree)) / (sizeof(degree[0]))); k++)
			{
				int xpoint = int(width / 2 * (1 + field[i] * cos(degree[k])));
				int ypoint = int(height / 2 * (1 - field[i] * sin(degree[k])));
				circle(makImg, Point(xpoint, ypoint), R, Scalar(0, 0, 0), -1, 8);
			}
		}
	}
	//���ƾ��ο�
	else if (flag == 18)
	{
	makImg = srcImg;

	for (i = step_x; i <= width; i += (4 * step_x))
	{
		for (j = step_y; j < height; j += (4 * step_y))
		{
			int width = 80;
			int height = 40;		
			rectangle(makImg, Point(i - width / 2, j - height / 2), Point(i + width / 2, j + width / 2), Scalar(B1, G1, R1), -1, 8);

		}
	}
	}

	//��������ͼ(�ڱ�������������)
	else if (flag == 20)
	{
		makImg = srcImg;
		for (i = 1; i <= xPoint; i++)
		{
			for (j = 1; j <= yPoint; j++)
			{
				line(makImg, Point(0, height*i / (xPoint + 1)-R/2), Point(width, height*i / (xPoint + 1)-R/2), Scalar(B1, G1, R1), R);
				line(makImg, Point(width*j / (yPoint + 1)-R/2, 0), Point(width*j / (yPoint + 1)-R/2, height), Scalar(B1, G1, R1), R);
			}
		}
	}
	//��������ͼ(�ڱ�������������,���߿�)
	else if (flag == 21)
	{
		makImg = srcImg;
		for (i = 0; i <= xPoint+1; i++)
		{
			for (j = 0; j <= yPoint+1; j++)
			{
				line(makImg, Point(0, height * i / (xPoint + 1) - R/2), Point(width, height * i / (xPoint + 1) - R / 2), Scalar(0, 255, 0), R);
				line(makImg, Point(width * j / (yPoint + 1) - R/2 , 0), Point(width * j / (yPoint + 1) - R / 2 ,  height), Scalar(0, 255, 0), R);
			}
		}
	}
	/*	line(makImg, Point(0, R-1), Point(width, R-1), Scalar(255, 255, 255), R);*/

	//��������ͼ���ױ������������ߣ�
	else if (flag == 22)
	{
		makImg = ~srcImg;
		for (i = 1; i <= xPoint; i++)
		{
			for (j = 1; j <= yPoint; j++)
			{

				line(makImg, Point(0, height*i / (xPoint + 1)), Point(width, height*i / (xPoint + 1)), Scalar(0, 0, 0), R);
				line(makImg, Point(width*j / (xPoint + 1), 0), Point(width*j / (xPoint + 1), height), Scalar(0, 0, 0), R);
			}
		}
	}

	//��������ͼ���ױ������������ߣ����߿�
	else if (flag == 23)
	{
		makImg = ~srcImg;
		for (i = 0; i <= xPoint + 1; i++)
		{
			for (j = 0; j <= yPoint + 1; j++)
			{
				line(makImg, Point(0, height*i / (xPoint + 1)), Point(width, height*i / (xPoint + 1)), Scalar(0, 0, 0), R);
				line(makImg, Point(width*j / (xPoint + 1), 0), Point(width*j / (xPoint + 1), height), Scalar(0, 0, 0), R);
			}
		}
	}
	//30: �߶�ͼ���ڵװ��ߺ����߶ԣ�
	else if (flag == 30)
	{
		makImg = srcImg;
		for (i = 0; i < height; i++)
		{
			for (j = 0; j < width; j++)
			{
				if (int(i / R) % 2 == 0)
				{
					makImg.at<Vec3b>(i, j)[0] = B1;
					makImg.at<Vec3b>(i, j)[1] = G1;
					makImg.at<Vec3b>(i, j)[2] = R1;
				}
			}
		}
	}
	//31 �߶�ͼ���ڵװ��������߶ԣ�
	else if (flag == 31)
	{
		makImg = srcImg;
		for (i = 0; i < height; i++)
		{
			for (j = 0; j < width; j++)
			{
				if (int(j / R) % 2 == 0)
				{
					makImg.at<Vec3b>(i, j)[0] = B1;
					makImg.at<Vec3b>(i, j)[1] = G1;
					makImg.at<Vec3b>(i, j)[2] = R1;
				}
			}
		}
	}
	//32�����߶�ͼ����ͼ���ױ������߶԰׵�
	else if (flag == 32)
	{
		makImg = Mat(height, width, CV_8UC3, Scalar(255, 255, 255));
		for (i = 0; i < height; i++)
		{
			for (j = 0; j < width; j++)
			{
				if (int(i / R) % 2 == 0)
				{
					makImg.at<Vec3b>(i, j)[0] = 0;
					makImg.at<Vec3b>(i, j)[1] = 0;
					makImg.at<Vec3b>(i, j)[2] = 0;
				}
				if (int(j / R) % 2 == 0)
				{
					makImg.at<Vec3b>(i, j)[0] = 0;
					makImg.at<Vec3b>(i, j)[1] = 0;
					makImg.at<Vec3b>(i, j)[2] = 0;
				}
			}
		}
	}
	//33 VIDÿ��5���������׵�
	else if (flag == 33)
	{
		makImg = Mat(height, width, CV_8UC3, Scalar(0, 0, 0));
		for (i = 0; i < height; i++)
		{
			for (j = 0; j < width; j++)
			{
				if (int(i / R) % 5 == 0 && int(j / R) % 5 == 0)
				{
					makImg.at<Vec3b>(i, j)[0] = 0;
					makImg.at<Vec3b>(i, j)[1] = 255;
					makImg.at<Vec3b>(i, j)[2] = 0;
				}
			}
		}
	}
	//34 VIDÿ��5������һ���׵㣬�м���һ��ֱ��Ϊ5��Բ
	else if (flag == 34)
	{
		makImg = Mat(height, width, CV_8UC3, Scalar(0, 0, 0));
		for (i = 0; i < height; i++)
		{
			for (j = 0; j < width; j++)
			{
				if (int(i / R) % 6 == 0 && int(j / R) % 6 == 0)
				{
					makImg.at<Vec3b>(i, j)[0] = 255;
					makImg.at<Vec3b>(i, j)[1] = 255;
					makImg.at<Vec3b>(i, j)[2] = 255;
				}
			}
		}
		int xcenter = width / 2;
		int ycenter = height / 2;
		circle(makImg, Point(xcenter, ycenter), 2, Scalar(255, 255, 255), -1, 0);
	}
	//40 ����Բ���ڵװ�Բ��Բ�ģ�
	else if (flag == 40)
	{
		makImg = srcImg;
		int xcenter = width / 2;
		int ycenter = height / 2;
		circle(makImg, Point(xcenter, ycenter), R, Scalar(255, 255, 255), -1, 0);

	}
	else if (flag == 401)
	{
		makImg = srcImg;
		int xcenter = width / 2;
		int ycenter = height / 2;
		circle(makImg, Point(xcenter, ycenter), R, Scalar(255, 255, 255), -1, 0);
		int linewidth = 1;
		for (i = 0; i < height; i++)
		{
			for (j = 0; j < width; j++)
			{
				if (int(i / linewidth) % 2 == 0)
				{
					makImg.at<Vec3b>(i, j)[0] = 0;
					makImg.at<Vec3b>(i, j)[1] = 0;
					makImg.at<Vec3b>(i, j)[2] = 0;
				}
			}
		}
	}
	else if (flag == 402)
	{
		makImg = srcImg;
		int xcenter = width / 2;
		int ycenter = height / 2;
		circle(makImg, Point(xcenter, ycenter), R, Scalar(255, 255, 255), -1, 0);
		line(makImg, Point(0, ycenter), Point(width, ycenter), Scalar(0, 0, 0), 1, 8, 0);
		line(makImg, Point(0, ycenter - 4), Point(width, ycenter - 4), Scalar(0, 0, 0), 2, 8, 0);
		line(makImg, Point(0, ycenter + 4), Point(width, ycenter + 4), Scalar(0, 0, 0), 3, 8, 0);
		line(makImg, Point(0, ycenter - 6), Point(width, ycenter + 4), Scalar(0, 0, 0), 5, 8, 0);
		line(makImg, Point(0, ycenter + 6), Point(width, ycenter + 4), Scalar(0, 0, 0), 4, 8, 0);
	}
	//41 ����Բ���ڵװ�Բ��Բ�ģ�
	else if (flag == 41)
	{
		makImg = srcImg;
		int xcenter = width / 2;
		int ycenter = height / 2;
		circle(makImg, Point(xcenter, ycenter), R, Scalar(255, 255, 255), 4, 0);
		circle(makImg, Point(xcenter, ycenter), 2, Scalar(255, 255, 255), 2);
	}
	//42 ����Բ���׵׺�Բ��Բ�ģ�
	else if (flag == 42)
	{
		makImg = ~srcImg;
		int xcenter = width / 2;
		int ycenter = height / 2;
		circle(makImg, Point(xcenter, ycenter), R, Scalar(0, 0, 0), 4, 0);
	}
	//43 ����Բ���׵׺�Բ��Բ�ģ�
	else if (flag == 43)
	{
		makImg = ~srcImg;
		int xcenter = width / 2;
		int ycenter = height / 2;
		circle(makImg, Point(xcenter, ycenter), R, Scalar(0, 0, 0), 4, 0);
		circle(makImg, Point(xcenter, ycenter), 2, Scalar(0, 0, 0), 2);
	}
	//�ڵװ�Բ��ʮ������
	else if (flag == 44)
	{
		makImg = srcImg;
		int xcenter = width / 2;
		int ycenter = height / 2;
		circle(makImg, Point(xcenter + 50, ycenter), R, Scalar(255, 255, 255), 2, 0);
		/*circle(makImg, Point(xcenter, ycenter), 2, Scalar(255, 255, 255), 2);*/
		line(makImg, Point(xcenter - (xPoint / 2), ycenter), Point(xcenter + (xPoint / 2), ycenter), Scalar(255, 255, 255), 1);
		line(makImg, Point(xcenter + 50, ycenter - (yPoint / 2)), Point(xcenter + 50, ycenter + (yPoint / 2)), Scalar(255, 255, 255), 1);
	}
	//�׵׺�Բ��ʮ������
	else if (flag == 45)
	{
		makImg = ~srcImg;
		int xcenter = width / 2;
		int ycenter = height / 2;
		circle(makImg, Point(xcenter, ycenter), R, Scalar(0, 0, 0), 4, 0);
		circle(makImg, Point(xcenter, ycenter), 2, Scalar(0, 0, 0), 2);
		line(makImg, Point(xcenter - (xPoint / 2), ycenter), Point(xcenter + (xPoint / 2), ycenter), Scalar(0, 0, 0), 1);
		line(makImg, Point(xcenter, ycenter - (yPoint / 2)), Point(xcenter, ycenter + (yPoint / 2)), Scalar(0, 0, 0), 1);
	}
	//�ڵװ�Բͬ��Բ
	else if (flag == 46)
	{
		makImg = srcImg;
		int xcenter = width / 2;
		int ycenter = height / 2;
		circle(makImg, Point(xcenter, ycenter), 2, Scalar(255, 255, 255), 2);
		for (int i = 1; i <= xPoint; i++)
		{
			circle(makImg, Point(xcenter, ycenter), i*R, Scalar(255, 255, 255), 4, 0);
		}
	}
	//�ڵװ�Բͬ��Բ��ʮ��
	else if (flag == 47)
	{
		makImg = srcImg;
		int xcenter = width / 2;
		int ycenter = height / 2;
		circle(makImg, Point(xcenter, ycenter), 2, Scalar(255, 255, 255), 2);
		line(makImg, Point(0, ycenter), Point(width, ycenter), Scalar(255, 255, 255), 2);
		line(makImg, Point(xcenter, 0), Point(xcenter, height), Scalar(255, 255, 255), 2);
		for (int i = 1; i <= xPoint; i++)
		{
			circle(makImg, Point(xcenter, ycenter), i*R, Scalar(255, 255, 255), 4, 0);
		}
	}
	//�׵׺�Բͬ��Բ
	else if (flag == 48)
	{
		makImg = ~srcImg;
		int xcenter = width / 2;
		int ycenter = height / 2;
		circle(makImg, Point(xcenter, ycenter), 2, Scalar(0, 0, 0), 2);
		for (int i = 1; i <= xPoint; i++)
		{
			circle(makImg, Point(xcenter, ycenter), i*R, Scalar(0, 0, 0), 4, 0);
		}
	}
	//�׵׺�Բͬ��Բ��ʮ��
	else if (flag == 49)
	{
		makImg = ~srcImg;
		int xcenter = width / 2;
		int ycenter = height / 2;
		circle(makImg, Point(xcenter, ycenter), 2, Scalar(0, 0, 0), 2);
		line(makImg, Point(0, ycenter), Point(width, ycenter), Scalar(0, 0, 0), 2);
		line(makImg, Point(xcenter, 0), Point(xcenter, height), Scalar(0, 0, 0), 2);
		for (int i = 1; i <= xPoint; i++)
		{
			circle(makImg, Point(xcenter, ycenter), i*R, Scalar(0, 0, 0), 4, 0);
		}
	}

	else
	{
		cout << "��������Ϊ������ͼƬflag��Ŵ���" << endl;
		return -1;
	}
	/*namedWindow("makImg", WINDOW_NORMAL);*/
	/*imshow("makImg", makImg);*/
	imwrite(filePath, makImg);
	return 0;
}
