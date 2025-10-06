using System;
using System.Runtime.InteropServices;

namespace cvColorVision
{
    //Aoi检测部分
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

    public partial class cvCameraCSLib
    {
        [DllImport(LIBRARY_CVCAMERA, CharSet = CharSet.Auto, EntryPoint = "CreateAOIDetector", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr CreateAOIDetector();

        [DllImport(LIBRARY_CVCAMERA, CharSet = CharSet.Auto, EntryPoint = "ReleaseAOIDetector", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool ReleaseAOIDetector(IntPtr handle);

        [DllImport(LIBRARY_CVCAMERA, CharSet = CharSet.Auto, EntryPoint = "AOIDetectorSetParam", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool AOIDetectorSetParam(IntPtr handle, AoiParam aoiParam);

        [DllImport(LIBRARY_CVCAMERA, CharSet = CharSet.Auto, EntryPoint = "AOIDetectorInput",CallingConvention = CallingConvention.Cdecl)]
        public static extern int AOIDetectorInput(IntPtr handle, UInt32 w, UInt32 h, UInt32 bpp, UInt32 channels, byte[] rawArray);

        [DllImport(LIBRARY_CVCAMERA, EntryPoint = "GetAoiDetectorBlob",CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public unsafe static extern bool GetAoiDetectorBlob(IntPtr handle, int nIndex, PartiCle tParticle);
    }
}