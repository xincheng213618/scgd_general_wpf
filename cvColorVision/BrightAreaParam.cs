#pragma warning disable  CA2101,CA1707,CA1401,CA1051,CA1838,CA1711,CS0649,CA2211,CA1708,CA1720

using System.Runtime.InteropServices;
using System;

namespace cvColorVision
{
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

    public partial class cvCameraCSLib
    {

        [DllImport(LIBRARY_CVCAMERA, CharSet = CharSet.Auto, EntryPoint = "SetBrightAreaParam", CallingConvention = CallingConvention.Cdecl)]
        public static extern void SetBrightAreaParam(BrightAreaParam brightAreaParam);

        [DllImport(LIBRARY_CVCAMERA, CharSet = CharSet.Auto, EntryPoint = "FindBrightArea", CallingConvention = CallingConvention.Cdecl)]
        public static extern int FindBrightArea(UInt32 w, UInt32 h, UInt32 bpp, UInt32 channels, byte[] rawArray);

        [DllImport(LIBRARY_CVCAMERA, CharSet = CharSet.Auto, EntryPoint = "GetBrightArea", CallingConvention = CallingConvention.Cdecl)]
        public static extern int GetBrightArea(int nIndex, int[] Pointx, int[] Point);
    }

}