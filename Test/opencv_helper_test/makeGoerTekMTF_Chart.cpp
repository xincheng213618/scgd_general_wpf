#include "function.h"


//制作不同视场的四线对
//int width:图像宽度
//int height：图像高度
// double fieldx[] 水平视场
// double fieldy[] 垂直视场
//int linelength:线的长度
//int linethickness：线的宽度
// double backgroudcolor[]:背景色
//double linecolor[];线宽的颜色
// const char* filePath:数据保存路径
//int flag：标志位,为0时表示的是视场，为1的时候表示坐标


int makeGoerTekMTF_Chart(int width, int height, double fieldx[],double fieldy[], int linelength, int linethickness, double backgroundcolor[],double linecolor[], const char* filePath, int flag)
{

	//背景颜色
	double B0 = backgroundcolor[0];
	double G0 = backgroundcolor[1];
	double R0 = backgroundcolor[2];


	//制作对应的背景图片
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
	//视场0x
	int x0point_1 = int(cent_x - cent_x * x0);
	int x0point_2 = int(cent_x + cent_x * x0);
	int x0point_3 = int(cent_x - cent_x * x0);
	int x0point_4 = int(cent_x + cent_x * x0);

	//视觉场10
	int y0point_1 = int(cent_y - cent_y * y0);;
	int y0point_2 = int(cent_y - cent_y * y0);
	int y0point_3 = int(cent_y + cent_y * y0);
	int y0point_4 = int(cent_y + cent_y * y0);


	//视场1x
	int x1point_1 = int(cent_x - cent_x * x1);
	int x1point_2 = int(cent_x + cent_x * x1);
	int x1point_3 = int(cent_x - cent_x * x1);
	int x1point_4 = int(cent_x +cent_x * x1);
	//视场1y
	int y1point_1 = int(cent_y- cent_y * y1);
	int y1point_2= int(cent_y - cent_y * y1);
	int y1point_3 = int(cent_y +cent_y * y1);
	int y1point_4 = int(cent_y + cent_y * y1);

	//视场2x
	int x2point_1 = int(cent_x - cent_x * x2);
	int x2point_2 = int(cent_x + cent_x * x2);
	int x2point_3 = int(cent_x - cent_x * x2);
	int x2point_4 = int(cent_x + cent_x * x2);
	//视场2y
	int y2point_1 = int(cent_y - cent_y * y2);
	int y2point_2 = int(cent_y - cent_y * y2);
	int y2point_3 = int(cent_y + cent_y * y2);
	int y2point_4 = int(cent_y + cent_y * y2);

	//视场3x
	int x3point_1 = int(cent_x - cent_x * x3);
	int x3point_2 = int(cent_x + cent_x * x3);
	int x3point_3 = int(cent_x - cent_x * x3);
	int x3point_4 = int(cent_x + cent_x * x3);
	//视场3y
	int y3point_1 = int(cent_y - cent_y * y3);
	int y3point_2 = int(cent_y - cent_y * y3);
	int y3point_3 = int(cent_y + cent_y * y3);
	int y3point_4 = int(cent_y + cent_y * y3);


	//视场0
	fourLinePair(srcImg,  width, height, linelength, linethickness, linecolor, x0point_1, y0point_1, 0);
	fourLinePair(srcImg,  width, height, linelength, linethickness, linecolor, x0point_2, y0point_2, 0);
	fourLinePair(srcImg,  width, height, linelength, linethickness, linecolor, x0point_3, y0point_3, 0);
	fourLinePair(srcImg,  width, height, linelength, linethickness, linecolor, x0point_4, y0point_4, 0);
	//视场1

	fourLinePair(srcImg,  width, height, linelength, linethickness, linecolor,x1point_1, y1point_1, 0);
	fourLinePair(srcImg,  width, height, linelength, linethickness, linecolor, x1point_2, y1point_2, 0);
	fourLinePair(srcImg,  width, height, linelength, linethickness, linecolor,x1point_3, y1point_3, 0);
	fourLinePair(srcImg,  width, height, linelength, linethickness, linecolor,x1point_4, y1point_4, 0);
	//视场2
	fourLinePair(srcImg,  width, height, linelength, linethickness, linecolor,x2point_1, y2point_1, 0);
	fourLinePair(srcImg,  width, height, linelength, linethickness, linecolor,x2point_2, y2point_2, 0);
	fourLinePair(srcImg,  width, height, linelength, linethickness, linecolor,x2point_3, y2point_3, 0);
	fourLinePair(srcImg,  width, height, linelength, linethickness, linecolor,x2point_4, y2point_4, 0);
	//视场3
	fourLinePair(srcImg,  width, height, linelength, linethickness, linecolor, x3point_1, y3point_1, 0);
	fourLinePair(srcImg,  width, height, linelength, linethickness, linecolor, x3point_2, y3point_2, 0);
	fourLinePair(srcImg,  width, height, linelength, linethickness, linecolor, x3point_3, y3point_3, 0);
	fourLinePair(srcImg,  width, height, linelength, linethickness, linecolor, x3point_4, y3point_4, 0);

	imwrite(filePath, srcImg);
	return 0;
}
