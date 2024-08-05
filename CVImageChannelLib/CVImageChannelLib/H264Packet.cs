using System;
using System.IO;

namespace CVImageChannelLib;

public class H264Packet : ISerializer<H264Packet>
{
	public int len { get; set; }

	public int pos { get; set; }

	public byte[] data { get; set; }

	public H264Packet()
	{
		len = 0;
		pos = 0;
		data = null;
	}

	public void Deserialize(BinaryReader reader)
	{
		try
		{
			if (len == 0)
			{
				len = reader.ReadInt32();
				if (len > 0)
				{
					data = new byte[len];
				}
			}
			if (len > 0 && pos < len)
			{
				byte[] array = reader.ReadBytes(len - pos);
				if (array != null && array.Length != 0)
				{
					Buffer.BlockCopy(array, 0, data, pos, array.Length);
					pos += array.Length;
				}
			}
		}
		catch (Exception)
		{
		}
	}

	public void Serialize(BinaryWriter writer)
	{
		throw new NotImplementedException();
	}
}
