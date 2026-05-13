namespace MQTTMessageLib.SMU;

public class DeviceSMUScanResponse : DeviceSMUResponse
{
	public SMUScanResultData Data { get; set; }

	public DeviceSMUScanResponse(int code, string desc, SMUScanResultData data, long totalTime)
		: base(code, desc, totalTime)
	{
		Data = data;
	}

	public DeviceSMUScanResponse(CVBaseDeviceResponse status, SMUScanResultData data, DeviceParamScan scanRequestParam, long totalTime)
		: this(status.Code, status.Desc, data, totalTime)
	{
	}

	public static DeviceSMUScanResponse Success(SMUScanResultData data, DeviceParamScan scanRequestParam, long totalTime)
	{
		return new DeviceSMUScanResponse(CVBaseDeviceResponse.Success(), data, scanRequestParam, totalTime);
	}
}
