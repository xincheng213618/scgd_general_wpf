#include "function.h"


int makeArTestPic(string fold, int width, int height, int pictureFLag)
{
	//设置背景色，设置绘图色
	double backgroundColor[] = { 0,0,0};
	double drawColor[] = { 0,255,0 };

	//确定生成图像文件名string类型
	string Widthstr = to_string(width);
	string heightstr = to_string(height);
	//> create a new file nemed NewFileName on the current file path
	// 
	//bool flag = CreateDirectory(saveCompareRaw.c_str(), NULL);

	//string filePath = "OldFilePath\\"; //> current file path
	//string saveCompareRaw = filePath + "NewFileName";
	////> create a new file nemed NewFileName on the current file path
	//bool flag = CreateDirectory(saveCompareRaw.c_str(), NULL);

	//LPCWSTR dirName = L"C:\\Users\\97979\\Desktop\\makepicture\\1920x1080"; 
	//	// 创建目录
	//	if (CreateDirectory(dirName, NULL)) {
	//		std::wcout << L"目录创建成功: " << dirName << std::endl;
	//	}
	//	else {
	//		std::wcerr << L"目录创建失败: " << GetLastError() << std::endl;
	//	}

		//system("md C:\\Users\\97979\\Desktop\\makepicture\\1920x1920");//创建文件夹


	//system(filefolder.c_str());//std::string转化为const char*的另一种方法

	//使用mkdir新建文件夹
	string filefolder = fold + Widthstr + "x" + heightstr + "TestPicture";
	const char* path = filefolder.c_str();
	_mkdir(path);

	/*_mkdir(strcat(path, name)*/	

	//纯色图
	string filename = filefolder + "\\" + Widthstr + "x" + heightstr + "_pure_";
	//bmp图
	// 
	string w255 = filename + "white_255.bmp";
	string w128 = filename + "w128.bmp";
	string w64 = filename + "w64.bmp";
	string w32 = filename + "w32.bmp";
	string w16 = filename + "w16.bmp";
	string w3 = filename + "w3.bmp";
	string BK = filename + "BK.bmp";
	string R = filename + "R.bmp";
	string G = filename + "G.bmp";
	string G25 = filename + "G25.bmp";
	string G51 = filename + "G51.bmp";
	string B = filename + "B.bmp";
	// 
	//jpg图
	//string w255 = filename + "white_255.jpg";
	//string w128 = filename + "w128.jpg";
	//string w64 = filename + "w64.jpg";
	//string w32 = filename + "w32.jpg";
	//string w16 = filename + "w16.jpg";
	//string w3 = filename + "w3.jpg";
	//string BK = filename + "BK.jpg";
	//string R = filename + "R.jpg";
	//string G = filename + "G.jpg";
	//string B = filename + "B.jpg";


	//chessboard chart

	string CameraChart = filefolder + "\\" + Widthstr + "x" + heightstr;
	////bmp图
	string ANSI_Constrast0 = CameraChart + "_4x4_ANSI_Constrast_BK.bmp";
	string ANSI_Constrast1 = CameraChart + "_4x4_ANSI_Constrast_W.bmp";
	//jpg图
	//string ANSI_Constrast0 = CameraChart + "_4x4_ANSI_Constrast_BK.jpg";
	//string ANSI_Constrast1 = CameraChart + "_4x4_ANSI_Constrast_W.jpg";



	//水平垂直MTF图
	string Hori_MTF = filefolder + "\\" + Widthstr + "x" + heightstr + "hor_line_";
	//bmp图
	string hori1 = Hori_MTF + "1.bmp";
	string hori2 = Hori_MTF + "2.bmp";
	string hori3 = Hori_MTF + "3.bmp";
	string hori4 = Hori_MTF + "4.bmp";
	string hori5 = Hori_MTF + "5.bmp";
	//jpg图
	//string hori1 = Hori_MTF + "1.jpg";
	//string hori2 = Hori_MTF + "2.jpg";
	//string hori3 = Hori_MTF + "3.jpg";
	//string hori4 = Hori_MTF + "4.jpg";




	//垂直MTF
	string Vert_MTF = filefolder + "\\" + Widthstr + "x" + heightstr + "ver_line_";

	//bmp图
	string Vert1 = Vert_MTF + "1.bmp";
	string Vert2 = Vert_MTF + "2.bmp";
	string Vert3 = Vert_MTF + "3.bmp";
	string Vert4 = Vert_MTF + "4.bmp";
	string Vert5 = Vert_MTF + "5.bmp";
	////jpg图
	//string Vert1 = Vert_MTF + "1.jpg";
	//string Vert2 = Vert_MTF + "2.jpg";
	//string Vert3 = Vert_MTF + "3.jpg";
	//string Vert4 = Vert_MTF + "4.jpg";



	//特定视角水平垂直交叉MTF图卡
	//bmp图
	string MTF_fourline1 = CameraChart + "_MTF_fourline1_Chart.bmp";
	string MTF_fourline2 = CameraChart + "_MTF_fourline2_Chart.bmp";
	string MTF_fourline3 = CameraChart + "_MTF_fourline3_Chart.bmp";
	string MTF_fourline4 = CameraChart + "_MTF_fourline4_Chart.bmp";
	//string MTF_fourline5 = CameraChart + "_MTF_fourline5_Chart.bmp";

	//jpg图
	//string MTF_fourline1 = CameraChart + "_MTF_fourline1_Chart.jpg";
	//string MTF_fourline2 = CameraChart + "_MTF_fourline2_Chart.jpg";
	//string MTF_fourline3 = CameraChart + "_MTF_fourline3_Chart.jpg";
	//string MTF_fourline4 = CameraChart + "_MTF_fourline4_Chart.jpg";


	//TV畸变图	
	//bmp图

	string TvDistortion00 = CameraChart + "TVDistortionChart_00.bmp";
	string TvDistortion01 = CameraChart + "TVDistortionChart_01.bmp";

	//jpg图
	//string TvDistortion00 = CameraChart + "TVDistortionChart_00.jpg";
	//string TvDistortion01 = CameraChart + "TVDistortionChart_01.jpg";



	//光学畸变图
	//bmp图
	string OptDistortion01 = CameraChart + "OptDistortionChart.bmp";
	//jpg图
	//string OptDistortion01 = CameraChart + "OptDistortionChart.jpg";


	//VR鬼影图
	//bmp图
	string 	VRghost01 = CameraChart + "VRghostChart.bmp";
	string VRghost02 = CameraChart + "VRghostCircleChart.bmp";
	////jpg图
	//string 	VRghost01 = CameraChart + "VRghostChart.jpg";
	//string VRghost02 = CameraChart + "VRghostCircleChart.jpg";

	//AR鬼影图
	//bmp图
	string 	ARghost01 = CameraChart + "ARghostChart.bmp";
	string 	ARghost02 = CameraChart + "ARghostSquareChart.bmp";
	//jpg图
	//string 	ARghost01 = CameraChart + "ARghostChart.jpg";
	//string 	ARghost02 = CameraChart + "ARghostSquareChart.jpg";


	//SFR斜方块图
	//bmp图
	string SFR01 = CameraChart + "F0.7_斜方块5°_SFR_Chart.bmp";
	//jpg图
	//string SFR01 = CameraChart + "F0.7_斜方块5°_SFR_Chart.jpg";

	//SFR宝马标图
	//bmp图
	string SFR02 = CameraChart + "F0.7_宝马标5°_SFR_Chart.bmp";
	//jpg图
	//string SFR02 = CameraChart + "F0.7_宝马标5°_SFR_Chart.jpg";
	//双目合像图卡
	//bmp图

	string  binocularFusion01 = CameraChart + "binocularFusionChart1.bmp";
	string  binocularFusion02 = CameraChart + "binocularFusionChart2.bmp";
	//jpg图
	//string  binocularFusion01 = CameraChart + "binocularFusionChart1.jpg";
	//string  binocularFusion02 = CameraChart + "binocularFusionChart2.jpg";

	//VID测试图卡
	//bmp图
	string  VID00 = CameraChart + "point_VID_00.bmp";
	string  VID01 = CameraChart + "point_VID_01.bmp";
	string  VID02 = CameraChart + "point_VID_02.bmp";
	string  VID03 = CameraChart + "point_VID_03.bmp";
	string  VID04 = CameraChart + "point_VID_04.bmp";
	//jpg图
	//string  VID00 = CameraChart + "point_VID_00.jpg";
	//string  VID01 = CameraChart + "point_VID_01.jpg";
	//string  VID02 = CameraChart + "point_VID_02.jpg";
	//string  VID03 = CameraChart + "point_VID_03.jpg";
	//string  VID04 = CameraChart + "point_VID_04.jpg";

	

	//不同视场十字图卡
	////bmp图
	string crossline01 = CameraChart + "corssline_01.bmp";

	//jpg图
	//string crossline01 = CameraChart + "corssline_01.jpg";

	//string转const char*
	const char* W_255 = w255.c_str();
	const char* w_128 = w128.c_str();
	const char* w_64 = w64.c_str();
	const char* w_32 = w32.c_str();
	const char* w_16 = w16.c_str();
	const char* w_3 = w3.c_str();
	const char* BK_0 = BK.c_str();
	const char* R_255 = R.c_str();
	const char* G_255 = G.c_str();
	const char* G_25 = G25.c_str();
	const char* G_51 = G51.c_str();
	const char* B_255 = B.c_str();//  使用c_str()获取C风格字符串

	const char* hori_1 = hori1.c_str();
	const char* hori_2 = hori2.c_str();
	const char* hori_3 = hori3.c_str();
	const char* hori_4 = hori4.c_str();
	const char* hori_5 = hori5.c_str();


	const char* Vert_1 = Vert1.c_str();
	const char* Vert_2 = Vert2.c_str();
	const char* Vert_3 = Vert3.c_str();
	const char* Vert_4 = Vert4.c_str();
	const char* Vert_5 = Vert5.c_str();

	const char* MTF_fourline_1 = MTF_fourline1.c_str();
	const char* MTF_fourline_2= MTF_fourline2.c_str();
	const char* MTF_fourline_3 = MTF_fourline3.c_str();
	const char* MTF_fourline_4 = MTF_fourline4.c_str();
	//const char* MTF_fourline_5 = MTF_fourline5.c_str();


	const char* ANSI_Constrast_0 = ANSI_Constrast0.c_str();
	const char* ANSI_Constrast_1 = ANSI_Constrast1.c_str();

	const char* TvDistortion_00 = TvDistortion00.c_str();
	const char* TvDistortion_01 = TvDistortion01.c_str();
	const char* OptDistortion_01 = OptDistortion01.c_str();
	const char* VRghost_01 = VRghost01.c_str();
	const char* VRghost_02 = VRghost02.c_str();
	const char* ARghost_01 = ARghost01.c_str();
	const char* ARghost_02 = ARghost02.c_str();
	const char* SFR_01 = SFR01.c_str();
	const char* SFR_02 = SFR02.c_str();
	const char* binocularFusion_01 = binocularFusion01.c_str();
	const char* binocularFusion_02 = binocularFusion02.c_str();
	const char* VID_00 = VID00.c_str();
	const char* VID_01 = VID01.c_str();
	const char* VID_02 = VID02.c_str();
	const char* VID_03 = VID03.c_str();
	const char* VID_04 = VID04.c_str();
	const char* crossline_01 = crossline01.c_str();



	//const char* W_255 = "C:\\Users\\97979\\Desktop\\makepicture\\1.bmp";
	//const char* w_128 = "C:\\Users\\97979\\Desktop\\makepicture\\2.bmp";
	//const char* w_64 = "C:\\Users\\97979\\Desktop\\makepicture\\3.bmp";
	//const char* w_32 = "C:\\Users\\97979\\Desktop\\makepicture\\4.bmp";
	//const char* w_16 = "C:\\Users\\97979\\Desktop\\makepicture\\5.bmp";
	//const char* w_3 = "C:\\Users\\97979\\Desktop\\makepicture\\6.bmp";
	//const char* BK_0 = "C:\\Users\\97979\\Desktop\\makepicture\\7.bmp";
	//const char* R_255 = "C:\\Users\\97979\\Desktop\\makepicture\\8.bmp";
	//const char* G_255 = "C:\\Users\\97979\\Desktop\\makepicture\\9.bmp";
	//const char* B_255 = "C:\\Users\\97979\\Desktop\\makepicture\\10.bmp";
	//const char* hor_MTF_1 = "C:\\Users\\97979\\Desktop\\makepicture\\11.bmp";
	//const char* hor_MTF_2 = "C:\\Users\\97979\\Desktop\\makepicture\\12.bmp";
	//const char* hor_MTF_3 = "C:\\Users\\97979\\Desktop\\makepicture\\13.bmp";
	//const char* hor_MTF_4 = "C:\\Users\\97979\\Desktop\\makepicture\\14.bmp";
	//const char* ver_MTF_1 = "C:\\Users\\97979\\Desktop\\makepicture\\15.bmp";
	//const char* ver_MTF_2 = "C:\\Users\\97979\\Desktop\\makepicture\\16.bmp";
	//const char* ver_MTF_3 = "C:\\Users\\97979\\Desktop\\makepicture\\17.bmp";
	//const char* ver_MTF_4 = "C:\\Users\\97979\\Desktop\\makepicture\\18.bmp";
	//const char* ghost_1 = "C:\\Users\\97979\\Desktop\\makepicture\\19.bmp";
	//const char* vid_1 = "C:\\Users\\97979\\Desktop\\makepicture\\20.bmp";
	//const char* binocular = "C:\\Users\\97979\\Desktop\\makepicture\\21.bmp";
	//const char* chessboard = "C:\\Users\\97979\\Desktop\\makepicture\\22.bmp";
	//const char* optiDistortion = "C:\\Users\\97979\\Desktop\\makepicture\\23.bmp";
	//const char* TVDistortion = "C:\\Users\\97979\\Desktop\\makepicture\\24.bmp";
	//const char* FOV = "C:\\Users\\97979\\Desktop\\makepicture\\25.bmp";

	//000：制作纯白图片		
	makePurePic(width, height, 255, 255, 255, W_255, 000);
	//001：制作纯灰128图片		
	makePurePic(width, height,128, 128, 128, w_128, 001);
	//002：制作纯灰64图片		
	makePurePic(width, height, 64, 64, 64, w_64, 002);
	//003：制作纯灰32图片		
	makePurePic(width, height,32, 32, 32, w_32, 003);
	//004：制作纯灰16图片		
	makePurePic(width, height, 16, 16, 16, w_16, 004);
	//005：制作灰阶L3图片		
	makePurePic(width, height, 3, 3, 3, w_3, 005);
	//006：制作纯黑图片		
	makePurePic(width, height, 0, 0, 0, BK_0, 006);
	//100：制作纯红图片		
	makePurePic(width, height, 0, 0, 255, R_255, 100);
	//200：制作纯绿图片		
	/*makePurePic(width, height, 0, 0, 0, G_255, 200);*/
	makePurePic(width, height, 0, 255, 0, G_255, 1000);
	makePurePic(width, height, 0, 25, 0, G_25, 1000);
	makePurePic(width, height, 0, 51, 0, G_51, 1000);
	//300：制作纯蓝图片
	makePurePic(width, height, 255, 0, 0, B_255, 300);

	//制作MTF线对测试图卡
	makePerioPicture(width, height, 3, 3, 1, backgroundColor, drawColor, hori_1, 30);
	makePerioPicture(width, height, 3, 3, 2, backgroundColor, drawColor, hori_2, 30);
	makePerioPicture(width, height, 3, 3, 3, backgroundColor, drawColor, hori_3, 30);
	makePerioPicture(width, height, 3, 3, 4, backgroundColor, drawColor, hori_4, 30);
	makePerioPicture(width, height, 3, 3, 5, backgroundColor, drawColor, hori_5, 30);
	makePerioPicture(width, height, 3, 3, 1, backgroundColor, drawColor, Vert_1, 31);
	makePerioPicture(width, height, 3, 3, 2, backgroundColor, drawColor, Vert_2, 31);
	makePerioPicture(width, height, 3, 3, 3, backgroundColor, drawColor, Vert_3, 31);
	makePerioPicture(width, height, 3, 3, 4, backgroundColor, drawColor, Vert_4, 31);
	makePerioPicture(width, height, 3, 3, 5, backgroundColor, drawColor, Vert_5, 31);

	//制作四边MTF图卡
	double fieldx[] = { 0,0.4,0.8 };
	double fieldy[] = { 0,0.4,0.8 };
	//线长为22
	/*makeGoerTekMTF_Chart(width,height,fieldx, fieldy, 22, 1, backgroundColor, drawColor, MTF_fourline_1, 0);
	makeGoerTekMTF_Chart(width, height, fieldx, fieldy, 22, 2, backgroundColor, drawColor, MTF_fourline_2, 0);
	makeGoerTekMTF_Chart(width, height, fieldx, fieldy, 22, 3, backgroundColor, drawColor, MTF_fourline_3, 0);
	makeGoerTekMTF_Chart(width, height, fieldx, fieldy, 22, 4, backgroundColor, drawColor, MTF_fourline_4, 0);*/
	//makeGoerTekMTF_Chart(width, height, fieldx, fieldy, 50, 5, backgroundColor, drawColor, MTF_fourline_5, 0);


	//线长为50
	makeGoerTekMTF_Chart(width, height, fieldx, fieldy, 25, 1, backgroundColor, drawColor, MTF_fourline_1, 0);
	makeGoerTekMTF_Chart(width, height, fieldx, fieldy, 25, 2, backgroundColor, drawColor, MTF_fourline_2, 0);
	makeGoerTekMTF_Chart(width, height, fieldx, fieldy, 25, 3, backgroundColor, drawColor, MTF_fourline_3, 0);
	makeGoerTekMTF_Chart(width, height, fieldx, fieldy, 25, 4, backgroundColor, drawColor, MTF_fourline_4, 0);
	//makeGoerTekMTF_Chart(width, height, fieldx, fieldy, 50, 5, backgroundColor, drawColor, MTF_fourline_5, 0);

	//制作棋盘格对比度图（黑白黑白）
	makePerioPicture(width, height, 4, 4, 4, backgroundColor, drawColor, ANSI_Constrast_0, 00);	
	//制作棋盘格对比度图（白黑白黑）
	makePerioPicture(width, height, 4, 4, 4, backgroundColor, drawColor,ANSI_Constrast_1, 01);
	//制作Ar鬼影图卡,0，0.3,0.5,0.7,0.9视场，点的半径为10个pixel
	//makePerioPicture(width, height, 10, 10, 20, VRghost_01, 14);
	//制作vr鬼影图片，圆环图片
	makeGhostCicle(width, height, 130, 140, backgroundColor, drawColor, VRghost_02, 0);
	//制作ar鬼影图卡,1/10位置处，点的半径为40个pixel
	/*makePerioPicture(width, height, 10, 10, 20, ARghost_01, 13);	*/
	//制作ar鬼影图卡，背景纯色，中间长度为200，宽度为150pixe的纯色块30*30,320*240
	makeGhostSquare(width, height, 320, 240, backgroundColor, drawColor, ARghost_02, 0);	
	//制作Ar边缘视场9点TV畸变图卡，点的半径为20个pixel
	int pointRadiu = 4;
	make_9point_DistortionChart(width, height, pointRadiu, backgroundColor, drawColor, TvDistortion_00, 0);	
	//制作9点畸变图，flag为1制作特定坐标的9点图
	/*make_9point_DistortionChart(width, height, pointRadiu, backgroundColor, drawColor, TvDistortion_01, 1);*/

	////制作9点均匀性图（标准位置方框图1/10）
	makePerioPicture(width, height, 10, 10, 25, backgroundColor, drawColor, TvDistortion_01, 131);

	////制作9点均匀性图（均匀分布9点方框图）
	//makePerioPicture(width, height, 3, 3, 25, backgroundColor, drawColor, TvDistortion_01, 132);

	////制作9点均匀性图（均匀分布9点方框图）2/10
	/*makePerioPicture(width, height, 10, 10, 25, backgroundColor, drawColor, TvDistortion_01, 133);*/


	//makePerioPicture(width, height, 10, 10, 25, backgroundColor, drawColor, TvDistortion_01, 18);
	//makePerioPicture(width, height, 10, 10, 5, TvDistortion_01, 13);
	//制作光学畸变图卡
	//makePerioPicture(width, height, 10, 10, 3, OptDistortion_01, 11);
	//制作SFR测试图卡,0,0.3,0.6,0.7，尺寸为100pixel，倾斜7°，白色背景黑色斜方块图卡/宝马标图卡,0为斜方块
	//makeSFRpic(SFR_01, width, height, 0.7, 0.7, 100, 7, 1, 1);
	//制作SFR测试图卡,0,0.3,0.6,0.7，尺寸为100pixel，倾斜7°，白色背景黑色斜方块图卡/宝马标图卡，1为宝马标
	//makeSFRpic(SFR_02, width, height, 0.7, 0.7, 100, 7, 0, 1);
	
	//制作双目合像测试图卡，线宽为3，线的位置等分	
	makePerioPicture(width, height, 3, 3, 2, backgroundColor, drawColor, binocularFusion_01, 20);
	
	//制作双目融合测试图卡，线宽为2，线的位置不是等分位置	
	makeBinocularFusionChart(width, height, 250, 130, 2, backgroundColor, drawColor, binocularFusion_02, 0);
	//makeBinocularFusionChart(width, height, 200, 120, 2, backgroundColor, drawColor, binocularFusion_02, 0);
	//makeBinocularFusionChart(width, height, 240, 180, 3, backgroundColor, drawColor, binocularFusion_02, 0);
	
	//制作VID测试图卡
	//makePerioPicture(width, height, 5, 5, 1, VID_01, 10);

	/*makePerioPicture(width, height, 5, 5, 1, VID_02, 10);*/
	//点阵图，点间距4，点边长为2
	makeVidPointChart(width, height, 0.5, 0.5, 4, 4, 2, backgroundColor, drawColor, VID_00, 0);

	//水平线对图，线宽为2
	makeVidPointChart(width, height, 0.5, 0.5, 4, 4, 2, backgroundColor, drawColor, VID_01, 1);
	
	//垂直线对图，线宽为2
	makeVidPointChart(width, height, 0.5, 0.5, 4, 4, 2, backgroundColor, drawColor, VID_02, 2);
	//
	//网格图
	//makeVidPointChart(width, height, 0.5, 0.5, 4, 4, 2, backgroundColor, drawColor, VID_03, 3);

	//棋盘格图，棋盘格长边短边长度为20pixel
	makeVidPointChart(width, height, 0.5, 0.5, 20, 20, 2, backgroundColor, drawColor, VID_04, 4);

	//制作不同视场十字对位图卡200,200
	double fieldx1[] = { 0};
	double fieldy1[] = { 0};
	double xRadio = 0.5;
	double yRadio = 0.5;
	double xwidth = width * xRadio;
	double yheight = height * yRadio;
	//长宽各200个pixel
	makeCrossLineChart(width, height, fieldx1, fieldy1, 640, 480, 1, backgroundColor, drawColor, crossline_01, 0);
	/*makeCrossLineChart(width, height, fieldx1, fieldy1, 400, 400, 1, backgroundColor, drawColor, crossline_01, 0);*/

	/*makeCrossLineChart(width, height, fieldx1, fieldy1, xwidth, yheight, 2, backgroundColor, drawColor, crossline_01, 0);*/

	return 0;
}