namespace MQTTMessageLib.PG;

public class DevicePGClose : DeviceCVBaseNoParamRequest<PGRequestType>, IDevPGRequest, IDeviceRequest
{
	public DevicePGClose(string deviceName, string serialNumber)
		: base(deviceName, serialNumber, PGRequestType.Close)
	{
	}
}
