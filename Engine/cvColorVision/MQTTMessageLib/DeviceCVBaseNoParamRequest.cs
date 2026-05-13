namespace MQTTMessageLib;

public abstract class DeviceCVBaseNoParamRequest<C>
{
	public string DeviceCode { get; set; }

	public string SerialNumber { get; set; }

	public bool Ready { get; set; }

	public string Reason { get; set; }

	public string Version { get; set; }

	public bool NeedAuth { get; set; }

	public int ZIndex { get; set; }

	public C DeviceRequestType { get; set; }

	public DeviceCVBaseNoParamRequest(string deviceCode, string serialNumber, C request)
		: this(deviceCode, serialNumber, -1, request)
	{
	}

	public DeviceCVBaseNoParamRequest(string deviceCode, string serialNumber, int zindex, C request)
		: this(deviceCode, serialNumber, zindex, request, string.Empty)
	{
	}

	public DeviceCVBaseNoParamRequest(string deviceCode, string serialNumber, C request, string version)
		: this(deviceCode, serialNumber, -1, request, version)
	{
	}

	public DeviceCVBaseNoParamRequest(string deviceCode, string serialNumber, int zindex, C request, string version)
	{
		DeviceCode = deviceCode;
		SerialNumber = serialNumber;
		DeviceRequestType = request;
		Version = version;
		ZIndex = zindex;
		Ready = true;
		NeedAuth = true;
	}
}
