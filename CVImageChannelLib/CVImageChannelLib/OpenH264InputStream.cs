using System;
using System.Collections.Generic;
using System.IO;

namespace CVImageChannelLib;

public class OpenH264InputStream : MemoryStream
{
	private UDPClientRecv udpRecv;

	private Dictionary<int, List<byte[]>> packets;

	private int headLen;

	private List<byte[]> data;

	private object objLock;

	public event H264ReceivedEventHandler H264Received;

	public OpenH264InputStream(string localIp, int localPort)
		: this(new UDPClientRecv(localIp, localPort))
	{
	}

	public OpenH264InputStream(UDPClientRecv udpClient)
	{
		headLen = 4;
		packets = new Dictionary<int, List<byte[]>>();
		data = new List<byte[]>();
		objLock = new object();
		udpRecv = udpClient;
		udpRecv.UDPMessageReceived += UdpClient_UDPMessageReceived;
	}

	private void UdpClient_UDPMessageReceived(UdpStateEventArgs args)
	{
		if (args.buffer.Length != 0)
		{
			byte[] array = AddPacket(args.buffer);
			if (array != null)
			{
				this.H264Received?.Invoke(new H264StateEventArgs
				{
					packet = array
				});
			}
		}
	}

	private void WriteBuffer(byte[] bytes)
	{
		data.Add(bytes);
	}

	public byte[] ReadBuffer()
	{
		byte[] result = null;
		if (data.Count > 0)
		{
			result = data[0];
			data.RemoveAt(0);
		}
		return result;
	}

	private void Write(int value)
	{
		Write(new byte[4]
		{
			(byte)value,
			(byte)(value >> 8),
			(byte)(value >> 16),
			(byte)(value >> 24)
		}, 0, 4);
	}

	public byte[] AddPacket(byte[] buffer)
	{
		byte[] array;
		if (buffer[0] == 0)
		{
			int num = buffer.Length - headLen;
			array = new byte[num];
			Buffer.BlockCopy(buffer, headLen, array, 0, num);
			return array;
		}
		int num2 = buffer.Length - headLen;
		array = new byte[num2];
		Buffer.BlockCopy(buffer, headLen, array, 0, num2);
		if (!packets.ContainsKey(buffer[0]))
		{
			packets.Add(buffer[0], new List<byte[]>());
		}
		packets[buffer[0]].Add(array);
		if (packets[buffer[0]].Count == buffer[1])
		{
			int num3 = 0;
			foreach (byte[] item in packets[buffer[0]])
			{
				num3 += item.Length;
			}
			array = new byte[num3];
			int num4 = 0;
			for (int i = 0; i < buffer[1]; i++)
			{
				Buffer.BlockCopy(packets[buffer[0]][i], 0, array, num4, packets[buffer[0]][i].Length);
				num4 += packets[buffer[0]][i].Length;
			}
			packets.Remove(buffer[0]);
			return array;
		}
		return null;
	}

	public UDPClientRecv GetUdp()
	{
		return udpRecv;
	}

	protected override void Dispose(bool disposing)
	{
		udpRecv.Close();
		udpRecv.Dispose();
		base.Dispose(disposing);
	}
}
