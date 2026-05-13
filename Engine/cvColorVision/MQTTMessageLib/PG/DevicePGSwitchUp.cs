namespace MQTTMessageLib.PG;

public class DevicePGSwitchUp : DeviceCVBaseNoParamRequest<PGRequestType>, IDevPGRequest, IDeviceRequest
{
	public DevicePGSwitchUp(string deviceName, string serialNumber)
		: base(deviceName, serialNumber, PGRequestType.SwitchUp)
	{
	}
}
