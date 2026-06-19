#pragma warning disable
using Newtonsoft.Json;
using System;

namespace cvColorVision
{
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

    public enum ImageFilterType 
    {
        Color_Filter = 0,//滤色片
        ND = 1//
    };
    public enum ConfigType : int
    {
        Cfg_Camera = 0,
        Cfg_ExpTime = 1,
        Cfg_Calibration = 2,
        Cfg_Channels = 3,
        Cfg_SYSTEM = 4,
    };

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
