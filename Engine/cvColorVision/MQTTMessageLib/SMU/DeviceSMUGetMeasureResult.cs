namespace MQTTMessageLib.SMU;

public class DeviceSMUGetMeasureResult : DeviceCVBaseRequest<SMURequestType, SMUGetMeasureResultRequestParam>, IDevSMURequest, IDeviceRequest
{
	public DeviceSMUGetMeasureResult(string deviceName, string serialNumber, int zindex, SMUGetMeasureResultRequestParam param)
		: base(deviceName, serialNumber, zindex, SMURequestType.GetMeasureResult, param)
	{
	}
}
