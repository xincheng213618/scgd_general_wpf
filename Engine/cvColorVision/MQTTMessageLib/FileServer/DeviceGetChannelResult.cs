using CVCommCore.CVImage;

namespace MQTTMessageLib.FileServer;

public class DeviceGetChannelResult : DeviceFileUpdownParam
{
	public CVImageChannelType ChannelType { get; set; }

	public string FileURL { get; set; }

	public DeviceGetChannelResult()
	{
	}

	public DeviceGetChannelResult(CVImageChannelType channelType, string fileURL, DeviceFileUpdownParam param)
		: base(param)
	{
		ChannelType = channelType;
		FileURL = fileURL;
	}
}
