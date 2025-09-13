// makePic.cpp : 此文件包含 "main" 函数。程序执行将在此处开始并结束。
//
// 图片制作.cpp : 此文件包含 "main" 函数。程序执行将在此处开始并结束。
//
#include "function.h" 
//#include "armadillo"
//
//int main()
////{
////
////	Mat srcImage(1600, 1600, CV_8UC3);
////	srcImage.setTo(0);;
////	if (!srcImage.data)
////	{
////		cout << "读取原始图失败!" << endl;
////		return -1;
////	}
////	namedWindow("srcImage", WINDOW_NORMAL);// 注意这个宏，使用WINDOW NORMAL可以允许用户自由伸缩窗口大小
////	imshow("srcImage", srcImage);
////	Mat logo = imread("C:\\Users\\97979\\Desktop\\pic\\code.png");
////	if (!logo.data)
////	{
////		cout <<"读取原始logo图失败!"<< endl;
////		return -1;
////	}
////	Mat SFRimage=imread("C:\\Users\\97979\\Desktop\\斜方块.bmp");
////	Mat dstimg;
////	resize(SFRimage, dstimg,Size(0 ,0), 0.2, 0.2, INTER_LINEAR);
////	Mat imageR0I = srcImage(Rect(800- dstimg.cols/2, 800- dstimg.rows/2, dstimg.cols, dstimg.rows)); //M原图中出形区域，Ret第-二参数表示左上角定点的坐标，用于定位，后两个参数表
////	imshow("ROI", imageR0I);
////	addWeighted(imageR0I,0, dstimg,1,0, imageR0I);//dst = rc11*alpha+ rc2I1*heta + gamma; 第-第四个参就是各自权重，第5个参教就是公式中
////	namedWindow("原图加dstimg",WINDOW_NORMAL);
////	imshow("原图加dstimg",srcImage);
////	imwrite("C:\\Users\\97979\\Desktop\\自定义斜方块.bmp", srcImage);
////	waitKey();
////	return 0;
////}
//
//int main()
//{
//	const char* pointchart = "C:\\Users\\97979\\Desktop\\circl_R_25_line1_chart.bmp";
//	/*makechessPicture(1600, 1600, 8.0, 60, pointchart, 01);*/
//	/*makechessPicture_SFR(1600, 1600, 8.0, 40, pointchart, 01);*/
//	makePerioPicture(1600, 1600,10, 10, 10, pointchart, 13);
//	//makeChart(3000, 2000, 17,11,5, pointchart,3);
//	//const char* crosschart = "C:\\Users\\97979\\Desktop\\cross_chart.bmp";
//	//makeChart(3000,2000, 8, 8, 2, crosschart,2);
//	//makePurePic(1920, 1080, 120,30, 8, pointchart, 006);
//	////应用伪彩色图
//	//Mat srcImg=imread("C:\\Users\\97979\\Desktop\\pic\\panel_1.png");
//	//Mat destImg;
//	//applyColorMap(srcImg, destImg, 2);
//	//namedWindow("destImg", WINDOW_NORMAL);
//	//imshow("destImg", destImg);
//	//imwrite(pointchart, destImg);
//	//waitKey(0);
//	/*const char* pointchart = "F://OEM客户//18-BOE//京东方字符logo检测设备//京东方平板后壳镭雕字符检测//环光//Image_20220428104726879.bmp";
//	logoDetect(pointchart, 2592, 1944);*/
////	//makeGradiantPic(2340, 1080, 15, pointchart, 500);
//	waitKey(0);
//	return 0;
//}
//	
//
//int main()
//{
//	Mat SFRimage = imread("C:\\Users\\97979\\Desktop\\斜方块.bmp");
//	const char* pointchart = "C:\\Users\\97979\\Desktop\\circl_R_25_line1_chart.bmp";
//	/*makechessPicture(1600, 1600, 8.0, 60, pointchart, 01);*/
//	/*makechessPicture_SFR(1600, 1600, 8.0, 40, pointchart, 01);*/
//	/*makePerioPicture(1600, 1600,40, 40, 25, pointchart, 402);*/
//	//makeChart(3000, 2000, 17,11,5, pointchart,3);
//	//const char* crosschart = "C:\\Users\\97979\\Desktop\\cross_chart.bmp";
//	//makeChart(3000,2000, 8, 8, 2, crosschart,2);
//	//makePurePic(1920, 1080, 120,30, 8, pointchart, 006);
//	////应用伪彩色图
//	//Mat srcImg=imread("C:\\Users\\97979\\Desktop\\pic\\panel_1.png");
//	//Mat destImg;
//	//applyColorMap(srcImg, destImg, 2);
//	//namedWindow("destImg", WINDOW_NORMAL);
//	//imshow("destImg", destImg);
//	//imwrite(pointchart, destImg);
//	//waitKey(0);
//	/*const char* pointchart = "F://OEM客户//18-BOE//京东方字符logo检测设备//京东方平板后壳镭雕字符检测//环光//Image_20220428104726879.bmp";
//	logoDetect(pointchart, 2592, 1944);*/
////	//makeGradiantPic(2340, 1080, 15, pointchart, 500);
//	waitKey(0);
//	return 0;
//}

//制作线对图，SFR图
int main()
{
	
	//const char* SFR_chart1 ="C:\\Users\\97979\\Desktop\\makepicture\\1920x1920.bmp";
	/*makeGhostCicle(1920, 1920, 0.5, 0.55, SFR_chart1,1);   */   


	string path = "C:\\Users\\97979\\Desktop\\makepicture\\";

	//Mat srcImg(1920, 1080, CV_8UC3,Scalar(0,255,0));
	//imshow("green", srcImg);
	//waitKey(0);
	//double backgroundColor[] = { 0,0,0 };
	//double Drawcolor[] = { 255,255,255 };
	//makeGhostSquare(640, 480, 100, 80, backgroundColor, Drawcolor, SFR_chart1, 0);

	//makeVrTestPic_AAC(path, 640, 480, 0);
	//makeVrTestPicture(path, 1920, 1920, 0);
	/*makeArTestPic(path, 1920, 1080, 0);*/

	//makeArTestPic(path, 640, 255, 0);

	makeArTestPic(path, 640, 480, 0);
	//makeArTestPic(path, 1280, 800, 0);
	//makeArTestPic(path, 1280, 720, 0);


    // 创建目录（如果不存在）
    

	//double fieldx[] = { 0,0.3,0.5,0.7 };
	//double fieldy[] = { 0,0.3,0.5,0.7 };
	//double backgroundColor[] = { 0,0,0 };
	//double DrawColor[] = { 255,255,255 };
	/*const char* SFR_chart1 = "C:\\Users\\97979\\Desktop\\makepicture\\1920x1920MTFforline.bmp";*/

	/*makeVidPointChart(640, 480, 0.5, 0.5, 20, 20, 2, backgroundColor, DrawColor, SFR_chart1, 4);*/
	/*makeCrossLineChart(640, 480, fieldx, fieldy, 30, 20, 1, backgroundColor, DrawColor, SFR_chart1, 0);*/

	/*makeBinocularFusionChart(640, 480, 200, 150,2, backgroundColor, Drawcolor, SFR_chart1, 0);*/
	/*Mat srcImg(1920, 1920, CV_8UC3, Scalar(0, 0, 0));
	Mat dstImage=srcImg;
	fourLinePair(srcImg, dstImage, 1920, 1920, 100, 2, 960, 960, 1);
	fourLinePair(dstImage, dstImage, 1920, 1920, 100, 2, 200, 960, 1);
	fourLinePair(dstImage, dstImage, 1920, 1920, 100, 2, 500, 960, 1);

	imshow("dstImage", dstImage);
	waitKey(0);
	

	//制作不同位置的MTF图卡
	double fieldx[] = { 0,0.3,0.5,0.7};
	double fieldy[] = { 0,0.3,0.5,0.7};
	double backgroundColor[] = { 0,0,0 };
	double Drawcolor[] = { 255,0,0 };	
	const char* SFR_chart1 = "C:\\Users\\97979\\Desktop\\makepicture\\1920x1920MTFforline.bmp";
	makeGoerTekMTF_Chart(1920, 1080, fieldx, fieldy, 40, 4, backgroundColor, Drawcolor, SFR_chart1, 0);
	
	//double backgroundcolor[3] = { 0, 0, 0 };
	//double pointcolor[3] = { 0, 255, 0 };
	//make_9point_DistortionChart(1920, 1080, 20, backgroundcolor, pointcolor, SFR_chart1, 0);

	//makelinePair(1920, 1080, 100, 4, 0.5, 0.5, SFR_chart1, 2, 0);

	//const char* SFR_chart1 ="C:\\Users\\97979\\Desktop\\makepicture\\1280x480GridLinewidth_1.bmp";
	//const char* SFR_chart2 = "C:\\Users\\97979\\Desktop\\makepicture\\1280x480GridLinewidth_2.bmp";
	//const char* SFR_chart3 = "C:\\Users\\97979\\Desktop\\makepicture\\1280x480VerticalLinewidth_3.bmp";
	//const char* SFR_chart4 = "C:\\Users\\97979\\Desktop\\makepicture\\1280x480VerticalLinewidth_4.bmp";
	//makeSFRpic(SFR_chart1, 1920, 1080, 0.7, 0.7, 100, 5,1);

	/*const char* MTF_chart = "C:\\Users\\97979\\Desktop\\MTF_chart.bmp";*/
	/*makelinePair(1600, 1600, 40, 2,0.5,0.5, MTF_chart,2,0);	*/
	/*makePerioPicture(1280, 480, 20,10, 1, SFR_chart1, 21);*/
	/*makePerioPicture(1280, 480, 16, 9, 1, SFR_chart2, 21);*/
	//makePerioPicture(1280, 480, 16, 9, 3, SFR_chart3, 31);
	//makePerioPicture(1280, 480, 16, 9, 4, SFR_chart4, 31);

	/*makePurePic(2280, 2280, 0, 0, 0, MTF4_chart, 300);*/
	/*makeXimenziLine(SFR_chart, 2280, 2280, 0, 0, 1140, 1, 0);*/
	//waitKey(0);

	return 0;
}


//制作标定棋盘格
//
//#include<iostream>
//#include<opencv2\core\core.hpp>
//#include<opencv2\highgui\highgui.hpp>
//
//using namespace std;

//int makeChessPicture(int width, int height, int xNum, int yNum, int pixelSize, const char *fileName, int imageFlag[], int errNum)
//{
//	int basisHeight = (height - pixelSize * yNum) / 2;
//	int basisWidth = (width - pixelSize * xNum) / 2;
//	if (basisHeight < 0 || basisWidth < 0)
//	{
//		cout << "Resolution doesn't match!" << endl;
//	}
//	cv::Mat image(Size(width, height), CV_8UC1, Scalar::all(255));
//	int flag = 0;
//	for (int j = 0; j < yNum; j++)
//	{
//		for (int i = 0; i < xNum; i++)
//		{
//			flag = (i + j) % 2;
//			if (flag == 0)
//			{
//				for (int n = j * pixelSize; n < (j + 1) * pixelSize; n++)
//					for (int m = i * pixelSize; m < (i + 1) * pixelSize; m++)
//						image.at<uchar>(n + basisHeight, m + basisWidth) = 0;
//			}
//		}
//	}
//	//cv::imshow("haha",image);
//	cv::imwrite(fileName, image);
//	//cv::waitKey(0);
//	return 0;
//};

//
//int main()
//{
//	const char *file = "E:\\04 code\\testPic\\chessboard.bmp";
//	int imageFlag[] = { 0, 0 };
//	int errorNum = 0;
//	makeChessPicture(9000, 7000, 9,7, 1000, file, imageFlag, errorNum);
//	return 0;
//}

