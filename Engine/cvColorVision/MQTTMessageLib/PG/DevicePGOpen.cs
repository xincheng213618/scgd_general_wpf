namespace MQTTMessageLib.PG;

public class DevicePGOpen : DeviceCVBaseNoParamRequest<PGRequestType>, IDevPGRequest, IDeviceRequest
{
	public DevicePGOpen(string deviceName, string serialNumber)
		: base(deviceName, serialNumber, PGRequestType.Open)
	{
	}
}
