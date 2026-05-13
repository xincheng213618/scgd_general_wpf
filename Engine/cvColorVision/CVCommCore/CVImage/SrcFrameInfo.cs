using Newtonsoft.Json;

namespace CVCommCore.CVImage;

public struct SrcFrameInfo
{
	public uint width;

	public uint height;

	public uint bpp;

	public uint channels;

	[JsonIgnore]
	public int widthInt => (int)width;

	[JsonIgnore]
	public int heightInt => (int)height;

	[JsonIgnore]
	public int bppInt => (int)bpp;

	[JsonIgnore]
	public int channelsInt => (int)channels;
}
