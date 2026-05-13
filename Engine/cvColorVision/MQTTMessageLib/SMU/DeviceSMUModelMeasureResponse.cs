namespace MQTTMessageLib.SMU;

public class DeviceSMUModelMeasureResponse : DeviceSMUResponse
{
	public SMUModelResultData Data { get; set; }

	public DeviceSMUModelMeasureResponse(int code, string desc, SMUModelResultData data, long totalTime)
		: base(code, desc, totalTime)
	{
		Data = data;
	}

	public DeviceSMUModelMeasureResponse(CVBaseDeviceResponse status, SMUModelResultData data, long totalTime)
		: base(status, totalTime)
	{
		Data = data;
	}

	public static DeviceSMUModelMeasureResponse Success(SMUModelResultData data, long totalTime)
	{
		return new DeviceSMUModelMeasureResponse(CVBaseDeviceResponse.Success(), data, totalTime);
	}
}
