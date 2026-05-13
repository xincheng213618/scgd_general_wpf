namespace MQTTMessageLib.SMU;

public class DeviceSMUClose : DeviceCVBaseNoParamRequest<SMURequestType>, IDevSMURequest, IDeviceRequest
{
	public DeviceSMUClose(string deviceName, string serialNumber)
		: base(deviceName, serialNumber, SMURequestType.Close)
	{
	}
}
