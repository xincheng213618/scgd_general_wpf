namespace MQTTMessageLib.CVSystem;

public class DeviceLog4zLevel : DeviceCVBaseRequest<CVSystemRequestType, Log4zLevelParam>, IDevCVSystemRequest, IDeviceRequest
{
	public DeviceLog4zLevel(string deviceName, string serialNumber, int zIndex, Log4zLevelParam param)
		: base(deviceName, serialNumber, zIndex, CVSystemRequestType.Log4z_Level, param)
	{
	}
}
