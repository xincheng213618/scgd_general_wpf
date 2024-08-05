using System;
using System.Drawing;
using System.IO.MemoryMappedFiles;
using System.Windows;
using System.Windows.Media.Imaging;

namespace CVImageChannelLib;

public class MMFReader : CVImageReaderProxy
{
	private MMFEndpointProxy<CVImagePacket> subscriber;

	public MMFReader(string mapNamePrefix)
	{
		MemoryMappedFile memoryMappedFile = null;
		try
		{
			memoryMappedFile = MemoryMappedFile.OpenExisting(mapNamePrefix);
		}
		catch (Exception)
		{
			memoryMappedFile = MemoryMappedFile.OpenExisting(mapNamePrefix);
		}
		if (memoryMappedFile != null)
		{
			subscriber = new MMFEndpointProxy<CVImagePacket>(memoryMappedFile);
		}
	}

	public CVImagePacket SubscribePacket()
	{
		if (subscriber != null)
		{
			return subscriber.Subscribe();
		}
		return null;
	}

    private WriteableBitmap writeableBitmap = null;
    private System.Windows.Controls.Image imageControl = null;
    private Window window = null;

    public override Bitmap Subscribe()
	{
		if (subscriber != null)
		{
			CVImagePacket cVImagePacket = subscriber.Subscribe();
			if (cVImagePacket != null && cVImagePacket.len > 0)
			{
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (writeableBitmap == null || writeableBitmap.PixelWidth != cVImagePacket.width || writeableBitmap.PixelHeight != cVImagePacket.height)
                    {
                        writeableBitmap = new WriteableBitmap(cVImagePacket.width, cVImagePacket.height, 96, 96, System.Windows.Media.PixelFormats.Bgr24, null);
                    }

                    // 写入数据到 WriteableBitmap
                    writeableBitmap.Lock();
                    writeableBitmap.WritePixels(
                        new Int32Rect(0, 0, cVImagePacket.width, cVImagePacket.height),
                        cVImagePacket.data,
                        cVImagePacket.width * cVImagePacket.channels,
                        0);
                    writeableBitmap.Unlock();

                    // 显示到 Image 控件
                    if (imageControl == null)
                    {
                        imageControl = new System.Windows.Controls.Image();
                        imageControl.Source = writeableBitmap;

                        // 创建新的 Window 并添加 Image 控件
                        window = new Window
                        {
                            Title = "Image Display",
                            Content = imageControl,
                            Width = cVImagePacket.width,
                            Height = cVImagePacket.height
                        };
                        window.Show();
                    }
                    else
                    {
                        imageControl.Source = writeableBitmap;
                    }
                });




                //Mat mat = Mat.FromPixelData(cVImagePacket.height, cVImagePacket.width, MatType.MakeType(0, cVImagePacket.channels), cVImagePacket.data, 0L);
                //            WriteableBitmap result;
                //if (cVImagePacket.channels == 1)
                //{
                //	Mat mat2 = new Mat();
                //	Cv2.CvtColor(mat, mat2, ColorConversionCodes.GRAY2BGR);
                //	result = mat2.ToWriteableBitmap();
                //}
                //else
                //{
                //	result = mat.ToWriteableBitmap();
                //}
                //return null;
            }
		}
		return null;
	}

	public override void Dispose()
	{
		if (subscriber != null)
		{
			subscriber.Dispose();
		}
		base.Dispose();
	}
}
