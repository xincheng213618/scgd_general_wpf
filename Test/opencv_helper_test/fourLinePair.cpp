#include"function.h"

//Mat srcImage: ����ͼ��
//Mat dstImage�����ͼ��
//int width:ͼ����
//int height��ͼ��߶�
//int line_Lenth:�ߵĳ���
//int line_Thickness���ߵĿ��
//double xpoint:�߶����ĵ�ˮƽ�ӳ�ͼ������
//double ypoint:�߶����ĵĴ�ֱ�ӳ�ͼ������
// double lineColor:�ߵ���ɫ
//int flag����־λ
//�����ض�λ�õ����߶�
int fourLinePair(Mat srcImage, int width, int height, int line_Lenth, int line_Thickness, double lineColor[],int xpoint, int ypoint, int flag)
{
	//������ɫ
	double B1 = lineColor[0];
	double G1 = lineColor[1];
	double R1 = lineColor[2];
	//��������Ϊheight,����Ϊwidth
	Mat srcImg = srcImage;
	int x_cent = width / 2;
	int y_cent = height / 2;
	int i, j;
	for (i = 0; i < line_Lenth; i++)
	{
		for (j = 0; j < line_Lenth; j++)
		{
			//���߶�
			if (int(i / line_Thickness) % 2 == 0)
			{
				//�ض�λ���߶�ͼ
				
				//����
				srcImg.at<Vec3b>(ypoint - line_Lenth + i, xpoint - line_Lenth + j)[0] = B1;
				srcImg.at<Vec3b>(ypoint - line_Lenth + i, xpoint - line_Lenth + j)[1] = G1;
				srcImg.at<Vec3b>(ypoint - line_Lenth + i, xpoint - line_Lenth + j)[2] = R1;

				//����
				srcImg.at<Vec3b>(ypoint + i, xpoint + j)[0] = B1;
				srcImg.at<Vec3b>(ypoint + i, xpoint + j)[1] = G1;
				srcImg.at<Vec3b>(ypoint + i, xpoint + j)[2] = R1;
			}
			//���߶�
			if (int(j / line_Thickness) % 2 == 0)
			{
				//����
				srcImg.at<Vec3b>(ypoint - line_Lenth + i, xpoint + j)[0] = B1;
				srcImg.at<Vec3b>(ypoint - line_Lenth + i, xpoint + j)[1] = G1;
				srcImg.at<Vec3b>(ypoint - line_Lenth + i, xpoint + j)[2] = R1;
				//����
				srcImg.at<Vec3b>(ypoint + i, xpoint - line_Lenth + j)[0] = B1;
				srcImg.at<Vec3b>(ypoint + i, xpoint - line_Lenth + j)[1] = G1;
				srcImg.at<Vec3b>(ypoint + i, xpoint - line_Lenth + j)[2] = R1;
			}
		}
	}	
	return 0;
}
