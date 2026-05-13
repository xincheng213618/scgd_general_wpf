using CVCommCore.CVImage;

namespace MQTTMessageLib.FileServer;

public class GetChannelParam
{
	public CVImageChannelType ChannelType { get; set; }

	public int RecId { get; set; }

	public string FileName { get; set; }

	public string FileURL { get; set; }

	public sbyte FileType { get; set; }
}
