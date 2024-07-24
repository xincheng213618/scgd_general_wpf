#pragma warning disable CA1401,CA1051,CA2101,CA1707
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.Engine
{
    public struct HImage : IDisposable
    {
        public int rows;
        public int cols;
        public int channels;
        public int depth; //bpp
        public int stride;

        public readonly int Type => (((depth & ((1 << 3) - 1)) + ((channels - 1) << 3)));

        public int ElemSize => ((((((((depth & ((1 << 3) - 1)) + ((channels - 1) << 3))) & ((512 - 1) << 3)) >> 3) + 1) *
                        ((0x28442211 >> (((((depth & ((1 << 3) - 1)) + ((channels - 1) << 3))) & ((1 << 3) - 1)) * 4)) & 15)));

        public readonly uint Size { get => (uint)(rows * cols * channels * (depth / 8)); }


        public IntPtr pData;

        public void Dispose()
        {
            // 使用 Marshal.FreeHGlobal来释放由 Marshal.AllocHGlobal 分配的内存
            if (pData != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(pData);
                pData = IntPtr.Zero;
            }
            GC.SuppressFinalize(this);
        }

    }


    public static class HImageExtension
    {
        [DllImport("kernel32.dll", EntryPoint = "RtlMoveMemory")]
        private static extern void RtlMoveMemory(IntPtr Destination, IntPtr Source, uint Length);

        public static WriteableBitmap ToWriteableBitmap(this HImage hImage)
        {
            PixelFormat format = hImage.channels switch
            {
                1 => hImage.depth switch
                {
                    8 => PixelFormats.Gray8,
                    16 => PixelFormats.Gray16,
                    _ => PixelFormats.Gray8,
                },
                3 => hImage.depth switch
                {
                    8 => PixelFormats.Bgr24,
                    16 => PixelFormats.Rgb48,
                     _=> PixelFormats.Bgr24,
                },
                4 => PixelFormats.Bgr32,
                _ => PixelFormats.Default,
            };

            WriteableBitmap writeableBitmap = new(hImage.cols, hImage.rows, 96.0, 96.0, format, null);

            writeableBitmap.Lock();

            unsafe
            {
                byte* src = (byte*)hImage.pData;
                byte* dst = (byte*)writeableBitmap.BackBuffer;

                for (int y = 0; y < hImage.rows; y++)
                {
                    RtlMoveMemory(new IntPtr(dst), new IntPtr(src), (uint)(hImage.cols * hImage.channels * (hImage.depth / 8)));
                    src += hImage.stride;
                    dst += writeableBitmap.BackBufferStride;
                }
            }

            //RtlMoveMemory(writeableBitmap.BackBuffer, hImage.pData, (uint)(hImage.cols * hImage.rows * hImage.channels * (hImage.depth / 8)));
            //writeableBitmap.Lock();

            writeableBitmap.AddDirtyRect(new Int32Rect(0, 0, writeableBitmap.PixelWidth, writeableBitmap.PixelHeight));
            writeableBitmap.Unlock();
            writeableBitmap.Freeze();
            return writeableBitmap;
        }


        public static HImage ToHImage(this BitmapImage bitmapImage) => bitmapImage.ToWriteableBitmap().ToHImage();

        public static WriteableBitmap ToWriteableBitmap(this BitmapImage bitmapImage) => new(bitmapImage);

        public static HImage ToHImage(this WriteableBitmap writeableBitmap)
        {
            // Determine the number of channels and Depth based on the pixel format
            int channels;
            int depth;
            switch (writeableBitmap.Format.ToString())
            {
                case "Bgr32":
                case "Bgra32":
                case "Pbgra32":
                    channels = 4; // BGRA format has 4 channels
                    depth = 8; // 8 bits per channel
                    break;
                case "Bgr24":
                case "Rgb24":
                    channels = 3; // RGB format has 3 channels
                    depth = 8; // 8 bits per channel
                    break;
                case "Rgb48":
                    channels = 3; // RGB format has 3 channels
                    depth = 16; // 8 bits per channel
                    break;
                case "Gray8":
                    channels = 1; // Gray scale has 1 channel
                    depth = 8; // 8 bits per channel
                    break;
                case "Gray16":
                    channels = 1; // Gray scale has 1 channel
                    depth = 16; // 16 bits per channel
                    break;
                case "Gray32Float":
                    channels = 1; // Gray scale has 1 channel
                    depth = 32; // 16 bits per channel
                    break;
                default:
                    MessageBox.Show($"{writeableBitmap.Format}暂不支持的格式,请联系开发人员");
                    throw new NotSupportedException("The pixel format is not supported.");
            }

            // Create a new HImageCache instance
            HImage hImage = new()
            {
                rows = writeableBitmap.PixelHeight,
                cols = writeableBitmap.PixelWidth,
                channels = channels,
                depth = depth, // You might need to adjust this based on the actual bits per pixel
                pData = Marshal.AllocHGlobal(writeableBitmap.PixelWidth * writeableBitmap.PixelHeight * channels* (depth/8))
            };

            // Copy the pixel data from the WriteableBitmap to the HImageCache
            writeableBitmap.Lock();
            //RtlMoveMemory(hImage.pData, writeableBitmap.BackBuffer, (uint)(hImage.cols * hImage.rows * hImage.channels*(depth / 8)));
            unsafe
            {
                byte* src = (byte*)writeableBitmap.BackBuffer;
                byte* dst = (byte*)hImage.pData;

                for (int y = 0; y < hImage.rows; y++)
                {
                    RtlMoveMemory(new IntPtr(dst), new IntPtr(src), (uint)(hImage.cols * hImage.channels * (hImage.depth / 8)));
                    src += writeableBitmap.BackBufferStride;
                    dst += hImage.cols * hImage.channels * (hImage.depth / 8);
                }
            }


            writeableBitmap.Unlock();
            return hImage;
        }
    }
    public enum ColormapTypes
    {
        COLORMAP_AUTUMN = 0, //!< ![autumn](pics/colormaps/colorscale_autumn.jpg)
        COLORMAP_BONE = 1, //!< ![bone](pics/colormaps/colorscale_bone.jpg)
        COLORMAP_JET = 2, //!< ![jet](pics/colormaps/colorscale_jet.jpg)
        COLORMAP_WINTER = 3, //!< ![winter](pics/colormaps/colorscale_winter.jpg)
        COLORMAP_RAINBOW = 4, //!< ![rainbow](pics/colormaps/colorscale_rainbow.jpg)
        COLORMAP_OCEAN = 5, //!< ![ocean](pics/colormaps/colorscale_ocean.jpg)
        COLORMAP_SUMMER = 6, //!< ![summer](pics/colormaps/colorscale_summer.jpg)
        COLORMAP_SPRING = 7, //!< ![spring](pics/colormaps/colorscale_spring.jpg)
        COLORMAP_COOL = 8, //!< ![cool](pics/colormaps/colorscale_cool.jpg)
        COLORMAP_HSV = 9, //!< ![HSV](pics/colormaps/colorscale_hsv.jpg)
        COLORMAP_PINK = 10, //!< ![pink](pics/colormaps/colorscale_pink.jpg)
        COLORMAP_HOT = 11, //!< ![hot](pics/colormaps/colorscale_hot.jpg)
        COLORMAP_PARULA = 12, //!< ![parula](pics/colormaps/colorscale_parula.jpg)
        COLORMAP_MAGMA = 13, //!< ![magma](pics/colormaps/colorscale_magma.jpg)
        COLORMAP_INFERNO = 14, //!< ![inferno](pics/colormaps/colorscale_inferno.jpg)
        COLORMAP_PLASMA = 15, //!< ![plasma](pics/colormaps/colorscale_plasma.jpg)
        COLORMAP_VIRIDIS = 16, //!< ![viridis](pics/colormaps/colorscale_viridis.jpg)
        COLORMAP_CIVIDIS = 17, //!< ![cividis](pics/colormaps/colorscale_cividis.jpg)
        COLORMAP_TWILIGHT = 18, //!< ![twilight](pics/colormaps/colorscale_twilight.jpg)
        COLORMAP_TWILIGHT_SHIFTED = 19, //!< ![twilight shifted](pics/colormaps/colorscale_twilight_shifted.jpg)
        COLORMAP_TURBO = 20, //!< ![turbo](pics/colormaps/colorscale_turbo.jpg)
        COLORMAP_DEEPGREEN = 21  //!< ![deepgreen](pics/colormaps/colorscale_deepgreen.jpg)
    };




    public static class OpenCVHelper
    {
        private const string LibOpenCVHelper = "libs\\OpenCVHelper.dll";



        [DllImport(LibOpenCVHelper, CharSet = CharSet.Unicode)]
        public static extern void ReadCVFile(string FullPath);

        [DllImport(LibOpenCVHelper, CallingConvention = CallingConvention.Cdecl)]
        public static extern int ReadGhostImage([MarshalAs(UnmanagedType.LPStr)] string FilePath, int singleLedPixelNum, int[] LEDPixelX, int[] LEDPixelY, int singleGhostPixelNum, int[] GhostPixelX, int[] GhostPixelY, out HImage hImage);

        /// <summary>
        /// 伪彩色
        /// </summary>
        /// <param name="image"></param>
        /// <param name="hImage"></param>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <param name="colormapTypes"></param>
        /// <returns></returns>
        [DllImport(LibOpenCVHelper, CallingConvention = CallingConvention.Cdecl)]
        public static extern int CM_PseudoColor(HImage image, out HImage hImage, uint min, uint max , ColormapTypes colormapTypes =ColormapTypes.COLORMAP_JET);

        /// <summary>
        /// 自动对比度
        /// </summary>
        /// <param name="image"></param>
        /// <param name="hImage"></param>
        /// <returns></returns>
        [DllImport(LibOpenCVHelper, CharSet = CharSet.Unicode)]
        public static extern int CM_AutoLevelsAdjust(HImage image, out HImage hImage);

        /// <summary>
        /// 自动颜色
        /// </summary>
        /// <param name="image"></param>
        /// <param name="hImage"></param>
        /// <returns></returns>
        [DllImport(LibOpenCVHelper, CharSet = CharSet.Unicode)]
        public static extern int CM_AutomaticColorAdjustment(HImage image, out HImage hImage);

        /// <summary>
        /// 自动色调
        /// </summary>
        /// <param name="image"></param>
        /// <param name="hImage"></param>
        /// <returns></returns>
        [DllImport(LibOpenCVHelper, CharSet = CharSet.Unicode)]
        public static extern int CM_AutomaticToneAdjustment(HImage image, out HImage hImage);


        [DllImport(LibOpenCVHelper)]
        public static extern int CM_Fusion(string fusionjson, out HImage hImage);

        [DllImport(LibOpenCVHelper, CallingConvention = CallingConvention.Cdecl)]
        public static extern void FreeHImageData(IntPtr data);

        [DllImport(LibOpenCVHelper)]
        public unsafe static extern void SetInitialFrame(nint pRoutineHandler);

        [DllImport(LibOpenCVHelper, CharSet = CharSet.Unicode)]
        public static extern void ReadVideoTest(string FullPath);





    }
}
