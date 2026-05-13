namespace MQTTMessageLib.SMU;

public class DeviceSMUGetMeasureResultResponse : DeviceSMUResponse
{
	public SMUMeasureResultData Data { get; set; }

	public DeviceSMUGetMeasureResultResponse(int code, string desc, SMUMeasureResultData data, long totalTime)
		: base(code, desc, totalTime)
	{
		Data = data;
	}

	public DeviceSMUGetMeasureResultResponse(CVBaseDeviceResponse status, SMUMeasureResultData data, long totalTime)
		: this(status.Code, status.Desc, data, totalTime)
	{
	}

	public static DeviceSMUGetMeasureResultResponse Success(SMUMeasureResultData data, long totalTime)
	{
		return new DeviceSMUGetMeasureResultResponse(CVBaseDeviceResponse.Success(), data, totalTime);
	}
}
