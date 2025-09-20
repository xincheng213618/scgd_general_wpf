//#include "function.h"
//
//
//int makeArTestPic(string fold, int width, int height, int pictureFLag)
//{
//	//确定生成图像文件名string类型
//	string Widthstr = to_string(width);
//	string heightstr = to_string(height);
//	//> create a new file nemed NewFileName on the current file path
//	// 
//	//bool flag = CreateDirectory(saveCompareRaw.c_str(), NULL);
//
//	//string filePath = "OldFilePath\\"; //> current file path
//	//string saveCompareRaw = filePath + "NewFileName";
//	////> create a new file nemed NewFileName on the current file path
//	//bool flag = CreateDirectory(saveCompareRaw.c_str(), NULL);
//
//	//LPCWSTR dirName = L"C:\\Users\\97979\\Desktop\\makepicture\\1920x1080"; 
//	//	// 创建目录
//	//	if (CreateDirectory(dirName, NULL)) {
//	//		std::wcout << L"目录创建成功: " << dirName << std::endl;
//	//	}
//	//	else {
//	//		std::wcerr << L"目录创建失败: " << GetLastError() << std::endl;
//	//	}
//
//		//system("md C:\\Users\\97979\\Desktop\\makepicture\\1920x1920");//创建文件夹
//
//
//	//system(filefolder.c_str());//std::string转化为const char*的另一种方法
//
//	//使用mkdir新建文件夹
//	string filefolder = fold + Widthstr + "x" + heightstr + "TestPicture";
//	const char* path = filefolder.c_str();
//	_mkdir(path);
//
//	/*_mkdir(strcat(path, name)*/
//
//
//	//纯色图
//	string filename = filefolder + "\\" + Widthstr + "x" + heightstr + "_pure_";
//	string w255 = filename + "white_255.bmp";
//	string w128 = filename + "w128.bmp";
//	string w64 = filename + "w64.bmp";
//	string w32 = filename + "w32.bmp";
//	string w16 = filename + "w16.bmp";
//	string w3 = filename + "w3.bmp";
//	string BK = filename + "BK.bmp";
//	string R = filename + "R.bmp";
//	string G = filename + "G.bmp";
//	string B = filename + "B.bmp";
//
//	//MTF图
//	string Hori_MTF = filefolder + "\\" + Widthstr + "x" + heightstr + "hor_line_";
//	string hori1 = Hori_MTF + "1.bmp";
//	string hori2 = Hori_MTF + "2.bmp";
//	string hori3 = Hori_MTF + "3.bmp";
//	string hori4 = Hori_MTF + "4.bmp";
//
//
//	string Vert_MTF = filefolder + "\\" + Widthstr + "x" + heightstr + "ver_line_";
//	string Vert1 = Vert_MTF + "1.bmp";
//	string Vert2 = Vert_MTF + "2.bmp";
//	string Vert3 = Vert_MTF + "3.bmp";
//	string Vert4 = Vert_MTF + "4.bmp";
//
//	string CameraChart = filefolder + "\\" + Widthstr + "x" + heightstr;
//	string ANSI_Constrast0 = CameraChart + "_4x4_ANSI_Constrast_BK.bmp";
//	string ANSI_Constrast1 = CameraChart + "_4x4_ANSI_Constrast_W.bmp";
//
//	//TV畸变图	
//	string TvDistortion01 = CameraChart + "TVDistortionChart.bmp";
//	//光学畸变图
//	string OptDistortion01 = CameraChart + "OptDistortionChart.bmp";
//	//VR鬼影图
//	string 	VRghost01 = CameraChart + "VRghostChart.bmp";
//	string VRghost02 = CameraChart + "VRghostCircleChart.bmp";
//	//AR鬼影图
//	string 	ARghost01 = CameraChart + "ARghostChart.bmp";
//	//SFR斜方块图
//	string SFR01 = CameraChart + "F0.7_斜方块5°_SFR_Chart.bmp";
//	//SFR宝马标图
//	string SFR02 = CameraChart + "F0.7_宝马标5°_SFR_Chart.bmp";
//	//双目合像图卡
//	string  binocularFusion01 = CameraChart + "binocularFusionChart.bmp";
//	//VID测试图卡
//	string  VID01 = CameraChart + "point_VID_01.bmp";
//
//	//string转const char*
//	const char* W_255 = w255.c_str();
//	const char* w_128 = w128.c_str();
//	const char* w_64 = w64.c_str();
//	const char* w_32 = w32.c_str();
//	const char* w_16 = w16.c_str();
//	const char* w_3 = w3.c_str();
//	const char* BK_0 = BK.c_str();
//	const char* R_255 = R.c_str();
//	const char* G_255 = G.c_str();
//	const char* B_255 = B.c_str();//  使用c_str()获取C风格字符串
//	const char* hori_1 = hori1.c_str();
//	const char* hori_2 = hori2.c_str();
//	const char* hori_3 = hori3.c_str();
//	const char* hori_4 = hori4.c_str();
//	const char* Vert_1 = Vert1.c_str();
//	const char* Vert_2 = Vert2.c_str();
//	const char* Vert_3 = Vert3.c_str();
//	const char* Vert_4 = Vert4.c_str();
//	const char* ANSI_Constrast_0 = ANSI_Constrast0.c_str();
//	const char* ANSI_Constrast_1 = ANSI_Constrast1.c_str();
//	const char* TvDistortion_01 = TvDistortion01.c_str();
//	const char* OptDistortion_01 = OptDistortion01.c_str();
//	const char* VRghost_01 = VRghost01.c_str();
//	const char* VRghost_02 = VRghost02.c_str();
//	const char* ARghost_01 = ARghost01.c_str();
//	const char* SFR_01 = SFR01.c_str();
//	const char* SFR_02 = SFR02.c_str();
//	const char* binocularFusion_01 = binocularFusion01.c_str();
//	const char* VID_01 = VID01.c_str();
//
//
//
//	//const char* W_255 = "C:\\Users\\97979\\Desktop\\makepicture\\1.bmp";
//	//const char* w_128 = "C:\\Users\\97979\\Desktop\\makepicture\\2.bmp";
//	//const char* w_64 = "C:\\Users\\97979\\Desktop\\makepicture\\3.bmp";
//	//const char* w_32 = "C:\\Users\\97979\\Desktop\\makepicture\\4.bmp";
//	//const char* w_16 = "C:\\Users\\97979\\Desktop\\makepicture\\5.bmp";
//	//const char* w_3 = "C:\\Users\\97979\\Desktop\\makepicture\\6.bmp";
//	//const char* BK_0 = "C:\\Users\\97979\\Desktop\\makepicture\\7.bmp";
//	//const char* R_255 = "C:\\Users\\97979\\Desktop\\makepicture\\8.bmp";
//	//const char* G_255 = "C:\\Users\\97979\\Desktop\\makepicture\\9.bmp";
//	//const char* B_255 = "C:\\Users\\97979\\Desktop\\makepicture\\10.bmp";
//	//const char* hor_MTF_1 = "C:\\Users\\97979\\Desktop\\makepicture\\11.bmp";
//	//const char* hor_MTF_2 = "C:\\Users\\97979\\Desktop\\makepicture\\12.bmp";
//	//const char* hor_MTF_3 = "C:\\Users\\97979\\Desktop\\makepicture\\13.bmp";
//	//const char* hor_MTF_4 = "C:\\Users\\97979\\Desktop\\makepicture\\14.bmp";
//	//const char* ver_MTF_1 = "C:\\Users\\97979\\Desktop\\makepicture\\15.bmp";
//	//const char* ver_MTF_2 = "C:\\Users\\97979\\Desktop\\makepicture\\16.bmp";
//	//const char* ver_MTF_3 = "C:\\Users\\97979\\Desktop\\makepicture\\17.bmp";
//	//const char* ver_MTF_4 = "C:\\Users\\97979\\Desktop\\makepicture\\18.bmp";
//	//const char* ghost_1 = "C:\\Users\\97979\\Desktop\\makepicture\\19.bmp";
//	//const char* vid_1 = "C:\\Users\\97979\\Desktop\\makepicture\\20.bmp";
//	//const char* binocular = "C:\\Users\\97979\\Desktop\\makepicture\\21.bmp";
//	//const char* chessboard = "C:\\Users\\97979\\Desktop\\makepicture\\22.bmp";
//	//const char* optiDistortion = "C:\\Users\\97979\\Desktop\\makepicture\\23.bmp";
//	//const char* TVDistortion = "C:\\Users\\97979\\Desktop\\makepicture\\24.bmp";
//	//const char* FOV = "C:\\Users\\97979\\Desktop\\makepicture\\25.bmp";
//
//	//000：制作纯白图片		
//	makePurePic(width, height, 0, 0, 0, W_255, 000);
//	//001：制作纯灰128图片		
//	makePurePic(width, height, 0, 0, 0, w_128, 001);
//	//002：制作纯灰64图片		
//	makePurePic(width, height, 0, 0, 0, w_64, 002);
//	//003：制作纯灰32图片		
//	makePurePic(width, height, 0, 0, 0, w_32, 003);
//	//004：制作纯灰16图片		
//	makePurePic(width, height, 0, 0, 0, w_16, 004);
//	//005：制作灰阶L3图片		
//	makePurePic(width, height, 0, 0, 0, w_3, 005);
//	//006：制作纯黑图片		
//	makePurePic(width, height, 0, 0, 0, BK_0, 006);
//	//100：制作纯红图片		
//	makePurePic(width, height, 0, 0, 0, R_255, 100);
//	//200：制作纯绿图片		
//	makePurePic(width, height, 0, 0, 0, G_255, 200);
//	//300：制作纯蓝图片
//	makePurePic(width, height, 0, 0, 0, B_255, 300);
//
//	double backgroundColor[] = { 0,0,0 };
//	double drawColor[] = { 255,255,255 };
//	//制作MTF线对测试图卡
//	makePerioPicture(width, height, 3, 3, 1, backgroundColor, drawColor, hori_1, 30);
//	makePerioPicture(width, height, 3, 3, 2, backgroundColor, drawColor, hori_2, 30);
//	makePerioPicture(width, height, 3, 3, 3, backgroundColor, drawColor, hori_3, 30);
//	makePerioPicture(width, height, 3, 3, 4, backgroundColor, drawColor, hori_4, 30);
//	makePerioPicture(width, height, 3, 3, 1, backgroundColor, drawColor, hori_1, 31);
//	makePerioPicture(width, height, 3, 3, 2, backgroundColor, drawColor, hori_2, 31);
//	makePerioPicture(width, height, 3, 3, 3, backgroundColor, drawColor, hori_3, 31);
//	makePerioPicture(width, height, 3, 3, 4, backgroundColor, drawColor, hori_4, 31);
//
//	//制作棋盘格对比度图（黑白黑白）
//	makePerioPicture(width, height, 4, 4, 4, backgroundColor, drawColor, ANSI_Constrast_0, 00);
//	//制作棋盘格对比度图（白黑白黑）
//	makePerioPicture(width, height, 4, 4, 4, backgroundColor, drawColor, ANSI_Constrast_1, 01);
//	//制作AEr鬼影图卡,0，0.3,0.5,0.7,0.9视场，点的半径为10个pixel
//	//makePerioPicture(width, height, 10, 10, 20, VRghost_01, 14);
//	//制作vr鬼影图片，圆环图片
//	makeGhostCicle(width, height, 200, 220, VRghost_02, 0);
//	//制作ar鬼影图卡,1/10位置处，点的半径为40个pixel
//	/*makePerioPicture(width, height, 10, 10, 20, ARghost_01, 13);	*/
//	//制作Ar边缘视场9点TV畸变图卡，点的半径为20个pixel
//
//	int pointRadiu = 20;
//	make_9point_DistortionChart(width, height, pointRadiu, backgroundColor, drawColor, TvDistortion_01, 0);
//	//makePerioPicture(width, height, 10, 10, 20, TvDistortion_01, 13);
//	////制作光学畸变图卡
//	//makePerioPicture(width, height, 10, 10, 3, OptDistortion_01, 11);
//	////制作SFR测试图卡,0,0.3,0.6,0.7，尺寸为100pixel，倾斜7°，白色背景黑色斜方块图卡/宝马标图卡,0为斜方块
//	//makeSFRpic(SFR_01, width, height, 0.7, 0.7, 100, 7, 1, 1);
//	////制作SFR测试图卡,0,0.3,0.6,0.7，尺寸为100pixel，倾斜7°，白色背景黑色斜方块图卡/宝马标图卡，1为宝马标
//	//makeSFRpic(SFR_02, width, height, 0.7, 0.7, 100, 7, 0, 1);
//	//制作双目合像测试图卡，线宽为3
//	makePerioPicture(width, height, 3, 3, 3, backgroundColor, drawColor, binocularFusion_01, 20);
//	//制作VID测试图卡
//	/*makePerioPicture(width, height, 5, 5, 1, VID_01, 10);*/
//	//制作十字对位图卡
//	return 0;
//}