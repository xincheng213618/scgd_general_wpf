#include <opencv2/opencv.hpp>
#include <iostream>
#include <stdlib.h>
#include <stdio.h>
#include <opencv2\opencv.hpp > 
#include <opencv2/core/core.hpp>
#include "opencv2/photo/photo.hpp"
#include <opencv2/highgui/highgui.hpp>
#include <opencv2/highgui/highgui_c.h> 
#include <opencv2/imgproc/imgproc.hpp>
#include "opencv2/imgcodecs.hpp"
#include <opencv2/features2d/features2d.hpp>
#include <opencv2/calib3d/calib3d.hpp>
#include <opencv2/stitching.hpp>
#include <vector>  
#include <math.h>
#include <fstream>
#include <string>
#include <io.h>
#include <direct.h>
#include <iostream>
#include <filesystem>
#include <opencv2/opencv.hpp>
#include <chrono>
#include <windows.h>
#include <sys/stat.h> 




#define PI 3.14159265

using namespace std;
using namespace cv;




//int width��ͼ��Ŀ��
//int height��ͼ��ĸ߶�
//int R��ͼ���ɫͨ����ֵ
//int G��ͼ����ɫͨ����ֵ
//int B: ͼ����ɫͨ����ֵ
//const char*filePath�������ͼ�������λ��
//int flag:�����������͵�ͼƬ//000����������ͼƬ
//001����������128ͼƬ
//002����������64ͼƬ
//003����������32ͼƬ
//004����������16ͼƬ
//005�������ҽ�L3ͼƬ
//006����������ͼƬ
//007����������160ͼƬ
//008����������192ͼƬ
//009����������224ͼƬ
//100����������ͼƬ
//200����������ͼƬ
//300����������ͼƬ
//1000�������Զ���ͼƬ
int makePurePic(int width, int height, int R, int G, int B, const char *filePath, int flag);

//int width��ͼ��Ŀ��
//int height��ͼ��ĸ߶�
//int R��ͼ���ɫͨ����ֵ
//int G��ͼ����ɫͨ����ֵ
//int B: ͼ����ɫͨ����ֵ
//const char*filePath�������ͼ�������λ��
//int flag:�����������͵�ͼƬ
//int direction�����䷽��
//000�������ڰ׽���ͼ�����ҽ��䣩
//001���������½���ͼ�����½��䣩
//002���������ϵ����½���ͼ��45�ȣ�
//004:�������µ����Ͻ���ͼ��45�ȣ�
//100:������ɫ����ͼ
//200��������ɫ����ͼ
//300��������ɫ����ͼ
//400: ��������ͼ
//500������б��ͼ
//1000�������Զ���ͼƬ
int makeGradiantPic(int width, int height, int colorNum, const char *filePath, int flag);

//width���������
//width�������߶�
//double x��ˮƽ�ӳ�ռ��0�ӳ���0.3�ӳ���0.5�ӳ���0.707�ӳ���0.85�ӳ�
//double y����ֱ�ӳ�ռ��0�ӳ���0.3�ӳ���0.5�ӳ���0.707�ӳ���0.85�ӳ�
//int R:б����ߴ�
//double angle ��ת�Ƕ�
//const char* picFile����ͼƬ����·��
//int SFRchartflag:SFRchartflag=0��ʾ���̸�SFRchartflag=1��ʾ�����
//int flag  ͼƬ����  0����Ϊ��ɫ��1����Ϊ��ɫ
int makeSFRpic(const char* picFile, int width, int height, double x, double y, int R, double angle, int SFRchartflag, int flag);

//int width��ͼ��Ŀ��
//int height��ͼ��ĸ߶�
//int xPoint��ˮƽ��������и��㣬����
//int yPoint����ֱƽ��������и��㣬����
//int R:Բ�İ뾶���ߵĿ��
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
//40������ͬ��Բ����������Բ��
//41������ͬ��Բ����������Բ��ʮ�֣�
//42������ͬ��Բ������ͬ�ӳ���

int makePerioPicture(int width, int height, int xPoint, int yPoint, int R, const char *filePath, int flag);




int logoDetect(const char*input_path, int width, int height);
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
int makePerioPicture(int width, int height, int xPoint, int yPoint, int R, double backgroundColor[], double drawColor[], const char* filePath, int flag);


//00:��������б������ת
void Rotate(const cv::Mat &srcImage, cv::Mat &dstImage, double angle, cv::Point2f center, double scale);

int makechessPicture_SFR(int width, int height, double angle, int R, const char *filePath, int flag);
//������ת����
void DrawRotatedRect(cv::Mat mask, const cv::RotatedRect &rotatedrect, const cv::Scalar &color, int thickness, int lineType);
//�����߶�ͼ
/*
int width:����ͼ��Ŀ�
int height:����ͼ��ĸ�
int R������ͼ���߶Եĳ�
int linethickness�������߶ԵĿ�
double xfield�����ɶ���ˮƽ�ӳ����߶ԣ�����0.5�ӳ�
double yfield�����ɶ��ٴ�ֱ�ӳ����߶ԣ�����0.5�ӳ�
const char *filePath������ͼƬ����·��
int flag:����ͼ�ı����Ǻڱ������ǰױ�����0�ڱ�����1�ױ�����
*/

int makelinePair(int width, int height, int R, int linethickness, double xfield, double yfield, const char *filePath, int lineType, int flag);
//Mat srcImage: �������ͼ��
//int width:ͼ����
//int height��ͼ��߶�
//int line_Lenth:�ߵĳ���
//int line_Thickness���ߵĿ��
//double xpoint:�߶����ĵ�ˮƽ�ӳ�ͼ������
//double ypoint:�߶����ĵĴ�ֱ�ӳ�ͼ������
// double lineColor:�ߵ���ɫ
//int flag����־λ
//�����ض�λ�õ����߶�

int fourLinePair(Mat srcImage, int width, int height, int line_Lenth, int line_Thickness, double lineColor[],int xpoint, int ypoint, int flag);

//������������ͼ
int makeXimenziLine(const char* picFile, int width, int height, double x, double y, int R, double angle, int flag);
#pragma once

//int width:ͼ��Ŀ��
//int height��ͼ��ĸ߶�
//int xNum��ͼ��ˮƽ�����ж��ٸ�
//int yNum��ͼ��ֱ������ٸ�����
//int pixelSize:ÿ�����̸�ĳ���ߴ磨���أ�
//char *fileName��ͼ����ļ�����·��
//int imageFlag[]��ͼ��ı�־λ��
//	flag[0,0]�ڰ׺ڰף������Ͻǿ�ʼ���ڰ�ͼ1λ
//	flag[1,0]�׺ڰٺڣ������Ͻǿ�ʼ��
//	flag[0,1]�ڰ׺ڰף���ɫͼ3λ
//int errNum��ͼ��Ĵ������
//	errNum=1��ͼ�����

//
int makeVrTestPicture(string fold, int width, int height, int pictureFLag);
//int width:ͼ��Ŀ��
//int height��ͼ��ĸ߶�
//double smallRadius:flagΪ0��ʾСԲ�뾶��flagΪ1��ʾ�ӳ�
//double bigRadius��flagΪ0��ʾ��Բ�뾶��flagΪ1��ʾ�ӳ�
// double backgroudcolor[]:����ɫ
//double double drawColor[];��ͼ����ɫ
//const char* filePath ��ͼƬ����λ��·��
//int flag����ʶ��λ

int makeGhostCicle(int width, int height, double smallRadius, double bigRadius, double backgroundColor[], double drawColor[], const char* filePath, int flag);

//������Ե�������ͼ��
//int width:ͼ����
//int height��ͼ��߶�
//int xLenth������ˮƽ��
//nt yLenth:���δ�ֱ�� 
//double Drawcolor[]:������ɫpointcolor
//double backgroundcolor[]:������ɫ
// const char* filePath:���ݱ���·��
//int flag����־λ

int makeGhostSquare(int width, int height, int xLenth, int yLenth, double backgroundcolor[], double Drawcolor[], const char* filePath, int flag);

//����AAC��VR����ͼ��
int makeVrTestPic_AAC(string fold, int width, int height, int pictureFLag);

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

int makeGoerTekMTF_Chart(int width, int height, double fieldx[], double fieldy[], int linelength, int linethickness, double backgroundcolor[], double linecolor[], const char* filePath, int flag);


//������Ե�������ͼ��
//int width:ͼ����
//int height��ͼ��߶�
//int R����İ뾶
//double pointcolor[]:�����ɫ
//dpuble backgroundcolor[]:��Ե����㱳����ɫ
// const char* filePath:���ݱ���·��
//int flag����־λ

int make_9point_DistortionChart(int width, int height, int R, double backgroundcolor[], double pointcolor[], const char* filePath, int flag);
//����AR����ͼͼ����
int makeArTestPic(string fold, int width, int height, int pictureFLag);

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

int makeBinocularFusionChart(int width, int height, int xLenth, int yLenth, int linethickness, double backgroundcolor[], double Drawcolor[], const char* filePath, int flag);


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

int CrossLineChart(Mat srcImage, int width, int height, int Xpoint, int Ypoint, int xLength, int yLength, int lineThinkness, double DrawColor[]);
//������ͬ�ӳ���ʮ�ְб�
//int width:ͼ����
//int height��ͼ��߶�
// double fieldx[] ˮƽ����
// double fieldy[] ��ֱ�ӳ�
//int xLength:�ߵ�ˮƽ����
//int yLength���ߵĴ�ֱ����
// int lineThinkness:�ߵĿ��
// double backgroudcolor[]:����ɫ
//double linecolor[];�߿����ɫ
// const char* filePath:���ݱ���·��
//int flag����־λ,Ϊ0ʱ��ʾ�����ӳ���Ϊ1��ʱ���ʾ����

int makeCrossLineChart(int width, int height, double fieldx[], double fieldy[], int xLength, int yLength, int lineThinkness, double backgroundcolor[], double DrawColor[], const char* filePath, int flag);

//����VID����ͼ��
//int width:ͼ����
//int height��ͼ��߶�
//int xStep��ˮƽ���������
//int yStep: ��ֱ��������� 
// int DrawSize:Ϊ�ߵ�ʱ�򣬱�ʾ�߿�,Ϊ���ʱ�򣬱�ʾ�߳�
//double pointcolor[]:������ɫpointcolor
//double backgroundcolor[]:������ɫ
// const char* filePath:���ݱ���·��
//int flag����־λ0Ϊ����1Ϊ���߶ԣ�2Ϊ���߶ԣ�3Ϊ�����߶ԣ�4Ϊ���̸�
int makeVidPointChart(int width, int height, double xField, double yField, int xStep, int yStep, int DrawSize, double backgroundColor[], double drawColor[], const char* filePath, int flag);