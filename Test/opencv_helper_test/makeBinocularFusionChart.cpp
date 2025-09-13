#include "function.h"

//������Ե�������ͼ��
//int width:ͼ����
//int height��ͼ��߶�
//int xLenth��ˮƽ��
//int yLenth: ��ֱ�� 
// int linethickness:�߿�
//double pointcolor[]:������ɫpointcolor
//double backgroundcolor[]:������ɫ
// const char* filePath:���ݱ���·��
//int flag����־λ

int makeBinocularFusionChart(int width, int height, int xLenth, int yLenth,int linethickness, double backgroundcolor[], double Drawcolor[], const char* filePath, int flag)
{

	//������ɫ
	double B0 = backgroundcolor[0];
	double G0 = backgroundcolor[1];
	double R0 = backgroundcolor[2];


	//�����ɫ
	double B1 = Drawcolor[0];
	double G1 = Drawcolor[1];
	double R1 = Drawcolor[2];



	//���Ʊ���Ϊ��B1.G1,R1����ͼ��
	Mat srcImg(height, width, CV_8UC3, Scalar(B0, G0, R0));  //����һ����Ϊheight,��Ϊwidth�ģ�3ͨ��ͼ��
	int xcent = width / 2;
	int ycent = height / 2;
	for (int i = 0; i <=2; i++)
	{
		for (int j = 0; j <=2; j++)
		{
			line(srcImg, Point(0,ycent+yLenth*(i-1)), Point(width, ycent + yLenth * (i - 1)), Scalar(B1, G1, R1), linethickness, LINE_4);
			line(srcImg, Point(xcent+xLenth*(j-1) , 0), Point(xcent + xLenth * (j - 1),height), Scalar(B1, G1, R1), linethickness, LINE_4);
		}
	}
	//imshow("srcImg", srcImg);
	//waitKey(0);
	imwrite(filePath, srcImg);	
	return 0;
}








