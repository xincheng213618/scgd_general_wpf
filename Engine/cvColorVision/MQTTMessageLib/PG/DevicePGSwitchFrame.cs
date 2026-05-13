namespace MQTTMessageLib.PG;

public class DevicePGSwitchFrame : DeviceCVBaseRequest<PGRequestType, PGRequestSwitchFrameParam>, IDevPGRequest, IDeviceRequest
{
	public DevicePGSwitchFrame(string deviceName, string serialNumber, PGRequestSwitchFrameParam param)
		: base(deviceName, serialNumber, PGRequestType.SwitchFrame, param)
	{
	}
}
