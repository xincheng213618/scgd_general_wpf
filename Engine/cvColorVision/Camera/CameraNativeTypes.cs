#pragma warning disable
using Newtonsoft.Json;
using System;
using System.Runtime.InteropServices;

namespace cvColorVision
{
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
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
    // Matches native HImage: four uint32 values followed by the data pointer.
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct CVImage
    {
        public uint nWidth;
        public uint nHeight;
        public uint nChannels;
        public uint nBpp;
        public IntPtr pData;
    };

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct AutoFocusCfg
    {
        public double forwardparam;                    //步径摆动范围
        public double curtailparam;                    //步径每次缩减系数
        public int curStep;                            //目前使用步径
        public int stopStep;                           //停止步径
        public int minPosition;                        //电机移动区间下限
        public int maxPosition;                        //电机移动区间上限
        public EvaFunc eEvaFunc;                       //评价函数类型
        public double dMinValue;                       //最低评价值
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

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct IRECT
    {
        public IRECT(int tx, int ty, int tcx, int tcy)
        {
            x = tx;
            y = ty;
            cx = tcx;
            cy = tcy;
        }
        public int x;
        public int y;
        public int cx;
        public int cy;
    };
    public enum DistortionType //TV畸变H,V方向与光学畸变的检测方法
    {
        OpticsDist = 0,
        TVDistH = 1,
        TVDistV = 2,
    };
}
