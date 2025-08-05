using System.IO;
using System.Runtime.InteropServices;

namespace CVImageChannelLib;

public class CVImagePacket : ISerializer<CVImagePacket>
{
	public int width { get; set; }

	public int height { get; set; }

	public int bpp { get; set; }

	public int channels { get; set; }

	public int len { get; set; }

	public byte[] data { get; set; }

	public void Deserialize(BinaryReader reader)
	{
		width = reader.ReadInt32();
		height = reader.ReadInt32();
		bpp = reader.ReadInt32();
		channels = reader.ReadInt32();
		len = reader.ReadInt32();
		data = reader.ReadBytes(len);
	}

	public void Serialize(BinaryWriter writer)
	{
		writer.Write(width);
		writer.Write(height);
		writer.Write(bpp);
		writer.Write(channels);
		writer.Write(len);
		writer.Write(data);
		writer.Flush();
	}
}
