#include "function.h"


int makeArTestPic(string fold, int width, int height, int pictureFLag)
{
	//���ñ���ɫ�����û�ͼɫ
	double backgroundColor[] = { 0,0,0};
	double drawColor[] = { 0,255,0 };

	//ȷ������ͼ���ļ���string����
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
	//	// ����Ŀ¼
	//	if (CreateDirectory(dirName, NULL)) {
	//		std::wcout << L"Ŀ¼�����ɹ�: " << dirName << std::endl;
	//	}
	//	else {
	//		std::wcerr << L"Ŀ¼����ʧ��: " << GetLastError() << std::endl;
	//	}

		//system("md C:\\Users\\97979\\Desktop\\makepicture\\1920x1920");//�����ļ���


	//system(filefolder.c_str());//std::stringת��Ϊconst char*����һ�ַ���

	//ʹ��mkdir�½��ļ���
	string filefolder = fold + Widthstr + "x" + heightstr + "TestPicture";
	const char* path = filefolder.c_str();
	_mkdir(path);

	/*_mkdir(strcat(path, name)*/	

	//��ɫͼ
	string filename = filefolder + "\\" + Widthstr + "x" + heightstr + "_pure_";
	//bmpͼ
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
	//jpgͼ
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
	////bmpͼ
	string ANSI_Constrast0 = CameraChart + "_4x4_ANSI_Constrast_BK.bmp";
	string ANSI_Constrast1 = CameraChart + "_4x4_ANSI_Constrast_W.bmp";
	//jpgͼ
	//string ANSI_Constrast0 = CameraChart + "_4x4_ANSI_Constrast_BK.jpg";
	//string ANSI_Constrast1 = CameraChart + "_4x4_ANSI_Constrast_W.jpg";



	//ˮƽ��ֱMTFͼ
	string Hori_MTF = filefolder + "\\" + Widthstr + "x" + heightstr + "hor_line_";
	//bmpͼ
	string hori1 = Hori_MTF + "1.bmp";
	string hori2 = Hori_MTF + "2.bmp";
	string hori3 = Hori_MTF + "3.bmp";
	string hori4 = Hori_MTF + "4.bmp";
	string hori5 = Hori_MTF + "5.bmp";
	//jpgͼ
	//string hori1 = Hori_MTF + "1.jpg";
	//string hori2 = Hori_MTF + "2.jpg";
	//string hori3 = Hori_MTF + "3.jpg";
	//string hori4 = Hori_MTF + "4.jpg";




	//��ֱMTF
	string Vert_MTF = filefolder + "\\" + Widthstr + "x" + heightstr + "ver_line_";

	//bmpͼ
	string Vert1 = Vert_MTF + "1.bmp";
	string Vert2 = Vert_MTF + "2.bmp";
	string Vert3 = Vert_MTF + "3.bmp";
	string Vert4 = Vert_MTF + "4.bmp";
	string Vert5 = Vert_MTF + "5.bmp";
	////jpgͼ
	//string Vert1 = Vert_MTF + "1.jpg";
	//string Vert2 = Vert_MTF + "2.jpg";
	//string Vert3 = Vert_MTF + "3.jpg";
	//string Vert4 = Vert_MTF + "4.jpg";



	//�ض��ӽ�ˮƽ��ֱ����MTFͼ��
	//bmpͼ
	string MTF_fourline1 = CameraChart + "_MTF_fourline1_Chart.bmp";
	string MTF_fourline2 = CameraChart + "_MTF_fourline2_Chart.bmp";
	string MTF_fourline3 = CameraChart + "_MTF_fourline3_Chart.bmp";
	string MTF_fourline4 = CameraChart + "_MTF_fourline4_Chart.bmp";
	//string MTF_fourline5 = CameraChart + "_MTF_fourline5_Chart.bmp";

	//jpgͼ
	//string MTF_fourline1 = CameraChart + "_MTF_fourline1_Chart.jpg";
	//string MTF_fourline2 = CameraChart + "_MTF_fourline2_Chart.jpg";
	//string MTF_fourline3 = CameraChart + "_MTF_fourline3_Chart.jpg";
	//string MTF_fourline4 = CameraChart + "_MTF_fourline4_Chart.jpg";


	//TV����ͼ	
	//bmpͼ

	string TvDistortion00 = CameraChart + "TVDistortionChart_00.bmp";
	string TvDistortion01 = CameraChart + "TVDistortionChart_01.bmp";

	//jpgͼ
	//string TvDistortion00 = CameraChart + "TVDistortionChart_00.jpg";
	//string TvDistortion01 = CameraChart + "TVDistortionChart_01.jpg";



	//��ѧ����ͼ
	//bmpͼ
	string OptDistortion01 = CameraChart + "OptDistortionChart.bmp";
	//jpgͼ
	//string OptDistortion01 = CameraChart + "OptDistortionChart.jpg";


	//VR��Ӱͼ
	//bmpͼ
	string 	VRghost01 = CameraChart + "VRghostChart.bmp";
	string VRghost02 = CameraChart + "VRghostCircleChart.bmp";
	////jpgͼ
	//string 	VRghost01 = CameraChart + "VRghostChart.jpg";
	//string VRghost02 = CameraChart + "VRghostCircleChart.jpg";

	//AR��Ӱͼ
	//bmpͼ
	string 	ARghost01 = CameraChart + "ARghostChart.bmp";
	string 	ARghost02 = CameraChart + "ARghostSquareChart.bmp";
	//jpgͼ
	//string 	ARghost01 = CameraChart + "ARghostChart.jpg";
	//string 	ARghost02 = CameraChart + "ARghostSquareChart.jpg";


	//SFRб����ͼ
	//bmpͼ
	string SFR01 = CameraChart + "F0.7_б����5��_SFR_Chart.bmp";
	//jpgͼ
	//string SFR01 = CameraChart + "F0.7_б����5��_SFR_Chart.jpg";

	//SFR�����ͼ
	//bmpͼ
	string SFR02 = CameraChart + "F0.7_�����5��_SFR_Chart.bmp";
	//jpgͼ
	//string SFR02 = CameraChart + "F0.7_�����5��_SFR_Chart.jpg";
	//˫Ŀ����ͼ��
	//bmpͼ

	string  binocularFusion01 = CameraChart + "binocularFusionChart1.bmp";
	string  binocularFusion02 = CameraChart + "binocularFusionChart2.bmp";
	//jpgͼ
	//string  binocularFusion01 = CameraChart + "binocularFusionChart1.jpg";
	//string  binocularFusion02 = CameraChart + "binocularFusionChart2.jpg";

	//VID����ͼ��
	//bmpͼ
	string  VID00 = CameraChart + "point_VID_00.bmp";
	string  VID01 = CameraChart + "point_VID_01.bmp";
	string  VID02 = CameraChart + "point_VID_02.bmp";
	string  VID03 = CameraChart + "point_VID_03.bmp";
	string  VID04 = CameraChart + "point_VID_04.bmp";
	//jpgͼ
	//string  VID00 = CameraChart + "point_VID_00.jpg";
	//string  VID01 = CameraChart + "point_VID_01.jpg";
	//string  VID02 = CameraChart + "point_VID_02.jpg";
	//string  VID03 = CameraChart + "point_VID_03.jpg";
	//string  VID04 = CameraChart + "point_VID_04.jpg";

	

	//��ͬ�ӳ�ʮ��ͼ��
	////bmpͼ
	string crossline01 = CameraChart + "corssline_01.bmp";

	//jpgͼ
	//string crossline01 = CameraChart + "corssline_01.jpg";

	//stringתconst char*
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
	const char* B_255 = B.c_str();//  ʹ��c_str()��ȡC����ַ���

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

	//000����������ͼƬ		
	makePurePic(width, height, 255, 255, 255, W_255, 000);
	//001����������128ͼƬ		
	makePurePic(width, height,128, 128, 128, w_128, 001);
	//002����������64ͼƬ		
	makePurePic(width, height, 64, 64, 64, w_64, 002);
	//003����������32ͼƬ		
	makePurePic(width, height,32, 32, 32, w_32, 003);
	//004����������16ͼƬ		
	makePurePic(width, height, 16, 16, 16, w_16, 004);
	//005�������ҽ�L3ͼƬ		
	makePurePic(width, height, 3, 3, 3, w_3, 005);
	//006����������ͼƬ		
	makePurePic(width, height, 0, 0, 0, BK_0, 006);
	//100����������ͼƬ		
	makePurePic(width, height, 0, 0, 255, R_255, 100);
	//200����������ͼƬ		
	/*makePurePic(width, height, 0, 0, 0, G_255, 200);*/
	makePurePic(width, height, 0, 255, 0, G_255, 1000);
	makePurePic(width, height, 0, 25, 0, G_25, 1000);
	makePurePic(width, height, 0, 51, 0, G_51, 1000);
	//300����������ͼƬ
	makePurePic(width, height, 255, 0, 0, B_255, 300);

	//����MTF�߶Բ���ͼ��
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

	//�����ı�MTFͼ��
	double fieldx[] = { 0,0.4,0.8 };
	double fieldy[] = { 0,0.4,0.8 };
	//�߳�Ϊ22
	/*makeGoerTekMTF_Chart(width,height,fieldx, fieldy, 22, 1, backgroundColor, drawColor, MTF_fourline_1, 0);
	makeGoerTekMTF_Chart(width, height, fieldx, fieldy, 22, 2, backgroundColor, drawColor, MTF_fourline_2, 0);
	makeGoerTekMTF_Chart(width, height, fieldx, fieldy, 22, 3, backgroundColor, drawColor, MTF_fourline_3, 0);
	makeGoerTekMTF_Chart(width, height, fieldx, fieldy, 22, 4, backgroundColor, drawColor, MTF_fourline_4, 0);*/
	//makeGoerTekMTF_Chart(width, height, fieldx, fieldy, 50, 5, backgroundColor, drawColor, MTF_fourline_5, 0);


	//�߳�Ϊ50
	makeGoerTekMTF_Chart(width, height, fieldx, fieldy, 25, 1, backgroundColor, drawColor, MTF_fourline_1, 0);
	makeGoerTekMTF_Chart(width, height, fieldx, fieldy, 25, 2, backgroundColor, drawColor, MTF_fourline_2, 0);
	makeGoerTekMTF_Chart(width, height, fieldx, fieldy, 25, 3, backgroundColor, drawColor, MTF_fourline_3, 0);
	makeGoerTekMTF_Chart(width, height, fieldx, fieldy, 25, 4, backgroundColor, drawColor, MTF_fourline_4, 0);
	//makeGoerTekMTF_Chart(width, height, fieldx, fieldy, 50, 5, backgroundColor, drawColor, MTF_fourline_5, 0);

	//�������̸�Աȶ�ͼ���ڰ׺ڰף�
	makePerioPicture(width, height, 4, 4, 4, backgroundColor, drawColor, ANSI_Constrast_0, 00);	
	//�������̸�Աȶ�ͼ���׺ڰ׺ڣ�
	makePerioPicture(width, height, 4, 4, 4, backgroundColor, drawColor,ANSI_Constrast_1, 01);
	//����Ar��Ӱͼ��,0��0.3,0.5,0.7,0.9�ӳ�����İ뾶Ϊ10��pixel
	//makePerioPicture(width, height, 10, 10, 20, VRghost_01, 14);
	//����vr��ӰͼƬ��Բ��ͼƬ
	makeGhostCicle(width, height, 130, 140, backgroundColor, drawColor, VRghost_02, 0);
	//����ar��Ӱͼ��,1/10λ�ô�����İ뾶Ϊ40��pixel
	/*makePerioPicture(width, height, 10, 10, 20, ARghost_01, 13);	*/
	//����ar��Ӱͼ����������ɫ���м䳤��Ϊ200�����Ϊ150pixe�Ĵ�ɫ��30*30,320*240
	makeGhostSquare(width, height, 320, 240, backgroundColor, drawColor, ARghost_02, 0);	
	//����Ar��Ե�ӳ�9��TV����ͼ������İ뾶Ϊ20��pixel
	int pointRadiu = 4;
	make_9point_DistortionChart(width, height, pointRadiu, backgroundColor, drawColor, TvDistortion_00, 0);	
	//����9�����ͼ��flagΪ1�����ض������9��ͼ
	/*make_9point_DistortionChart(width, height, pointRadiu, backgroundColor, drawColor, TvDistortion_01, 1);*/

	////����9�������ͼ����׼λ�÷���ͼ1/10��
	makePerioPicture(width, height, 10, 10, 25, backgroundColor, drawColor, TvDistortion_01, 131);

	////����9�������ͼ�����ȷֲ�9�㷽��ͼ��
	//makePerioPicture(width, height, 3, 3, 25, backgroundColor, drawColor, TvDistortion_01, 132);

	////����9�������ͼ�����ȷֲ�9�㷽��ͼ��2/10
	/*makePerioPicture(width, height, 10, 10, 25, backgroundColor, drawColor, TvDistortion_01, 133);*/


	//makePerioPicture(width, height, 10, 10, 25, backgroundColor, drawColor, TvDistortion_01, 18);
	//makePerioPicture(width, height, 10, 10, 5, TvDistortion_01, 13);
	//������ѧ����ͼ��
	//makePerioPicture(width, height, 10, 10, 3, OptDistortion_01, 11);
	//����SFR����ͼ��,0,0.3,0.6,0.7���ߴ�Ϊ100pixel����б7�㣬��ɫ������ɫб����ͼ��/�����ͼ��,0Ϊб����
	//makeSFRpic(SFR_01, width, height, 0.7, 0.7, 100, 7, 1, 1);
	//����SFR����ͼ��,0,0.3,0.6,0.7���ߴ�Ϊ100pixel����б7�㣬��ɫ������ɫб����ͼ��/�����ͼ����1Ϊ�����
	//makeSFRpic(SFR_02, width, height, 0.7, 0.7, 100, 7, 0, 1);
	
	//����˫Ŀ�������ͼ�����߿�Ϊ3���ߵ�λ�õȷ�	
	makePerioPicture(width, height, 3, 3, 2, backgroundColor, drawColor, binocularFusion_01, 20);
	
	//����˫Ŀ�ںϲ���ͼ�����߿�Ϊ2���ߵ�λ�ò��ǵȷ�λ��	
	makeBinocularFusionChart(width, height, 250, 130, 2, backgroundColor, drawColor, binocularFusion_02, 0);
	//makeBinocularFusionChart(width, height, 200, 120, 2, backgroundColor, drawColor, binocularFusion_02, 0);
	//makeBinocularFusionChart(width, height, 240, 180, 3, backgroundColor, drawColor, binocularFusion_02, 0);
	
	//����VID����ͼ��
	//makePerioPicture(width, height, 5, 5, 1, VID_01, 10);

	/*makePerioPicture(width, height, 5, 5, 1, VID_02, 10);*/
	//����ͼ������4����߳�Ϊ2
	makeVidPointChart(width, height, 0.5, 0.5, 4, 4, 2, backgroundColor, drawColor, VID_00, 0);

	//ˮƽ�߶�ͼ���߿�Ϊ2
	makeVidPointChart(width, height, 0.5, 0.5, 4, 4, 2, backgroundColor, drawColor, VID_01, 1);
	
	//��ֱ�߶�ͼ���߿�Ϊ2
	makeVidPointChart(width, height, 0.5, 0.5, 4, 4, 2, backgroundColor, drawColor, VID_02, 2);
	//
	//����ͼ
	//makeVidPointChart(width, height, 0.5, 0.5, 4, 4, 2, backgroundColor, drawColor, VID_03, 3);

	//���̸�ͼ�����̸񳤱߶̱߳���Ϊ20pixel
	makeVidPointChart(width, height, 0.5, 0.5, 20, 20, 2, backgroundColor, drawColor, VID_04, 4);

	//������ͬ�ӳ�ʮ�ֶ�λͼ��200,200
	double fieldx1[] = { 0};
	double fieldy1[] = { 0};
	double xRadio = 0.5;
	double yRadio = 0.5;
	double xwidth = width * xRadio;
	double yheight = height * yRadio;
	//�����200��pixel
	makeCrossLineChart(width, height, fieldx1, fieldy1, 640, 480, 1, backgroundColor, drawColor, crossline_01, 0);
	/*makeCrossLineChart(width, height, fieldx1, fieldy1, 400, 400, 1, backgroundColor, drawColor, crossline_01, 0);*/

	/*makeCrossLineChart(width, height, fieldx1, fieldy1, xwidth, yheight, 2, backgroundColor, drawColor, crossline_01, 0);*/

	return 0;
}