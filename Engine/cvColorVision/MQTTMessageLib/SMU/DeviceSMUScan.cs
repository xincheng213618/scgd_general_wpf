namespace MQTTMessageLib.SMU;

public class DeviceSMUScan : DeviceCVBaseRequest<SMURequestType, SMUScanRequestParam>, IDevSMURequest, IDeviceRequest
{
	public DeviceSMUScan(string deviceName, string serialNumber, int zindex, SMUScanRequestParam param)
		: base(deviceName, serialNumber, zindex, SMURequestType.Scan, param)
	{
	}
}
