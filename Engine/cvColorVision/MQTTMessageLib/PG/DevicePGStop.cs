namespace MQTTMessageLib.PG;

public class DevicePGStop : DeviceCVBaseNoParamRequest<PGRequestType>, IDevPGRequest, IDeviceRequest
{
	public DevicePGStop(string deviceName, string serialNumber)
		: base(deviceName, serialNumber, PGRequestType.Stop)
	{
	}
}
