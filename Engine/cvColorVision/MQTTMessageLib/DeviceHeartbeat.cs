namespace MQTTMessageLib;

public class DeviceHeartbeat
{
	public string DeviceCode { get; set; }

	public string DeviceStatus { get; set; }

	public DeviceHeartbeat(string deviceCode, string status)
	{
		DeviceCode = deviceCode;
		DeviceStatus = status;
	}

}
