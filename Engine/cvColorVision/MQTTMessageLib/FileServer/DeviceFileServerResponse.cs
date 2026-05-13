namespace MQTTMessageLib.FileServer;

public class DeviceFileServerResponse : CVBaseDeviceResponse, IDevFileServerResponse, IDeviceResponse
{
	public long TotalTime { get; set; }

	public DeviceFileServerResponse(int code, string desc)
		: base(code, desc)
	{
	}

	public DeviceFileServerResponse(CVBaseDeviceResponse status)
		: base(status)
	{
	}
}
