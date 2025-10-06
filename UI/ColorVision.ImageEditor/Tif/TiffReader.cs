using System.IO;
using System.Windows.Media.Imaging;

namespace ColorVision.ImageEditor.Tif
{
    public class TiffReader
    {
        public static BitmapSource ReadTiff(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("File not found", filePath);

            // 使用 BitmapDecoder 读取 TIFF 图像
            using (FileStream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                TiffBitmapDecoder decoder = new TiffBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
             
                BitmapSource bitmapSource = decoder.Frames[0];
                // 确保图像数据已加载
                bitmapSource.Freeze();
                return bitmapSource;
            }
        }
    }
}
