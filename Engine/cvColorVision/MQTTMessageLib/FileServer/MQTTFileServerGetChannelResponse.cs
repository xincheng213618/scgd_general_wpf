namespace MQTTMessageLib.FileServer;

public class MQTTFileServerGetChannelResponse : MQTTCVBaseResponse<DeviceGetChannelResult>
{
	public MQTTFileServerGetChannelResponse(MQTTCVRequestHeader request, DeviceFileServerGetChannelResponse response)
		: base(request, new MQTTCVResponseStatus(response.Code, response.Desc), response.Result)
	{
	}
}
