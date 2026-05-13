namespace MQTTMessageLib.PG;

public class DevicePGReset : DeviceCVBaseNoParamRequest<PGRequestType>, IDevPGRequest, IDeviceRequest
{
	public DevicePGReset(string deviceName, string serialNumber)
		: base(deviceName, serialNumber, PGRequestType.Reset)
	{
	}
}
