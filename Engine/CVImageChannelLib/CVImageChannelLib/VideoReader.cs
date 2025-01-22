#pragma warning disable CS8603
using System;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using log4net;

namespace CVImageChannelLib;

public class VideoReader : CVImageReaderProxy
{
	private static readonly ILog logger = LogManager.GetLogger(typeof(VideoReader));

	private MMFReader reader;

	private H264Reader h264Reader;

	private bool OpenVideo;

	public event H264ReaderRecvHandler OnFrameRecv;

	public VideoReader()
	{
		OpenVideo = false;
	}

	public void Startup(string name, bool isLocal)
    {
		if (isLocal)
		{
			reader = new MMFReader(name);
            OpenVideo = true;
			Task.Run(async delegate
			{
				await StartupAsync();
			});
		}
	}

	public void Close()
	{
		OpenVideo = false;
	}

	public int Open(string localIp, int localPort)
	{
		if (localIp == "127.0.0.1")
		{
			return 1;
		}
		h264Reader = new H264Reader(localIp, localPort);
		h264Reader.H264ReaderRecv += H264Reader_H264ReaderRecv;
		return h264Reader.GetLocalPort();
	}

	private async Task StartupAsync()
	{
		while (OpenVideo)
		{
			try
			{
                WriteableBitmap bmp = reader?.Subscribe();
				if (bmp != null)
				{
					this.OnFrameRecv?.Invoke(bmp);
				}
				await Task.Delay(20);
			}
			catch (Exception ex)
			{
				logger.Error(ex.Message);
			}
		}
	}

	private void H264Reader_H264ReaderRecv(WriteableBitmap bmp)
	{
		this.OnFrameRecv?.Invoke(bmp);
	}

	public override WriteableBitmap Subscribe()
	{
		return reader?.Subscribe();
	}

	public override void Dispose()
	{
		base.Dispose();
		h264Reader?.Dispose();
		reader?.Dispose();
		GC.SuppressFinalize(this);
	}
}
