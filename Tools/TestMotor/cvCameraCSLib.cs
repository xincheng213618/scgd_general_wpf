#define gBVMode


using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;


namespace cvColorVision
{
    public delegate void TiffShowEvent(string value, bool bfast);
    public delegate void LiveShowEvent(int w, int h, byte[] rawArray);

    //[StructLayout(LayoutKind.Sequential)]
    public struct OPTIC_DATA
    {
        public int shape;  //0，代表圆，1代表矩形
        public int px;     //中心x
        public int py;     //中心y
        public int w_radius;   //长或半径
        public int h;          //宽
        public float X;        //光学三刺激值X
        public float Y_LUM;        //光学三刺激值Y,同时也代表亮度，单位cd
        public float Z;        //光学三刺激值Z
        public float cie_x;    //CIE色坐标x
        public float cie_y;    //CIE色坐标y
        public float cie_u;    //CIE色坐标u
        public float cie_v;    //CIE色坐标v
        public float CCT;      //色温
        public float mainWave; //主波长

    };

    public class ExpTimeCfg
    {
        public int autoExpTimeBegin { set; get; }           //建议10或20，如果样品很暗的话用2或5
        public bool autoExpFlag { set; get; }               //默认false即可
                                                            //自动同步频率
        public float autoExpSyncFreq { set; get; }          //样品hz，不需要的话就用-1，可咨询colorVison光学工程师
        public float autoExpSaturation { set; get; }        //默认70即可
        public UInt16 autoExpSatMaxAD { set; get; }         //正常情况65535，特殊情况如果图片是8bit用255
                                                            //
        public double autoExpMaxPecentage { set; get; }       //默认0.01，如果发光区占图片面积很小，可以用0.001或0.0001
                                                              //误差值
        public float autoExpSatDev { set; get; }            //默认20即可
                                                            //最大/小曝光
        public float maxExpTime { set; get; }               //建议60000
        public float minExpTime { set; get; }               //要设置的比autoExpTimeBegin小
                                                            //burst的阈值
        public float burstThreshold { set; get; }           //默认200;2000万像素的用200即可；6000万像素的用300
    }
    public class AutoExpJson
    {
        public AutoExpJson()
        {
            expTimeCfg = new ExpTimeCfg();
        }
        public ExpTimeCfg expTimeCfg;
    }

    public class CameraCfg
    {
        public int ob { set; get; }
        public int obR { set; get; }
        public int obB { set; get; }
        public int obT { set; get; }
        public bool tempCtlChecked { set; get; }
        public float targetTemp { set; get; }
        public float usbTraffic { set; get; }
        public int offset { set; get; }
        public int gain { set; get; }
        public int ex { set; get; }
        public int ey { set; get; }
        public int ew { set; get; }
        public int eh { set; get; }
    }


    public enum ImageChannelType : int
    {
        Channel_X = 0,
        Channel_Y = 1,
        Channel_Z = 2
    };

    public struct BrightAreaParam
    {
        public bool bBinarize { set; get; }// 二值化
        public int nBinarizeThresh { set; get; }

        public bool bBlur { set; get; }// 均值滤波
        public int nblur_size { set; get; }

        public bool bRoi { set; get; }// roi
        public int nleft { set; get; }
        public int nright { set; get; }
        public int ntop { set; get; }
        public int nbottom { set; get; }

        public bool bErode { set; get; }            // 腐蚀               
        public int nerode_size { set; get; }

        public bool bDilate { set; get; }           // 膨胀             
        public int ndilate_size { set; get; }

        public bool bFilterRect { set; get; }       // 过滤    
        public int Widht { set; get; }
        public int Height { set; get; }

        public bool bFilterArea { set; get; }       // 过滤
        public int nMax_area { set; get; }          // 面积大小        
        public int nMin_area { set; get; }
    };

    public enum ConfigType : int
    {
        Cfg_Camera = 0,
        Cfg_ExpTime = 1,
        Cfg_Calibration = 2,
        Cfg_Channels = 3,
    };
    public enum CameraType : int
    {
        CV_Q = 0,
        LV_Q,
        BV_Q,
        MIL_CL,
        MIL_CXP,
        BV_H,
        LV_H,
        HK_CXP,
        LV_MIL_CL,
        CV_MIL_CL,
        MIL_CXP_VIDEO,
        BV_HK_CL,
        LV_HK_CL,
        CV_HK_CL,
        Non_CameraType,
        CameraType_Total,
    };

    public enum FovPattern
    {
        FovCircle = 0,
        FovRectangle,
    };
    public enum FovType
    {
        Horizontal = 0,
        Vertical,
        Leaning,
    };
    public struct AoiParam
    {
        public bool filter_by_area;
        public int max_area;
        public int min_area;
        public bool filter_by_contrast;
        public float max_contrast;
        public float min_contrast;
        public float contrast_brightness;
        public float contrast_darkness;
        public int blur_size;
        public int min_contour_size;
        public int erode_size;
        public int dilate_size;

        public int left;
        public int right;
        public int top;
        public int bottom;
    };
    class cvCameraCSLib
    {




        private const string LIBRARY_CVCAMERA = "cvCamera.dll";

        public delegate int DeviceOnline_CallBack(IntPtr pData, bool OnLine, string id);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "InitResource",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern void InitResource(DeviceOnline_CallBack CallBackFunc, IntPtr hOperate_data);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "ReleaseResource",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        /*释放dll资源*/
        public static extern void ReleaseResource();

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetCameraID",
          CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        /*搜索电脑连接的相机id
    参数:
    CameraType：相机类型；
    jsonCfg：output，id组成的json头指针
    strLen：input，json长度256即可*/
        private static extern int CM_GetCameraID(CameraType eType, StringBuilder sn, int len);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetSpecialTasks",
    CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        /* 获取需要调用的特殊函数信息，如设置转轮、电机的COM口
   参数：
   HANDLE：input，相机句柄
   pTasks：，output，细节信息*/
        private static extern int CM_GetSpecialTasks(IntPtr handle, StringBuilder str, int len);


        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetOpticsData",
       CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern unsafe int GetOpticsData(IntPtr handle, IntPtr pOptciData, int pointNummber);

        public static int CM_GetOpticsData(IntPtr handle, OPTIC_DATA[] pOD, int pointNummber = 10)
        {

            int size = Marshal.SizeOf(typeof(OPTIC_DATA));
            IntPtr pToArr = Marshal.AllocHGlobal(size * pOD.Length);

            for (int i = 0; i < pOD.Length; i++)
            {
                IntPtr currentPtr = new IntPtr(pToArr.ToInt64() + size * i);
                Marshal.StructureToPtr(pOD[i], currentPtr, false);
            }

            int res = GetOpticsData(handle, pToArr, pOD.Length);
            for (int i = 0; i < pOD.Length; i++)
            {
                IntPtr currentPtr = new IntPtr(pToArr.ToInt64() + size * i);
                pOD[i] = Marshal.PtrToStructure<OPTIC_DATA>(currentPtr);
            }

            Marshal.FreeHGlobal(pToArr);

            if (res != 1)
            {
                return res;
            }
            return 1;
        }
        /*设置硬件连接COM口，若设置了不需要设置的类目，不会导致报错
参数：
HANDLE：相机句柄
comType:input，com的类型，0代表转轮COM口；1 代表电动镜头COM口；2 代表继电器COM口；
ComNummber：input，COM号;
flag:暂时忽略
*/
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SetComSimple",
     CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int CM_SetComSimple(IntPtr handle, int comType, string ComNummber, int flag = 0);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_CreatChildHandle",
      CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int CM_CreatChildHandle(IntPtr handle, ref IntPtr childHandle, byte ucMachineNO, int handleType = 0);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SetCaliFilePath",
 CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]



        public static extern int CM_SetCaliFilePath(IntPtr handle, string caliFilePath);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetCameraIDV1",
     CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]

        public static extern int CM_GetCameraIDV1(int camType, StringBuilder pStr, int strLength);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetErrorMessage",
       CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        /*获取函数返回值的意义
    * 
    参数:
    nErr：input,函数返回值
    szMsg：output,该返回值所对应的意义
    strLen：：output,字符的长度*/
        public static extern int CM_GetErrorMessage(int errCode, StringBuilder pStr, ref int strLength);


        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_CreatCameraManagerSimple",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]

        /*创建一个相机句柄并返回
    参数:

    dcfFile：相机配置dcf文件路径及名称，如"cfg\\CV2000test.dcf"。每台相机具有唯一性*/
        public unsafe static extern IntPtr CM_CreatCameraManagerSimple(string dcfFile);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_ReleaseCameraManagerSimple",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        /*释放相机句柄
    参数:
    HANDLE：相机句柄。*/
        public static extern int CM_ReleaseCameraManagerSimple(IntPtr handle);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_IsOpen",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int CM_IsOpen(IntPtr handle);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_PixelScaling",
           CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int CM_PixelScaling(UInt32 w, UInt32 h, UInt32 bpp, UInt32 channels, byte[] imgdata, double dRateR, double dRateG, double dRateB);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetDeviceOnline",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int CM_GetDeviceOnline(IntPtr handle);



        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_UpdateCfgJson",
          CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        /*CM_UpdateCfgJson 设置相机取图单个通道中单个像素的位数，相机必须处于关闭状态
    参数：
    HANDLE：相机句柄
    eType：更新模块
    jsonCfg：配置字符串
    */
        public static extern int CM_UpdateCfgJson(IntPtr handle, ConfigType eType, string jsonCfg);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_ResetEx",
           CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        /*重置相机，在相机是关闭的状态下，尝试将相机置为正常状态，针对CV_Q，BV_Q，LV_Q相机
     参数：
     HANDLE：相机句柄
     delayTimeMs:重置的延时时间，如果电脑配置较差，时间可加长*/
        public static extern int CM_ResetEx(IntPtr handle, int delayTimeMs);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetComputerUsbType",
    CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        /*获取电脑与相机连接的USB端口型号
    参数：
    HANDLE：相机句柄
    nUsbNo:2代表USB2.0；2代表USB3.0*/
        public static extern int CM_GetComputerUsbType(IntPtr handle, ref int nUsbNo);


        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_OpenSimple",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        /*打开相机，
    参数：
    HANDLE：相机句柄*/
        public static extern int CM_OpenSimple(IntPtr handle);


        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_OpenLiveSimple",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        /*以视频模式打开相机，
    参数：
    HANDLE：相机句柄*/
        public static extern int CM_OpenLiveSimple(IntPtr handle);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_Close",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        /*关闭相机
    参数：
    HANDLE：相机句柄*/
        public unsafe static extern void CM_Close(IntPtr handle);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SetExpTimeSimple",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int CM_SetExpTimeSimple(IntPtr handle, float exp1, float exp2 = 10, float exp3 = 10);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetAutoExpTimeSimple",
           CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]

        /*获取自动曝光时间
    参数：
    HANDLE：相机句柄
    exp1,exp2,exp3:output,曝光时间,如果带转轮的相机exp1，exp2，exp3分别代表R、G、B通道曝光时间
    saturation:output,饱和度，如果带转轮的相机分别代表RGBt通道的饱和度*/
        public static extern int CM_GetAutoExpTimeSimple(IntPtr handle, ref float exp1, ref float exp2, ref float exp3, float[] staturation);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetCalibrationGroupNum",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int CM_GetCalibrationGroupNum(IntPtr handle);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetAllCalibrationGroupTitles",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int CM_GetAllCalibrationGroupTitles(IntPtr handle, IntPtr[] allCaliTitle);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_ChooseCalibrationTitle",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int CM_ChooseCalibrationTitle(IntPtr handle, string CaliTitle);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetCaliGropupItems",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        /*获取一组矫正文件的组成配置json
    参数：
    HANDLE：相机句柄
    title:input,矫正的名称
    groupItemsJson:output,这组矫正用到的矫正文件配置信息，以json格式传出*/
        public static extern int CM_GetCaliGropupItems(IntPtr handle, string CaliTitle, StringBuilder groupItemsJson);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_EnableCali",
           CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]

        /*激活或取消某种类型的矫正，正常情况无需调用！！！除非对速度有极致要求
    参数：
    HANDLE：相机句柄
    caliType:input,矫正的类型,3代表坏点，4代表DSNU，5代表均匀场，11代表畸变，6代表亮色度,-1代表所有矫正，
    enable：input，是否启用*/
        public static extern int CM_EnableCali(IntPtr handle, int caliType, bool enable);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SetPortComSimple",
           CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int CM_SetPortComSimple(IntPtr handle, string portCom);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SetFocusComSimple",
      CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int CM_SetFocusComSimple(IntPtr handle, string portCom);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CalFocusLevelByEdgeSimple",
     CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int CalFocusLevelByEdgeSimple(IntPtr handle, IRECT[] rectArr, float[]result,int length,int flag=0);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_Sleep",
        CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern void CM_Sleep(float ms);



        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SetPort",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int CM_SetPort(IntPtr handle, int port);


        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_InitialGenerateCali",
       CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int CM_InitialGenerateCali(IntPtr handle, int caliType, int number, int type = 0);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SetColorCaliData",
      CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int CM_SetColorCaliData(IntPtr handle, int caliType, IRECT ir, int part, float cie_x, float cie_y, float luminace);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SaveCaliFile",
     CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int CM_SaveCaliFile(IntPtr handle, int caliType, string fileName);


        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetFrameSimple",
        CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        /*获取一张图片并矫正
    参数：
    HANDLE：相机句柄
    w：output,图片的长
    h:output,图片的宽
    srcbpp:output,图片的位数 16位，8位？
    bpp:output,XYZ图的位数
    channels：output,图片通道数
    srcimgdata：output,源图的头指针，做过均匀场，坏点等矫正，排列方式为...bgrbgrbgr...,
    imgdata：output,色度图的头指针，排列方式为  XXX... YYY... ZZZ...*/
        public static extern int CM_GetFrameSimple(IntPtr handle, ref UInt32 w, ref UInt32 h,
       ref UInt32 srcbpp, ref UInt32 bpp, ref UInt32 channels, byte[] srcrawArray, byte[] rawArray);



        private const string LIBRARY_TOOL_LIB = "1TOOL_LIB.dll";

        [DllImport(LIBRARY_TOOL_LIB, EntryPoint = "writeCSV_int",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public  static extern bool writeCSV_int(string fileName, string[] headTitle, int[] point, int length);

        [DllImport(LIBRARY_TOOL_LIB, EntryPoint = "writeCSV_flo",
           CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern bool writeCSV_flo(string fileName, string[] headTitle, float[] point, int length);


        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_ExportToTIFF",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        /*将数据以TIFF格式保存为一张图片
    参数：
    fileName: input,保存图片的文件名称
    w: input,图片的长； h: input，图片的高
    bpp： input，图片的位数；channels： input，图片的通道数
    rawArray： input，图片的指针*/
        public unsafe static extern int CM_ExportToTIFF(string fileName, UInt32 w, UInt32 h, UInt32 bpp, UInt32 channels, byte[] rawArray);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_InitXYZ",
             CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        /*初始化XYZ数据，必须做一次，且在取图前做
    参数：
    handle:input， 相机句柄；
    */
        public static extern int CM_InitXYZ(IntPtr handle);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_UnInitXYZ",
             CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        /*释放XYZ数据
    参数：
    handle:input， 相机句柄；
    */
        public static extern int CM_UnInitXYZ(IntPtr handle);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetxyuvCCTWaveCircleEx",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int CM_GetxyuvCCTWaveCircleEx(IntPtr handle, int[] pX, int[] pY, int nLen, float[] pdx, float[] pdy, float[] pdu, float[] pdv, float[] pCCT, float[] pWave, string filename, double nRadius = 0.0);


        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetXYZxyuvCircle",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        /*获取一个圆形关注点的平均色度X,Y,x,y,u，v。
    * 参数：
    * handle:input， 相机句柄；
    nX, nY: 关注点的圆心
    dX,dY,dZ: output，XYZ的平均值；dx，dy： 色度xy的平均值；u、v：色度uv的平均值
    dy : output, 色度v的平均值
    szFileName： 数据的保存文件名称
    nRadius : 关注点的半径 */
        public static extern int CM_GetXYZxyuvCircle(IntPtr handle, int nX, int nY, ref float dX, ref float dY, ref float dZ, ref float dx, ref float dy, ref float du, ref float dv, double nRadius = 3.0);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetYCircleEx",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int CM_GetYCircleEx(IntPtr handle, int[] pX, int[] pY, float[] pdY, int nLen, string filename, double nRadius = 0.0);


        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetSrcFrameInfo",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        /*获取当前模式下一帧图片的尺寸规格，若成功返回true
    参数：
    HANDLE：相机句柄
    imagew：output,图片的长
    imageh：output,图片的宽
    bpp：output,图片位数，16位，8位？
    channels：output图片通道数，1，3？*/
        public static extern UInt32 CM_GetSrcFrameInfo(IntPtr handle, ref uint w, ref uint h, ref uint bpp, ref uint channels);


        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetLiveFrame",
              CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]

        public unsafe static extern UInt32 CM_GetLiveFrame(IntPtr handle, ref uint w, ref uint h, byte[] vedioData);


        private static string UnicodeToGB(string text)
        {
            System.Text.RegularExpressions.MatchCollection mc = System.Text.RegularExpressions.Regex.Matches(text, "\\\\u([\\w]{4})");
            if (mc != null && mc.Count > 0)
            {
                foreach (System.Text.RegularExpressions.Match m2 in mc)
                {
                    string v = m2.Value;
                    string word = v.Substring(2);
                    byte[] codes = new byte[2];
                    int code = System.Convert.ToInt32(word.Substring(0, 2), 16);
                    int code2 = System.Convert.ToInt32(word.Substring(2), 16);
                    codes[0] = (byte)code2;
                    codes[1] = (byte)code;
                    text = text.Replace(v, Encoding.Unicode.GetString(codes));
                }
            }
            else
            {

            }
            return text;
        }



        [DllImport(LIBRARY_CVCAMERA, CharSet = CharSet.Auto, EntryPoint = "CM_SetCallBack",
            CallingConvention = CallingConvention.Cdecl)]
        public static extern int CM_SetCallBack(IntPtr handle, ProcCallBack callback, IntPtr obj);

        public delegate UInt64 ProcCallBack(int handle, IntPtr pData, int nW, int nH, int lss, int bpp, int channels, IntPtr usrData);

        [DllImport(LIBRARY_CVCAMERA, CharSet = CharSet.Auto, EntryPoint = "CM_UnregisterCallBack",
            CallingConvention = CallingConvention.Cdecl)]
        public static extern int CM_UnregisterCallBack(IntPtr handle);




        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SetGain",
             CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        /*设置相机增益
    参数：
    HANDLE：相机句柄
    exp: 增益*/
        public unsafe static extern int CM_SetGain(IntPtr handle, float fGain);


		[DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_LedCalInit",
  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
		public static extern int CM_LedCalInit(IntPtr handle, string cfgFn);


		[DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_LedCalFind",
  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
		public static extern int CM_LedCalFind(IntPtr handle, int index);

		[DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_LedCalComBine",
  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
		public static extern int CM_LedCalComBine(IntPtr handle, int n_pic, ref int w_comb, ref int h_comb, byte[] rst);

		[DllImport(LIBRARY_CVCAMERA, EntryPoint = "InitCom",
             CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int InitCom(IntPtr handle, FOCUS_COMMUN eFOCUS_COMMUN, string ComName, uint BaudRate);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "ShutDown",
             CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int ShutDown(IntPtr handle);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "GoHome",
             CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int GoHome(IntPtr handle);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "IsOpen",
             CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int IsOpen(IntPtr handle);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "MovePostion",
             CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int MovePostion(IntPtr handle, int nPosition, uint dwTimeOut);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "MoveAbsPostion",
             CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int MoveAbsPostion(IntPtr handle, int nPosition, uint dwTimeOut = 5000);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "MoveRelPostion",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int MoveRelPostion(IntPtr handle, int nPosition, uint dwTimeOut);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "ClearAlarm",
             CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int ClearAlarm(IntPtr handle);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "GetPosition",
             CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int GetPosition(IntPtr handle, ref int nPosition, uint dwTimeOut=5000);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "SetRunSpeed",
             CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int SetRunSpeed(IntPtr handle, int nSpeed, int nAcc, int ndec);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SetAutoFocusShowWnd",
             CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_SetAutoFocusShowWnd(IntPtr handle, IntPtr hWnd, CRECT rt);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SetAutoFocusExposure",
             CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_SetAutoFocusExposure(IntPtr handle, int nFit, double[] arrX, double[] arrY, int nDataSize);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "GhostGlareDectect",
           CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]

        public unsafe static extern int GhostGlareDectect(HImage tImg, int radius, int cols, int rows, float ratioH, float ratioL, string path, ref int allLedPixelNum, ref int ledNummber, ref int allGhostPixelNummber, ref int ghostNummber);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "getGhostResult",
           CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int getGhostResult(float[] centersX, float[] centersY, float[] blobGray, float[] dstGray, int[] singleLedPixelNum, int[] lED_pixel_X, int[] lED_pixel_Y, int[] singleGhostPixelNum, int[] ghost_pixel_X, int[] ghost_pixel_Y);


        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_CalcAutoFocus",
             CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_CalcAutoFocus(IntPtr handle, AutoFocusCfg tAtFsCfg);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "cvCalArticulation",
             CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern double cvCalArticulation(EvaFunc type, HImage iImg, int dx = 0, int dy = 1, int ksize = 5, double dRatio = 0.01 ,int h = 500, int nStep = 15, int nMaxCount = 20);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetAutoFocus",
             CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_GetAutoFocus(IntPtr handle, ref int nPos);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_AutoFocusCallBackEx",
             CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_AutoFocusCallBackEx(IntPtr handle, AutoFocus_CallBackEx CallBackFun, IntPtr usrData);

        public delegate int AutoFocus_CallBackEx(IntPtr usrData, int nW, int nH, int bpp, int channels, IntPtr pData);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CalDistanceExV3",
              CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CalDistanceExV3(int nPos, string disFile, ref double dDis);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "MoveDiaphragm",
             CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int MoveDiaphragm(IntPtr handle, float dPosition, uint dwTimeOut = 5000);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_readImage",
              CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_readImage(string fileName, ref HImage hi);

        //释放HImage
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_releaseImage",
              CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern void CM_releaseImage(ref HImage hi);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "FovCalculation",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int FovCalculation(HImage tImg, double Radio, double cameraDegrees, ref double fovDegrees, int thresholdValus, double dFovDist, float[] coordinates, FovPattern pattern, FovType type);

        public enum calGamutType
        {
            CIE1931_NTSC = 0,
            CIE1931_sRGB,
            CIE1931_Adobe_RGB,
            CIE1931_DCI_P3,
            CIE1931_BT_2020,
            CIE1931_PAL,

            CIE1976_NTSC,
            CIE1976_sRGB,
            CIE1976_Adobe_RGB,
            CIE1976_DCI_P3,
            CIE1976_BT_2020,
            CIE1976_PAL
        };
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CalGamut",
           CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern float CalGamut(float Rx, float Ry, float Gx, float Gy, float Bx, float By, calGamutType fType);


        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "SFRCalculation",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int SFRCalculation(HImage tImg, CRECT rtROI, double gamma, float[] pdfrequency, float[] pdomainSamplingData, int nLen);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "DistortionCalculation",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int DistortionCalculation(HImage tImg, SIZE iSize, Blob_Threshold_Params tBlobThreParams, float[] finalPointsX, float[] finalPointsY, ref double pointx, ref double pointy, ref double maxErrorRatio, ref double t, CornerType type /*= Circlepoint*/, SlopeType sType /*= CenterPoint*/, LayoutType lType /*= SlopeIN*/, DistortionType dType, int timeoutNumLimit = 50000);
    }

    public struct HImage
    {
        public uint nWidth;
        public uint nHeight;
        public uint nChannels;
        public uint nBpp;
        public IntPtr pData;

    };

    enum FOCUS_COMMUN
    {
        VID_SERIAL,
        CANON_SERIAL,
        NED_SERIAL,
    };

    public struct SIZE
    {
        public int cx;
        public int cy;
    };

    public enum DistortionPattern
    {
        NOT_EXISTING,
        CHESSBOARD,
        CIRCLES_GRID,
        ASYMMETRIC_CIRCLES_GRID
    };



    public struct Blob_Threshold_Params
    {
        public bool filterByColor; //是否使用颜色过滤
                                   //unsigned char blobColor; //亮斑255暗斑0
        public int blobColor; //亮斑255暗斑0
        public float minThreshold; //阈值每次间隔值
        public float thresholdStep; //斑点最小灰度
        public float maxThreshold; //斑点最大灰度

        /*version-1用到的特殊几个参数*/
        public bool ifDEBUG;
        public float darkRatio;
        public float contrastRatio;
        public int bgRadius;
        /*   end*/

        public float minDistBetweenBlobs; //斑点间隔距离
        public bool filterByArea; //是否使用面积过滤
        public float minArea; //斑点最小面积值
        public float maxArea;  //斑点最大面积值
        public int minRepeatability;  //重复次数认定
        public bool filterByCircularity;  //形状控制（圆，方）
        public float minCircularity;
        public float maxCircularity;
        public bool filterByConvexity;  //形状控制（豁口）
        public float minConvexity;
        public float maxConvexity;
        public bool filterByInertia;  //形状控制（椭圆度）
        public float minInertiaRatio;
        public float maxInertiaRatio;
    }

    enum CornerType //角点提取方法
    {
        Circlepoint = 0,    //圆点提取
        Checkerboard = 1,   //棋盘格角点提取
    };

    enum SlopeType  //斜率计算方法
    {
        CenterPoint = 0,    //中心点九点取斜率
        lb_Variance = 1,        //去除方差较大的点后取斜率
    };

    enum LayoutType //理想点布点方法
    {
        SlopeIN = 0,    //采用斜率布点
        SlopeOUT = 1,   //不采用斜率布点
    };
    enum DistortionType //TV畸变H,V方向与光学畸变的检测方法
    {
        OpticsDist = 0,
        TVDistH = 1,
        TVDistV = 2,
    };

    public struct IRECT
    {
        public IRECT(int tx, int ty, int tcx, int tcy)
        {
            x = tx;
            y = ty;
            rw = tcx;
            rh = tcy;
        }
        public void setValue(int tx, int ty, int tcx, int tcy)
        {
            x = tx;
            y = ty;
            rw = tcx;
            rh = tcy;
        }
        public int x;
        public int y;
        public int rw;
        public int rh;
    };

    public struct IIntputData
    {
        public float iexp_x;
        public float iexp_y;
        public float iexp_z;
        public float iCx;
        public float iCy;
        public float iLv;
    };

    public struct DPOINT
    {
        public double x;
        public double y;
    };

    public struct PartiCle
    {
        public double contrast;
        public int area;
        public int x;
        public int y;
        public int color;
    };

    public struct CRECT
    {
        //左上角起点
        public int x;
        public int y;
        //长、宽
        public int cx;
        public int cy;
    };

    public enum EvaFunc
    {
        fun1 = 0,
        fun2 = 1,
        fun3,
        fun4,
		fun5,
	};

    public struct AutoFocusCfg
    {
        public double forwardparam { set; get; }        //步径摆动范围
        public double curtailparam { set; get; }        //步径每次缩减系数
        public int curStep { set; get; }                //目前使用步径
        public int stopStep { set; get; }               //停止步径
        public int minPosition { set; get; }            //电机移动区间下限
        public int maxPosition { set; get; }            //电机移动区间上限
        public EvaFunc eEvaFunc { set; get; }           //评价函数类型
        public double dMinValue { set; get; }           //最低评价值
    };
}
