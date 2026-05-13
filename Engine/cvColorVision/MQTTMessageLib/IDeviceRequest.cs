namespace MQTTMessageLib;

public interface IDeviceRequest
{
	string SerialNumber { get; set; }

	string Version { get; set; }

	string DeviceCode { get; set; }

	int ZIndex { get; set; }

	bool Ready { get; set; }

	bool NeedAuth { get; set; }

	string Reason { get; set; }
}
