#include "function.h"

//������ͬ�ӳ���ʮ�ְб�
//Mat srcImage�����ͼ��
//int width��ͼ���
//int height��ͼ���
//int Xpoint��������X����
//int Ypoint��������Y����
//int xLength��ˮƽ�ߵĳ���
//int yLength����ֱ�ߵĳ���
//int lineThinkness:�ߵĿ��
//double DrawColor[]:ʮ���ߵ���ɫ

int CrossLineChart(Mat srcImage, int width, int height, int Xpoint, int Ypoint,int xLength, int yLength, int lineThinkness, double DrawColor[])
{	
	//���Ƶ��ߵ���ɫ
	double B1 = DrawColor[0];
	double G1 = DrawColor[1];
	double R1 = DrawColor[2];
	line(srcImage, Point(Xpoint- xLength / 2, Ypoint), Point(Xpoint + xLength / 2, Ypoint), Scalar(B1, G1, R1), lineThinkness, 8, 0);
	line(srcImage, Point(Xpoint, Ypoint - yLength / 2), Point(Xpoint, Ypoint + yLength / 2), Scalar(B1, G1, R1), lineThinkness, 8, 0);	
	return 0;
}