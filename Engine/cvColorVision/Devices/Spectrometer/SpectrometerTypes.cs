using System;
using System.Runtime.InteropServices;
using System.Text;

namespace cvColorVision
{
    /// <summary>
    /// Spectrometer implementation selected by <see cref="Spectrometer.CM_CreateEmission(SpectrometerType, Spectrometer.Emission_CallBack)"/>.
    /// </summary>
    public enum SpectrometerType
    {
        CMvSpectra = 0,
        LightModule = 1,
        Gaolitong = 2,
    }

    [StructLayoutAttribute(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
    public struct COLOR_PARA
    {
        public float fCIEx;        //色坐标
        public float fCIEy;
        public float fCIEz;

        public float fCIEx_2015;       //2015色坐标
        public float fCIEy_2015;
        public float fCIEz_2015;

        public float fx;        //色度x
        public float fy;        //色度y
        public float fu;
        public float fv;

        public float fx_2015;          //2015色度x
        public float fy_2015;          //色度y
        public float fu_2015;          //色度u'
        public float fv_2015;			//色度v'

        public float fCCT;      //相关色温(K)
        public float dC;        //色差dC
        public float fLd;       //主波长(nm)
        public float fPur;      //色纯度(%)
        public float fLp;       //峰值波长(nm)
        public float fHW;       //半波宽(nm)
        public float fLav;      //平均波长(nm)
        public float fRa;       //显色性指数 Ra
        public float fRR;       //红色比
        public float fGR;       //绿色比
        public float fBR;       //蓝色比
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 15)]
        public float[] fRi;   //显色性指数 R1-R15

        public float fIp;      //峰值AD

        public float fPh;      //光度值
        public float fPhe;     //辐射度值
        public float fPlambda; //绝对光谱洗漱
        public float fSpect1;  //起始波长
        public float fSpect2;  //结束波长
        public float fInterval; //波长间隔

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 10000)]
        public float[] fPL;   //光谱数据
    };

    // 新增完整映射的 EQE 参数结构体
    [StructLayoutAttribute(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
    public struct COLOR_PARA_EQE
    {
        public float fx;            //色度x
        public float fy;            //色度y
        public float fu;            //色度u'
        public float fv;            //色度v'
        public float fCCT;          //相关色温(K)
        public float dC;            //色差dC
        public float fLd;           //主波长(nm) 
        public float fPur;          //色纯度(%)
        public float fLp;           //峰值波长(nm)
        public float fHW;           //半波宽(nm)
        public float fLav;          //平均波长(nm)
        public float fRa;           //显色性指数 Ra
        public float fRR;           //红色比
        public float fGR;           //绿色比
        public float fBR;           //蓝色比

        public float fIp;           //峰值AD

        public float fPh;           //光度值
        public float fPhe;          //辐射度值
        public float fPlambda;      //绝对光谱系数
        public float fSpect1;       //起始波长
        public float fSpect2;       //结束波长
        public float fInterval;

        // 注意：C++ 中这里数组大小为 4001
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4001)]
        public float[] fPL;         //光谱数据

        public float dIm;
        public double dW;
        public double dEqe;
        public double dVoltage;
        public double dCurrent;
        public double dP;
    }

    public enum TRIGGER_MODE
    {
        SOFTWARE_SYNCHRONOUS,   //软件同步模式
        SOFTWARE_ASYNCHRONOUS,  //软件异步模式
        SOFTWARE_AUTO,          //自动采样模式
        EXINT_RISING_EDGE,      //上升沿触发
        EXINT_FALLING_EDGE,     //下降沿触发
        EXINT_HIGH_LEVEL,       //高电平触发模式
        EXINT_LOW_LEVEL,        //低电平触发模式
    }
}
