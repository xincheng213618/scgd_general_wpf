namespace MQTTMessageLib.SMU;

public class DeviceSMUResponse : CVBaseDeviceResponseWithResult, IDevSMUResponse, IDeviceResponseWithResult, IDeviceResponse
{
	public DeviceSMUResponse(int code, string desc, long totalTime)
		: base(200, code, desc, totalTime)
	{
	}

	public DeviceSMUResponse(CVBaseDeviceResponse status, long totalTime)
		: this(status.Code, status.Desc, totalTime)
	{
	}

	public static DeviceSMUResponse Failed(string msg, long totalTime)
	{
		return new DeviceSMUResponse(new CVBaseDeviceResponse(-1, msg), totalTime);
	}

	public new static DeviceSMUResponse Failed()
	{
		return new DeviceSMUResponse(CVBaseDeviceResponse.Failed(), -1L);
	}

	public new static DeviceSMUResponse Failed(string msg)
	{
		return new DeviceSMUResponse(new CVBaseDeviceResponse(-1, msg), -1L);
	}

	public static DeviceSMUResponse Failed(long totalTime)
	{
		return new DeviceSMUResponse(CVBaseDeviceResponse.Failed(), totalTime);
	}
}
