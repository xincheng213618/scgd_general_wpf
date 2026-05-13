namespace MQTTMessageLib.PG;

public class DevicePGStart : DeviceCVBaseNoParamRequest<PGRequestType>, IDevPGRequest, IDeviceRequest
{
	public DevicePGStart(string deviceName, string serialNumber)
		: base(deviceName, serialNumber, PGRequestType.Start)
	{
	}
}
