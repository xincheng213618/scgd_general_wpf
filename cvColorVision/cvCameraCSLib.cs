#pragma warning disable  CA2101,CA1707,CA1401,CA1051,CA1838,CA1711,CS0649,CA2211,CA1708,CA1720
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;
using System.IO;
using System.ComponentModel;
using cvColorVision.Util;
using OpenCvSharp;

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

    public class ExpTimeCfg
    {
        public int autoExpTimeBegin { set; get; }
        public bool autoExpFlag { set; get; }
        //自动同步频率
        public float autoExpSyncFreq { set; get; }
        public float autoExpSaturation { set; get; }
        public ushort autoExpSatMaxAD { set; get; }
        //
        public double autoExpMaxPecentage { set; get; }
        //误差值
        public float autoExpSatDev { set; get; }
        //最大/小曝光
        public float maxExpTime { set; get; }
        public float minExpTime { set; get; }
        //burst的阈值
        public float burstThreshold { set; get; }
    }
    public struct SIZE
    {
        public int cx;
        public int cy;
    };
    public enum CornerType //角点提取方法
    {
        Circlepoint = 0,    //圆点提取
        Checkerboard = 1,   //棋盘格角点提取
    };
    public enum SlopeType  //斜率计算方法
    {
        CenterPoint = 0,    //中心点九点取斜率
        lb_Variance = 1,        //去除方差较大的点后取斜率
    };

    public enum LayoutType //理想点布点方法
    {
        SlopeIN = 0,    //采用斜率布点
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
            st_cfg.filterByColor = this.filterByColor;
            st_cfg.blobColor = this.blobColor;
            st_cfg.minThreshold = this.minThreshold;
            st_cfg.thresholdStep = this.thresholdStep;
            st_cfg.maxThreshold = this.maxThreshold;
            st_cfg.minDistBetweenBlobs = this.minDistBetweenBlobs;
            st_cfg.filterByArea = this.filterByArea;
            st_cfg.minArea = this.minArea;
            st_cfg.maxArea = this.maxArea;
            st_cfg.minRepeatability = this.minRepeatability;
            st_cfg.filterByCircularity = this.filterByCircularity;
            st_cfg.minCircularity = this.minCircularity;
            st_cfg.maxCircularity = this.maxCircularity;
            st_cfg.filterByConvexity = this.filterByConvexity;
            st_cfg.minConvexity = this.minConvexity;
            st_cfg.maxConvexity = this.maxConvexity;
            st_cfg.filterByInertia = this.filterByInertia;
            st_cfg.minInertiaRatio = this.minInertiaRatio;
            st_cfg.maxInertiaRatio = this.maxInertiaRatio;
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
    public struct HImage
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
        public ExpTimeCfg expTimeCfg;
        public CameraCfg cameraCfg { set; get; }
        public List<CalibrationItem> calibrationLibCfg;
        public List<ChannelCfg> channelCfg;

        public ProjectSysCfg()
        {
            expTimeCfg = new ExpTimeCfg();
            calibrationLibCfg = new List<CalibrationItem>();
            channelCfg = new List<ChannelCfg>();
            cameraCfg = new CameraCfg();
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
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetCameraID",
         CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        private unsafe static extern bool GetCameraID(CameraType eType, StringBuilder sn, int len);

        public static bool GetCameraID(CameraType eType, ref string szText)
        {
            StringBuilder builder = new StringBuilder(256);

            if (GetCameraID(eType, builder, 256))
            {
                szText = builder.ToString();
                return true;
            }

            return false;
        }
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetAllCameraID", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        private unsafe static extern bool GetAllCameraID_Gen(CameraType eType, StringBuilder sn, int len);
        public static bool GetAllCameraID(CameraType eType, ref string szText)
        {
            StringBuilder builder = new StringBuilder(256);
            if (GetAllCameraID_Gen(eType, builder, 256))
            {
                szText = builder.ToString();
                return true;
            }
            return false;
        }

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
        public unsafe static extern bool CM_SetCalibParam(IntPtr handle, CalibrationType cType, bool bEnabled, string filename);

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

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetXYZxyuvRect", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_GetXYZxyuvRect(IntPtr handle, int pX, int pY, ref float X, ref float Y, ref float Z , ref float x, ref float y, ref float u, ref float v, int nRw, int nRh);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetXYZCircle", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_GetXYZCircle(IntPtr handle, int nX, int nY, ref float dX, ref float dY, ref float dZ,  double nRadius = 0.0);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetXYZCircleEx",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_GetXYZCircleEx(IntPtr handle, int[] pX, int[] pY, float[] pdX, float[] pdY,  float[] pdZ, int nLen, string szFileName, double nRadius = 0.0);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetYCircle", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_GetYCircle(IntPtr handle, int nX, int nY, ref float dY, double nRadius = 0.0);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetYCircleEx", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_GetYCircleEx(IntPtr handle, int[] pX, int[] pY, float[] pdY, int nLen, string szFileName, double nRadius = 0.0);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetYRect",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_GetYRect(IntPtr handle, int pX, int pY, ref float Y, int nRw, int nRh);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetYRectEx", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_GetYRectEx(IntPtr handle, int[] pX, int[] pY, float[] pdY, int nLen, string szFileName,
            int nRw, int nRh);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_SetBufferXYZ",CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern bool CM_SetBufferXYZ(IntPtr handle, uint w, uint h, uint bpp, uint channels, byte[] imgdata);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_InitXYZ",CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]//初始化XYZ用于执行校正
        public static extern bool CM_InitXYZ(IntPtr handle);


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
        public unsafe static extern double cvCalArticulation(EvaFunc type, HImage iImg, int dx = 0, int dy = 1, int ksize = 5, double dRatio = 0.01);

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
        public unsafe static extern bool cvDisplayImage(IntPtr hWnd, CRECT rt, HImage iImg);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "cvCalcFit",   CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool cvCalcFit(int nFit, double[] arrX, double[] arrY, int nDataSize);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_CreateFit",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_CreateFit(int id, int nFit, double[] arrX, double[] arrY, int nDataSize);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "cvGetFitFy",   CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool cvGetFitFy(double dXvalue, ref double dYvalue);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CM_GetFitFy", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_GetFitFy(int id, double dXvalue, ref double dYvalue);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "cvCalcFiveDot",   CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool cvCalcFiveDot(HImage iImg, double[] x, double[] y, int nThreshold);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "getCirclePoint",   CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool getCirclePoint(uint w, uint h, uint bpp, uint channels, byte[] imgData, int thresholdValue, ref float x, ref float y);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "FovImgCentre",    CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool FovImgCentre(HImage tImg, double Radio, double cameraDegrees, ref double fovDegrees, int thresholdValus, double dFovDist, FovPattern pattern, FovType type);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "SFRCalculation",     CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int SFRCalculation(HImage tImg, CRECT rtROI, double gamma, float[] pdfrequency, float[] pdomainSamplingData, int nLen);
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "getCirclePoint",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool getCirclePoint(HImage matsrc, int thresholdValue, ref System.Windows.Point tPt);
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
        //public unsafe static extern int DistortionCheck(HImage tImg, SIZE iSize, BlobThreParams tBlobThreParams, float[] finalPointsX, float[] finalPointsY, ref double pointx, ref double pointy, ref double maxErrorRatio, ref double t, CornerType type /*= Circlepoint*/, SlopeType sType /*= CenterPoint*/, LayoutType lType /*= SlopeIN*/);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "DistortionCheck", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int DistortionCheck(HImage tImg, SIZE iSize, ST_BlobThreParams tBlobThreParams, float[] finalPointsX, float[] finalPointsY, ref double pointx, ref double pointy, ref double maxErrorRatio, ref double t, CornerType type /*= Circlepoint*/, SlopeType sType /*= CenterPoint*/, LayoutType lType /*= SlopeIN*/, DistortionType dType);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "FovImgCentreEX",CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool FovImgCentreEX(HImage tImg, float x_c, float y_c, float x_p, float y_p, double Radio, double cameraDegrees, ref double fovDegrees, int thresholdValus, double dFovDist, FovPattern pattern, FovType type);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "GhostGlareDectect",
        CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool GhostGlareDectect(HImage tImg, int radius, int cols, int rows, float ratioH, float ratioL, string path, float[] centersX, float[] centersY, float[] blobGray, float[] dstGray, ref int memSizeH, ref int numArrH, int[] arrH, int[] dataH_X, int[] dataH_Y, ref int memSizeL, ref int numArrL, int[] arrL, int[] dataL_X, int[] dataL_Y);
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
        public int x;
        public int y;
        public int cx;
        public int cy;
    };




    public enum CVOLED_ERROR
    {
        CVOLED_SUCCESS = 0,
        CVOLED_PARAM_E,   //参数错误
        CVOLED_INPUT_E,   //输入错误
        CVOLED_SCRREN_NOT_SUPPORT, //屏幕类型不支持 
        CVOLED_INIT_E,     //初始化错误
        SAVE_E,           //保存文件错误
        OUT_OF_BOUNDRY,   //越界
        ALGORITHM_E,      //算法错误
        MORIE_E, //       摩尔纹
    };
    public enum CVLED_COLOR
    {
        BLUE = 0,
        GREEN = 1,
        RED = 2,
    };
    public enum DistortionType //TV畸变H,V方向与光学畸变的检测方法
    {
        OpticsDist = 0,
        TVDistH = 1,
        TVDistV = 2,
    };

    public class CvOledDLL
    {
        private const string LIBRARY_CVOLED = "cvOled.dll";
        [DllImport(LIBRARY_CVOLED, EntryPoint = "CvOledInit",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern void CvOledInit();
        [DllImport(LIBRARY_CVOLED, EntryPoint = "CvOledRealse",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern void CvOledRealse();

        [DllImport(LIBRARY_CVOLED, EntryPoint = "CvLoadParam",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern CVOLED_ERROR CvLoadParam(string json);
        [DllImport(LIBRARY_CVOLED, EntryPoint = "loadPictureMemLength",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern ulong loadPictureMemLength(string path);
        [DllImport(LIBRARY_CVOLED, EntryPoint = "loadPicture",   CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern int loadPicture(string path, ref int w, ref int h, byte[] imgdata);
        [DllImport(LIBRARY_CVOLED, EntryPoint = "findDotsArray",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern CVOLED_ERROR findDotsArray(int w, int h, byte[] imgdata, int type, CVLED_COLOR color);
        [DllImport(LIBRARY_CVOLED, EntryPoint = "rebuildPixels", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern CVOLED_ERROR rebuildPixels(int w, int h, byte[] imgdata, int type, CVLED_COLOR color, float exp, string path);
        [DllImport(LIBRARY_CVOLED, EntryPoint = "morieFilter", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern CVOLED_ERROR morieFilter(int w, int h, byte[] imgdata, int type, string path);
    }
    public class KBDLL
    {
        private const string LIBRARY_KB = "cvCameraKb.dll";
        [DllImport(LIBRARY_KB, EntryPoint = "createResultCsv", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern void createResultCsv(string cfg_path, string rst_path);
        [DllImport(LIBRARY_KB, EntryPoint = "processKeyborad",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern void processKeyborad(string img_path, string cfg_path, string rst_path, string setting_json, string serial_no, float exp, uint w, uint h, int PicPart, double correctData, string correctPath);
        [DllImport(LIBRARY_KB, EntryPoint = "getPassFail",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern void getPassFail(StringBuilder pass);
        [DllImport(LIBRARY_KB, EntryPoint = "calculateHalo",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern double calculateHalo(uint w, uint h, byte[] rawArray, int x, int y, int width, int height, int outMOVE);
        [DllImport(LIBRARY_KB, EntryPoint = "InitResource",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern void InitResource();
        [DllImport(LIBRARY_KB, EntryPoint = "CM_ExportToTIFF",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool CM_ExportToTIFF(string fileName, uint w, uint h, byte[] rawArray, ulong buflen, bool iscolor, float img_rotate_angle);

    }
    public enum PG_Type
    {
        GX09C_LCM = 0,
        SKYCODE,
    };

    public enum Communicate_Type
    {
        Communicate_Tcp = 0,
        Communicate_Serial,
    };

    public struct ScannCodeData
    {
        public Communicate_Type communicate_Type;
        public string szComName;
        public ulong BaudRate;
        public string szCmd;
        public ulong dwTimeOut;
        public int CmdType;
    }

    public struct TpCheckData
    {
        public Communicate_Type communicate_Type;
        public string szIPAddress;
        public uint nPort;
        public string szPassWord;
        public string szStart;
        public string szFilePath;
        public int findWaitTime;
    }

    public struct PGComData
    {
        public PG_Type pg_type;
        public Communicate_Type communicate_Type;
        public string szComName;
        public ulong BaudRate;
    }






    public class SimpleFeatures 
    {
        public class FOVParam
        {
            [Category("FOV"), Description("计算FOV时中心区亮度的百分比多少认为是暗区")]
            public double Radio { get; set; }
            [Category("FOV"), Description("相机镜头有效像素对应的角度")]
            public double CameraDegrees { get; set; }
            [Category("FOV"), Description("FOV中计算圆心或者矩心时使用的二值化阈值")]
            public int ThresholdValus { get; set; }

            [Category("FOV"), Description("相机镜头使用的有效像素")]
            public double DFovDist { get; set; }

            [Category("FOV"), Description("计算pattern(FovCircle-圆形；FovRectangle-矩形)")]
            public FovPattern FovPattern { get; set; }
            [Category("FOV"), Description("计算路线(Horizontal-水平；Vertical-垂直；Leaning-斜向)")]
            public FovType FovType { get; set; }

            [Category("SFR"), Description("SFR gamma")]
            public double SFR_gamma { get; set; }
            [Category("MTF"), Description("MTF dRatio")]
            public double MTF_dRatio { get; set; }

            [Category("Ghost"), Description("待检测鬼影点阵的半径长度(像素)")]
            public int Ghost_radius { get; set; }
            [Category("Ghost"), Description("待检测鬼影点阵的列数")]
            public int Ghost_cols { get; set; }
            [Category("Ghost"), Description("待检测鬼影点阵的行数")]
            public int Ghost_rows { get; set; }
            [Category("Ghost"), Description("待检测鬼影的中心灰度百分比上限")]
            public float Ghost_ratioH { get; set; }
            [Category("Ghost"), Description("待检测鬼影的中心灰度百分比下限")]
            public float Ghost_ratioL { get; set; }

            [Category("CentreLine"), Description("图像进行二值化的阈值")]
            public int CL_iThresh { get; set; }
            [Category("CentreLine"), Description("轮廓的周长阈值")]
            public int CL_minSize { get; set; }
            [Category("CentreLine"), Description("最终获取的中心线点集最小内部坐标数量")]
            public int CL_minCentresSize { get; set; }
            [Category("CentreLine"), Description("中心线的大概宽度")]
            public int CL_lineWidth { get; set; }
            [Category("CentreLine"), Description("中心线递归使用递归线段长度(一般默认20即可)")]
            public int CL_LENGTH { get; set; }


            public static FOVParam cfg;

            public FOVParam()
            {
                Radio = 0.2;
                CameraDegrees = 0.2;
                ThresholdValus = 20;
                DFovDist = 8443;
                FovPattern = FovPattern.FovCircle;
                FovType = FovType.Horizontal;
                SFR_gamma = 1.0;
                MTF_dRatio = 0.01;
                Ghost_radius = 65;
                Ghost_ratioH = 0.4f;
                Ghost_ratioL = 0.2f;
                Ghost_cols = 3;
                Ghost_rows = 3;
                CL_iThresh = 85;
                CL_minSize = 200;
                CL_minCentresSize = 100;
                CL_lineWidth = 60;
                CL_LENGTH = 20;
            }

            public static FOVParam Load(string fileName)
            {
                FOVParam pm = CfgFile.Load<FOVParam>(fileName);
                if (pm == null)
                {
                    pm = new FOVParam();
                    CfgFile.Save(fileName, pm);
                }
                cfg = pm;
                return cfg;
            }
        }


        public bool FOV(CameraType connectType, uint wRGB,uint hRGB,uint bppRGB,uint channalsRGB, byte[] srcrawRGB, string fovParamCfg,ref double fovDegrees_ref, ref string ErrorData) 
        {
            //if (wRGB > 0& hRGB>0& bppRGB>0&& channalsRGB>0)
            //{
            //    if (connectType == CameraType.LV_Q || connectType == CameraType.BV_Q || connectType == CameraType.CV_Q)
            //    {
            //        FOVParam pm = CfgFile.Load<FOVParam>(fovParamCfg);
            //        if (pm==null)
            //        {
            //            ErrorData = "没有读到设定的FOV本地配置文件";
            //            return false;
            //        }

            //        HImage himage_Fov = new HImage();
            //        himage_Fov.nWidth = wRGB;
            //        himage_Fov.nHeight = hRGB;
            //        himage_Fov.nBpp = bppRGB;
            //        himage_Fov.nChannels = channalsRGB;

            //        fovDegrees_ref = 0;
            //        unsafe
            //        {
            //            if (srcrawRGB != null)
            //            {
            //                fixed (byte* pAdr = srcrawRGB)
            //                {
            //                    himage_Fov.pData = (IntPtr)pAdr;

            //                    try
            //                    {
            //                        if (!cvCameraCSLib.FovImgCentre(himage_Fov, pm.Radio, pm.CameraDegrees, ref fovDegrees_ref, pm.thresholdValus, pm.dFovDist, FovPattern.FovCircle, FovType.Horizontal))
            //                        {
            //                            ErrorData = "FOV执行失败！";
            //                            return false;
            //                        }
            //                        else
            //                        {
            //                            //string mess = "fov:" + fovDegrees_ref.ToString();
            //                            //MessageBox.Show(mess);
            //                            return true;
            //                        }
            //                    }
            //                    catch (Exception ex)
            //                    {
            //                        ErrorData = "FOV执行失败！| " + ex.Message;
            //                        return false;
            //                    }

            //                }
            //            }
            //            else
            //            {
            //                ErrorData = "图像数据为空！";
            //                return false;
            //            }
            //        }
            //    }
            //    else
            //    {
            //        ErrorData = "图像格式只支持LV,BV,CV";
            //        return false;
            //    }
            //}
            //else
            //{
            //    ErrorData = "图像数据W,H,BPP,channels中有一个小于等于0";
            //    return false;
            //}

            if (wRGB > 0 & hRGB > 0 & bppRGB > 0 && channalsRGB > 0)
            {
                CameraModeType cameraMode = connectType.GetCameraModeType();
                if (cameraMode == CameraModeType.LV || cameraMode == CameraModeType.BV || cameraMode == CameraModeType.CV)
                {
                    FOVParam pm = CfgFile.Load<FOVParam>(fovParamCfg);

                    if (pm == null)
                        return false;
                    HImage himage_Fov = new HImage();
                    himage_Fov.nWidth = wRGB;
                    himage_Fov.nHeight = hRGB;
                    himage_Fov.nBpp = bppRGB;
                    himage_Fov.nChannels = channalsRGB;
                    fovDegrees_ref = 0;
                    FovPattern fovPattern = pm.FovPattern;
                    FovType fovType = pm.FovType;
                    bool fovResult = false;

                    unsafe
                    {
                        if (srcrawRGB != null)
                        {
                            fixed (byte* pAdr = srcrawRGB)
                            {
                                himage_Fov.pData = (IntPtr)pAdr;

                                try
                                {
                                    //if (fovPattern == FovPattern.FovCircle && fovType == FovType.Leaning)
                                    //{
                                    //    if (isFovCircle && roiPointData_Temporary != null)
                                    //    {
                                    //        fovResult = cvCameraCSLib.FovImgCentreEX(himage_Fov,
                                    //            roiPointData_Temporary.Img_x, roiPointData_Temporary.Img_y, fovLeaning.X, fovLeaning.Y,
                                    //            pm.Radio, pm.CameraDegrees, ref fovDegrees_ref, pm.thresholdValus, pm.dFovDist, fovPattern, fovType);
                                    //        if (fovResult) saveCsv_FOV("Result", "FovCircle", "Leaning", fovDegrees_ref);
                                    //        else MessageBox.Show("FOV执行失败！");
                                    //    }
                                    //    else
                                    //    {
                                    //        MessageBox.Show("请先选择圆形测量点");
                                    //    }
                                    //}
                                    //else
                                    //{

                                    //}

                                    fovType = FovType.Horizontal;
                                    fovResult = cvCameraCSLib.FovImgCentre(himage_Fov, pm.Radio, pm.CameraDegrees, ref fovDegrees_ref, pm.ThresholdValus, pm.DFovDist, fovPattern, fovType);
                                    if (fovResult)
                                    {
                                        saveCsv_FOV("Result", GetFovPattern(pm.FovPattern), "Horizontal", fovDegrees_ref);
                                    }
                                    else 
                                    {
                                        ErrorData = "Horizontal FOV执行失败！";
                                        return false;
                                    } 



                                    fovType = FovType.Vertical;
                                    fovResult = cvCameraCSLib.FovImgCentre(himage_Fov, pm.Radio, pm.CameraDegrees, ref fovDegrees_ref, pm.ThresholdValus, pm.DFovDist, fovPattern, fovType);
                                    if (fovResult)
                                    {
                                        saveCsv_FOV("Result", GetFovPattern(pm.FovPattern), "Vertical", fovDegrees_ref);
                                        return true;
                                    }
                                    else 
                                    {
                                        ErrorData = "Vertical FOV执行失败！";
                                        return false;
                                    } 
                                }
                                catch (Exception ex)
                                {
                                    ErrorData = "FOV执行失败！| " + ex.Message;
                                    return false;
                                }
                            }
                        }
                        else
                        {
                            ErrorData = "图像数据为空！";
                            return false;
                        }
                    }
                }
                else
                {
                    ErrorData = "图像格式只支持LV,BV,CV";
                    return false;
                }
            }
            else
            {
                ErrorData = "请先点击测量 ";
                return false;
            }
        }

        private static string GetFovPattern(FovPattern pattern)
        {
            string fovPattern = "";
            switch (pattern)
            {
                case FovPattern.FovCircle:
                    fovPattern = "FovCircle";
                    break;
                case FovPattern.FovRectangle:
                    fovPattern = "FovRectangle";
                    break;
                default:
                    fovPattern = "FovCircle";
                    break;
            }

            return fovPattern;
        }

        private static void saveCsv_FOV(string path, string fovPattern, string fovType, double fovDegrees)
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
            path += $"{(path.Substring(path.Length - 1, 1) != "/" ? "\\" : "")}FOVResult_{DateTime.Now:yyyyMMddhhmmss}.csv";
            if (!File.Exists(path))
                cvCameraCSLib.CSVinitialized(path, new List<string>() { "FovPattern", "FovType", "Value" });
            using FileStream fs = new FileStream(path, FileMode.Append, FileAccess.Write);
            using StreamWriter sw = new StreamWriter(fs, Encoding.UTF8);
            sw.WriteLine($"{fovPattern},{fovType},{fovDegrees}");
        }

        public class DistoData 
        {
            public uint wRGB { set; get; }
            public uint hRGB { set; get; }
            public uint bppRGB { set; get; }
            public uint channalsRGB { set; get; }
            public byte[] srcrawRGB { set; get; }
            public string DistoParamCfg { set; get; }
            public string ErrorData { set; get; }

            public bool checkResult { set; get; }
        }



        public static bool Disto(DistoData distoData) 
        {
            try
            {
                //try the five second method with a 6 second timeout
                CallWithTimeout(distoData, FiveSecondMethod, 15000);
                return true;
            }
            catch
            {
                MessageBox.Show("畸变计算超时"); 
                return false; 
            }
        }

        static void CallWithTimeout(DistoData distoData,Action<DistoData> action, int timeoutMilliseconds)
        {
            Thread threadToKill = null;
            Action wrappedAction = () =>
            {
                threadToKill = Thread.CurrentThread;
                action(distoData);
            };

            IAsyncResult result = wrappedAction.BeginInvoke(null, null);
            if (result.AsyncWaitHandle.WaitOne(timeoutMilliseconds))
            {
                wrappedAction.EndInvoke(result);
            }
            else
            {
#pragma warning disable SYSLIB0006
                threadToKill?.Abort();
#pragma warning restore SYSLIB0006
                throw new TimeoutException();

            }
        }

        public static void FiveSecondMethod(DistoData distoData)
        {
            BlobThreParams blobThreParams = BlobThreParams.Load();
            if (distoData.wRGB > 0&& distoData.hRGB >0&& distoData.bppRGB >0&& distoData.channalsRGB >0)
            {
                //初始化HImage
                HImage tImg = new HImage();
                tImg.nBpp = distoData.bppRGB;
                tImg.nChannels = distoData.channalsRGB;
                tImg.nWidth = distoData.wRGB;
                tImg.nHeight = distoData.hRGB;

                if (distoData.srcrawRGB != null)
                {
                    GCHandle hObject = GCHandle.Alloc(distoData.srcrawRGB, GCHandleType.Pinned);
                    tImg.pData = hObject.AddrOfPinnedObject();
                    //初始化SIZE
                    SIZE sIZE = new SIZE();
                    sIZE.cx = blobThreParams.cx;
                    sIZE.cy = blobThreParams.cy;
                    //初始化输出的理想坐标点系XY坐标集
                    float[] finalPointsX = new float[10000];
                    float[] finalPointsY = new float[10000];
                    //初始化最大畸变点XY坐标
                    double pointx = 0, pointy = 0;
                    //初始化最大畸变点畸变率
                    double maxErrorRatio = 0;
                    //初始化图像在平面的旋转角度
                    double t = 0;
                    //选取使用角点提取的方法
                    CornerType cornerType = blobThreParams.cornerType;
                    //选取点阵斜率计算方法
                    SlopeType slopeType = SlopeType.CenterPoint;
                    //选取生成理想点阵的布点方式
                    LayoutType layoutType = LayoutType.SlopeIN;
                    DistortionType distortionType = DistortionType.OpticsDist;
                    string strDisType = "OpticsDist";
                    if (cvCameraCSLib.DistortionCheck(tImg, sIZE, blobThreParams.st_cfg, finalPointsX,
                    finalPointsY, ref pointx, ref pointy, ref maxErrorRatio, ref t, cornerType,
                    slopeType, layoutType, distortionType) < 1)
                    {
                        //MessageBox.Show("DistortionCheck执行结果失败！");
                        //return;
                        distoData.ErrorData = "DistortionCheck执行结果失败";
                        distoData.checkResult= false;
                    }
                    else
                    {
                        //保存结果
                        saveCsv_Distortion("Result", pointx, pointy, maxErrorRatio, t, strDisType);
                        //MessageBox.Show("执行结束");
                    }
                    distortionType = DistortionType.TVDistH;
                    strDisType = "TVDistH";
                    if (cvCameraCSLib.DistortionCheck(tImg, sIZE, blobThreParams.st_cfg, finalPointsX,
                    finalPointsY, ref pointx, ref pointy, ref maxErrorRatio, ref t, cornerType,
                    slopeType, layoutType, distortionType) < 1)
                    {
                        //MessageBox.Show("DistortionCheck执行结果失败！");
                        //return;
                        distoData.ErrorData = "DistortionCheck执行结果失败";
                        distoData.checkResult = false;
                    }
                    else
                    {
                        //保存结果
                        saveCsv_Distortion("Result", pointx, pointy, maxErrorRatio, t, strDisType);
                        //MessageBox.Show("执行结束");
                    }
                    distortionType = DistortionType.TVDistV;
                    strDisType = "TVDistV";
                    if (cvCameraCSLib.DistortionCheck(tImg, sIZE, blobThreParams.st_cfg, finalPointsX,
                    finalPointsY, ref pointx, ref pointy, ref maxErrorRatio, ref t, cornerType,
                    slopeType, layoutType, distortionType) < 1)
                    {
                        //MessageBox.Show("DistortionCheck执行结果失败！");
                        //return;
                        distoData.ErrorData = "DistortionCheck执行结果失败";
                        distoData.checkResult = false;
                    }
                    else
                    {
                        //保存结果
                        saveCsv_Distortion("Result", pointx, pointy, maxErrorRatio, t, strDisType);
                        //MessageBox.Show("执行结束");
                    }
                    //MessageBox.Show("执行结束");
                    hObject.Free();
                    distoData.ErrorData = "执行结束";
                    distoData.checkResult = true;
                }
                else
                {
                    //MessageBox.Show("图像数据为空");
                    //MessageBox.Show("请先点击测量");
                    distoData.ErrorData = "图像数据为空";
                    distoData.checkResult = false;
                }
            }
            else
            {
                //MessageBox.Show("请先点击测量");
                distoData.ErrorData = "请先点击测量";
                distoData.checkResult = false;
            }
        }

        private static void saveCsv_Distortion(string path, double pointx, double pointy, double maxErrorRatio, double t, string strDisType)
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
            path = path + (path.Substring(path.Length - 1, 1) != "/" ?  "\\" :"")  + "DistortionResult.csv";
            if (!File.Exists(path))
                cvCameraCSLib.CSVinitialized(path, new List<string>() { "Time", "pointx", "pointy", "maxErrorRatio", "t", "DistortionType" });


            using FileStream fs = new FileStream(path, FileMode.Append, FileAccess.Write);
            using StreamWriter sw = new StreamWriter(fs, Encoding.UTF8);
            sw.Write(DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss.fff"));
            sw.Write(",");
            sw.Write(pointx);
            sw.Write(",");
            sw.Write(pointy);
            sw.Write(",");
            sw.Write(maxErrorRatio);
            sw.Write(",");
            sw.Write(t);
            sw.Write(",");
            sw.Write(strDisType);
            sw.Write(",");
            sw.WriteLine("");
            sw.Flush();
        }


        public bool Ghost(List<System.Drawing.Point> listGhostH, List<System.Drawing.Point> listGhostL, HImage tImg) 
        {
            listGhostH?.Clear();
            listGhostL?.Clear();
            FOVParam pm = FOVParam.cfg;

            int NxN = pm.Ghost_cols * pm.Ghost_rows;
            int memSizeH = 20 * 1024;//储存所有点阵坐标需要申请的内存
            int memSizeL = 20 * 1024;//储存所有鬼影坐标需要申请的内存
            int numArrH = NxN;//包含的点阵数量
            int numArrL = NxN;//包含的鬼影集数量
            int[] arrH = new int[numArrH];//每个点阵轮廓的点坐标数量
            int[] arrL = new int[numArrL];//每个鬼影轮廓的点坐标数量
            int[] dataH_X = new int[memSizeH];//所有点阵轮廓的X坐标集
            int[] dataL_X = new int[memSizeL];//所有鬼影轮廓的X坐标集
            int[] dataH_Y = new int[memSizeH];//所有点阵轮廓的Y坐标集
            int[] dataL_Y = new int[memSizeL];//所有鬼影轮廓的Y坐标集
            float[] centersX = new float[NxN];//检出的鬼影点阵质心X坐标
            float[] centersY = new float[NxN];//检出的鬼影点阵质心Y坐标
            float[] dstGray = new float[NxN];//检出鬼影区域的灰度均值集
            float[] blobGray = new float[NxN];//检出光斑的灰度均值集
            string path = GetPath("Result");//
            bool ret = cvCameraCSLib.GhostGlareDectect(tImg, pm.Ghost_radius, pm.Ghost_cols, pm.Ghost_rows, pm.Ghost_ratioH, pm.Ghost_ratioL, path, centersX, centersY, blobGray, dstGray, ref memSizeH, ref numArrH, arrH, dataH_X, dataH_Y, ref memSizeL, ref numArrL, arrL, dataL_X, dataL_Y);
            if (ret)
            {
                save_Ghost_result("Result", NxN, centersX, centersY, blobGray, dstGray, numArrH, arrH, dataH_X, dataH_Y, numArrL, arrL, dataL_X, dataL_Y);
            }
            else if (memSizeH > 20 * 1024 || memSizeL > 20 * 1024)
            {
                dataH_X = new int[memSizeH];//所有点阵轮廓的X坐标集
                dataL_X = new int[memSizeL];//所有鬼影轮廓的X坐标集
                dataH_Y = new int[memSizeH];//所有点阵轮廓的Y坐标集
                dataL_Y = new int[memSizeL];//所有鬼影轮廓的Y坐标集

                ret = cvCameraCSLib.GhostGlareDectect(tImg, pm.Ghost_radius, pm.Ghost_cols, pm.Ghost_rows, pm.Ghost_ratioH, pm.Ghost_ratioL, path, centersX, centersY, blobGray, dstGray,
                ref memSizeH, ref numArrH, arrH, dataH_X, dataH_Y, ref memSizeL, ref numArrL, arrL, dataL_X, dataL_Y);
                if (ret)
                {
                    save_Ghost_result("Result", NxN, centersX, centersY, blobGray, dstGray, numArrH, arrH, dataH_X, dataH_Y, numArrL, arrL, dataL_X, dataL_Y);
                }

            }
            else
            {
                return false;
            }
            return true;
        }

        private static string GetPath(string path)
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
            return path + (path.Substring(path.Length - 1, 1) != "/"? "\\":"");
        }

        private List<System.Drawing.Point> listGhostH = new List<System.Drawing.Point>();
        private List<System.Drawing.Point> listGhostL = new List<System.Drawing.Point>();
        private  void save_Ghost_result(string path, int nxN, float[] centersX, float[] centersY, float[] blobGray, float[] dstGray, int numArrH, int[] arrH, int[] dataH_X, int[] dataH_Y, int numArrL, int[] arrL, int[] dataL_X, int[] dataL_Y)
        {
            listGhostH ??= new List<System.Drawing.Point>();
            listGhostL ??= new List<System.Drawing.Point>();
            saveCsv_Ghost_xy(path, nxN, centersX, centersY, blobGray, dstGray);
            saveCsv_Ghost_point(path, "点阵", numArrH, arrH, dataH_X, dataH_Y, listGhostH);
            saveCsv_Ghost_point(path, "鬼影", numArrL, arrL, dataL_X, dataL_Y, listGhostL);
        }

        private static void saveCsv_Ghost_xy(string path, int nxN, float[] centersX, float[] centersY, float[] blobGray, float[] dstGray)
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
            path += $"{(path.Substring(path.Length - 1, 1) != "/" ? "\\" : "")}GhostXYResult_{DateTime.Now:yyyyMMddhhmmss}.csv";
            if (!File.Exists(path))
                cvCameraCSLib.CSVinitialized(path, new List<string>() { "centersX", "centersY", "blobGray", "dstGray" });
            using FileStream fs = new FileStream(path, FileMode.Append, FileAccess.Write);
            using StreamWriter sw = new StreamWriter(fs, Encoding.UTF8);
            for (int i = 0; i < nxN; i++)
                sw.WriteLine($"{centersX[i]},{centersY[i]},{blobGray[i]},{dstGray[i]}");
        }

        private static void saveCsv_Ghost_point(string path, string name, int numArr, int[] arr, int[] data_X, int[] data_Y, List<System.Drawing.Point> list)
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
            path += $"{(path.Substring(path.Length - 1, 1) != "/" ? "\\" : "")}GhostResult_{name}_{DateTime.Now:yyyyMMddhhmmss}.csv";
            if (!File.Exists(path))
                cvCameraCSLib.CSVinitialized(path, new List<string>() { "X", "Y" });

            using FileStream fs = new FileStream(path, FileMode.Append, FileAccess.Write);
            using StreamWriter sw = new StreamWriter(fs, Encoding.UTF8);
            int idx = 0;
            for (int x = 0; x < numArr; x++)
                for (int y = 0; y < arr[x]; y++)
                {
                    sw.WriteLine($"{data_X[idx]},{data_Y[idx]}");
                    idx++;
                }
        }







        public class CameraParamGroup
        {
            public float iexp_x { set; get; }

            public float iexp_y { set; get; }

            public float iexp_z { set; get; }

            public float iCx { set; get; }

            public float iCy { set; get; }

            public float iLv { set; get; }
        }

        public class FourColorRoiData
        {
            public int tarHeight { set; get; }

            public int tarWidth { set; get; }

            public int xPos { set; get; }

            public int yPos { set; get; }

        }

        public class ImageData
        {
            public uint iHei { set; get; }

            public uint iWid { set; get; }

            public uint iBpp { set; get; }

            public uint iChannels { set; get; }

            public byte[] pData { set; get; }

        }

        public class TakeCameraCfg
        {
            public float gain;
            public ushort offset;
            public int usbTraffic;
            public string binng;
            public string aperture;
        }

        public class LumChromaParam
        {
            public TakeCameraCfg cameraCfg;
            public float Texp_x;
            public float Texp_y;
            public float Texp_z;
            public float Gain_x;
            public float Gain_y;
            public float Gain_z;
            public float a;
            public float b;
            public float c;
            public float d;

            public LumChromaParam(float texp_x, float texp_y, float texp_z, float gain_x, float gain_y, float gain_z, float a, float b, float c, float d)
            {
                Texp_x = texp_x;
                Texp_y = texp_y;
                Texp_z = texp_z;
                Gain_x = gain_x;
                Gain_y = gain_y;
                Gain_z = gain_z;
                this.a = a;
                this.b = b;
                this.c = c;
                this.d = d;
            }
        }

        public class FourLumChromaParam : LumChromaParam
        {
            public float e;
            public float f;
            public float g;
            public float h;
            public float i;

            public FourLumChromaParam(float texp_x, float texp_y, float texp_z, float gain_x, float gain_y, float gain_z, float a, float b, float c, float d, double e, double f, double g, double h, double i) :
                base(texp_x, texp_y, texp_z, gain_x, gain_y, gain_z, a, b, c, d)
            {
                this.e = (float)e;
                this.f = (float)f;
                this.g = (float)g;
                this.h = (float)h;
                this.i = (float)i;
            }
        }

        public static bool FourColorCreat(string calibrationName, IIntputData[] cameraParamGroup, FourColorRoiData fourColorRoiData, ImageData[] vImgM, ref string ErrorData) 
        {
            if (calibrationName.Length==0)
            {
                ErrorData = "请输入校正文件名";
                return false;
            }
            //判断四个图片的流程是否完成
            //if (this.dataGridView1.Rows[0].Cells[8].Value.ToString() != "测量完成" || this.dataGridView1.Rows[1].Cells[8].Value.ToString() != "测量完成" || this.dataGridView1.Rows[2].Cells[8].Value.ToString() != "测量完成" || this.dataGridView1.Rows[3].Cells[8].Value.ToString() != "测量完成")
            //{
            //    MessageBox.Show("测量流程未全部完成");
            //    return;
            //}

            ////读取一下四个图片的xy等信息
            //for (int i = 0; i < 4; i++)
            //{
            //    intputDatasGroup[i].iexp_x = cameraParamGroup[i].ExpTime[0];
            //    intputDatasGroup[i].iexp_y = cameraParamGroup[i].ExpTime[1];
            //    intputDatasGroup[i].iexp_z = cameraParamGroup[i].ExpTime[2];
            //    if (!float.TryParse(this.dataGridView1.Rows[i].Cells[1].Value.ToString(), out intputDatasGroup[i].iCx))
            //    {
            //        MessageBox.Show("第" + i + "列数据Cx的值输入格式错误，请检查后重新输入");
            //        return;
            //    }
            //    if (!float.TryParse(this.dataGridView1.Rows[i].Cells[2].Value.ToString(), out intputDatasGroup[i].iCy))
            //    {
            //        MessageBox.Show("第" + i + "列数据Cy的值输入格式错误，请检查后重新输入");
            //        return;
            //    }
            //    if (i == 3)
            //    {
            //        if (!float.TryParse(this.dataGridView1.Rows[i].Cells[3].Value.ToString(), out intputDatasGroup[i].iLv))
            //        {
            //            //intputDatasGroup[i].iLv = 0;
            //            MessageBox.Show("第" + i + "列数据Lv的值输入格式错误，请检查后重新输入");
            //            return;
            //        }
            //    }
            //    else
            //    {
            //        intputDatasGroup[i].iLv = 0;
            //    }
            //}

            if (cameraParamGroup == null || cameraParamGroup.Length < 4)
            {
                ErrorData = "cameraParamGroup的数量小于4";
                return false;
            }

            if (vImgM == null || vImgM.Length < 4)
            {
                ErrorData = "vImgM的数量小于4";
                return false;
            }

            //int tarHeight = 0;
            //int tarWidth = 0;
            //int xPos = 0;
            //int yPos = 0;
            //if (!int.TryParse(this.textBox_height.Text, out tarHeight) || !int.TryParse(this.textBox_width.Text, out tarWidth) || !int.TryParse(this.textBox_X.Text, out xPos) || !int.TryParse(this.textBox_Y.Text, out yPos))
            //{
            //    MessageBox.Show("错误的测量点坐标及区域参数，请输入正整数");
            //    return;
            //}

            IRECT iRECTGroup;//计算用的框选范围信息组
            iRECTGroup.cx = fourColorRoiData.tarWidth;
            iRECTGroup.cy = fourColorRoiData.tarHeight;
            iRECTGroup.x = fourColorRoiData.xPos;
            iRECTGroup.y = fourColorRoiData.yPos;

            //for (int i = 0; i < 4; i++)
            //{
            //    iRECTGroup[i].cx = tarHeight;
            //    iRECTGroup[i].cy = tarHeight;
            //    iRECTGroup[i].x = xPos;
            //    iRECTGroup[i].y = yPos;
            //}

            //执行四色校正的计算
            double[]? vcdRltData = new double[9];

            if (!CalColorFour(vImgM, iRECTGroup, cameraParamGroup, ref vcdRltData))
            {
                ErrorData="四色校正计算失败";
                return false;
            }
            //保存四色校正的计算结果
            if (vcdRltData?.Length != 9)
            {
                ErrorData = "四色校正保存异常";
                return false;
            }
            FourLumChromaParam lumChroma = new FourLumChromaParam(0, 0, 0, 1, 1, 1, (float)vcdRltData[0], (float)vcdRltData[1], (float)vcdRltData[2], (float)vcdRltData[3], vcdRltData[4], vcdRltData[5], vcdRltData[6], vcdRltData[7], vcdRltData[8]);
            string szFileName = calibrationName + ".dat";
            string jsonData = JsonConvert.SerializeObject(lumChroma);
            using StreamWriter sw = new StreamWriter(szFileName);
            sw.Write(jsonData);
            return true;
        }


        public struct IIntputData
        {
            public float iexp_x;
            public float iexp_y;
            public float iexp_z;
            public float iCx;
            public float iCy;
            public float iLv;
        };

        /// <summary>
        /// 通过输入的BPP和channels来判断目标图像的mat格式
        public static int Gettype(int nbpp, int nChanles)
        {
            return nbpp switch
            {
                8 => nChanles switch
                {
                    1 => MatType.CV_8UC1,
                    2 => MatType.CV_8UC2,
                    3 => MatType.CV_8UC3,
                    4 => MatType.CV_8UC4,
                    _ => -1,
                },
                16 => nChanles switch
                {
                    1 => MatType.CV_16UC1,
                    2 => MatType.CV_16UC2,
                    3 => MatType.CV_16UC3,
                    4 => MatType.CV_16UC4,
                    _ => -1,
                },
                32 => nChanles switch
                {
                    1 => MatType.CV_32FC1,
                    2 => MatType.CV_32FC2,
                    3 => MatType.CV_32FC3,
                    4 => MatType.CV_32FC4,
                    _ => -1,
                },
                64 => nChanles switch
                {
                    1 => MatType.CV_64FC1,
                    2 => MatType.CV_64FC2,
                    3 => MatType.CV_64FC3,
                    4 => MatType.CV_64FC4,
                    _ => -1,
                },
                _ => -1,
            };
        }

         static float GetRECTGray(byte[] imgdata, int arrY_Height, int arrY_Width, IRECT tRect)
        {
            int idx_w = tRect.x;
            int idx_h = tRect.y;

            int iWid = tRect.cx;
            int iHei = tRect.cy;

            ushort[] tarDataY = new ushort[iWid * iHei];

            for (int i = 0; i < iHei; i++)
            {
                for (int j = 0; j < iWid; j++)
                {
                    tarDataY[i * iWid + j] = imgdata[(i + idx_h) * arrY_Width + idx_w + j];
                }
            }

            float gray = Mean_get(tarDataY, iHei, iWid, new IObRECT(0, 0, 0, 0));
            return gray;
        }

        public struct IObRECT
        {
            public int ob;
            public int obR;
            public int obB;
            public int obT;

            public IObRECT(int iob, int iobR, int iobT, int iobB)
            {
                ob = iob;
                obR = iobR;
                obT = iobT;
                obB = iobB;
            }
        };

        static float Mean_get(ushort[] imageData, int imgHeight, int imgWidth, IObRECT obRect)
        {
            long sum = 0;
            // ob area
            int h = imgHeight;
            int w = imgWidth;

            int wMax = w - obRect.obR;
            int hMax = h - obRect.obB;

            long num = 0;

            for (int i = obRect.obT; i < hMax; i++)             //此处参数？
            {
                for (int j = obRect.ob; j < wMax; j++)
                {
                    sum += imageData[i * w + j];
                    num++;
                }
            }

            return (float)sum / num;
        }

        static double[] Gauss_Gao(double[][] a, ref double[] ans)
        {
            int row = 9;
            int column = 10;
            for (int i = 0; i < column - 1; i++)
            {
                for (int j = i + 1; j < row; j++)
                {
                    double t = -a[j][i] / a[i][i];
                    double[] l = new double[10];
                    for (int k = 0; k < column; k++)
                    {
                        l[k] = a[i][k] * t + a[j][k];
                    }
                    for (int k = 0; k < column; k++)
                    {
                        a[j][k] = l[k];
                    }
                }
            }

            for (int i = row - 1; i >= 0; i--)
            {
                //ans[i] = a[i][column-1]/a[i][column-2];
                double t = a[i][column - 1];
                for (int j = column - 2; j > i; j--)
                {

                    t -= (a[i][j] * ans[j]);
                }
                ans[i] = t / a[i][i];
            }

            return ans;
        }

        static bool CalColorFour(ImageData[] vImgM, IRECT vcIRECT, IIntputData[] vcIIntputData, ref double[]? vcdRlt)
        {
            if (vImgM.Length != 4 && vcIIntputData.Length != 4)
            {
                return false;
            }
            List<Point3f> vecImgxyz = new List<Point3f>(0);
            int ntype = Gettype((int)vImgM[0].iBpp, (int)vImgM[0].iChannels);
            if (ntype == -1)
            {
                //获取图像格式失败
                return false;
            }
            for (int i = 0; i < 4; i++)
            {
                Mat matSrc = new Mat((int)vImgM[i].iHei, (int)vImgM[i].iWid, ntype, vImgM[i].pData);

                Mat src_x;
                Mat src_y;
                Mat src_z;

                IIntputData tInputData = vcIIntputData[i];

                Mat[] channels;
                Cv2.Split(matSrc, out channels);
                //split(matSrc, channels);

                if (channels.Length >= 3)
                {
                    src_x = channels[2];
                    src_y = channels[1];
                    src_z = channels[0];
                }
                else
                {
                    return false;
                }
                byte[] srcyData = new byte[src_y.Total()];
                Marshal.Copy(src_y.Data, srcyData, 0, srcyData.Length);
                float gray_y = GetRECTGray(srcyData, matSrc.Rows, matSrc.Cols, vcIRECT);
                byte[] srcxData = new byte[src_x.Total()];
                Marshal.Copy(src_x.Data, srcxData, 0, srcxData.Length);
                float gray_x = GetRECTGray(srcxData, matSrc.Rows, matSrc.Cols, vcIRECT);
                byte[] srczData = new byte[src_z.Total()];
                Marshal.Copy(src_z.Data, srczData, 0, srczData.Length);
                float gray_z = GetRECTGray(srczData, matSrc.Rows, matSrc.Cols, vcIRECT);
                float x0 = gray_x / tInputData.iexp_x;
                float y0 = gray_y / tInputData.iexp_y;
                float z0 = gray_z / tInputData.iexp_z;

                vecImgxyz.Add(new Point3f(x0, y0, z0));
            }

            int idx = 0;

            double[][] result = new double[9][];
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = new double[10];
            }

            for (int i = 0; i < 4; i++)
            {
                float GX0 = vecImgxyz[i].X;
                float GY0 = vecImgxyz[i].Y;
                float GZ0 = vecImgxyz[i].Z;

                result[idx][0] = vcIIntputData[i].iCx * GX0 - GX0;
                result[idx][1] = vcIIntputData[i].iCx * GY0 - GY0;
                result[idx][2] = vcIIntputData[i].iCx * GZ0 - GZ0;
                result[idx][3] = vcIIntputData[i].iCx * GX0;
                result[idx][4] = vcIIntputData[i].iCx * GY0;
                result[idx][5] = vcIIntputData[i].iCx * GZ0;
                result[idx][6] = vcIIntputData[i].iCx * GX0;
                result[idx][7] = vcIIntputData[i].iCx * GY0;
                result[idx][8] = vcIIntputData[i].iCx * GZ0;
                result[idx][9] = 0;
                idx++;
                result[idx][0] = vcIIntputData[i].iCy * GX0;
                result[idx][1] = vcIIntputData[i].iCy * GY0;
                result[idx][2] = vcIIntputData[i].iCy * GZ0;
                result[idx][3] = vcIIntputData[i].iCy * GX0 - GX0;
                result[idx][4] = vcIIntputData[i].iCy * GY0 - GY0;
                result[idx][5] = vcIIntputData[i].iCy * GZ0 - GZ0;
                result[idx][6] = vcIIntputData[i].iCy * GX0;
                result[idx][7] = vcIIntputData[i].iCy * GY0;
                result[idx][8] = vcIIntputData[i].iCy * GZ0;
                result[idx][9] = 0;
                idx++;
                if (i == 3)
                {
                    result[idx][0] = 0;
                    result[idx][1] = 0;
                    result[idx][2] = 0;
                    result[idx][3] = GX0;
                    result[idx][4] = GY0;
                    result[idx][5] = GZ0;
                    result[idx][6] = 0;
                    result[idx][7] = 0;
                    result[idx][8] = 0;
                    result[idx][9] = vcIIntputData[i].iLv;
                    idx++;
                }
            }

            double[] gao_result = new double[9];

            Gauss_Gao(result, ref gao_result);

            vcdRlt = new double[9];

            for (int i = 0; i < 9; i++)
            {
                vcdRlt[i] = gao_result[i];
            }

            return true;
        }


    }
}