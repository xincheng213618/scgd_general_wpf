namespace MQTTMessageLib;

public interface IDeviceResponseWithResult : IDeviceResponse
{
	long TotalTime { get; set; }

	int MasterId { get; set; }

	int DeviceResultType { get; set; }

	string DeviceResultCode { get; set; }
}
