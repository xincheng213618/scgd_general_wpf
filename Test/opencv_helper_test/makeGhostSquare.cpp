#include "function.h"

//������Ե�������ͼ��
//int width:ͼ����
//int height��ͼ��߶�
//int xLenth������ˮƽ��
//nt yLenth:���δ�ֱ�� 
//double pointcolor[]:������ɫpointcolor
//double backgroundcolor[]:������ɫ
// const char* filePath:���ݱ���·��
//int flag����־λ

int makeGhostSquare(int width, int height, int xLenth,int yLenth, double backgroundcolor[], double Drawcolor[], const char* filePath, int flag)
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
	int xCent = width/2;
	int yCent = height/2;	
	/*double xfield = 0.8;
	double yfield = 0.8;
	int xLeftup = width / 2 *(1 - xfield);
	int yLeftup = height / 2 * (1 -yfield);
	int xRightup = width / 2 * (1 + xfield);
	int yRightup = height / 2 * (1 - yfield);
	int xLeftDown = width/ 2 * (1 - xfield);
	int yLeftDown = height / 2 * (1 + yfield);
	int xRightDown = width / 2 * (1 + xfield);
	int yRightDown = height / 2 * (1 + yfield);*/

	rectangle(srcImg, Point(xCent- xLenth/2, yCent- yLenth/2), cv::Point(xCent + xLenth / 2, yCent + yLenth / 2), Scalar(B1, G1, R1), -1); // -1��ʾ�����������
	//rectangle(srcImg, Point(xLeftup - xLenth / 2, yLeftup - yLenth / 2), cv::Point(xLeftup + xLenth / 2, yLeftup + yLenth / 2), Scalar(B1, G1, R1), -1); // -1��ʾ�����������
	//rectangle(srcImg, Point(xRightup - xLenth / 2, yRightup - yLenth / 2), cv::Point(xRightup + xLenth / 2, yRightup + yLenth / 2), Scalar(B1, G1, R1), -1); // -1��ʾ�����������
	//rectangle(srcImg, Point(xLeftDown - xLenth / 2, yLeftDown - yLenth / 2), cv::Point(xLeftDown + xLenth / 2, yLeftDown + yLenth / 2), Scalar(B1, G1, R1), -1); // -1��ʾ�����������
	//rectangle(srcImg, Point(xRightDown - xLenth / 2, yRightDown - yLenth / 2), cv::Point(xRightDown + xLenth / 2, yRightDown + yLenth / 2), Scalar(B1, G1, R1), -1); // -1��ʾ�����������

	imwrite(filePath, srcImg);
	return 0;
}
