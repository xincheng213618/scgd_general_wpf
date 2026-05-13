namespace MQTTMessageLib.SMU;

public class DeviceSMUMeasure : DeviceCVBaseRequest<SMURequestType, SMUMeasureRequestParam>, IDevSMURequest, IDeviceRequest
{
	public DeviceSMUMeasure(string deviceName, string serialNumber, int zindex, SMUMeasureRequestParam param)
		: base(deviceName, serialNumber, zindex, SMURequestType.Measure, param)
	{
	}
}
