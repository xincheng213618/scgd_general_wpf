namespace MQTTMessageLib.FileServer;

public class DeviceFileServerGetChannelResponse : DeviceFileServerResultResponse<DeviceGetChannelResult>
{
	public DeviceFileServerGetChannelResponse(CVBaseDeviceResponse status, long totalTime)
		: base(FileServerResultType.GetChannel, status, totalTime)
	{
	}

	public DeviceFileServerGetChannelResponse(DeviceGetChannelResult result, int code, string desc, long totalTime)
		: base(FileServerResultType.GetChannel, code, desc, totalTime)
	{
		base.Result = result;
	}

	public static DeviceFileServerGetChannelResponse Success(DeviceGetChannelResult result, long totalTime)
	{
		return new DeviceFileServerGetChannelResponse(result, 0, "ok", totalTime);
	}
}
