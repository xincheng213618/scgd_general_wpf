using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using cvColorVision;
using Newtonsoft.Json.Linq;
using StructTestN;

namespace CsharpDEMO
{


    class DemoType
    {
        public void DoTestDEMO()
        {
			

			cvCameraCSLib.DeviceOnline_CallBack deviceMonitor = new cvCameraCSLib.DeviceOnline_CallBack(CallBackDeviceOnLine);


            IntPtr pp = new IntPtr(11);

            //初始化dll，这个函数在最先必须调且只能调一次！！
            cvCameraCSLib.InitResource(deviceMonitor, pp);




            //建立相机句柄
            IntPtr camHandle = cvCameraCSLib.CM_CreatCameraManagerSimple("..\\dcf\\CV2000Demo2Nocali.dcf");

            if (camHandle == IntPtr.Zero)
            {
                Console.WriteLine("创建句柄失败！请查看log");
                return;
            }
            cvCameraCSLib.CM_SetCaliFilePath(camHandle, "..\\cfg\\");

            //设置转轮COM口，针对通过COM口控制转轮的设备，其它类型设备无需调用或者随便传参进去
            int success = cvCameraCSLib.CM_SetPortComSimple(camHandle, "COM3");

            //获取矫正组的个数
            int caliGroupNum = cvCameraCSLib.CM_GetCalibrationGroupNum(camHandle);

            //用于存储所有矫正title
            string[] caliGroupTitles = null;

            if (caliGroupNum > 0)
            {
                caliGroupTitles = new string[caliGroupNum];

                IntPtr[] pStr = new IntPtr[caliGroupNum];
                for (int i = 0; i < caliGroupNum; i++)
                {
                    pStr[i] = Marshal.AllocHGlobal(64);
                }

                //获取所有矫正title
                cvCameraCSLib.CM_GetAllCalibrationGroupTitles(camHandle, pStr);

                for (int i = 0; i < caliGroupNum; i++)
                {
                    caliGroupTitles[i] = Marshal.PtrToStringAnsi(pStr[i]);

                    //打印每组矫正title，示例而已
                    Console.WriteLine(caliGroupTitles[i]);

                    Marshal.FreeHGlobal(pStr[i]);
                }

                //这里选择第一组矫正Title
                cvCameraCSLib.CM_ChooseCalibrationTitle(camHandle, caliGroupTitles[0]);

                StringBuilder sBuff = new StringBuilder(2500);
                //获取这组矫正的矫正配置信息
                cvCameraCSLib.CM_GetCaliGropupItems(camHandle, caliGroupTitles[0], sBuff);
            }


            //  cvCameraCSLib.CM_ResetEx(camHandle, 5000);

            //打开相机,这一步程序会检查license
            success = cvCameraCSLib.CM_OpenSimple(camHandle);

            if (success != 1)
            {
                //若失败，打印失败原因
                int strLength = 256;
                StringBuilder builder = new StringBuilder(256);
                cvCameraCSLib.CM_GetErrorMessage(success, builder, ref strLength);
                string errMessge = builder.ToString();
                Console.WriteLine(errMessge);

                return;
            }
            if (true)
            {
                int usb = 0;
                cvCameraCSLib.CM_GetComputerUsbType(camHandle, ref usb);
            }

            //激活所有矫正，一般无需调用，示例而已，因为默认就是激活状态
            // cvCameraCSLib.CM_EnableCali(camHandle, -1, true);

            //获取自动曝光时间，非必调函数，用于同一型号样品检测获取一个合适的曝光时间
            float expR = 0, expG = 0, expB = 0;
            //float[] saturation = new float[3];
            AutoExpJson jsonObj = new AutoExpJson();
            jsonObj.expTimeCfg.autoExpFlag = false;
            jsonObj.expTimeCfg.autoExpMaxPecentage = 0.01;
            jsonObj.expTimeCfg.autoExpSatDev = 20;
            jsonObj.expTimeCfg.autoExpSatMaxAD = 65535;
            jsonObj.expTimeCfg.autoExpSaturation = 70;
            jsonObj.expTimeCfg.autoExpSyncFreq = 60;
            jsonObj.expTimeCfg.autoExpTimeBegin = 20;
            jsonObj.expTimeCfg.minExpTime = 0.2f;
            jsonObj.expTimeCfg.maxExpTime = 60000;
            jsonObj.expTimeCfg.burstThreshold = 200;
            string autoExp = Newtonsoft.Json.JsonConvert.SerializeObject(jsonObj);
            success = cvCameraCSLib.CM_UpdateCfgJson(camHandle, ConfigType.Cfg_ExpTime, autoExp);

            //cvCameraCSLib.CM_GetAutoExpTimeSimple(camHandle, ref expR, ref expG, ref expB, saturation);

            //以下分别为图片长，宽，位数，通道数，等待获取
            uint w = 0, h = 0, srcbpp = 0, bpp = 32, channels = 0;

            //获取存储照片所需的内存长度
            cvCameraCSLib.CM_GetSrcFrameInfo(camHandle, ref w, ref h, ref srcbpp, ref channels);



            byte[] src = new byte[srcbpp / 8 * w * h * channels];           //灰度数据每个像素点占两字节
            byte[] imgdata = new byte[w * h * channels * 4];       //通道值数据每个像素点占四字节

			//设置曝光
			cvCameraCSLib.CM_SetExpTimeSimple(camHandle, 30, 300, 300);
			cvCameraCSLib.CM_LedCalInit(camHandle, "test_spacing2x2_wo_AOI_20240820.cfg");//调一次即可
			

            success = cvCameraCSLib.CM_GetFrameSimple(camHandle, ref w, ref h, ref srcbpp, ref bpp, ref channels, src, imgdata);
            cvCameraCSLib.CM_LedCalFind(camHandle, 0);

			success = cvCameraCSLib.CM_GetFrameSimple(camHandle, ref w, ref h, ref srcbpp, ref bpp, ref channels, src, imgdata);
			cvCameraCSLib.CM_LedCalFind(camHandle, 1);

			success = cvCameraCSLib.CM_GetFrameSimple(camHandle, ref w, ref h, ref srcbpp, ref bpp, ref channels, src, imgdata);
			cvCameraCSLib.CM_LedCalFind(camHandle, 2);

			success = cvCameraCSLib.CM_GetFrameSimple(camHandle, ref w, ref h, ref srcbpp, ref bpp, ref channels, src, imgdata);
			cvCameraCSLib.CM_LedCalFind(camHandle, 3);
			/*.......................间隔点亮灯珠，一直拍16次，此处省略！！！*/
			cvCameraCSLib.CM_LedCalFind(camHandle, 16);
			int comW = 0;
			int comH = 0;
            byte[]comData=new byte[w*h*4];
			cvCameraCSLib.CM_LedCalComBine(camHandle, 16,ref comW,ref comH, comData);

			OPTIC_DATA[] poLed = new OPTIC_DATA[2];
			poLed[0].shape = 0;
			poLed[0].px = 0;
			poLed[0].py = 0;
			poLed[0].w_radius = 0;

			poLed[1].shape = 0;
			poLed[1].px = 639;
			poLed[1].py = 479;
			poLed[1].w_radius = 0;
            //获取这两个点位灯珠的数据
			cvCameraCSLib.CM_GetOpticsData(camHandle, poLed);

			IRECT[] foucsRects = new IRECT[4];
            //设置计算的矩形区域,要根据实际图像挖区域
            foucsRects[0].setValue(400, 1140, 100, 200);
            foucsRects[1].setValue(1200, 660, 200, 100);
            foucsRects[2].setValue(2350, 1080, 100, 200);
            foucsRects[3].setValue(1260, 1930, 200, 100);

            float[] foucusR = new float[4];      //结果
            cvCameraCSLib.CalFocusLevelByEdgeSimple(camHandle, foucsRects, foucusR, 4, 11);

            //开始取图
            if (success == 1)
            {


                /*根据需求对数据进行操作,此处只是示例*/

                //获取关注坐标对应的光学数据，这里取的坐标分别是（400,300）和（1000,1000）两个点
                OPTIC_DATA[] po = new OPTIC_DATA[2];
                po[0].shape = 0;
                po[0].px = 400;
                po[0].py = 300;
                po[0].w_radius = 50;

                po[1].shape = 0;
                po[1].px = 1000;
                po[1].py = 1000;
                po[1].w_radius = 50;
                cvCameraCSLib.CM_GetOpticsData(camHandle, po);
                float ciex = po[0].cie_x;


                //存下原始图片
                cvCameraCSLib.CM_ExportToTIFF("..\\TIFF\\src.tif", w, h, srcbpp, channels, src);

                if (channels == 3)
                {

                    //存下光学刺激值X
                    cvCameraCSLib.CM_ExportToTIFF("..\\TIFF\\cie_X.tif", w, h, 32, 1, imgdata);


                    //存下光学刺激值Y,也就是亮度值Y
                    byte[] Y = new byte[w * h * 4];
                    Buffer.BlockCopy(imgdata, (int)(w * h * 4), Y, 0, (int)(w * h * 4));
                    cvCameraCSLib.CM_ExportToTIFF("..\\TIFF\\cie_Y.tif", w, h, 32, 1, Y);


                    //存下光学刺激值Z
                    byte[] Z = new byte[w * h * 4];
                    Buffer.BlockCopy(imgdata, (int)(w * h * 8), Z, 0, (int)(w * h * 4));
                    cvCameraCSLib.CM_ExportToTIFF("..\\TIFF\\cie_Z.tif", w, h, 32, 1, Z);
                }
                else
                {

                    //存下亮度值,此时只有亮度值
                    cvCameraCSLib.CM_ExportToTIFF("..\\TIFF\\lum.tif", w, h, 32, 1, imgdata);
                }

            }



            //关闭相机
            cvCameraCSLib.CM_Close(camHandle);

            //释放相机句柄
            cvCameraCSLib.CM_ReleaseCameraManagerSimple(camHandle);

            //释放dll资源，这个函数在最后调且只能调一次！！
            cvCameraCSLib.ReleaseResource();
        }


        public void videoMode()
        {

            int success = 0;

            cvCameraCSLib.DeviceOnline_CallBack deviceMonitor = new cvCameraCSLib.DeviceOnline_CallBack(CallBackDeviceOnLine);


            IntPtr pp = new IntPtr(11);

            //初始化dll
            cvCameraCSLib.InitResource(deviceMonitor, pp);


            //建立相机句柄
            IntPtr camHandle = cvCameraCSLib.CM_CreatCameraManagerSimple("cfg\\BV2000Ae-DEMO1.dcf");

            if (camHandle == IntPtr.Zero)
            {
                Console.WriteLine("创建句柄失败！请查看log");
                return;
            }

            //打开相机
            success = cvCameraCSLib.CM_OpenLiveSimple(camHandle);
            if (success != 1)
            {
                Console.WriteLine("打开视频模式失败！！");
                return;
            }

            //获取存储一帧数据的所需的字节长度
            uint w = 0, h = 0, srcbpp = 0;
            cvCameraCSLib.CM_GetSrcFrameInfo(camHandle, ref w, ref h, ref srcbpp, ref vedioChannels);



            vedioData = new byte[w * h * vedioChannels];


            //设置曝光
            cvCameraCSLib.CM_SetExpTimeSimple(camHandle, 200);

            //设置增益
            cvCameraCSLib.CM_SetGain(camHandle, 1);




            if (callback == null)
            {
                callback = new cvCameraCSLib.ProcCallBack(CallBackFunction);
            }

            //开启回调函数，此时vedioCallBack不断刷新数据
            cvCameraCSLib.CM_SetCallBack(camHandle, callback, IntPtr.Zero);

            //视频取图15秒
            Thread.Sleep(15000);


            //停止回调
            cvCameraCSLib.CM_UnregisterCallBack(camHandle);

            //关闭相机
            cvCameraCSLib.CM_Close(camHandle);

            //释放相机句柄
            cvCameraCSLib.CM_ReleaseCameraManagerSimple(camHandle);

            //释放dll资源
            cvCameraCSLib.ReleaseResource();

        }

        public void generateCali()
        {
            cvCameraCSLib.DeviceOnline_CallBack deviceMonitor = new cvCameraCSLib.DeviceOnline_CallBack(CallBackDeviceOnLine);


            IntPtr pp = new IntPtr(11);

            //初始化dll，这个函数在最先必须调且只能调一次！！
            cvCameraCSLib.InitResource(deviceMonitor, pp);




            //建立相机句柄
            IntPtr camHandle = cvCameraCSLib.CM_CreatCameraManagerSimple("dcf\\bv2000TestCaliSimple.dcf");

            if (camHandle == IntPtr.Zero)
            {
                Console.WriteLine("创建句柄失败！请查看log");
                Console.ReadKey();
                return;
            }




            //获取矫正组的个数
            int caliGroupNum = cvCameraCSLib.CM_GetCalibrationGroupNum(camHandle);

            //用于存储所有矫正title
            string[] caliGroupTitles = null;

            if (caliGroupNum > 0)
            {
                caliGroupTitles = new string[caliGroupNum];

                IntPtr[] pStr = new IntPtr[caliGroupNum];
                for (int i = 0; i < caliGroupNum; i++)
                {
                    pStr[i] = Marshal.AllocHGlobal(64);
                }

                //获取所有矫正title
                cvCameraCSLib.CM_GetAllCalibrationGroupTitles(camHandle, pStr);

                for (int i = 0; i < caliGroupNum; i++)
                {
                    caliGroupTitles[i] = Marshal.PtrToStringAnsi(pStr[i]);

                    //打印每组矫正title，示例而已
                    Console.WriteLine(caliGroupTitles[i]);

                    Marshal.FreeHGlobal(pStr[i]);
                }

                //这里选择第一组矫正Title,实际标定时要选择适配的组
                cvCameraCSLib.CM_ChooseCalibrationTitle(camHandle, caliGroupTitles[0]);

                StringBuilder sBuff = new StringBuilder(2500);
                //获取这组矫正的矫正配置信息
                cvCameraCSLib.CM_GetCaliGropupItems(camHandle, caliGroupTitles[0], sBuff);
            }


            //打开相机,这一步程序会检查license
            int success = cvCameraCSLib.CM_OpenSimple(camHandle);

            if (success != 1)
            {
                //若失败，打印失败原因
                int strLength = 256;
                StringBuilder builder = new StringBuilder(256);
                cvCameraCSLib.CM_GetErrorMessage(success, builder, ref strLength);
                string errMessge = builder.ToString();
                Console.WriteLine(errMessge);
                Console.ReadKey();
                return;
            }


            //获取自动曝光时间，非必调函数，用于同一型号样品检测获取一个合适的曝光时间
            float expR = 0, expG = 0, expB = 0;
            //float[] saturation = new float[3];



            //以下分别为图片长，宽，位数，通道数，等待获取
            uint w = 0, h = 0, srcbpp = 0, bpp = 32, channels = 0;

            //获取存储照片所需的内存长度
            cvCameraCSLib.CM_GetSrcFrameInfo(camHandle, ref w, ref h, ref srcbpp, ref channels);



            byte[] src = new byte[srcbpp / 8 * w * h * channels];           //灰度数据每个像素点占两字节
            byte[] imgdata = new byte[w * h * channels * 4];       //通道值数据每个像素点占四字节


            //8代表四色矫正，4这里随便设
            int caliType = 8;
            cvCameraCSLib.CM_InitialGenerateCali(camHandle, 8, 4);

            IRECT rect = new IRECT((int)w / 2, (int)h / 2, 40, 40);       //这里获取的是图像上挖取这个矩形区域作为标定参考区域

            float cie_x = 0;
            float cie_y = 0;
            float lum = 0;

            //拍摄红色,根据需要设置曝光
            Console.WriteLine("\n请输入设置的曝光时间ms:\n");
            string get = Console.ReadLine();
            float exp = float.Parse(get);
            cvCameraCSLib.CM_SetExpTimeSimple(camHandle, exp, exp, exp);
            success = cvCameraCSLib.CM_GetFrameSimple(camHandle, ref w, ref h, ref srcbpp, ref bpp, ref channels, src, imgdata);
            Console.WriteLine("\n请输入色坐标x:\n");
            get = Console.ReadLine();
            cie_x = float.Parse(get);      //这里的色坐标需要观察外部色度计来获取，以实际测量情况获取的数据填入！
            Console.WriteLine("\n请输入色坐标y:\n");
            get = Console.ReadLine();
            cie_y = float.Parse(get);
            success = cvCameraCSLib.CM_SetColorCaliData(camHandle, caliType, rect, 0, cie_x, cie_y, lum);

            //拍摄绿色,根据需要设置曝光
            Console.WriteLine("\n请输入设置的曝光时间ms:\n");
            get = Console.ReadLine();
            exp = float.Parse(get);
            cvCameraCSLib.CM_SetExpTimeSimple(camHandle, exp, exp, exp);
            success = cvCameraCSLib.CM_GetFrameSimple(camHandle, ref w, ref h, ref srcbpp, ref bpp, ref channels, src, imgdata);
            Console.WriteLine("\n请输入色坐标x:\n");
            get = Console.ReadLine();
            cie_x = float.Parse(get);      //这里的色坐标需要观察外部色度计来获取，以实际测量情况获取的数据填入！
            Console.WriteLine("\n请输入色坐标y:\n");
            get = Console.ReadLine();
            cie_y = float.Parse(get);

            success = cvCameraCSLib.CM_SetColorCaliData(camHandle, caliType, rect, 1, cie_x, cie_y, lum);

            //拍摄蓝色,根据需要设置曝光
            Console.WriteLine("\n请输入设置的曝光时间ms:\n");
            get = Console.ReadLine();
            exp = float.Parse(get);
            cvCameraCSLib.CM_SetExpTimeSimple(camHandle, exp, exp, exp);
            success = cvCameraCSLib.CM_GetFrameSimple(camHandle, ref w, ref h, ref srcbpp, ref bpp, ref channels, src, imgdata);
            Console.WriteLine("\n请输入色坐标x:\n");
            get = Console.ReadLine();
            cie_x = float.Parse(get);      //这里的色坐标需要观察外部色度计来获取，以实际测量情况获取的数据填入！
            Console.WriteLine("\n请输入色坐标y:\n");
            get = Console.ReadLine();
            cie_y = float.Parse(get);

            success = cvCameraCSLib.CM_SetColorCaliData(camHandle, caliType, rect, 2, cie_x, cie_y, lum);

            //拍摄白色,根据需要设置曝光，白色的part必须是3，其它颜色无所谓
            Console.WriteLine("\n请输入设置的曝光时间ms:\n");
            get = Console.ReadLine();
            exp = float.Parse(get);
            cvCameraCSLib.CM_SetExpTimeSimple(camHandle, exp, exp, exp);
            success = cvCameraCSLib.CM_GetFrameSimple(camHandle, ref w, ref h, ref srcbpp, ref bpp, ref channels, src, imgdata);
            Console.WriteLine("\n请输入色坐标x:\n");
            get = Console.ReadLine();
            cie_x = float.Parse(get);      //这里的色坐标需要观察外部色度计来获取，以实际测量情况获取的数据填入！
            Console.WriteLine("\n请输入色坐标y:\n");
            get = Console.ReadLine();
            cie_y = float.Parse(get);

            Console.WriteLine("\n请输入亮度值:\n");
            get = Console.ReadLine();
            lum = float.Parse(get);          //切记，只有白色必须要填入色度计测量的亮度数据

            success = cvCameraCSLib.CM_SetColorCaliData(camHandle, caliType, rect, 3, cie_x, cie_y, lum);

            //生成矫正文件保存
            success = cvCameraCSLib.CM_SaveCaliFile(camHandle, caliType, "4colorSimpleTest.dat");






            //关闭相机
            cvCameraCSLib.CM_Close(camHandle);

            //释放相机句柄
            cvCameraCSLib.CM_ReleaseCameraManagerSimple(camHandle);

            //释放dll资源，这个函数在最后调且只能调一次！！
            cvCameraCSLib.ReleaseResource();
            Console.WriteLine("完成！按任意键退出");
            Console.ReadKey();

        }

        public void testMotor()
        {
            logDebug.logCreatEx();
			autoFocusCfg atuoCfg = new autoFocusCfg();

			string cfgFn = "cfg\\autoFocusParameters.cfg";
			FileStream fs = new FileStream(cfgFn, FileMode.Open, FileAccess.Read);
			if (!fs.CanRead)
			{
                Console.WriteLine($"读取{cfgFn}失败！");
				return ;
			}
			BinaryReader binaryReader = new BinaryReader(fs);
            byte[]fsData= binaryReader.ReadBytes((int)fs.Length);
			string jsonString= Encoding.UTF8.GetString(fsData);
			atuoCfg = Newtonsoft.Json.JsonConvert.DeserializeObject<autoFocusCfg>(jsonString);
			cvCameraCSLib.DeviceOnline_CallBack deviceMonitor = new cvCameraCSLib.DeviceOnline_CallBack(CallBackDeviceOnLine);


            IntPtr pp = new IntPtr(11);

            //初始化dll，这个函数在最先必须调且只能调一次！
            cvCameraCSLib.InitResource(deviceMonitor, pp);
          
            //建立相机句柄
            IntPtr camHandle = cvCameraCSLib.CM_CreatCameraManagerSimple(atuoCfg.dcf);

            if (camHandle == IntPtr.Zero)
            {
                Console.WriteLine("创建句柄失败！请查看log");
                //return;
            }

            //设置镜头COM口,查看设备管理器来设置
            int success = cvCameraCSLib.CM_SetComSimple(camHandle, 1, atuoCfg.focusCOM);
            if (success != 1) {

				Console.WriteLine("CM_SetComSimple失败！请查看log");
                return ;
			}

            IntPtr motorHandle = IntPtr.Zero;
            success = cvCameraCSLib.CM_CreatChildHandle(camHandle, ref motorHandle, (byte)atuoCfg.motorNum);

            //首先回到初始位置
            //success = cvCameraCSLib.GoHome(motorHandle);


            success = cvCameraCSLib.MoveDiaphragm(motorHandle, 5.6f);

            if (success != 1)
            {
                Console.WriteLine("设置对焦环失败！");
            }

            ///爬山法
            MountainClimbing(camHandle, motorHandle, ref atuoCfg);

            //autoFocus_EdgeFocus(camHandle, motorHandle, ref atuoCfg);


            cvCameraCSLib.ShutDown(motorHandle);

            cvCameraCSLib.CM_Close(camHandle);
            //释放相机句柄
            cvCameraCSLib.CM_ReleaseCameraManagerSimple(camHandle);

            //释放dll资源，这个函数在最后调且只能调一次！！
            cvCameraCSLib.ReleaseResource();
            logDebug.logRelease();
            return;
            int pos = 3000;



            success = cvCameraCSLib.GetPosition(motorHandle, ref pos, 5000);

            pos = 3500;


            for (int i = 0; i < 7700; i += 500)
            {
                success = cvCameraCSLib.MoveAbsPostion(motorHandle, i);

                if (success != 1)
                {
                    MessageBox.Show("MoveAbsPostion Fail!");
                }

                success = cvCameraCSLib.GetPosition(motorHandle, ref pos, 5000);
                if (success != 1 || pos != i)
                {
                    MessageBox.Show("GetPosition Fail!");
                }

            }


            success = cvCameraCSLib.MoveAbsPostion(motorHandle, pos);
            success = cvCameraCSLib.MoveAbsPostion(motorHandle, 1000);
            success = cvCameraCSLib.MoveAbsPostion(motorHandle, 2000);
            success = cvCameraCSLib.MoveAbsPostion(motorHandle, 3000);
            success = cvCameraCSLib.MoveAbsPostion(motorHandle, 4000);
            success = cvCameraCSLib.MoveAbsPostion(motorHandle, 5000);
            success = cvCameraCSLib.MoveAbsPostion(motorHandle, 7000);
            success = cvCameraCSLib.GetPosition(motorHandle, ref pos, 5000);

            cvCameraCSLib.ShutDown(motorHandle);
            //释放相机句柄
            cvCameraCSLib.CM_ReleaseCameraManagerSimple(camHandle);

            //释放dll资源，这个函数在最后调且只能调一次！！
            cvCameraCSLib.ReleaseResource();
        }

        //camHandle相机句柄；motorHandle电机句柄
        public class positionInfor
        {
            public positionInfor(int min, int max, float aver)
            {
                sectionMin = min;
                sectionMax = max;
                averageLevel = aver;
            }
            public int sectionMin;
            public int sectionMax;
            public float averageLevel;
        }
        public void autoFocus(IntPtr camHandle, IntPtr motorHandle)
        {

            int scopeMin = 0, scopeMax = 4800,/*电机最大值*/ stepMax = 350;

            int startPotion = scopeMin;
            List<positionInfor> list_averageLevel = new List<positionInfor>();     //用于存储电机位区间和评价值  
            IRECT[] foucsRects = new IRECT[4];

            //设置计算的矩形区域,要根据实际图像挖区域，这里需要根据实际情况挖取
            foucsRects[0].setValue(2250, 850, 800, 400);
            foucsRects[1].setValue(700, 2000, 600, 500);
            foucsRects[2].setValue(2300, 2300, 900, 400);
            foucsRects[3].setValue(4250, 1500, 400, 500);
            float[] focusResult = new float[4];
			focusResult[0] = 0;
			focusResult[1] = 0;
			focusResult[2] = 0;
			focusResult[3] = 0;

			int res = 0;
            // string[]head = new string[4] {"rect0", "rect1", "rect2", "rect3"};
            //image
            uint w = 0, h = 0, srcbpp = 0, bpp = 0, channels = 0;
            res = cvCameraCSLib.CM_OpenSimple(camHandle);
            cvCameraCSLib.CM_SetExpTimeSimple(camHandle, 150);  //设置曝光
            cvCameraCSLib.CM_GetSrcFrameInfo(camHandle, ref w, ref h, ref srcbpp, ref channels);


            byte[] src = new byte[srcbpp / 8 * w * h * channels];           //灰度数据每个像素点占两字节
            byte[] imgdata = new byte[w * h * channels * 4];       //通道值数

            float averageLevel = 0;

            //第一步进行粗定焦



            for (int i = scopeMin; i < scopeMax; i += stepMax)
            {
                res = cvCameraCSLib.MoveAbsPostion(motorHandle, i);
                if (res != 1)
                {
                    Console.WriteLine($"move{i} Fail");
                    return;
                }
                res = cvCameraCSLib.CM_GetFrameSimple(camHandle, ref w, ref h, ref srcbpp, ref bpp, ref channels, src, imgdata);
                if (res != 1)
                {
                    Console.WriteLine("fail to take img");
                    return;
                }
               // cvCameraCSLib.CM_ExportToTIFF($"TIFF\\Tfirst电机位{i}.tif", w, h, srcbpp, channels, src);

               // res = cvCameraCSLib.CalFocusLevelByEdgeSimple(camHandle, foucsRects, focusResult, 4, 11);

                //Console.WriteLine($"level[0],{focusResult[0]},[1],{focusResult[1]},[2],{focusResult[2]},[3],{focusResult[3]}, at position:{i}");
                //cvCameraCSLib.writeCSV_flo("result\\level.csv", head, focusResult, 4);
                // 计算矩形的平均清晰度
                averageLevel = (focusResult[0] + focusResult[1] + focusResult[2] + focusResult[3]) / 4;

                positionInfor pi = new positionInfor(i - stepMax / 2, i + stepMax / 2, averageLevel);
                //将此时电机所处范围及清晰度记录下来
                list_averageLevel.Add(pi);

            }
            //    list_averageLevel.Clear();
            //    Console.WriteLine("\n~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~\n");
            //}
           res = cvCameraCSLib.GoHome(motorHandle);
            float bestLevel = 100;
            int highPrecisionMin = 0, highPrecisionMax = 10;
            //寻找最优电机区间
            foreach (var obj in list_averageLevel)
            {
                if (obj.averageLevel < bestLevel)
                {
                    bestLevel = obj.averageLevel;
                    if (obj.sectionMin < 0)
                    {
                        obj.sectionMin = 0;
                    }
                    highPrecisionMin = obj.sectionMin;
                    highPrecisionMax = obj.sectionMax;
                }
            }

            list_averageLevel.Clear();
            Console.WriteLine($"the advance scope is:{highPrecisionMin}-{highPrecisionMax}");



            int indexMin = 20;
            //int f = 0;
            //float[]banace=new float[4] { -99, -99, -99, -99};
            //cvCameraCSLib.writeCSV_flo("result\\level.csv", head, banace, 4);
            //在第一步筛选出的电机范围找出精确的定焦范围
            for (int i = highPrecisionMin; i < highPrecisionMax; i += indexMin)
            {



                res = cvCameraCSLib.MoveAbsPostion(motorHandle, i);
                if (res != 1)
                {
                    Console.WriteLine("{move $Fail\n}", i);
                    return;
                }
                int temp = 0;
                cvCameraCSLib.GetPosition(motorHandle, ref temp);
                if (temp != i)
                {
                    Console.WriteLine($"positon error:{i}!={temp}");
                }
                res = cvCameraCSLib.CM_GetFrameSimple(camHandle, ref w, ref h, ref srcbpp, ref bpp, ref channels, src, imgdata);
                if (res != 1)
                {
                    Console.WriteLine("fail to take img");
                    return;
                }
               // cvCameraCSLib.CM_ExportToTIFF($"TIFF\\电机位{i}.tif", w, h, srcbpp, channels, src);

                res = cvCameraCSLib.CalFocusLevelByEdgeSimple(camHandle, foucsRects, focusResult, 4, 11);
                Console.WriteLine($"level[0],{focusResult[0]},[1],{focusResult[1]},[2],{focusResult[2]},[3],{focusResult[3]}, at position:{i}");
                //cvCameraCSLib.writeCSV_flo("result\\level.csv", head, focusResult, 4);
                // 计算矩形的平均清晰度
                averageLevel = (focusResult[0] + focusResult[1] + focusResult[2] + focusResult[3]) / 4;
                positionInfor pi = new positionInfor(i - indexMin / 2, i + indexMin / 2, averageLevel);
                //将此时电机所处范围及清晰度记录下来
                list_averageLevel.Add(pi);

            }
          

            int finalyPosition = 0;     //最终对焦电机位
            bestLevel = 100;
            foreach (var obj in list_averageLevel)
            {
                if (obj.averageLevel < bestLevel)
                {
                    bestLevel = obj.averageLevel;
                    //取区间内的平均值
                    finalyPosition = (obj.sectionMin + obj.sectionMax) / 2;
                }
            }
            list_averageLevel.Clear();

            Console.WriteLine($"the focus position is:{finalyPosition}");
          res = cvCameraCSLib.GoHome(motorHandle);
            //调到最佳电机位并取图存图
            res = cvCameraCSLib.MoveAbsPostion(motorHandle, finalyPosition);
            res = cvCameraCSLib.CM_GetFrameSimple(camHandle, ref w, ref h, ref srcbpp, ref bpp, ref channels, src, imgdata);
            if (res != 1)
            {
                Console.WriteLine("fail to take img");
                return;
            }
            cvCameraCSLib.CM_ExportToTIFF($"TIFF\\best-{finalyPosition}.tif", w, h, srcbpp, channels, src);
        }


        public void MountainClimbing(IntPtr camHandle, IntPtr motorHandle, ref autoFocusCfg cfgObj)
        {
            int ret;
            int startPotion = cfgObj.focus_Min;
            List<positionInfor> list_averageLevel = new List<positionInfor>();     //用于存储电机位区间和评价值  
            IRECT[] foucsRects = new IRECT[4];
            int res = 0;
            uint w = 0, h = 0, srcbpp = 0, bpp = 0, channels = 0;
            res = cvCameraCSLib.CM_OpenSimple(camHandle);
            if (res != 1)
            {
                Console.WriteLine("fail to CM_OpenSimple");
                return;
            }
            ret = cvCameraCSLib.CM_SetExpTimeSimple(camHandle, 20);  //设置曝光
           cvCameraCSLib.CM_GetSrcFrameInfo(camHandle, ref w, ref h, ref srcbpp, ref channels);


            byte[] src = new byte[srcbpp / 8 * w * h * channels];           //灰度数据每个像素点占两字节
            byte[] imgdata = new byte[w * h * channels * 4];       //通道值数

            double averageLevel = 0;

            //获取清晰度
            double GetArticulation(ref autoFocusCfg cfgObj)
            {
                res = cvCameraCSLib.CM_GetFrameSimple(camHandle, ref w, ref h, ref srcbpp, ref bpp, ref channels, src, imgdata);
                if (res != 1)
                {
                    Console.WriteLine("fail to take img");
                    return -1;
                }
                HImage tHimage = new HImage();
                tHimage.nHeight = h;
                tHimage.nWidth = w;
                tHimage.nChannels = channels;
                tHimage.nBpp = srcbpp;
                unsafe
                {
                    fixed (byte* tp = src)
                    {
                        tHimage.pData = new IntPtr(tp);
                    }
                }
                return cvCameraCSLib.cvCalArticulation(EvaFunc.fun5, tHimage, cfgObj.EdgeFocus.offy, cfgObj.EdgeFocus.d, cfgObj.EdgeFocus.w, 0.01, cfgObj.EdgeFocus.h, cfgObj.EdgeFocus.nStep, cfgObj.EdgeFocus.nMaxCount);
            }

            //第一步进行粗定焦


            for (int i = cfgObj.focus_Min; i < cfgObj.focus_Max; i += cfgObj.focus_Step)
            {
                res = cvCameraCSLib.MoveAbsPostion(motorHandle, i);
                if (res != 1)
                {
                    Console.WriteLine($"move{i} Fail");
                    return;
                }
                averageLevel =GetArticulation(ref cfgObj);
                string sLog = $"电机位:{i},评价值：,{averageLevel}";
                Console.WriteLine(sLog);
                logDebug.logRecord(sLog);

                positionInfor pi = new positionInfor(i - cfgObj.focus_Step / 2, i + cfgObj.focus_Step / 2, (float)averageLevel);
                //将此时电机所处范围及清晰度记录下来
                list_averageLevel.Add(pi);

            }


            res = cvCameraCSLib.GoHome(motorHandle);
            float bestLevel = 0;
            int highPrecisionMin = 0, highPrecisionMax = 10;
            //寻找最优电机区间
            foreach (var obj in list_averageLevel)
            {
                if (obj.averageLevel > bestLevel)
                {
                    bestLevel = obj.averageLevel;
                    if (obj.sectionMin < 0)
                    {
                        obj.sectionMin = 0;
                    }
                    highPrecisionMin = obj.sectionMin;
                    highPrecisionMax = obj.sectionMax;
                }
            }

            list_averageLevel.Clear();
            Console.WriteLine($"the advance scope is:{highPrecisionMin}-{highPrecisionMax}");

            int indexMin = 20;

            //在第一步筛选出的电机范围找出精确的定焦范围
            for (int i = highPrecisionMin; i < highPrecisionMax; i += indexMin)
            {
                res = cvCameraCSLib.MoveAbsPostion(motorHandle, i);
                if (res != 1)
                {
                    Console.WriteLine("{move $Fail\n}", i);
                    return;
                }
                int temp = 0;
                cvCameraCSLib.GetPosition(motorHandle, ref temp);
                if (temp != i)
                {
                    Console.WriteLine($"positon error:{i}!={temp}");
                }
                averageLevel = GetArticulation(ref cfgObj);
                positionInfor pi = new positionInfor(i - indexMin / 2, i + indexMin / 2, (float)averageLevel);
                list_averageLevel.Add(pi);
            }

            //爬山法
            int step = 1000;
            int stepover = 10;
            int npos = 0;

            Dictionary<double, double> records = new Dictionary<double, double>();

            cvCameraCSLib.GetPosition(motorHandle, ref npos);
            bool first = true;
            double preValue = averageLevel;
            while (Math.Abs(step) > stepover)
            {
                npos = npos + step;
                res = cvCameraCSLib.MoveAbsPostion(motorHandle, npos);
                cvCameraCSLib.GetPosition(motorHandle, ref npos);

                double Artculation = GetArticulation(ref cfgObj);

                if (records.TryAdd(npos, Artculation))
                    records[npos] = Artculation;

                if (Artculation < records.Aggregate((l, r) => l.Value > r.Value ? l : r).Value)
                {
                    Console.WriteLine($"the focus position is:{npos}");
                    break;
                }
                if (Artculation > preValue)
                {

                }
                else
                {
                    //如果是第一次则反向运动到原位置
                    if (first)
                    {
                        first = false;
                        step = -step;
                        continue;
                    }
                    step = -step / 2;
                }
                first = false;
                preValue = Artculation;
            }

            res = cvCameraCSLib.CM_GetFrameSimple(camHandle, ref w, ref h, ref srcbpp, ref bpp, ref channels, src, imgdata);
            if (res != 1)
            {
                Console.WriteLine("fail to take img");
                return;
            }
            MessageBox.Show("爬山法聚焦成功");
        }



        public void autoFocus_EdgeFocus(IntPtr camHandle, IntPtr motorHandle, ref autoFocusCfg cfgObj)
		{

            //int scopeMin = 0, scopeMax = 4800,/*电机最大值*/ stepMax = 350;
          
			int startPotion = cfgObj.focus_Min;
			List<positionInfor> list_averageLevel = new List<positionInfor>();     //用于存储电机位区间和评价值  
			IRECT[] foucsRects = new IRECT[4];

			

			int res = 0;
			// string[]head = new string[4] {"rect0", "rect1", "rect2", "rect3"};
			//image
			uint w = 0, h = 0, srcbpp = 0, bpp = 0, channels = 0;
			res = cvCameraCSLib.CM_OpenSimple(camHandle);
            if(res != 1)
            {
				Console.WriteLine("fail to CM_OpenSimple");
                return;
			}
			cvCameraCSLib.CM_SetExpTimeSimple(camHandle, 20);  //设置曝光
			cvCameraCSLib.CM_GetSrcFrameInfo(camHandle, ref w, ref h, ref srcbpp, ref channels);


			byte[] src = new byte[srcbpp / 8 * w * h * channels];           //灰度数据每个像素点占两字节
			byte[] imgdata = new byte[w * h * channels * 4];       //通道值数

			double averageLevel = 0;

			//第一步进行粗定焦


			for (int i = cfgObj.focus_Min; i < cfgObj.focus_Max; i += cfgObj.focus_Step)
			{
				res = cvCameraCSLib.MoveAbsPostion(motorHandle, i);
				if (res != 1)
				{
					Console.WriteLine($"move{i} Fail");
					return;
				}
				res = cvCameraCSLib.CM_GetFrameSimple(camHandle, ref w, ref h, ref srcbpp, ref bpp, ref channels, src, imgdata);
				if (res != 1)
				{
					Console.WriteLine("fail to take img");
					return;
				}
				HImage tHimage=new HImage();
				tHimage.nHeight = h;
				tHimage.nWidth = w;
				tHimage.nChannels = channels;
				tHimage.nBpp = srcbpp;
				unsafe
				{
					fixed (byte* tp = src)
					{
						tHimage.pData = new IntPtr(tp);
					}
				}
                if (cfgObj.saveImage)
                {
					cvCameraCSLib.CM_ExportToTIFF($"TIFF\\粗定位电机位{i}.tif", w, h, srcbpp, channels, src);
				}

				averageLevel = cvCameraCSLib.cvCalArticulation(EvaFunc.fun5, tHimage, cfgObj.EdgeFocus.offy, cfgObj.EdgeFocus.d, cfgObj.EdgeFocus.w,0.01, cfgObj.EdgeFocus.h, cfgObj.EdgeFocus.nStep, cfgObj.EdgeFocus.nMaxCount);
                string sLog = $"电机位:{i},评价值：,{averageLevel}";
				Console.WriteLine(sLog);
				logDebug.logRecord(sLog);

				positionInfor pi = new positionInfor(i - cfgObj.focus_Step / 2, i + cfgObj.focus_Step / 2, (float)averageLevel);
				//将此时电机所处范围及清晰度记录下来
				list_averageLevel.Add(pi);

			}


			res = cvCameraCSLib.GoHome(motorHandle);
			float bestLevel =0;
			int highPrecisionMin = 0, highPrecisionMax = 10;
			//寻找最优电机区间
			foreach (var obj in list_averageLevel)
			{
				if (obj.averageLevel > bestLevel)
				{
					bestLevel = obj.averageLevel;
					if (obj.sectionMin < 0)
					{
						obj.sectionMin = 0;
					}
					highPrecisionMin = obj.sectionMin;
					highPrecisionMax = obj.sectionMax;
				}
			}

			list_averageLevel.Clear();
			Console.WriteLine($"the advance scope is:{highPrecisionMin}-{highPrecisionMax}");



			int indexMin = 20;
			//int f = 0;
			//float[]banace=new float[4] { -99, -99, -99, -99};
			//cvCameraCSLib.writeCSV_flo("result\\level.csv", head, banace, 4);
			//在第一步筛选出的电机范围找出精确的定焦范围
			for (int i = highPrecisionMin; i < highPrecisionMax; i += indexMin)
			{
				res = cvCameraCSLib.MoveAbsPostion(motorHandle, i);
				if (res != 1)
				{
					Console.WriteLine("{move $Fail\n}", i);
					return;
				}
				int temp = 0;
				cvCameraCSLib.GetPosition(motorHandle, ref temp);
				if (temp != i)
				{
					Console.WriteLine($"positon error:{i}!={temp}");
				}
				res = cvCameraCSLib.CM_GetFrameSimple(camHandle, ref w, ref h, ref srcbpp, ref bpp, ref channels, src, imgdata);
				if (res != 1)
				{
					Console.WriteLine("fail to take img");
					return;
				}
                if (cfgObj.saveImage)
                {
                    cvCameraCSLib.CM_ExportToTIFF($"TIFF\\精定位电机位{i}.tif", w, h, srcbpp, channels, src);
                }
				HImage tHimage = new HImage();
				tHimage.nHeight = h;
				tHimage.nWidth = w;
				tHimage.nChannels = channels;
				tHimage.nBpp = srcbpp;
				unsafe
				{
					fixed (byte* tp = src)
					{
						tHimage.pData = new IntPtr(tp);
					}
				}
				// cvCameraCSLib.CM_ExportToTIFF($"TIFF\\Tfirst电机位{i}.tif", w, h, srcbpp, channels, src);
				averageLevel = cvCameraCSLib.cvCalArticulation(EvaFunc.fun5, tHimage, cfgObj.EdgeFocus.offy, cfgObj.EdgeFocus.d, cfgObj.EdgeFocus.w, cfgObj.EdgeFocus.h, cfgObj.EdgeFocus.nStep, cfgObj.EdgeFocus.nMaxCount);


				positionInfor pi = new positionInfor(i - indexMin / 2, i + indexMin / 2, (float)averageLevel);
				//将此时电机所处范围及清晰度记录下来
				list_averageLevel.Add(pi);

			}


			int finalyPosition = 0;     //最终对焦电机位
			bestLevel = 0;
			foreach (var obj in list_averageLevel)
			{
				if (obj.averageLevel > bestLevel)
				{
					bestLevel = obj.averageLevel;
					//取区间内的平均值
					finalyPosition = (obj.sectionMin + obj.sectionMax) / 2;
				}
			}
			list_averageLevel.Clear();

			Console.WriteLine($"the focus position is:{finalyPosition}");
			res = cvCameraCSLib.GoHome(motorHandle);
			//调到最佳电机位并取图存图
			res = cvCameraCSLib.MoveAbsPostion(motorHandle, finalyPosition);
			res = cvCameraCSLib.CM_GetFrameSimple(camHandle, ref w, ref h, ref srcbpp, ref bpp, ref channels, src, imgdata);
			if (res != 1)
			{
				Console.WriteLine("fail to take img");
				return;
			}
			cvCameraCSLib.CM_ExportToTIFF($"TIFF\\best-{finalyPosition}.tif", w, h, srcbpp, channels, src);
		}


		//视频模式下的通道数
		uint vedioChannels = 1;
        cvCameraCSLib.ProcCallBack callback;

        //视频回调数据内存
        static byte[] vedioData = null;
        static UInt64 CallBackFunction(int enumImgType, IntPtr pData, int nW, int nH, int lss, int bpp
            , int channels, IntPtr usrData)
        {
            //这里是将获取的每一帧数据存储在文件里，实际使用可以加载到图像显示控件中，实现图像实时刷新
            Marshal.Copy(pData, vedioData, 0, nW * nH * channels);
            string fn = "TIFF\\video_" + autoFocusImageIndex.ToString() + ".tif";
            cvCameraCSLib.CM_ExportToTIFF(fn, (uint)nW, (uint)nH, (uint)bpp, (uint)channels, vedioData);
            autoFocusImageIndex++;
            return 0;
        }

        //当检测到有相机与电脑连接或断开时，就会触发
        static int CallBackDeviceOnLine(IntPtr pData, bool OnLine, string id)
        {
            string ss = pData.ToString();
            if (OnLine)
            {
                Console.WriteLine("相机与电脑连接了id:", id);
                return 1;
            }
            else
            {
                Console.WriteLine("相机与电脑断开了id:", id);
                return 1;
            }
        }

        static public int autoFocusImageIndex = 0;
        static int CallBackAutoFocus(IntPtr usrData, int nW, int nH, int bpp, int channels, IntPtr pData)
        {
            Marshal.Copy(pData, vedioData, 0, nW * nH * channels * (bpp / 8));
            string fn = "TIFF\\pos" + autoFocusImageIndex.ToString() + ".tif";
            cvCameraCSLib.CM_ExportToTIFF(fn, (uint)nW, (uint)nH, (uint)bpp, (uint)channels, vedioData);
            autoFocusImageIndex++;
            return 1;
        }

        //w,h,srcbpp,channels,rawArray分别为原图的长、宽、位数、通道数和指针；
        //hi：要切出的图片结构体HImage
        //px,py:要切的ROI左上角坐标
        //rw,rh:要切ROI的尺寸
        public bool getRoiRectFromImage(UInt32 w, UInt32 h, UInt32 srcbpp, UInt32 channels, byte[] rawArray, ref HImage hi, int px, int py, UInt32 rw, UInt32 rh)
        {
            if (px < 0 || py < 0 || px > w || py > h || px + rw > w || py + rh > h)
            {
                return false;
            }
            hi.pData = IntPtr.Zero;
            hi.nBpp = srcbpp;
            hi.nChannels = channels;
            hi.nWidth = rw;
            hi.nHeight = rh;
            int pixlByte = (int)srcbpp / 8;
            byte[] rectData = new byte[rw * rh * pixlByte * channels];
            int index = 0;

            int hw = (int)(w * pixlByte * channels);
            int rcLength = (int)(rw * pixlByte * channels);
            int rStartIndex = (int)(px * pixlByte * channels);
            for (int i = py; i < py + rh; i++)
            {
                Buffer.BlockCopy(rawArray, (int)(i * hw + rStartIndex), rectData, index, rcLength);
                index += rcLength;
            }
            unsafe
            {
                fixed (byte* tp = rectData)
                {
                    hi.pData = new IntPtr(tp);
                }
            }
            return true;

        }
    }
}
