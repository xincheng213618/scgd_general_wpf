namespace MQTTMessageLib.PG;

public class DevicePGResponse : CVBaseDeviceResponse, IDevPGResponse, IDeviceResponse
{
	public DevicePGResponse(int code, string desc)
		: base(code, desc)
	{
	}
}
