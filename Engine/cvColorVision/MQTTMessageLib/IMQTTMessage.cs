namespace MQTTMessageLib;

public interface IMQTTMessage
{
	string Version { get; set; }

	string ServiceName { get; set; }

	string DeviceCode { get; set; }

	string EventName { get; set; }

	string SerialNumber { get; set; }

	string MsgID { get; set; }

	int ZIndex { get; set; }
}
