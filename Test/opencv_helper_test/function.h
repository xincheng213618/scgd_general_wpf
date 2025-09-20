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




//int width：图像的宽度
//int height：图像的高度
//int R：图像红色通道的值
//int G：图像绿色通道的值
//int B: 图像蓝色通道的值
//const char*filePath：保存的图像的名称位置
//int flag:保存哪种类型的图片//000：制作纯白图片
//001：制作纯灰128图片
//002：制作纯灰64图片
//003：制作纯灰32图片
//004：制作纯灰16图片
//005：制作灰阶L3图片
//006：制作纯黑图片
//007：制作纯灰160图片
//008：制作纯灰192图片
//009：制作纯灰224图片
//100：制作纯红图片
//200：制作纯绿图片
//300：制作纯蓝图片
//1000：制作自定义图片
int makePurePic(int width, int height, int R, int G, int B, const char *filePath, int flag);

//int width：图像的宽度
//int height：图像的高度
//int R：图像红色通道的值
//int G：图像绿色通道的值
//int B: 图像蓝色通道的值
//const char*filePath：保存的图像的名称位置
//int flag:保存哪种类型的图片
//int direction：渐变方向
//000：制作黑白渐变图（左右渐变）
//001：制作上下渐变图（上下渐变）
//002：最作左上到右下渐变图（45度）
//004:制作左下到右上渐变图（45度）
//100:制作红色渐变图
//200：制作绿色渐变图
//300：制作蓝色渐变图
//400: 制作竖彩图
//500：制作斜彩图
//1000：制作自定义图片
int makeGradiantPic(int width, int height, int colorNum, const char *filePath, int flag);

//width：背景宽度
//width：背景高度
//double x：水平视场占比0视场，0.3视场，0.5视场，0.707视场，0.85视场
//double y：垂直视场占比0视场，0.3视场，0.5视场，0.707视场，0.85视场
//int R:斜方块尺寸
//double angle 旋转角度
//const char* picFile制作图片保存路径
//int SFRchartflag:SFRchartflag=0表示棋盘格，SFRchartflag=1表示宝马标
//int flag  图片背景  0背景为黑色，1背景为白色
int makeSFRpic(const char* picFile, int width, int height, double x, double y, int R, double angle, int SFRchartflag, int flag);

//int width：图像的宽度
//int height：图像的高度
//int xPoint：水平方向多少列个点，或线
//int yPoint：垂直平方向多少行个点，或线
//int R:圆的半径或线的宽度
//const char*filePath：保存的图像的名称位置
//int flag:保存哪种类型的图片
//00: 棋盘格（黑白黑白）
//01：棋盘格（白黑白黑）
//10：点阵图（黑底白点阵图）
//11：点阵图（白底黑点阵图）
//12: 点阵图（9点点阵图（HUD,VR测试Chart，Xpoint=10,ypoint=10））
//13: 白屏黑点阵图（9点点阵图（HUD,VR测试Chart）Xpoint=10,ypoint=10）
//20: 网格图（黑底白线）
//21: 网格图（黑底白线带边框）
//22: 网格图（白底黑线）
//23：网格图（白底黑线带边框）
//30: 线对图（黑底白线横向线对）
//31：线对图（黑底白线纵向线对）
//40：制作同心圆（中心区域圆）
//41：制作同心圆（中心区域圆加十字）
//42：制作同心圆环（不同视场）

int makePerioPicture(int width, int height, int xPoint, int yPoint, int R, const char *filePath, int flag);




int logoDetect(const char*input_path, int width, int height);
//int width：图像的宽度
//int height：图像的高度
//int xPoint：水平方向多少列个点，或线
//int yPoint：垂直平方向多少行个点，或线
//int R:圆的半径或线的宽度
//double backgroundColor[]：背景色
// double drawColor[]：绘图色
//const char*filePath：保存的图像的名称位置
//int flag:保存哪种类型的图片
//00: 棋盘格（黑白黑白）
//01：棋盘格（白黑白黑）
//10：点阵图（黑底白点阵图）
//11：点阵图（白底黑点阵图）
//12: 点阵图（9点点阵图（HUD,VR测试Chart，Xpoint=10,ypoint=10））
//13: 白屏黑点阵图（9点点阵图（HUD,VR测试Chart）Xpoint=10,ypoint=10）
//20: 网格图（黑底白线）
//21: 网格图（黑底白线带边框）
//22: 网格图（白底黑线）
//23：网格图（白底黑线带边框）
//30: 线对图（黑底白线横向线对）
//31：线对图（黑底白线纵向线对）
//32横竖线对图交叉图，白背景黑线对白点
//33 VID每隔5个点做个白点
//34 VID每隔5个点做一个白点，中间做一个直径为5的圆
//40：制作同心圆（中心区域圆）
//41：制作同心圆（中心区域圆加十字）
//42：制作同心圆环（不同视场）
int makePerioPicture(int width, int height, int xPoint, int yPoint, int R, double backgroundColor[], double drawColor[], const char* filePath, int flag);


//00:中心区域斜方块旋转
void Rotate(const cv::Mat &srcImage, cv::Mat &dstImage, double angle, cv::Point2f center, double scale);

int makechessPicture_SFR(int width, int height, double angle, int R, const char *filePath, int flag);
//绘制旋转矩形
void DrawRotatedRect(cv::Mat mask, const cv::RotatedRect &rotatedrect, const cv::Scalar &color, int thickness, int lineType);
//绘制线对图
/*
int width:生成图像的宽
int height:生成图像的高
int R：生成图像线对的长
int linethickness：生成线对的宽
double xfield：生成多少水平视场的线对，比如0.5视场
double yfield：生成多少垂直视场的线对，比如0.5视场
const char *filePath：生成图片保存路径
int flag:生成图的背景是黑背景还是白背景，0黑背景，1白背景。
*/

int makelinePair(int width, int height, int R, int linethickness, double xfield, double yfield, const char *filePath, int lineType, int flag);
//Mat srcImage: 输入输出图像
//int width:图像宽度
//int height：图像高度
//int line_Lenth:线的长度
//int line_Thickness：线的宽度
//double xpoint:线对中心的水平视场图像坐标
//double ypoint:线对中心的垂直视场图像坐标
// double lineColor:线的颜色
//int flag：标志位
//制作特定位置的四线对

int fourLinePair(Mat srcImage, int width, int height, int line_Lenth, int line_Thickness, double lineColor[],int xpoint, int ypoint, int flag);

//制作西门子星图
int makeXimenziLine(const char* picFile, int width, int height, double x, double y, int R, double angle, int flag);
#pragma once

//int width:图像的宽度
//int height：图像的高度
//int xNum：图像水平方向有多少格
//int yNum：图像垂直方向多少个格子
//int pixelSize:每个棋盘格的长款尺寸（像素）
//char *fileName：图像的文件保存路径
//int imageFlag[]：图像的标志位置
//	flag[0,0]黑白黑白（从左上角开始）黑白图1位
//	flag[1,0]白黑百黑（从左上角开始）
//	flag[0,1]黑白黑白，彩色图3位
//int errNum：图像的错误编码
//	errNum=1：图像错误

//
int makeVrTestPicture(string fold, int width, int height, int pictureFLag);
//int width:图像的宽度
//int height：图像的高度
//double smallRadius:flag为0表示小圆半径，flag为1表示视场
//double bigRadius：flag为0表示大圆半径，flag为1表示视场
// double backgroudcolor[]:背景色
//double double drawColor[];绘图的颜色
//const char* filePath ：图片保存位置路径
//int flag：标识别位

int makeGhostCicle(int width, int height, double smallRadius, double bigRadius, double backgroundColor[], double drawColor[], const char* filePath, int flag);

//制作边缘畸变测试图卡
//int width:图像宽度
//int height：图像高度
//int xLenth：方形水平长
//nt yLenth:方形垂直宽 
//double Drawcolor[]:方形颜色pointcolor
//double backgroundcolor[]:背景颜色
// const char* filePath:数据保存路径
//int flag：标志位

int makeGhostSquare(int width, int height, int xLenth, int yLenth, double backgroundcolor[], double Drawcolor[], const char* filePath, int flag);

//制作AAC的VR测试图卡
int makeVrTestPic_AAC(string fold, int width, int height, int pictureFLag);

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

int makeGoerTekMTF_Chart(int width, int height, double fieldx[], double fieldy[], int linelength, int linethickness, double backgroundcolor[], double linecolor[], const char* filePath, int flag);


//制作边缘畸变测试图卡
//int width:图像宽度
//int height：图像高度
//int R：点的半径
//double pointcolor[]:点的颜色
//dpuble backgroundcolor[]:边缘畸变点背景颜色
// const char* filePath:数据保存路径
//int flag：标志位

int make_9point_DistortionChart(int width, int height, int R, double backgroundcolor[], double pointcolor[], const char* filePath, int flag);
//制作AR测试图图卡集
int makeArTestPic(string fold, int width, int height, int pictureFLag);

//制作边缘畸变测试图卡
//int width:图像宽度
//int height：图像高度
//int xLenth：水平长
//int yLenth: 垂直宽 
// int linethickness:线宽
//double pointcolor[]:方形颜色pointcolor
//double backgroundcolor[]:背景颜色
// const char* filePath:数据保存路径
//int flag：标志位

int makeBinocularFusionChart(int width, int height, int xLenth, int yLenth, int linethickness, double backgroundcolor[], double Drawcolor[], const char* filePath, int flag);


//制作不同视场的十字靶标
//Mat srcImage：输出图像
//int width：图像宽
//int height：图像高
//int Xpoint：交叉点的X坐标
//int Ypoint：交叉点的Y坐标
//int xLength：水平线的长度
//int yLength：垂直线的长度
//int lineThinkness:线的宽度
//double DrawColor[]:十字线的颜色

int CrossLineChart(Mat srcImage, int width, int height, int Xpoint, int Ypoint, int xLength, int yLength, int lineThinkness, double DrawColor[]);
//制作不同视场的十字靶标
//int width:图像宽度
//int height：图像高度
// double fieldx[] 水平坐标
// double fieldy[] 垂直视场
//int xLength:线的水平长度
//int yLength：线的垂直长度
// int lineThinkness:线的宽度
// double backgroudcolor[]:背景色
//double linecolor[];线宽的颜色
// const char* filePath:数据保存路径
//int flag：标志位,为0时表示的是视场，为1的时候表示坐标

int makeCrossLineChart(int width, int height, double fieldx[], double fieldy[], int xLength, int yLength, int lineThinkness, double backgroundcolor[], double DrawColor[], const char* filePath, int flag);

//制作VID测试图卡
//int width:图像宽度
//int height：图像高度
//int xStep：水平间隔（点阵）
//int yStep: 垂直间隔（点阵） 
// int DrawSize:为线的时候，表示线宽,为点的时候，表示边长
//double pointcolor[]:方形颜色pointcolor
//double backgroundcolor[]:背景颜色
// const char* filePath:数据保存路径
//int flag：标志位0为点阵，1为横线对，2为竖线对，3为横竖线对，4为棋盘格。
int makeVidPointChart(int width, int height, double xField, double yField, int xStep, int yStep, int DrawSize, double backgroundColor[], double drawColor[], const char* filePath, int flag);