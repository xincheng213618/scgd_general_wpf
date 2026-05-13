namespace MQTTMessageLib.FileServer;

public class MQTTFileServerGetChannel : MQTTCVBaseRequest<GetChannelParam>
{
	public MQTTFileServerGetChannel(string serviceName, string serialNumber, GetChannelParam data)
		: base(serviceName, "File_GetChannel", serialNumber, data)
	{
	}
}
