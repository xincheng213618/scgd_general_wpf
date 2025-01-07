#pragma warning disable CS8625,CS8603

using System;
using System.IO.MemoryMappedFiles;
using System.Windows;
using System.Windows.Media.Imaging;

namespace CVImageChannelLib;

public class MMFReader : CVImageReaderProxy
{
	private MMFEndpointProxy<CVImagePacket> subscriber;
    MemoryMappedFile memoryMappedFile;
    public MMFReader(string mapNamePrefix)
	{
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

    private WriteableBitmap writeableBitmap;

    public override WriteableBitmap Subscribe()
	{
		if (subscriber != null)
		{
			CVImagePacket cVImagePacket = subscriber.Subscribe();
			if (cVImagePacket != null && cVImagePacket.len > 0)
			{
                _ = (Application.Current?.Dispatcher.Invoke(() =>
                {
                    if (writeableBitmap == null || writeableBitmap.PixelWidth != cVImagePacket.width || writeableBitmap.PixelHeight != cVImagePacket.height)
                    {
                        System.Windows.Media.PixelFormat pixelFormat;

                        switch (cVImagePacket.channels)
                        {
                            case 3:
                                pixelFormat = cVImagePacket.bpp switch
                                {
                                    16 => System.Windows.Media.PixelFormats.Rgb48,
                                    _ => System.Windows.Media.PixelFormats.Bgr24,
                                };
                                break;
                            default:
                                pixelFormat = cVImagePacket.bpp switch
                                {
                                    16 => System.Windows.Media.PixelFormats.Gray16,
                                    _ => System.Windows.Media.PixelFormats.Gray8,
                                };
                                pixelFormat = System.Windows.Media.PixelFormats.Gray8;
                                break;
                        }
                        writeableBitmap = new WriteableBitmap(cVImagePacket.width, cVImagePacket.height, 96, 96, pixelFormat, null);
                    }
                    // 写入数据到 WriteableBitmap
                    writeableBitmap.Lock();
                    writeableBitmap.WritePixels(
                        new Int32Rect(0, 0, cVImagePacket.width, cVImagePacket.height),
                        cVImagePacket.data,
                        cVImagePacket.width * cVImagePacket.channels * (cVImagePacket.bpp / 8),
                        0);
                    writeableBitmap.Unlock();

                    return writeableBitmap;
                }));
            }
		}
		return writeableBitmap;
	}

	public override void Dispose()
	{
		GC.SuppressFinalize(this);
        memoryMappedFile?.Dispose();
        base.Dispose();
	}
}
