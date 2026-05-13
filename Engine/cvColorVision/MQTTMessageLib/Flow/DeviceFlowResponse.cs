namespace MQTTMessageLib.Flow;

public class DeviceFlowResponse : CVBaseDeviceResponse, IDevFlowResponse, IDeviceResponse
{
	public long TotalTime { get; set; }

	public int MasterId { get; set; }

	public FlowResultType ResultType { get; set; }

	public string Status { get; set; }

	public DeviceFlowResponse(FlowResultType resultType, int code, string desc, string status)
		: this(resultType, code, desc, status, 0L)
	{
	}

	public DeviceFlowResponse(FlowResultType resultType, int code, string desc, string status, long totalTime)
		: base(code, desc)
	{
		ResultType = resultType;
		TotalTime = totalTime;
		Status = status;
		MasterId = -1;
	}

	public static DeviceFlowResponse Failed(FlowResultType resultType, string desc, string status, long totalTime)
	{
		return new DeviceFlowResponse(resultType, 400, desc, status, totalTime);
	}

	public static DeviceFlowResponse Failed(FlowResultType resultType, string desc, string status)
	{
		return new DeviceFlowResponse(resultType, 400, desc, status);
	}

	public static DeviceFlowResponse Pending(FlowResultType resultType)
	{
		return new DeviceFlowResponse(resultType, 102, "Pending", string.Empty);
	}

	public static DeviceFlowResponse Success(FlowResultType resultType, string status, long totalTime)
	{
		return new DeviceFlowResponse(resultType, 200, "ok", status, totalTime);
	}
}
