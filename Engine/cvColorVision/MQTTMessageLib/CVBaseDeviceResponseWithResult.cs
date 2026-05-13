namespace MQTTMessageLib;

public class CVBaseDeviceResponseWithResult : CVBaseDeviceResponse, IDeviceResponseWithResult, IDeviceResponse
{
	public long TotalTime { get; set; }

	public int MasterId { get; set; }

	public int DeviceResultType { get; set; }

	public CVBaseDeviceResponseWithResult(int deviceResultType, CVBaseDeviceResponse status, long totalTime, int masterId = -1)
		: base(status)
	{
		TotalTime = totalTime;
		MasterId = masterId;
		DeviceResultType = deviceResultType;
	}

	public CVBaseDeviceResponseWithResult(int deviceResultType, CVBaseDeviceResponseWithResult status)
		: base(status)
	{
		TotalTime = status.TotalTime;
		MasterId = status.MasterId;
		DeviceResultType = deviceResultType;
	}

	public CVBaseDeviceResponseWithResult(int deviceResultType, int code, string desc, long totalTime, int masterId = -1)
		: base(code, desc)
	{
		TotalTime = totalTime;
		MasterId = masterId;
		DeviceResultType = deviceResultType;
	}
}
