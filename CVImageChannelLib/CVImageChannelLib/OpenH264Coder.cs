using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using log4net;
using OpenCvSharp;
using OpenH264Lib;

namespace CVImageChannelLib;

public class OpenH264Coder : IDisposable
{
	private static readonly ILog logger = LogManager.GetLogger(typeof(OpenH264Coder));

	private const string H264DllName = "openh264-2.3.1-win64.dll";

	private Decoder Decoder;

	private Encoder Encoder;

	private int h264ImageWidth;

	private int h264ImageHeight;

	private const float fps = 15f;

	private const int bps = 50000000;

	private const float keyFrameInterval = 1f;

	private byte[] i420;

	public OpenH264Coder()
	{
		string location = Assembly.GetExecutingAssembly().Location;
		string dllName = Path.GetDirectoryName(location) + "\\openh264-2.3.1-win64.dll";
		Decoder = new Decoder(dllName);
		Encoder = new Encoder(dllName);
	}

	public int Setup(int width, int height, float resizeRatio, Encoder.OnEncodeCallback onEncode)
	{
		return Setup(width, height, resizeRatio, 50000000, 15f, 1f, onEncode);
	}

	public int Setup(int width, int height, float resizeRatio, int bps, float fps, float keyFrameInterval, Encoder.OnEncodeCallback onEncode)
	{
		if ((double)Math.Abs(resizeRatio) < 9E-07 || Math.Abs((double)resizeRatio - 1.0) < 9E-07)
		{
			h264ImageWidth = width;
			h264ImageHeight = height;
		}
		else
		{
			h264ImageWidth = Convert.ToInt32((float)width * resizeRatio);
			h264ImageHeight = Convert.ToInt32((float)height * resizeRatio);
		}
		GetMaxWid(ref h264ImageWidth, ref h264ImageHeight);
		return Encoder.Setup(h264ImageWidth, h264ImageHeight, bps, fps, keyFrameInterval, onEncode);
	}

	public Bitmap Decode(byte[] frame, int length)
	{
		return Decoder.Decode(frame, length);
	}

	public int EncodeI420(byte[] i420)
	{
		return Encoder.Encode(i420);
	}

	private static void GetMaxWid(ref int width, ref int height)
	{
		if (width * height > 9437184)
		{
			double num = 1.0;
			do
			{
				num -= 0.01;
				width = (int)((double)width * num);
				height = (int)((double)height * num);
			}
			while (width * height > 9437184);
			if (width % 2 != 0)
			{
				width--;
			}
			if (height % 2 != 0)
			{
				height--;
			}
		}
	}

	public void Encode(int width, int height, int bpp, int channels, byte[] bytes)
	{
		MatType type = MatType.CV_8UC(channels);
		if (bpp == 16)
		{
			type = MatType.CV_16UC(channels);
		}
		Mat mat = Mat.FromPixelData(height, width, type, bytes, 0L);
		Mat mat2 = mat;
		if (h264ImageWidth != width || height != h264ImageHeight)
		{
			mat2 = new Mat();
			Cv2.Resize(mat, mat2, new OpenCvSharp.Size(h264ImageWidth, h264ImageHeight));
		}
		if (channels == 1)
		{
			Cv2.CvtColor(mat2, mat2, ColorConversionCodes.GRAY2BGR);
		}
		Mat mat3 = new Mat();
		Cv2.CvtColor(mat2, mat3, ColorConversionCodes.BGR2YUV_I420);
		int num = (int)(mat3.Total() * mat3.ElemSize());
		if (i420 == null || i420.Length < num)
		{
			logger.DebugFormat("i420 len = {0}", num);
			i420 = new byte[num];
		}
		Marshal.Copy(mat3.Data, i420, 0, num);
		Encoder.Encode(i420);
	}

	public void Dispose()
	{
		Decoder.Dispose();
		Encoder.Dispose();
		GC.SuppressFinalize(this);
	}
}
