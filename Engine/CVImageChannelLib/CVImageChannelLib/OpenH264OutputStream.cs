#pragma warning disable CA2201
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using log4net;
using OpenH264Lib;

namespace CVImageChannelLib;

public class OpenH264OutputStream : MemoryStream
{
	private static readonly ILog logger = LogManager.GetLogger(typeof(OpenH264OutputStream));

	private OpenH264Coder h264Coder;

	private UdpClient udpSend;

	private Encoder.OnEncodeCallback onEncodeCallback;

	private IPEndPoint remotePoint;

	private static int pidx = 1;

	private static int gpidx;

	private const int udpMaxPacket = 65200;

	public OpenH264OutputStream(string localIp, int localPort, int width, int height, float resizeRatio)
		: this(width, height, resizeRatio, new OpenH264Coder(), new UdpClient(localIp, localPort))
	{
	}

	public OpenH264OutputStream(int width, int height, float resizeRatio, OpenH264Coder h264Coder, UdpClient udpClient)
		: this(h264Coder, udpClient)
	{
		Setup(width, height, resizeRatio);
	}

	public OpenH264OutputStream(string localIp, int localPort)
		: this(new OpenH264Coder(), new UdpClient(new IPEndPoint(IPAddress.Parse(localIp), localPort)))
	{
	}

	public OpenH264OutputStream(OpenH264Coder h264Coder, UdpClient udpClient)
	{
		this.h264Coder = h264Coder;
		udpSend = udpClient;
	}

	public void Setup(int width, int height, float resizeRatio)
	{
		onEncodeCallback = onEncodeCallbackImpl;
		int num = h264Coder.Setup(width, height, resizeRatio, onEncodeCallback);
		logger.DebugFormat("width={0},height={1},resizeRatio={2},H264Code Setup return={3}", width, height, resizeRatio, num);
	}

	public void SetRemotePoint(string remoteIP, int remotePort)
	{
		IPAddress address = IPAddress.Parse(remoteIP);
		remotePoint = new IPEndPoint(address, remotePort);
	}

	private void onEncodeCallbackImpl(byte[] data, int length, Encoder.FrameType keyFrame)
	{
		if (length > 65200)
		{
			int num = (length + 65200 - 1) / 65200;
			byte[] array = new byte[65204];
			int num2 = 0;
			int num3 = 0;
			int num4 = length;
			int num5 = 1;
			while (num4 > 0)
			{
				array[0] = (byte)pidx;
				array[1] = (byte)num;
				array[2] = (byte)num5++;
				array[3] = (byte)gpidx++;
				num3 = ((num4 <= 65200) ? num4 : 65200);
				Buffer.BlockCopy(data, num2, array, 4, num3);
				udpSend.Send(array, num3 + 4, remotePoint);
				num2 += num3;
				num4 = length - num2;
				gpidx %= 256;
				Thread.Sleep(1);
			}
			pidx++;
			pidx %= 256;
		}
		else
		{
			byte[] array2 = new byte[length + 4];
			array2[0] = 0;
			array2[1] = 0;
			array2[2] = 0;
			array2[3] = (byte)gpidx++;
			Buffer.BlockCopy(data, 0, array2, 4, length);
			udpSend.Send(array2, array2.Length, remotePoint);
			gpidx %= 256;
		}
	}

	public override void Flush()
	{
		base.Flush();
		Encode();
	}

	public void Encode()
	{
		Seek(0L, SeekOrigin.Begin);
		int width = ReadInt32();
		int height = ReadInt32();
		int bpp = ReadInt32();
		int channels = ReadInt32();
		int num = ReadInt32();
		if (num > 0)
		{
			byte[] array = new byte[num];
			int num2 = Read(array, 0, num);
			h264Coder.Encode(width, height, bpp, channels, array);
		}
	}

	public void Encode(CVImagePacket packet)
	{
		h264Coder.Encode(packet.width, packet.height, packet.bpp, packet.channels, packet.data);
	}

	public int ReadInt32()
	{
		byte[] buffer = GetBuffer();
		int num = (int)Position;
		int num2 = (num += 4);
		if (num2 > Length)
		{
			Position = Length;
			throw new IndexOutOfRangeException();
		}
		Position = num2;
		return buffer[num2 - 4] | (buffer[num2 - 3] << 8) | (buffer[num2 - 2] << 16) | (buffer[num2 - 1] << 24);
	}

	public new void Dispose()
	{
		base.Dispose();
		udpSend?.Close();
		udpSend?.Dispose();
		h264Coder.Dispose();
	}

	protected override void Dispose(bool disposing)
	{
		base.Dispose(disposing);
	}
}
