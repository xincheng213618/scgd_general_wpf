namespace MQTTMessageLib.SMU;

public class DeviceSMUModelMeasure : DeviceCVBaseRequest<SMURequestType, SMUModelMeasureRequestParam>, IDevSMURequest, IDeviceRequest
{
	public DeviceSMUModelMeasure(string deviceName, string serialNumber, int zindex, SMUModelMeasureRequestParam param)
		: base(deviceName, serialNumber, zindex, SMURequestType.ModelMeasure, param)
	{
	}
}
