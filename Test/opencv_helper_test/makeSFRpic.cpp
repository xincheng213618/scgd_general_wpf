#include "function.h"


//width：背景宽度
//width：背景高度
//double x：水平视场占比0视场，0.3视场，0.5视场，0.707视场，0.85视场
//double y：垂直视场占比0视场，0.3视场，0.5视场，0.707视场，0.85视场
//int R:斜方块尺寸
//double angle 旋转角度
//const char* picFile制作图片保存路径
//int SFRchartflag:SFRchartflag=0表示棋盘格，SFRchartflag=1表示宝马标
//int flag  图片背景  0背景为黑色，1背景为白色


int makeSFRpic(const char* picFile, int width, int height, double x, double y, int R, double angle,int SFRchartflag, int flag)
{
	Mat srcImg(height, width, CV_8UC3, Scalar(255, 0, 255));
	Mat makeImg;
	//srcImg.setTo(0);
	if (SFRchartflag == 0)
	{
		if (x == 0)
		{
			RotatedRect cent_rect(cv::Point(width / 2, height / 2), Size(R, R), angle);
			DrawRotatedRect(srcImg, cent_rect, cv::Scalar(0, 255, 0), -1, 16);//方块颜色修改
		}
		else
		{
			//绘制中心斜方块
			RotatedRect cent_rect(cv::Point(width / 2, height / 2), Size(R, R), angle);
			DrawRotatedRect(srcImg, cent_rect, cv::Scalar(255, 255, 255), -1, 16);
			//绘制对应视场四角斜方块
			RotatedRect LeftUpRect(cv::Point((1 - x) * width / 2, (1 - y) * height / 2), Size(R, R), angle);
			RotatedRect RightDownRect(cv::Point((1 + x) * width / 2, (1 + y) * height / 2), Size(R, R), angle);
			RotatedRect LeftDownRect(cv::Point((1 - x) * width / 2, (1 + y) * height / 2), Size(R, R), angle);
			RotatedRect RightUprect(cv::Point((1 + x) * width / 2, (1 - y) * height / 2), Size(R, R), angle);
			DrawRotatedRect(srcImg, LeftUpRect, cv::Scalar(255, 255, 255), -1, 16);
			DrawRotatedRect(srcImg, RightDownRect, cv::Scalar(255, 255, 255), -1, 16);
			DrawRotatedRect(srcImg, LeftDownRect, cv::Scalar(255, 255, 255), -1, 16);
			DrawRotatedRect(srcImg, RightUprect, cv::Scalar(255, 255, 255), -1, 16);

			//绘制0.3对应视场四角斜方块
			RotatedRect LeftUpRect_0(cv::Point((1 - 0.3) * width / 2, (1 - 0.3) * height / 2), Size(R, R), angle);
			RotatedRect RightDownRect_0(cv::Point((1 + 0.3) * width / 2, (1 + 0.3) * height / 2), Size(R, R), angle);
			RotatedRect LeftDownRect_0(cv::Point((1 - 0.3) * width / 2, (1 + 0.3) * height / 2), Size(R, R), angle);
			RotatedRect RightUprect_0(cv::Point((1 + 0.3) * width / 2, (1 - 0.3) * height / 2), Size(R, R), angle);
			DrawRotatedRect(srcImg, LeftUpRect_0, cv::Scalar(255, 255, 255), -1, 16);
			DrawRotatedRect(srcImg, RightDownRect_0, cv::Scalar(255, 255, 255), -1, 16);
			DrawRotatedRect(srcImg, LeftDownRect_0, cv::Scalar(255, 255, 255), -1, 16);
			DrawRotatedRect(srcImg, RightUprect_0, cv::Scalar(255, 255, 255), -1, 16);

			//绘制0.6对应视场四角斜方块
		/*	RotatedRect LeftUpRect_1(cv::Point((1 - 0.6) * width / 2, (1 - 0.6) * height / 2), Size(R, R), angle);
			RotatedRect RightDownRect_1(cv::Point((1 + 0.6) * width / 2, (1 + 0.6) * height / 2), Size(R, R), angle);
			RotatedRect LeftDownRect_1(cv::Point((1 - 0.6) * width / 2, (1 + 0.6) * height / 2), Size(R, R), angle);
			RotatedRect RightUprect_1(cv::Point((1 + 0.6) * width / 2, (1 - 0.6) * height / 2), Size(R, R), angle);
			DrawRotatedRect(srcImg, LeftUpRect_1, cv::Scalar(255, 255, 255), -1, 16);
			DrawRotatedRect(srcImg, RightDownRect_1, cv::Scalar(255, 255, 255), -1, 16);
			DrawRotatedRect(srcImg, LeftDownRect_1, cv::Scalar(255, 255, 255), -1, 16);
			DrawRotatedRect(srcImg, RightUprect_1, cv::Scalar(255, 255, 255), -1, 16);*/

			//绘制上下左右0.6视场斜方块
			RotatedRect LeftRect_2(cv::Point((1 - 0.6) * width / 2, height / 2), Size(R, R), angle);
			RotatedRect RightRect_2(cv::Point((1 + 0.6) * width / 2, height / 2), Size(R, R), angle);
			RotatedRect DownRect_2(cv::Point(width / 2, (1 + 0.6) * height / 2), Size(R, R), angle);
			RotatedRect Uprect_2(cv::Point(width / 2, (1 - 0.6) * height / 2), Size(R, R), angle);
			DrawRotatedRect(srcImg, LeftRect_2, cv::Scalar(255, 255, 255), -1, 16);
			DrawRotatedRect(srcImg, RightRect_2, cv::Scalar(255, 255, 255), -1, 16);
			DrawRotatedRect(srcImg, DownRect_2, cv::Scalar(255, 255, 255), -1, 16);
			DrawRotatedRect(srcImg, Uprect_2, cv::Scalar(255, 255, 255), -1, 16);

			////绘制中心宝马图
			//ellipse(srcImg, Point(width / 2, height / 2), Size(int(R), int(R)), 0, angle, angle + 90, cv::Scalar(255, 255, 255), -1, cv::LINE_AA, 0);
			//ellipse(srcImg, Point(width / 2, height / 2), Size(int(R), int(R)), 0, angle + 180, angle + 270, cv::Scalar(255, 255, 255), -1, cv::LINE_AA, 0);
			//
			////绘制边缘视场宝马图
			////左上
			//ellipse(srcImg, Point((1 - x)*width / 2, (1 - y)*height / 2), Size(int(R), int(R)), 0, angle, angle + 90, cv::Scalar(255, 255, 255), -1, cv::LINE_AA, 0);
			//ellipse(srcImg, Point((1 - x)*width / 2, (1 - y)*height / 2), Size(int(R), int(R)), 0, angle + 180, angle + 270, cv::Scalar(255, 255, 255), -1, cv::LINE_AA, 0);
			////左下
			//ellipse(srcImg, Point((1 - x)*width / 2, (1 + y)*height / 2), Size(int(R), int(R)), 0, angle, angle + 90, cv::Scalar(255, 255, 255), -1, cv::LINE_AA, 0);
			//ellipse(srcImg, Point((1 - x)*width / 2, (1 + y)*height / 2), Size(int(R), int(R)), 0, angle + 180, angle + 270, cv::Scalar(255, 255, 255), -1, cv::LINE_AA, 0);
			////右上
			//ellipse(srcImg, Point((1 + x)*width / 2, (1 - y)* height / 2), Size(int(R), int(R)), 0, angle, angle + 90, cv::Scalar(255, 255, 255), -1, cv::LINE_AA, 0);
			//ellipse(srcImg, Point((1 + x)*width / 2, (1 - y)* height / 2), Size(int(R), int(R)), 0, angle + 180, angle + 270, cv::Scalar(255, 255, 255), -1, cv::LINE_AA, 0);
			////右下
			//ellipse(srcImg, Point((1 + x)*width / 2, (1 + y)*height / 2), Size(int(R), int(R)), 0, angle, angle + 90, cv::Scalar(255, 255, 255), -1, cv::LINE_AA, 0);
			//ellipse(srcImg, Point((1 + x)*width / 2, (1 + y)*height / 2), Size(int(R), int(R)), 0, angle + 180, angle + 270, cv::Scalar(255, 255, 255), -1, cv::LINE_AA, 0);
		}
	}
	else if (SFRchartflag == 1)	
	{
		if (x == 0)
		{
			ellipse(srcImg, Point(width / 2, height / 2), Size(int(R), int(R)), 0, angle, angle + 90, cv::Scalar(255, 255, 255), -1, cv::LINE_AA, 0);
			ellipse(srcImg, Point(width / 2, height / 2), Size(int(R), int(R)), 0, angle + 180, angle + 270, cv::Scalar(255, 255, 255), -1, cv::LINE_AA, 0);
		}
		else
		{		

			//绘制中心宝马图
			ellipse(srcImg, Point(width / 2, height / 2), Size(int(R), int(R)), 0, angle, angle + 90, cv::Scalar(255, 255, 255), -1, cv::LINE_AA, 0);
			ellipse(srcImg, Point(width / 2, height / 2), Size(int(R), int(R)), 0, angle + 180, angle + 270, cv::Scalar(255, 255, 255), -1, cv::LINE_AA, 0);
			
			//绘制边缘对应视场宝马图
			//左上
			ellipse(srcImg, Point((1 - x)*width / 2, (1 - y)*height / 2), Size(int(R), int(R)), 0, angle, angle + 90, cv::Scalar(255, 255, 255), -1, cv::LINE_AA, 0);
			ellipse(srcImg, Point((1 - x)*width / 2, (1 - y)*height / 2), Size(int(R), int(R)), 0, angle + 180, angle + 270, cv::Scalar(255, 255, 255), -1, cv::LINE_AA, 0);
			//左下
			ellipse(srcImg, Point((1 - x)*width / 2, (1 + y)*height / 2), Size(int(R), int(R)), 0, angle, angle + 90, cv::Scalar(255, 255, 255), -1, cv::LINE_AA, 0);
			ellipse(srcImg, Point((1 - x)*width / 2, (1 + y)*height / 2), Size(int(R), int(R)), 0, angle + 180, angle + 270, cv::Scalar(255, 255, 255), -1, cv::LINE_AA, 0);
			//右上
			ellipse(srcImg, Point((1 + x)*width / 2, (1 - y)* height / 2), Size(int(R), int(R)), 0, angle, angle + 90, cv::Scalar(255, 255, 255), -1, cv::LINE_AA, 0);
			ellipse(srcImg, Point((1 + x)*width / 2, (1 - y)* height / 2), Size(int(R), int(R)), 0, angle + 180, angle + 270, cv::Scalar(255, 255, 255), -1, cv::LINE_AA, 0);
			//右下
			ellipse(srcImg, Point((1 + x)*width / 2, (1 + y)*height / 2), Size(int(R), int(R)), 0, angle, angle + 90, cv::Scalar(255, 255, 255), -1, cv::LINE_AA, 0);
			ellipse(srcImg, Point((1 + x)*width / 2, (1 + y)*height / 2), Size(int(R), int(R)), 0, angle + 180, angle + 270, cv::Scalar(255, 255, 255), -1, cv::LINE_AA, 0);

			//绘制0.3视场四角对应视场宝马图
			//左上
			ellipse(srcImg, Point((1 - 0.3) * width / 2, (1 - 0.3) * height / 2), Size(int(R), int(R)), 0, angle, angle + 90, cv::Scalar(255, 255, 255), -1, cv::LINE_AA, 0);
			ellipse(srcImg, Point((1 - 0.3) * width / 2, (1 - 0.3) * height / 2), Size(int(R), int(R)), 0, angle + 180, angle + 270, cv::Scalar(255, 255, 255), -1, cv::LINE_AA, 0);
			//左下
			ellipse(srcImg, Point((1 - 0.3) * width / 2, (1 + 0.3) * height / 2), Size(int(R), int(R)), 0, angle, angle + 90, cv::Scalar(255, 255, 255), -1, cv::LINE_AA, 0);
			ellipse(srcImg, Point((1 - 0.3) * width / 2, (1 + 0.3) * height / 2), Size(int(R), int(R)), 0, angle + 180, angle + 270, cv::Scalar(255, 255, 255), -1, cv::LINE_AA, 0);
			//右上
			ellipse(srcImg, Point((1 + 0.3) * width / 2, (1 - 0.3) * height / 2), Size(int(R), int(R)), 0, angle, angle + 90, cv::Scalar(255, 255, 255), -1, cv::LINE_AA, 0);
			ellipse(srcImg, Point((1 + 0.3) * width / 2, (1 - 0.3) * height / 2), Size(int(R), int(R)), 0, angle + 180, angle + 270, cv::Scalar(255, 255, 255), -1, cv::LINE_AA, 0);
			//右下
			ellipse(srcImg, Point((1 + 0.3) * width / 2, (1 + 0.3) * height / 2), Size(int(R), int(R)), 0, angle, angle + 90, cv::Scalar(255, 255, 255), -1, cv::LINE_AA, 0);
			ellipse(srcImg, Point((1 + 0.3) * width / 2, (1 + 0.3) * height / 2), Size(int(R), int(R)), 0, angle + 180, angle + 270, cv::Scalar(255, 255, 255), -1, cv::LINE_AA, 0);

			////绘制0.6视场四角视场宝马图
			////左上
			//ellipse(srcImg, Point((1 - 0.6)* width / 2, (1 - 0.6)* height / 2), Size(int(R), int(R)), 0, angle, angle + 90, cv::Scalar(255, 255, 255), -1, cv::LINE_AA, 0);
			//ellipse(srcImg, Point((1 - 0.6)* width / 2, (1 - 0.6)* height / 2), Size(int(R), int(R)), 0, angle + 180, angle + 270, cv::Scalar(255, 255, 255), -1, cv::LINE_AA, 0);
			////左下
			//ellipse(srcImg, Point((1 - 0.6)* width / 2, (1 + 0.6)* height / 2), Size(int(R), int(R)), 0, angle, angle + 90, cv::Scalar(255, 255, 255), -1, cv::LINE_AA, 0);
			//ellipse(srcImg, Point((1 - 0.6)* width / 2, (1 + 0.6)* height / 2), Size(int(R), int(R)), 0, angle + 180, angle + 270, cv::Scalar(255, 255, 255), -1, cv::LINE_AA, 0);
			////右上
			//ellipse(srcImg, Point((1 + 0.6)* width / 2, (1 - 0.6)* height / 2), Size(int(R), int(R)), 0, angle, angle + 90, cv::Scalar(255, 255, 255), -1, cv::LINE_AA, 0);
			//ellipse(srcImg, Point((1 + 0.6)* width / 2, (1 - 0.6)* height / 2), Size(int(R), int(R)), 0, angle + 180, angle + 270, cv::Scalar(255, 255, 255), -1, cv::LINE_AA, 0);
			////右下
			//ellipse(srcImg, Point((1 + 0.6)* width / 2, (1 + 0.6)* height / 2), Size(int(R), int(R)), 0, angle, angle + 90, cv::Scalar(255, 255, 255), -1, cv::LINE_AA, 0);
			//ellipse(srcImg, Point((1 + 0.6)* width / 2, (1 + 0.6)* height / 2), Size(int(R), int(R)), 0, angle + 180, angle + 270, cv::Scalar(255, 255, 255), -1, cv::LINE_AA, 0);


			//绘制0.6视场上下视场宝马图
			//左
			int dialmeter;
			dialmeter = sqrt(pow(width, 2) + pow(height, 2));
			ellipse(srcImg, Point(width/2- 0.6* dialmeter / 2, height / 2), Size(int(R), int(R)), 0, angle, angle + 90, cv::Scalar(255, 255, 255), -1, cv::LINE_AA, 0);
			ellipse(srcImg, Point(width/2- 0.6* dialmeter / 2, height / 2), Size(int(R), int(R)), 0, angle + 180, angle + 270, cv::Scalar(255, 255, 255), -1, cv::LINE_AA, 0);
			//上
			ellipse(srcImg, Point(width / 2, height/2 + 0.6* dialmeter / 2), Size(int(R), int(R)), 0, angle, angle + 90, cv::Scalar(255, 255, 255), -1, cv::LINE_AA, 0);
			ellipse(srcImg, Point(width / 2, height/2 + 0.6* dialmeter / 2), Size(int(R), int(R)), 0, angle + 180, angle + 270, cv::Scalar(255, 255, 255), -1, cv::LINE_AA, 0);
			//下
			ellipse(srcImg, Point(width / 2, height/2 - 0.6* dialmeter / 2), Size(int(R), int(R)), 0, angle, angle + 90, cv::Scalar(255, 255, 255), -1, cv::LINE_AA, 0);
			ellipse(srcImg, Point(width / 2, height/2 - 0.6* dialmeter / 2), Size(int(R), int(R)), 0, angle + 180, angle + 270, cv::Scalar(255, 255, 255), -1, cv::LINE_AA, 0);
			//右
			ellipse(srcImg, Point(width/2 + 0.6* dialmeter / 2, height / 2), Size(int(R), int(R)), 0, angle, angle + 90, cv::Scalar(255, 255, 255), -1, cv::LINE_AA, 0);
			ellipse(srcImg, Point(width/2 + 0.6* dialmeter / 2, height / 2), Size(int(R), int(R)), 0, angle + 180, angle + 270, cv::Scalar(255, 255, 255), -1, cv::LINE_AA, 0);
		}
	}
	if (flag == 0)
	{
		makeImg = srcImg;
	}
	else if (flag == 1)
	{
		makeImg = ~srcImg;
	}
	imshow("makeImg", makeImg);
	imwrite(picFile, makeImg);
	return 0;
}
