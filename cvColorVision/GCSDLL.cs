#pragma warning disable  CA1051
using System.Runtime.InteropServices;

namespace cvColorVision
{
    /// <summary>
    /// 连接光谱仪
    /// </summary>
    public class GCSDLL
    {
        /// <summary>
        /// 光谱仪传递参数
        /// </summary>
        public struct ColorParam
        {
            public float fx;       //色坐标
            public float fy;
            public float fu;
            public float fv;

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

            public float fRf;
            public float fRg;
        };

        private const string LIBRARY_CVCAMERA = "cvCamera.dll";

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void ColorParamReturn(ref ColorParam result, float intTime, int resultCode);

        //连接光谱仪
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CV_Init",  CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        private unsafe static extern int CV_Init();
        public static int CVInit() => CV_Init();

        //断开光谱仪
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "JK_Emission_Close", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        private unsafe static extern int JK_Emission_Close();
        public static int JKEmissionClose() => JK_Emission_Close();

        //启动光谱仪软件的服务
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "JK_Start_Server", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        private unsafe static extern int JK_Start_Server();
        public static int JKStartServer() => JK_Start_Server();


        //关闭光谱仪软件的服务
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "JK_Close_Server", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        private unsafe static extern int JK_Close_Server();
        public static int JKCloseServer() => JK_Close_Server();

        //暗点校正
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CV_Init_Dark",CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        private unsafe static extern int CV_Init_Dark(float fIntTime, int iAveNum);
        public static int CVInitDark(float fIntTime, int iAveNum) => CV_Init_Dark(fIntTime, iAveNum);


        //获取自动积分时间
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "JK_GetAutoTime", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        private unsafe static extern int JK_GetAutoTime(ref float fIntTime, int iLimitTime, float fTimeB);
        public static int JKGetAutoTime(ref float fIntTime, int iLimitTime, float fTimeB) => JK_GetAutoTime(ref fIntTime, iLimitTime, fTimeB);


        //单次测量
        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "CV_One_Test", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        private static extern int CV_One_Test(ColorParamReturn callBack, float IntTime, int AveNum, bool bUseAutoIntTime, bool bUseAutoDark);
        public static int CVOneTest(ColorParamReturn callBack, float IntTime, int AveNum, bool bUseAutoIntTime, bool bUseAutoDark) => CV_One_Test(callBack, IntTime, AveNum, bUseAutoIntTime, bUseAutoDark);

    }
}


