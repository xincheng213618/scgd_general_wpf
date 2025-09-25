#pragma warning disable  CA2101,CA1707,CA1401,CA1051,CA1838,CA1711,CS0649,CA2211,CA1708,CA1720
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.ComponentModel;
using cvColorVision.Util;

namespace cvColorVision
{
    public delegate void TiffShowEvent(string value, bool bfast);
    public delegate void LiveShowEvent(int w, int h, byte[] rawArray);
    public enum CONTROL_ID
    {
        /*0*/
        CONTROL_BRIGHTNESS = 0, //!< image brightness
        /*1*/
        CONTROL_CONTRAST,       //!< image contrast
        /*2*/
        CONTROL_WBR,            //!< red of white balance
        /*3*/
        CONTROL_WBB,            //!< blue of white balance
        /*4*/
        CONTROL_WBG,            //!< the green of white balance
        /*5*/
        CONTROL_GAMMA,          //!< screen gamma
        /*6*/
        CONTROL_GAIN,           //!< camera gain
        /*7*/
        CONTROL_OFFSET,         //!< camera offset
        /*8*/
        CONTROL_EXPOSURE,       //!< expose time (us)
        /*9*/
        CONTROL_SPEED,          //!< transfer speed
        /*10*/
        CONTROL_TRANSFERBIT,    //!< image depth bits
        /*11*/
        CONTROL_CHANNELS,       //!< image channels
        /*12*/
        CONTROL_USBTRAFFIC,     //!< hblank
        /*13*/
        CONTROL_ROWNOISERE,     //!< row denoise
        /*14*/
        CONTROL_CURTEMP,        //!< current cmos or ccd temprature
        /*15*/
        CONTROL_CURPWM,         //!< current cool pwm
        /*16*/
        CONTROL_MANULPWM,       //!< set the cool pwm
        /*17*/
        CONTROL_CFWPORT,        //!< control camera color filter wheel port
        /*18*/
        CONTROL_COOLER,         //!< check if camera has cooler
        /*19*/
        CONTROL_ST4PORT,        //!< check if camera has st4port
        /*20*/
        CAM_COLOR,
        /*21*/
        CAM_BIN1X1MODE,         //!< check if camera has bin1x1 mode
        /*22*/
        CAM_BIN2X2MODE,         //!< check if camera has bin2x2 mode
        /*23*/
        CAM_BIN3X3MODE,         //!< check if camera has bin3x3 mode
        /*24*/
        CAM_BIN4X4MODE,         //!< check if camera has bin4x4 mode
        /*25*/
        CAM_MECHANICALSHUTTER,                   //!< mechanical shutter
        /*26*/
        CAM_TRIGER_INTERFACE,                    //!< check if camera has triger interface
        /*27*/
        CAM_TECOVERPROTECT_INTERFACE,            //!< tec overprotect
        /*28*/
        CAM_SINGNALCLAMP_INTERFACE,              //!< singnal clamp
        /*29*/
        CAM_FINETONE_INTERFACE,                  //!< fine tone
        /*30*/
        CAM_SHUTTERMOTORHEATING_INTERFACE,       //!< shutter motor heating
        /*31*/
        CAM_CALIBRATEFPN_INTERFACE,              //!< calibrated frame
        /*32*/
        CAM_CHIPTEMPERATURESENSOR_INTERFACE,     //!< chip temperaure sensor
        /*33*/
        CAM_USBREADOUTSLOWEST_INTERFACE,         //!< usb readout slowest

        /*34*/
        CAM_8BITS,                               //!< 8bit depth
        /*35*/
        CAM_16BITS,                              //!< 16bit depth
        /*36*/
        CAM_GPS,                                 //!< check if camera has gps

        /*37*/
        CAM_IGNOREOVERSCAN_INTERFACE,            //!< ignore overscan area

        /*38*/
        QHYCCD_3A_AUTOBALANCE,
        /*39*/
        QHYCCD_3A_AUTOEXPOSURE,
        /*40*/
        QHYCCD_3A_AUTOFOCUS,
        /*41*/
        CONTROL_AMPV,                            //!< ccd or cmos ampv
        /*42*/
        CONTROL_VCAM,                            //!< Virtual Camera on off
        /*43*/
        CAM_VIEW_MODE,

        /*44*/
        CONTROL_CFWSLOTSNUM,         //!< check CFW slots number
        /*45*/
        IS_EXPOSING_DONE,
        /*46*/
        ScreenStretchB,
        /*47*/
        ScreenStretchW,
        /*48*/
        CONTROL_DDR,
        /*49*/
        CAM_LIGHT_PERFORMANCE_MODE,

        /*50*/
        CAM_QHY5II_GUIDE_MODE,
        /*51*/
        DDR_BUFFER_CAPACITY,
        /*52*/
        DDR_BUFFER_READ_THRESHOLD,
        /*53*/
        DefaultGain,
        /*54*/
        DefaultOffset,
        /*55*/
        OutputDataActualBits,
        /*56*/
        OutputDataAlignment,

        /*57*/
        CAM_SINGLEFRAMEMODE,
        /*58*/
        CAM_LIVEVIDEOMODE,
        /*59*/
        CAM_IS_COLOR,
        /*60*/
        hasHardwareFrameCounter,
        /*61*/
        CONTROL_MAX_ID_Error, //** No Use , last max index */
        /*62*/
        CAM_HUMIDITY,           //!<check if camera has	 humidity sensor  20191021 LYL Unified humidity function
        /*63*/
        CAM_PRESSURE,             //check if camera has pressure sensor
        /*64*/
        CONTROL_VACUUM_PUMP,        /// if camera has VACUUM PUMP
        /*65*/
        CONTROL_SensorChamberCycle_PUMP, ///air cycle pump for sensor drying
        /*66*/
        CAM_32BITS,
        /*67*/
        CAM_Sensor_ULVO_Status, /// Sensor working status [0:init  1:good  2:checkErr  3:monitorErr 8:good 9:powerChipErr]  410 461 411 600 268 [Eris board]
        /*68*/
        CAM_SensorPhaseReTrain, /// 2020,4040/PRO，6060,42PRO
        /*69*/
        CAM_InitConfigFromFlash, /// 2410 461 411 600 268 for now
        /*70*/
        CAM_TRIGER_MODE, //check if camera has multiple triger mode
        /*71*/
        CAM_TRIGER_OUT, //check if camera support triger out function
        /*72*/
        CAM_BURST_MODE, //check if camera support burst mode


        /* Do not Put Item after  CONTROL_MAX_ID !! This should be the max index of the list */
        /*Last One */
        CONTROL_MAX_ID
    };

    public class GetFrameParam
    {
        //滤色片数量
        public int channelCount;
        //测量次数
        public int measureCount;
        //ob
        public int ob;
        public int obR;
        public int obT;
        public int obB;
        //startBurst
        public uint startBurst;
        //endBurst
        public uint endBurst;
        public uint posBurst;
        //测量标题名称
        public string title;
        //自动曝光标识
        public bool autoExpFlag;
        //亮度校正
        public CalibrationItem lumChromaCheck;
        //滤色片通道
        public List<ChannelParam> channels;

        public GetFrameParam()
        {
            channels = new List<ChannelParam>();
        }

        public void BuildChannelsFileName(string path, string ext)
        {
            foreach (ChannelParam item in channels)
            {
                item.fileName = path + "\\" + title + ext;
            }
        }
    }

    public class CalibrationItem
    {
        public CalibrationType type { set; get; }
        public bool enable { set; get; }
        public string title { set; get; }
        public string doc { set; get; }

        public CalibrationItem(CalibrationType type, bool enable, string title, string fileName)
        {
            this.type = type;
            this.enable = enable;
            this.title = title;
            doc = fileName;
        }
    }

    public struct SIZE
    {
        public int cx;
        public int cy;
    };
    /// <summary>
    /// 角点提取方法
    /// </summary>
    public enum CornerType //角点提取方法
    {
        /// <summary>
        /// 圆点提取
        /// </summary>
        Circlepoint = 0,    //圆点提取
        /// <summary>
        /// 棋盘格角点提取
        /// </summary>
        Checkerboard = 1,   //棋盘格角点提取
    };
    /// <summary>
    /// 斜率计算方法
    /// </summary>
    public enum SlopeType  //斜率计算方法
    {
        /// <summary>
        /// 中心点九点取斜率
        /// </summary>
        CenterPoint = 0,    //中心点九点取斜率
        /// <summary>
        /// 去除方差较大的点后取斜率
        /// </summary>
        lb_Variance = 1,        //去除方差较大的点后取斜率
    };
    /// <summary>
    /// 理想点布点方法
    /// </summary>
    public enum LayoutType
    {
        /// <summary>
        /// 采用斜率布点
        /// </summary>
        SlopeIN = 0,    //采用斜率布点
        /// <summary>
        /// 不采用斜率布点
        /// </summary>
        SlopeOUT = 1,   //不采用斜率布点
    };
    public struct ST_BlobThreParams
    {
        public bool filterByColor; //是否使用颜色过滤
                                   //unsigned char blobColor; //亮斑255暗斑0
        public int blobColor; //亮斑255暗斑0
        public float minThreshold; //阈值每次间隔值
        public float thresholdStep; //斑点最小灰度
        public float maxThreshold; //斑点最大灰度
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

    public class BlobThreParams
    {
        [Category("\t\t\t全局"), Description("是否使用颜色过滤")]
        public bool filterByColor { set; get; } //是否使用颜色过滤
                                                //unsigned char blobColor; //亮斑255暗斑0
        [Category("\t\t\t全局"), Description("筛选亮斑写255暗斑写0")]
        public int blobColor { set; get; } //亮斑255暗斑0
        [Category("\t\t\t全局"), Description("重复次数认定次数(达到筛选出是斑点的次数几次后认定是斑点，结合扫描图像的阈值步进值理解这条)")]
        public int minRepeatability { set; get; }  //重复次数认定
        [Category("\t\t\t全局"), Description("角点提取类型[Circlepoint/圆点,Checkerboard/棋盘格]")]
        public CornerType cornerType { set; get; }

        [Category("\t阈值"), Description("斑点最小灰度阈值")]
        public float minThreshold { set; get; } //阈值每次间隔值
        [Category("\t阈值"), Description("每次扫描图像时阈值的步进值")]
        public float thresholdStep { set; get; } //斑点最小灰度
        [Category("\t阈值"), Description("斑点最大灰度阈值")]
        public float maxThreshold { set; get; }//斑点最大灰度
        [Category("\t阈值"), Description("斑点间隔距离")]
        public float minDistBetweenBlobs { set; get; } //斑点间隔距离
        [Category("面积"), Description("是否使用面积过滤")]
        public bool filterByArea { set; get; } //是否使用面积过滤
        [Category("面积"), Description("最小面积阈值")]
        public float minArea { set; get; }//斑点最小面积值
        [Category("面积"), Description("最大面积阈值")]
        public float maxArea { set; get; }  //斑点最大面积值
        [Category("形状圆控制"), Description("形状控制（圆，方）是否调用")]
        public bool filterByCircularity { set; get; } //形状控制（圆，方）
        [Category("形状圆控制"), Description("离1越近越接近圆")]
        public float minCircularity { set; get; }
        [Category("形状圆控制"), Description("越大越圆")]
        public float maxCircularity { set; get; }
        [Category("形状豁口控制"), Description("豁口是否调用")]
        public bool filterByConvexity { set; get; }  //形状控制（豁口）
        [Category("形状豁口控制"), Description("离1越近越没豁口")]
        public float minConvexity { set; get; }
        [Category("形状豁口控制"), Description("越大越圆")]
        public float maxConvexity { set; get; }
        [Category("形状椭圆控制"), Description("椭圆度是否调用")]
        public bool filterByInertia { set; get; }  //形状控制（椭圆度）
        [Category("形状椭圆控制"), Description("0的话可以近似认为是直线，1的话基本是圆")]
        public float minInertiaRatio { set; get; }
        [Category("形状椭圆控制"), Description("越大越圆")]
        public float maxInertiaRatio { set; get; }
        [Category("\t\t大小"), Description("宽度")]
        public int cx { set; get; }
        [Category("\t\t大小"), Description("高度")]
        public int cy { set; get; }

        public static BlobThreParams cfg;
        [JsonIgnore]
        public ST_BlobThreParams st_cfg;
        private static string DistoParamCfg = "cfg\\DistoParamSetup.cfg";

        public static BlobThreParams Load()
        {
            BlobThreParams blobThreParams = CfgFile.Load<BlobThreParams>(DistoParamCfg);
            if (blobThreParams == null)
            {
                blobThreParams = new BlobThreParams();
                blobThreParams.filterByColor = true;
                blobThreParams.blobColor = 0;
                blobThreParams.minThreshold = 10;
                blobThreParams.thresholdStep = 10;
                blobThreParams.maxThreshold = 220;
                blobThreParams.minDistBetweenBlobs = 50;
                blobThreParams.filterByArea = true;
                blobThreParams.minArea = 200;
                blobThreParams.maxArea = 10000;
                blobThreParams.minRepeatability = 2;
                blobThreParams.filterByCircularity = false;
                blobThreParams.minCircularity = 0.9f;
                blobThreParams.maxCircularity = (float)1e37;
                blobThreParams.filterByConvexity = false;
                blobThreParams.minConvexity = 0.9f;
                blobThreParams.maxConvexity = (float)1e37;
                blobThreParams.filterByInertia = false;
                blobThreParams.minInertiaRatio = 0.1f;
                blobThreParams.maxInertiaRatio = (float)1e37;
                blobThreParams.cx = 11;
                blobThreParams.cy = 8;
                blobThreParams.cornerType = CornerType.Checkerboard;

                blobThreParams.st_cfg = new ST_BlobThreParams();

                CfgFile.Save(DistoParamCfg, blobThreParams);
            }
            cfg = blobThreParams;
            blobThreParams.setValue();
            return cfg;
        }

        private void setValue()
        {
            st_cfg.filterByColor = filterByColor;
            st_cfg.blobColor = blobColor;
            st_cfg.minThreshold = minThreshold;
            st_cfg.thresholdStep = thresholdStep;
            st_cfg.maxThreshold = maxThreshold;
            st_cfg.minDistBetweenBlobs = minDistBetweenBlobs;
            st_cfg.filterByArea = filterByArea;
            st_cfg.minArea = minArea;
            st_cfg.maxArea = maxArea;
            st_cfg.minRepeatability = minRepeatability;
            st_cfg.filterByCircularity = filterByCircularity;
            st_cfg.minCircularity = minCircularity;
            st_cfg.maxCircularity = maxCircularity;
            st_cfg.filterByConvexity = filterByConvexity;
            st_cfg.minConvexity = minConvexity;
            st_cfg.maxConvexity = maxConvexity;
            st_cfg.filterByInertia = filterByInertia;
            st_cfg.minInertiaRatio = minInertiaRatio;
            st_cfg.maxInertiaRatio = maxInertiaRatio;
        }

        internal static void Save(BlobThreParams blobThreParams)
        {
            blobThreParams.setValue();
            CfgFile.Save(DistoParamCfg, blobThreParams);
        }
    }

    public struct CRECT
    {
        public int x;
        public int y;
        public int cx;
        public int cy;
    };
    public enum EvaFunc
    {
        Variance = 0,
        Tenengrad = 1,
        Laplace,
        CalResol,
    };
    public struct CVImage
    {
        public uint nWidth;
        public uint nHeight;
        public uint nChannels;
        public uint nBpp;
        public IntPtr pData;
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
        public double dMinValue { set; get; } 			//最低评价值
    };
    public class ChannelCfg
    {
        [JsonProperty]
        public string title { set; get; }
        [JsonProperty]
        public ushort cfwport { set; get; }
        [JsonProperty]
        public ImageChannelType chtype { set; get; }

        public override string ToString()
        {
            return string.Format("{0}", title);
        }
    }

    public class ProjectSysCfg
    {
        public List<CalibrationItem> calibrationLibCfg;
        public List<ChannelCfg> channelCfg;

        public ProjectSysCfg()
        {
            calibrationLibCfg = new List<CalibrationItem>();
            channelCfg = new List<ChannelCfg>();
        }
    }

    public class ChannelCalibration
    {
        //暗噪声校正参数
        public CalibrationItem dsnuCheck;
        //均匀场校正文件
        public CalibrationItem uniformityCheck;
        //白点校正文件
        public CalibrationItem defectCheck;
        //白点校正文件
        public CalibrationItem distortionCheck;
    }

    public class ChannelParam
    {
        //曝光时间，单位毫秒
        public float exp;
        //滤色轮端口
        public int cfwport;
        //通道类型
        public int channelType;
        //滤色片类型
        public int imageFilterType;//
        //校正
        public ChannelCalibration check;
        //文件名
        public string fileName;
    }
    public enum ImageFilterType 
    {
        Color_Filter = 0,//滤色片
        ND = 1//
    };

    public partial class cvCameraCSLib
    {
        private const string LIBRARY_CVCAMERA = "cvCamera.dll";



        public static TiffShowEvent event_ShowTiff;
        public static LiveShowEvent event_ShowLive;

        public static byte[] liveImageDataPartShow;
        public static int picw;
        public static int pich;
        public static int picbpp;
        public static int picchannels;


        private static string UnicodeToGB(string text) => Encoding.GetEncoding("gb2312").GetString(Encoding.Convert(Encoding.Unicode, Encoding.GetEncoding("gb2312"), Encoding.Unicode.GetBytes(text)));
        public delegate ulong QHYCCDProcCallBack(int handle, IntPtr pData, int nW, int nH, int lss, int bpp, int channels, IntPtr usrData);

        public static int connectedCameraType = 1;

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "InitResource",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern void InitResource(IntPtr CallBackFunc, IntPtr hOperate_data);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "ReleaseResource", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern void ReleaseResource();
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_CreatCameraManagerV1", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern IntPtr CM_CreatCameraManagerV1(CameraModel eMdl, CameraMode eMode, string cfgFilename);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetAllCameraID", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int GetAllCameraID(StringBuilder sn, int len);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetAllCameraIDMD5", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int GetAllCameraIDMD5(StringBuilder sn, int len);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetCameraIDV1",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        private unsafe static extern int GetAllCameraIDV1(CameraModel eMdl, StringBuilder sn, int len);


        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_CreatCameraManager",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern IntPtr CM_CreatCameraManager(CameraType eType, string CameraID, string cfgFilename);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_ReleaseCameraManager", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_ReleaseCameraManager(IntPtr handle);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SetCameraID",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern void CM_SetCameraID(IntPtr handle, string szCameraId);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SetImageBpp", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_SetImageBpp(IntPtr handle, int nBpp);
        //新建一个校正用的句柄
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CreatCalibrationManage", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern IntPtr CreatCalibrationManage();
        //释放校正用的句柄
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "ReleaseCalibrationManage", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool ReleaseCalibrationManage(IntPtr intPtr);

        [DllImport(LIBRARY_CVCAMERA, CharSet = CharSet.Auto, EntryPoint = "CM_SetIndent", CallingConvention = CallingConvention.Cdecl)]
        public static extern void CM_SetIndent(IntPtr handle, int nL, int nT, int nR, int nB);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SetCalibParam", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int CM_SetCalibParam(IntPtr handle, CalibrationType cType, bool bEnabled, string filename);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SetParamDarkNoise", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_SetParamDarkNoise(IntPtr handle, bool bEnabled, float darkNoiseRatio);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SetParamDefectWPoint", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_SetParamDefectWPoint(IntPtr handle, bool bEnabled, int[] pX, int[] pY, int nLen);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SetParamDefectBPoint",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_SetParamDefectBPoint(IntPtr handle, bool bEnabled, int[] pX, int[] pY, int nLen);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SetParamDefectPoint", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_SetParamDefectPoint(IntPtr handle, bool bEnabled, int[] pX, int[] pY, int nLen);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SetParamDSNU", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_SetParamDSNU(IntPtr handle, bool bEnabled, uint dsnuWidth, uint dsnuHeight, ushort[] pdsnuData);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SetParamUniformity", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_SetParamUniformity(IntPtr handle, bool bEnabled, uint UniftyWidth, uint UniftyHeight, float[] pUniftyData);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SetParamDistortion", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_SetParamDistortion(IntPtr handle, bool bEnabled, SIZE tImgSize, float[] cameraMatrix, float[] distCoeffs, float alpha, bool buseFisheye);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SetParamLuminance",
             CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_SetParamLuminance(IntPtr handle, bool bEnabled, float[] Texp, float[] Gain, float[] pa);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SetParamColorOne", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_SetParamColorOne(IntPtr handle, bool bEnabled, float[] Texp, float[] Gain, float[] pa);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SetParamColorFour", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_SetParamColorFour(IntPtr handle, bool bEnabled, float[] Texp, float[] Gain, float[] pa);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SetParamColorMulti", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_SetParamColorMulti(IntPtr handle, bool bEnabled, float[] pa, float[] gain, int N);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SetParamColorShift", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_SetParamColorShift(IntPtr handle, bool bEnabled, int OffX, int OffY, bool FillOffset);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SCGD_SDP_DarkNoise", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_SCGD_SDP_DarkNoise(IntPtr handle, int w, int h, int bpp, uint channels, byte[] imgdata);       
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SCGD_SDP_DefectWPoint",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_SCGD_SDP_DefectWPoint(IntPtr handle, int w, int h, int bpp, uint channels, byte[] imgdata);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SCGD_SDP_DefectBPoint", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_SCGD_SDP_DefectBPoint(IntPtr handle, int w, int h, int bpp, uint channels, byte[] imgdata);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SCGD_SDP_DefectPoint",CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_SCGD_SDP_DefectPoint(IntPtr handle, int w, int h, int bpp, uint channels, byte[] imgdata);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SCGD_SDP_Uniformity",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_SCGD_SDP_Uniformity(IntPtr handle, int w, int h, int bpp, uint channels, byte[] imgdata);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SCGD_SDP_DSNU",   CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_SCGD_SDP_DSNU(IntPtr handle, int w, int h, int bpp, uint channels, byte[] imgdata);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SCGD_SDP_ColorShift",CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_SCGD_SDP_ColorShift(IntPtr handle, int w, int h, int bpp, uint channels, byte[] imgdata);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SCGD_SDP_Distortion",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_SCGD_SDP_Distortion(IntPtr handle, int w, int h, int bpp, uint channels, byte[] imgdata);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SCGD_SDP_Luminance", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_SCGD_SDP_Luminance(IntPtr handle, uint w, uint h, int bpp, uint channels, byte[] srcimgdata  , byte[] dstimgdata, float[] dexp);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SCGD_SDP_ColorOne", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_SCGD_SDP_ColorOne(IntPtr handle, uint w, uint h, int bpp, uint channels, byte[] srcimgdata , byte[] dstimgdata, float[] dexp);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SCGD_SDP_ColorOneEx", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_SCGD_SDP_ColorOneEx(IntPtr handle, uint w, uint h, int bpp, uint channels, byte[] srcx, byte[] srcy , byte[] srcz, byte[] dstimgdata, float[] dexp);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SCGD_SDP_ColorFour", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_SCGD_SDP_ColorFour(IntPtr handle, uint w, uint h, int bpp, uint channels, byte[] srcimgdata , byte[] dstimgdata, float[] dexp);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SCGD_SDP_ColorFourEx", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_SCGD_SDP_ColorFourEx(IntPtr handle, uint w, uint h, int bpp, uint channels, byte[] srcx, byte[] srcy , byte[] srcz, byte[] dstimgdata, float[] dexp);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SCGD_SDP_ColorMulti",CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_SCGD_SDP_ColorMulti(IntPtr handle, uint w, uint h, int bpp, uint channels, byte[] srcimgdata , byte[] dstimgdata, float[] dexp);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SCGD_SDP_ColorMultiEx", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_SCGD_SDP_ColorMultiEx(IntPtr handle, uint w, uint h, int bpp, uint channels, byte[] srcx, byte[] srcy  , byte[] srcz, byte[] dstimgdata, float[] dexp);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_IsOpen",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_IsOpen(IntPtr handle);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetDeviceOnline", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_GetDeviceOnline(IntPtr handle);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SetTakeImageMode", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_SetTakeImageMode(IntPtr handle, TakeImageMode eTakeImageMode);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_Open",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_Open(IntPtr handle);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_LiveOpen",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_LiveOpen(IntPtr handle);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SetGain", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_SetGain(IntPtr handle, float fGain);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SetExpTime",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_SetExpTime(IntPtr handle, float expTime);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SetExpTimeEx",CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_SetExpTimeEx(IntPtr handle, int index, float expTime);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SetParam", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_SetParam(IntPtr handle, int pid, double val);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_IsFeatureAvailable",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_IsFeatureAvailable_Gen(IntPtr handle, int pid);
        public static bool CM_IsFeatureAvailable(IntPtr handle, int pid) => CM_IsFeatureAvailable_Gen(handle, pid);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_Sleep", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern void CM_Sleep(float ms);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SetPort",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_SetPort(IntPtr handle, int port);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_IsBurstmodeAvailable",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_IsBurstmodeAvailable(IntPtr handle);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetFrame_TIFF",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_GetFrame_TIFF(IntPtr handle, StringBuilder jsonPm);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetFrameEx_TIFF", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_GetFrameEx_TIFF(IntPtr handle, StringBuilder jsonPm);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetLiveFrame",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_GetLiveFrame(IntPtr handle, ref uint w, ref uint h, byte[] rawArray);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetFrame", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_GetFrame(IntPtr handle, string jsonPm, ref uint w, ref uint h, ref uint srcbpp, ref uint bpp, ref uint channels, byte[] srcrawArray, byte[] rawArray);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_ExportToTIFF", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_ExportToTIFF(string fileName, uint w, uint h, uint bpp, uint channels, byte[] rawArray);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_ExportToTIFFEx",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_ExportToTIFFEx(string fileName, uint w, uint h, uint bpp, uint channels, byte[] rawArray, double dRate);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetDeviceMode", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern void CM_GetDeviceMode(IntPtr handle, StringBuilder mode, int len);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "LedCheckYaQi", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern double LedCheckYaQi(bool isdebug, int checkChannel, UInt32 w, UInt32 h, UInt32 bpp, UInt32 channels, byte[] imgdata
            , int isguding, int gudingrid, int lunkuomianji, int pointNum, double hegexishu, int erzhihuapiancha, double[] databanjin
            , int[] datazuobiaoX, int[] datazuobiaoY, int picwid
            , int pichig, int[] 关注范围, int 发光区二值化补正, int boundry, double[] LengthCheck, double[] LengthRange, double[] LengthResult, bool isuseLocalRdPoint, float[] localRdMark, double[] PointX, double[] PointY);


        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetSN",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        private unsafe static extern void CM_GetSN(IntPtr handle, StringBuilder sn, int len);

        public static string CM_GetSN(IntPtr handle)
        {
            StringBuilder builder = new StringBuilder(50);
            CM_GetSN(handle, builder, 50);
            return builder.ToString();
        }
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SplitData", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_SplitData(uint w, uint h, uint bpp, uint channels, byte[] imgdata);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SplitDataEx", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_SplitDataEx(int w, int h, int bpp, byte[] srcimgdata, byte[] dstX, byte[] dstY, byte[] dstZ);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetXYZxyuvCircle",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_GetXYZxyuvCircle(IntPtr handle, uint pX, uint pY, ref float X, ref float Y, ref float Z, ref float x, ref float y, ref float u, ref float v, double nRadius = 3);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetXYZxyuvEx", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_GetXYZxyuvEx(uint[] pX, uint[] pY, float[] X, float[] Y, float[] Z, float[] x, float[] y, float[] u, float[] v, uint nLen, string fileName, double nRadius = 0.0);

        public struct ChromaInfo
        {
            public float fX { set; get; }
            public float fY { set; get; }
            public float fZ { set; get; }
            public float fx { set; get; }
            public float fy { set; get; }
            public float fu { set; get; }
            public float fv { set; get; }
            public float fCCT { set; get; }
            public float fWave { set; get; }
            public int nMidPointX { set; get; }
            public int nMidPointY { set; get; }
        };

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetMask", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_GetMask(IntPtr handle, int[] pX, int[] pY, uint nLen, ref ChromaInfo chromaInfo);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_MergeData", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_MergeData(uint w, uint h, uint bpp, uint channels, byte[] imgdata);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_MergeDataEx", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_MergeDataEx(uint w, uint h, uint bpp, byte[] imgdata, byte[] srcX, byte[] srcY, byte[] srcZ);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetBGRBuffer", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_GetBGRBuffer(IntPtr handle, ref int w, ref int h, ref int bpp, ref int channels, byte[] imgdata);
        public static void CM_GetFrame_TIFF(IntPtr handle, GetFrameParam param, bool isburst)
        {
            string json1 = JsonConvert.SerializeObject(param);
            Thread thread = new Thread(() => workTiffThread(handle, json1, isburst));
            thread.Start();
        }

        private static void workTiffThread(IntPtr handle, object json1, bool isburst)
        {
            StringBuilder json = new StringBuilder();
            json.Append(json1);
            string tifjson = json.ToString();
            if (isburst)
            {
                CM_GetFrameEx_TIFF(handle, json);
            }
            else
            {
                CM_GetFrame_TIFF(handle, json);
            }

            event_ShowTiff?.Invoke(json.ToString(),true);
        }

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetFrameMemLength",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern uint CM_GetFrameMemLength(IntPtr handle);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetFrameMaxMemLength",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern ulong CM_GetFrameMaxMemLength(IntPtr handle);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetCfgToJson",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern void CM_GetCfgToJson(IntPtr handle, ConfigType eType, StringBuilder jsonCfg, int len, bool bDefault);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "GetSysCfgJson",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        private unsafe static extern void GetSysCfgJson_Gen(IntPtr handle, StringBuilder jsonCfg, int len, bool bDefault);

        public static string GetSysCfgJson(IntPtr handle, StringBuilder jsonCfg, int len, bool bDefault)
        {
            StringBuilder builder = new StringBuilder(10240);
            GetSysCfgJson_Gen(handle, builder, 10240, bDefault);
            string json1 = builder.ToString();
            return UnicodeToGB(json1);
        }

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "GetDefaultSysCfgJson",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        private unsafe static extern void GetDefaultSysCfgJson_Gen(IntPtr handle, StringBuilder jsonCfg, int len, bool bDefault);
        public static string GetDefaultSysCfgJson(IntPtr handle, bool bDefault)
        {
            StringBuilder builder = new StringBuilder(10240);
            GetDefaultSysCfgJson_Gen(handle, builder, 10240, bDefault);
            string json1 = builder.ToString();
            return UnicodeToGB(json1);
        }

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "GetDefaultCameraCfgToJson", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        private unsafe static extern void GetDefaultCameraCfgToJson_Gen(IntPtr handle, StringBuilder jsonCfg, int len, bool bDefault);
        public static string GetDefaultCameraCfgToJson(IntPtr handle, bool bDefault)
        {
            StringBuilder builder = new StringBuilder(10240);
            CM_GetCfgToJson(handle, ConfigType.Cfg_Camera, builder, 10240, bDefault);
            string json1 = builder.ToString();
            return UnicodeToGB(json1);
        }

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "GetDefaultExpTimeCfgToJson", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        private unsafe static extern void GetDefaultExpTimeCfgToJson_Gen(IntPtr handle, StringBuilder jsonCfg, int len, bool bDefault);
        public static string GetDefaultExpTimeCfgToJson(IntPtr handle, bool bDefault)
        {
            StringBuilder builder = new StringBuilder(10240);
            CM_GetCfgToJson(handle, ConfigType.Cfg_ExpTime, builder, 10240, bDefault);
            string json1 = builder.ToString();
            return UnicodeToGB(json1);
        }

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "GetDefaultCaliLibCfgToJson", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        private unsafe static extern void GetDefaultCaliLibCfgToJson_Gen(IntPtr handle, StringBuilder jsonCfg, int len, bool bDefault);
        public static string GetDefaultCaliLibCfgToJson(IntPtr handle, bool bDefault)
        {
            StringBuilder builder = new StringBuilder(10240);
            CM_GetCfgToJson(handle, ConfigType.Cfg_Calibration, builder, 10240, bDefault);
            string json1 = builder.ToString();
            return UnicodeToGB(json1);
        }
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "GetDefaultChannelsCfgToJson",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        private unsafe static extern void GetDefaultChannelsCfgToJson_Gen(IntPtr handle, StringBuilder jsonCfg, int len, bool bDefault);

        public static string GetDefaultChannelsCfgToJson(IntPtr handle, bool bDefault)
        {
            StringBuilder builder = new StringBuilder(10240);
            GetDefaultChannelsCfgToJson_Gen(handle, builder, 10240, bDefault);
            string json1 = builder.ToString();
            return UnicodeToGB(json1);
        }

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "UpdateCaliLibCfgJson", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool UpdateCaliLibCfgJson(IntPtr handle, string jsonCfg);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "UpdateCameraCfgJson", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool UpdateCameraCfgJson(IntPtr handle, string jsonCfg);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "UpdateChannelCfgJson", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool UpdateChannelCfgJson(IntPtr handle, string jsonCfg);

        //新的修改参数
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_UpdateCfgJson", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern void UpdateCfgJson(IntPtr handle, ConfigType eType, string jsonCfg);

        public enum ConfigType : int
        {
            Cfg_Camera = 0,
            Cfg_ExpTime = 1,
            Cfg_Calibration = 2,
            Cfg_Channels = 3,
            Cfg_SYSTEM = 4,
        };

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "UpdateExpTimeCfgJson", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool UpdateExpTimeCfgJson(IntPtr handle, string jsonCfg);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetAutoExpTime",CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_GetAutoExpTime(IntPtr handle, float[] exp, float[] Saturat);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetSrcAutoExpTime", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_GetSrcAutoExpTime(IntPtr handle, float[] exp, float[] Saturat);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_Close", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern void CM_Close(IntPtr handle);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_LiveClose", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern void CM_LiveClose(IntPtr handle);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "UpdateSysCfgJson",CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool UpdateSysCfgJson(IntPtr handle, string jsonPm);
       

        [DllImport(LIBRARY_CVCAMERA, CharSet = CharSet.Auto, EntryPoint = "CM_SetCallBack",  CallingConvention = CallingConvention.Cdecl)]
        public static extern bool CM_SetCallBack(IntPtr handle, QHYCCDProcCallBack callback, IntPtr obj);

        [DllImport(LIBRARY_CVCAMERA, CharSet = CharSet.Auto, EntryPoint = "CM_UnregisterCallBack", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool CM_UnregisterCallBack(IntPtr handle);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetSrcFrame", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        private unsafe static extern bool CM_GetSrcFrame_Gen(IntPtr handle, ref uint w, ref uint h,ref uint bpp, ref uint channels, byte[] rawArray);

        public static bool CM_GetSrcFrame(IntPtr handle, ref uint w, ref uint h, ref uint bpp, ref uint channels, ref byte[] rawArray)
        {
            _=CM_GetSrcFrameInfo(handle, ref w, ref h, ref bpp, ref channels);
            uint nbbpMen = bpp / 8;
            rawArray = new byte[w * h * nbbpMen * channels];
            return CM_GetSrcFrame_Gen(handle, ref w, ref h, ref bpp, ref channels, rawArray);
        }

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetSrcFrameEx", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        private unsafe static extern bool CM_GetSrcFrameEx_Gen(IntPtr handle, ref uint w, ref uint h, ref uint bpp, ref uint channels, byte[] rawArray);
        public static bool CM_GetSrcFrameEx(IntPtr handle, ref uint w, ref uint h,  ref uint bpp, ref uint channels, ref byte[] rawArray)
        {
            _=CM_GetSrcFrameInfo(handle, ref w, ref h, ref bpp, ref channels);
            uint nbbpMen = bpp / 8;
            rawArray = new byte[w * h * nbbpMen * channels];
            return CM_GetSrcFrameEx_Gen(handle, ref w, ref h, ref bpp, ref channels, rawArray);
        }


        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetSrcFrameInfo", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern uint CM_GetSrcFrameInfo(IntPtr handle, ref uint w, ref uint h, ref uint bpp, ref uint channels);

        [DllImport(LIBRARY_CVCAMERA, CharSet = CharSet.Auto, EntryPoint = "CM_SetCfwport",CallingConvention = CallingConvention.Cdecl)]
        public static extern bool CM_SetCfwport(IntPtr handle, int nIndex, int nPort, ImageChannelType eImgChlType);


        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "ImageRect",CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern void ImageRect(int w, int h, int bpp, int channels, byte[] imgdata, IRECT tIRECT, byte[] imgDstdata);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "SkipTake",CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        private unsafe static extern void SkipTake(byte[] psrcdata, byte[] pdstdata, int nPos, int nCount);

        public static void SkipTake(byte[] psrcdata, ref byte[] pdstdata, int nPos, int nCount)
        {
            pdstdata = new byte[nCount];
            SkipTake(psrcdata, pdstdata, nPos, nCount);
        }


        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "IsOpenVid", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool IsOpenVid();

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SetAutoFocusShowWnd",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_SetAutoFocusShowWnd(IntPtr handle, IntPtr hWnd, CRECT rt);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SetAutoFocusExposure", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_SetAutoFocusExposure(IntPtr handle, int nFit, double[] arrX, double[] arrY, int nDataSize);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_CalcAutoFocus",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_CalcAutoFocus(IntPtr handle, AutoFocusCfg tAtFsCfg);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "cvCalArticulation", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern double cvCalArticulation(EvaFunc type, CVImage iImg, int dx = 0, int dy = 1, int ksize = 5, double dRatio = 0.01);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetAutoFocus", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_GetAutoFocus(IntPtr handle, ref int nPos);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_AutoFocusCallBack", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_AutoFocusCallBack(IntPtr handle, AutoFocusCallBack CallBackFun, IntPtr usrData);

        public delegate int AutoFocusCallBack(IntPtr usrData, int nW, int nH, int bpp, int channels, IntPtr pData, int Pos, double evalua);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CalDistanceEx",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CalDistanceEx(int nPos, ref double dDis);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "MovePostionVid", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool MovePostionVid(int nPosition, bool bdirection, uint dwTimeOut);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "GetPostionVid", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool GetPostionVid(ref int nPosition, uint dwTimeOut);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "InitComCanon",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool InitComCanon(string ComName, uint BaudRate);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "IsOpenCanon",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool IsOpenCanon();

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "ShutDownCanon",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool ShutDownCanon();

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "MovePostionCanon",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool MovePostionCanon(int nPosition, uint dwTimeOut);



        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "GetPostionCanon",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool GetPostionCanon(ref int nPosition, uint dwTimeOut);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "cvDisplayImage",   CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool cvDisplayImage(IntPtr hWnd, CRECT rt, CVImage iImg);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "cvCalcFit",   CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool cvCalcFit(int nFit, double[] arrX, double[] arrY, int nDataSize);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_CreateFit",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_CreateFit(int id, int nFit, double[] arrX, double[] arrY, int nDataSize);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "cvGetFitFy",   CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool cvGetFitFy(double dXvalue, ref double dYvalue);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetFitFy", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_GetFitFy(int id, double dXvalue, ref double dYvalue);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "cvCalcFiveDot",   CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool cvCalcFiveDot(CVImage iImg, double[] x, double[] y, int nThreshold);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "getCirclePoint",   CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool getCirclePoint(uint w, uint h, uint bpp, uint channels, byte[] imgData, int thresholdValue, ref float x, ref float y);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "FovImgCentre",    CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool FovImgCentre(CVImage tImg, double Radio, double cameraDegrees, ref double fovDegrees, int thresholdValus, double dFovDist, FovPattern pattern, FovType type);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "SFRCalculation",     CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int SFRCalculation(CVImage tImg, CRECT rtROI, double gamma, float[] pdfrequency, float[] pdomainSamplingData, int nLen);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "getCirclePoint",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool getCirclePoint(CVImage matsrc, int thresholdValue, ref System.Windows.Point tPt);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "SetMachineVid", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern void SetMachineVid(int ucMachineNO);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_ReleaseFit",CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_ReleaseFit(int nId);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "MoveRelPostion",    CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool MovePostion(IntPtr handle, int nPosition, uint dwTimeOut);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "GetPosition",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool GetPosition(IntPtr handle, ref int nPosition, uint dwTimeOut);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "GoHome",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool GoHome(IntPtr handle);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "ShutDown", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool ShutDown(IntPtr handle);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "InitCom",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool InitCom(IntPtr handle, FOCUS_COMMUN eFOCUS_COMMUN, string ComName, uint BaudRate);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "IsOpen",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool IsOpen(IntPtr handle);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "MoveDiaphragm",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool MoveDiaphragm(IntPtr handle, float dPosition, uint dwTimeOut);

        //[DllImport(LIBRARY_CVCAMERA, EntryPoint = "DistortionCheck",CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        //public unsafe static extern int DistortionCheck(CVImage tImg, SIZE iSize, BlobThreParams tBlobThreParams, float[] finalPointsX, float[] finalPointsY, ref double pointx, ref double pointy, ref double maxErrorRatio, ref double t, CornerType type /*= Circlepoint*/, SlopeType sType /*= CenterPoint*/, LayoutType lType /*= SlopeIN*/);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "DistortionCheck", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int DistortionCheck(CVImage tImg, SIZE iSize, ST_BlobThreParams tBlobThreParams, float[] finalPointsX, float[] finalPointsY, ref double pointx, ref double pointy, ref double maxErrorRatio, ref double t, CornerType type /*= Circlepoint*/, SlopeType sType /*= CenterPoint*/, LayoutType lType /*= SlopeIN*/, DistortionType dType);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "FovImgCentreEX",CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool FovImgCentreEX(CVImage tImg, float x_c, float y_c, float x_p, float y_p, double Radio, double cameraDegrees, ref double fovDegrees, int thresholdValus, double dFovDist, FovPattern pattern, FovType type);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "GhostGlareDectect",
        CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool GhostGlareDectect(CVImage tImg, int radius, int cols, int rows, float ratioH, float ratioL, string path, float[] centersX, float[] centersY, float[] blobGray, float[] dstGray, ref int memSizeH, ref int numArrH, int[] arrH, int[] dataH_X, int[] dataH_Y, ref int memSizeL, ref int numArrL, int[] arrL, int[] dataL_X, int[] dataL_Y);
    }
    public struct C_AoiParam
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

    public struct PartiCle
    {
        public double contrast;
        public int area;
        public int x;
        public int y;
        public int color;
    };

    public struct IRECT
    {
        public IRECT(int tx, int ty, int tcx, int tcy)
        {
            x = tx;
            y = ty;
            cx = tcx;
            cy = tcy;
        }
        public int x { get; set; }
        public int y { get; set; }
        public int cx { get; set; }
        public int cy { get; set; }
    };
    public enum DistortionType //TV畸变H,V方向与光学畸变的检测方法
    {
        OpticsDist = 0,
        TVDistH = 1,
        TVDistV = 2,
    };
}