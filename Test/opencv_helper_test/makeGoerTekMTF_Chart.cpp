#include "function.h"


//������ͬ�ӳ������߶�
//int width:ͼ����
//int height��ͼ��߶�
// double fieldx[] ˮƽ�ӳ�
// double fieldy[] ��ֱ�ӳ�
//int linelength:�ߵĳ���
//int linethickness���ߵĿ��
// double backgroudcolor[]:����ɫ
//double linecolor[];�߿����ɫ
// const char* filePath:���ݱ���·��
//int flag����־λ,Ϊ0ʱ��ʾ�����ӳ���Ϊ1��ʱ���ʾ����


int makeGoerTekMTF_Chart(int width, int height, double fieldx[],double fieldy[], int linelength, int linethickness, double backgroundcolor[],double linecolor[], const char* filePath, int flag)
{

	//������ɫ
	double B0 = backgroundcolor[0];
	double G0 = backgroundcolor[1];
	double R0 = backgroundcolor[2];


	//������Ӧ�ı���ͼƬ
	Mat srcImg(height, width, CV_8UC3, Scalar(B0, G0, R0));

	int cent_x = width / 2;
	int cent_y = height / 2;
	double x0 = fieldx[0];
	double x1 = fieldx[1];
	double x2 = fieldx[2];
	double x3 = fieldx[3];
	//double x4 = fieldx[4];
	double y0 = fieldy[0];
	double y1 = fieldy[1];
	double y2 = fieldy[2];
	double y3 = fieldy[3];
	//double y4 = fieldy[4];
	//�ӳ�0x
	int x0point_1 = int(cent_x - cent_x * x0);
	int x0point_2 = int(cent_x + cent_x * x0);
	int x0point_3 = int(cent_x - cent_x * x0);
	int x0point_4 = int(cent_x + cent_x * x0);

	//�Ӿ���10
	int y0point_1 = int(cent_y - cent_y * y0);;
	int y0point_2 = int(cent_y - cent_y * y0);
	int y0point_3 = int(cent_y + cent_y * y0);
	int y0point_4 = int(cent_y + cent_y * y0);


	//�ӳ�1x
	int x1point_1 = int(cent_x - cent_x * x1);
	int x1point_2 = int(cent_x + cent_x * x1);
	int x1point_3 = int(cent_x - cent_x * x1);
	int x1point_4 = int(cent_x +cent_x * x1);
	//�ӳ�1y
	int y1point_1 = int(cent_y- cent_y * y1);
	int y1point_2= int(cent_y - cent_y * y1);
	int y1point_3 = int(cent_y +cent_y * y1);
	int y1point_4 = int(cent_y + cent_y * y1);

	//�ӳ�2x
	int x2point_1 = int(cent_x - cent_x * x2);
	int x2point_2 = int(cent_x + cent_x * x2);
	int x2point_3 = int(cent_x - cent_x * x2);
	int x2point_4 = int(cent_x + cent_x * x2);
	//�ӳ�2y
	int y2point_1 = int(cent_y - cent_y * y2);
	int y2point_2 = int(cent_y - cent_y * y2);
	int y2point_3 = int(cent_y + cent_y * y2);
	int y2point_4 = int(cent_y + cent_y * y2);

	//�ӳ�3x
	int x3point_1 = int(cent_x - cent_x * x3);
	int x3point_2 = int(cent_x + cent_x * x3);
	int x3point_3 = int(cent_x - cent_x * x3);
	int x3point_4 = int(cent_x + cent_x * x3);
	//�ӳ�3y
	int y3point_1 = int(cent_y - cent_y * y3);
	int y3point_2 = int(cent_y - cent_y * y3);
	int y3point_3 = int(cent_y + cent_y * y3);
	int y3point_4 = int(cent_y + cent_y * y3);


	//�ӳ�0
	fourLinePair(srcImg,  width, height, linelength, linethickness, linecolor, x0point_1, y0point_1, 0);
	fourLinePair(srcImg,  width, height, linelength, linethickness, linecolor, x0point_2, y0point_2, 0);
	fourLinePair(srcImg,  width, height, linelength, linethickness, linecolor, x0point_3, y0point_3, 0);
	fourLinePair(srcImg,  width, height, linelength, linethickness, linecolor, x0point_4, y0point_4, 0);
	//�ӳ�1

	fourLinePair(srcImg,  width, height, linelength, linethickness, linecolor,x1point_1, y1point_1, 0);
	fourLinePair(srcImg,  width, height, linelength, linethickness, linecolor, x1point_2, y1point_2, 0);
	fourLinePair(srcImg,  width, height, linelength, linethickness, linecolor,x1point_3, y1point_3, 0);
	fourLinePair(srcImg,  width, height, linelength, linethickness, linecolor,x1point_4, y1point_4, 0);
	//�ӳ�2
	fourLinePair(srcImg,  width, height, linelength, linethickness, linecolor,x2point_1, y2point_1, 0);
	fourLinePair(srcImg,  width, height, linelength, linethickness, linecolor,x2point_2, y2point_2, 0);
	fourLinePair(srcImg,  width, height, linelength, linethickness, linecolor,x2point_3, y2point_3, 0);
	fourLinePair(srcImg,  width, height, linelength, linethickness, linecolor,x2point_4, y2point_4, 0);
	//�ӳ�3
	fourLinePair(srcImg,  width, height, linelength, linethickness, linecolor, x3point_1, y3point_1, 0);
	fourLinePair(srcImg,  width, height, linelength, linethickness, linecolor, x3point_2, y3point_2, 0);
	fourLinePair(srcImg,  width, height, linelength, linethickness, linecolor, x3point_3, y3point_3, 0);
	fourLinePair(srcImg,  width, height, linelength, linethickness, linecolor, x3point_4, y3point_4, 0);

	imwrite(filePath, srcImg);
	return 0;
}
