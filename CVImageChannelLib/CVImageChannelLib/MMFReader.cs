#pragma warning disable CS8625,CS8603

using System;
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

    private WriteableBitmap writeableBitmap;

    public override WriteableBitmap Subscribe()
	{
		if (subscriber != null)
		{
			CVImagePacket cVImagePacket = subscriber.Subscribe();
			if (cVImagePacket != null && cVImagePacket.len > 0)
			{
                Application.Current?.Dispatcher.Invoke(() =>
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

                    return writeableBitmap;
                });
            }
		}
		return writeableBitmap;
	}

	public override void Dispose()
	{
		GC.SuppressFinalize(this);
		base.Dispose();
	}
}
