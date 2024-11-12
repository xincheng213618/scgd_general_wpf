using System.IO;
using System.Runtime.InteropServices;
using OpenCvSharp;

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

	public void Resize(float resizeRatio)
	{
		Mat mat = Mat.FromPixelData(height, width, MatType.MakeType(OpenCvMatTools.GetMatDepth(bpp), channels), data, 0L);
		Mat mat2 = new Mat();
		width = (int)((float)width * resizeRatio);
		height = (int)((float)height * resizeRatio);
		Cv2.Resize(mat, mat2, new Size(width, height));
		len = width * height * channels * (bpp / 8);
		data = new byte[len];
		Marshal.Copy(mat2.Data, data, 0, len);
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
