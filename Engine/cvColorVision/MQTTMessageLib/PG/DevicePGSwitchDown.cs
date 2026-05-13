namespace MQTTMessageLib.PG;

public class DevicePGSwitchDown : DeviceCVBaseNoParamRequest<PGRequestType>, IDevPGRequest, IDeviceRequest
{
	public DevicePGSwitchDown(string deviceName, string serialNumber)
		: base(deviceName, serialNumber, PGRequestType.SwitchDown)
	{
	}
}
