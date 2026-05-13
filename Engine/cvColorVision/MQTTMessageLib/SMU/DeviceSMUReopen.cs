namespace MQTTMessageLib.SMU;

public class DeviceSMUReopen : DeviceCVBaseNoParamRequest<SMURequestType>, IDevSMURequest, IDeviceRequest
{
	public DeviceSMUReopen(string deviceName, string serialNumber)
		: base(deviceName, serialNumber, SMURequestType.Reopen)
	{
	}
}
