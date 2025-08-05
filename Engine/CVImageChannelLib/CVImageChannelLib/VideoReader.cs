#pragma warning disable CS8603
using log4net;
using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

namespace CVImageChannelLib;

public class VideoReader :IDisposable
{
	private static readonly ILog log = LogManager.GetLogger(typeof(VideoReader));

	private bool OpenVideo;

	public event H264ReaderRecvHandler OnFrameRecv;
    MemoryMappedFile memoryMappedFile;
    protected MemoryMappedViewStream memoryMappedViewStream;
	BinaryReader binaryReader;

    public VideoReader()
	{
		OpenVideo = false;
	}

	public int Startup(string mapNamePrefix)
    {
        try
        {
            memoryMappedFile = MemoryMappedFile.OpenExisting(mapNamePrefix);
        }
        catch (Exception ex)
        {
			log.Error(ex);
			return -1;
        }
        if (memoryMappedFile != null)
        {
			memoryMappedViewStream = memoryMappedFile.CreateViewStream();
			binaryReader = new BinaryReader(memoryMappedViewStream);
        }
        OpenVideo = true;
        frameCount = 0;
        lastFpsLogTime = DateTime.Now;
        Task.Run(async delegate
        {
            await StartupAsync();
        });
		return 0;
    }

	public void Close()
	{
		OpenVideo = false;
        memoryMappedFile?.Dispose();
        memoryMappedViewStream?.Dispose();
        binaryReader?.Dispose();
    }

    WriteableBitmap writeableBitmap;
    int frameCount = 0;
    DateTime lastFpsLogTime = DateTime.Now;

    private async Task StartupAsync()
    {
        while (OpenVideo)
		{
			try
            {
                memoryMappedViewStream.Position = 0L;
                CVImagePacket cVImagePacket = new CVImagePacket();
                cVImagePacket.Deserialize(binaryReader);
				if (cVImagePacket != null && cVImagePacket.len > 0)
				{
                    Application.Current.Dispatcher.Invoke(() =>
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
                        this.OnFrameRecv?.Invoke(writeableBitmap);
                        // 帧数递增
                        frameCount++;
                        // 每秒统计一次帧率
                        DateTime now = DateTime.Now;
                        if ((now - lastFpsLogTime).TotalSeconds >= 1)
                        {
                            log.Info($"Current FPS: {frameCount}");
                            frameCount = 0;
                            lastFpsLogTime = now;
                        }
                    });
                }
				await Task.Delay(10);
			}
			catch (Exception ex)
			{
				log.Error(ex.Message);
			}
		}
	}

	public void Dispose()
	{

        GC.SuppressFinalize(this);
	}
}
