namespace MQTTMessageLib.SMU;

public class DeviceSMUOpen : DeviceCVBaseNoParamRequest<SMURequestType>, IDevSMURequest, IDeviceRequest
{
	public DeviceSMUOpen(string deviceName, string serialNumber)
		: base(deviceName, serialNumber, SMURequestType.Open)
	{
	}
}
