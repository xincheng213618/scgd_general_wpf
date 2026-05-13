namespace MQTTMessageLib;

public class DeviceCVBaseRequest<C, P> : DeviceCVBaseNoParamRequest<C>
{
	public P Params { get; set; }

	public DeviceCVBaseRequest(string deviceCode, string serialNumber, C request, P param)
		: this(deviceCode, serialNumber, -1, request, param)
	{
	}

	public DeviceCVBaseRequest(string deviceCode, string serialNumber, int zindex, C request, P param)
		: this(deviceCode, serialNumber, zindex, request, string.Empty, param)
	{
	}

	public DeviceCVBaseRequest(string deviceCode, string serialNumber, int zindex, C request, string version, P param)
		: base(deviceCode, serialNumber, zindex, request, version)
	{
		Params = param;
	}
}
