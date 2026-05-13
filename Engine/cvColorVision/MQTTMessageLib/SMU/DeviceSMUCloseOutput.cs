namespace MQTTMessageLib.SMU;

public class DeviceSMUCloseOutput : DeviceCVBaseRequest<SMURequestType, SMUCloseOutputRequestParam>, IDevSMURequest, IDeviceRequest
{
	public DeviceSMUCloseOutput(string deviceName, string serialNumber, int zindex, SMUCloseOutputRequestParam param)
		: base(deviceName, serialNumber, zindex, SMURequestType.CloseOutput, param)
	{
	}
}
