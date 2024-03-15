#pragma warning disable CA1401,CA1051,CA2101
using System;
using System.Runtime.InteropServices;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Windows;
using System.Runtime.CompilerServices;

namespace ColorVision
{
    public struct HImage : IDisposable
    {
        public int rows;
        public int cols;
        public int channels;
        public int depth; //bpp

        public  int Type
        {
            get { return (((depth & ((1 << 3) - 1)) + ((channels - 1) << 3))); }
        }

        public  int ElemSize
        {
            get
            {
                return ((((((((depth & ((1 << 3) - 1)) + ((channels - 1) << 3))) & ((512 - 1) << 3)) >> 3) + 1) *
                        ((0x28442211 >> (((((depth & ((1 << 3) - 1)) + ((channels - 1) << 3))) & ((1 << 3) - 1)) * 4)) & 15)));
            }
        }

        public IntPtr pData;

        public void Dispose()
        {
            // 使用 Marshal.FreeHGlobal 来释放由 Marshal.AllocHGlobal 分配的内存
            if (pData != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(pData);
                pData = IntPtr.Zero;
            }
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
                1 => PixelFormats.Gray8,
                3 => PixelFormats.Bgr24,
                4 => PixelFormats.Bgr32,
                _ => PixelFormats.Default,
            };

            WriteableBitmap writeableBitmap = new WriteableBitmap(hImage.cols, hImage.rows, 96.0, 96.0, format, null);
            RtlMoveMemory(writeableBitmap.BackBuffer, hImage.pData, (uint)(hImage.cols * hImage.rows * hImage.channels));
            writeableBitmap.Lock();
            writeableBitmap.AddDirtyRect(new Int32Rect(0, 0, writeableBitmap.PixelWidth, writeableBitmap.PixelHeight));
            writeableBitmap.Unlock();
            return writeableBitmap;
        }
        public static HImage ToHImage(this BitmapImage bitmapImage) => bitmapImage.ToWriteableBitmap().ToHImage();

        public static WriteableBitmap ToWriteableBitmap(this BitmapImage bitmapImage) => new WriteableBitmap(bitmapImage);

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
                default:
                    MessageBox.Show($"{writeableBitmap.Format}暂不支持的格式,请联系开发人员");
                    throw new NotSupportedException("The pixel format is not supported.");
            }
            // Create a new HImageCache instance
            HImage hImage = new HImage
            {
                rows = writeableBitmap.PixelHeight,
                cols = writeableBitmap.PixelWidth,
                channels = channels,
                depth = depth, // You might need to adjust this based on the actual bits per pixel
                pData = Marshal.AllocHGlobal(writeableBitmap.PixelWidth * writeableBitmap.PixelHeight * channels)
            };

            // Copy the pixel data from the WriteableBitmap to the HImageCache
            writeableBitmap.Lock();
            RtlMoveMemory(hImage.pData, writeableBitmap.BackBuffer, (uint)(hImage.cols * hImage.rows * hImage.channels));
            writeableBitmap.Unlock();

            return hImage;
        }
    }


    public static class OpenCVHelper
    {

        [DllImport("ColorVisionCore.dll", CharSet = CharSet.Unicode)]
        public static extern int CVWrite(string FullPath, HImage hImage,int compression =0);

        [DllImport("ColorVisionCore.dll", CharSet = CharSet.Unicode)]
        public static extern int CVRead(string FullPath, out HImage hImage);


        [DllImport("kernel32.dll", EntryPoint = "RtlMoveMemory")]
        public static extern void RtlMoveMemory(IntPtr Destination, IntPtr Source, uint Length);

        [DllImport("OpenCVHelper.dll", CharSet = CharSet.Unicode)]
        public static extern void ReadCVFile(string FullPath);

        [DllImport("OpenCVHelper.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int ReadGhostImage([MarshalAs(UnmanagedType.LPStr)] string FilePath, int singleLedPixelNum, int[] LEDPixelX, int[] LEDPixelY, int singleGhostPixelNum, int[] GhostPixelX, int[] GhostPixelY, out HImage hImage);

        [DllImport("OpenCVHelper.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int PseudoColor(HImage image, out HImage hImage, uint min, uint max);

        [DllImport("OpenCVHelper.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void FreeHImageData(IntPtr data);



        [DllImport("OpenCVHelper.dll")]
        public unsafe static extern void SetInitialFrame(nint pRoutineHandler);

        [DllImport("OpenCVHelper.dll", CharSet = CharSet.Unicode)]
        public static extern void ReadVideoTest(string FullPath);



    }
}
