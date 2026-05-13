namespace MQTTMessageLib.SMU;

public class DeviceSMUMeasureResponse : DeviceSMUResponse
{
	public SMUResultData Data { get; set; }

	public DeviceSMUMeasureResponse(CVBaseDeviceResponse status, SMUResultData data, long totalTime)
		: base(status, totalTime)
	{
		Data = data;
	}

	public static DeviceSMUMeasureResponse Success(SMUResultData data, long totalTime)
	{
		return new DeviceSMUMeasureResponse(CVBaseDeviceResponse.Success(), data, totalTime);
	}
}
