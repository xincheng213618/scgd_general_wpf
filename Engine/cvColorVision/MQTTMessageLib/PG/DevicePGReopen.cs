namespace MQTTMessageLib.PG;

public class DevicePGReopen : DeviceCVBaseNoParamRequest<PGRequestType>, IDevPGRequest, IDeviceRequest
{
	public DevicePGReopen(string deviceName, string serialNumber)
		: base(deviceName, serialNumber, PGRequestType.Reopen)
	{
	}
}
