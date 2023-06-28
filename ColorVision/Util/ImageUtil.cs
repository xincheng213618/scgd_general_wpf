using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Windows.Media;

namespace ColorVision.Util
{
    /// <summary>
    /// 图像操作的一些静态方法
    /// </summary>
    public static class ImageUtil
    {
        /// <summary>
        /// 创建一个新的BitmapImage
        /// </summary>
        public static BitmapImage CreateSolidColorBitmap(int width, int height, System.Windows.Media.Color color)
        {
            // 创建一个 WriteableBitmap，用于绘制纯色图像
            WriteableBitmap writeableBitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Pbgra32, null);

            // 将所有像素设置为指定的颜色

            writeableBitmap.Lock();
            unsafe
            {
                byte* pBackBuffer = (byte*)writeableBitmap.BackBuffer;
                int stride = writeableBitmap.BackBufferStride;

                for (int y = 0; y < writeableBitmap.PixelHeight; y++)
                {
                    for (int x = 0; x < writeableBitmap.PixelWidth; x++)
                    {
                        pBackBuffer[y * stride + 4 * x] = color.B;     // 蓝色通道
                        pBackBuffer[y * stride + 4 * x + 1] = color.G; // 绿色通道
                        pBackBuffer[y * stride + 4 * x + 2] = color.R; // 红色通道
                        pBackBuffer[y * stride + 4 * x + 3] = color.A; // 透明度通道
                    }
                }
            }


            writeableBitmap.Unlock();

            BitmapImage bitmapImage = new BitmapImage();
            using (var stream = new System.IO.MemoryStream())
            {
                BitmapEncoder encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(writeableBitmap));
                encoder.Save(stream);
                stream.Position = 0;

                bitmapImage.BeginInit();
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.StreamSource = stream;
                bitmapImage.EndInit();
            }
            return bitmapImage;
        }



    }
}
