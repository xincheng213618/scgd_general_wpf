namespace MQTTMessageLib;

public class MQTTCVRequestHeader : IMQTTMessage
{
	public string Version { get; set; }

	public string ServiceName { get; set; }

	public string DeviceCode { get; set; }

	public string EventName { get; set; }

	public string SerialNumber { get; set; }

	public string MsgID { get; set; }

	public int ZIndex { get; set; }

	public MQTTCVRequestHeader(string version, string serviceName, string deviceCode, string eventName, string serialNumber, string msgID, int zIndex)
	{
		Version = version;
		ServiceName = serviceName;
		DeviceCode = deviceCode;
		EventName = eventName;
		SerialNumber = serialNumber;
		MsgID = msgID;
		ZIndex = zIndex;
	}

	public MQTTCVRequestHeader()
	{
	}
}
