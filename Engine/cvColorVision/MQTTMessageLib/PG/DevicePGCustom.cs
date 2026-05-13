namespace MQTTMessageLib.PG;

public class DevicePGCustom : DeviceCVBaseRequest<PGRequestType, PGRequestCustomParam>, IDevPGRequest, IDeviceRequest
{
	public DevicePGCustom(string deviceName, string serialNumber, PGRequestCustomParam param)
		: base(deviceName, serialNumber, PGRequestType.Custom, param)
	{
	}
}
