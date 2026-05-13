using System.Collections.Generic;

namespace MQTTMessageLib.Flow;

public class DeviceGetCombinedResultResponse<T> : DeviceFlowResponse
{
	public List<T> Results { get; set; }

	public DeviceGetCombinedResultResponse(int code, string desc, List<T> results)
		: this(code, desc, string.Empty)
	{
		Results = results;
	}

	public DeviceGetCombinedResultResponse(int code, string desc, string status)
		: base(FlowResultType.GetCombinedResult, code, desc, status)
	{
	}

	public DeviceGetCombinedResultResponse(int code, string desc, string status, long totalTime)
		: base(FlowResultType.GetCombinedResult, code, desc, status, totalTime)
	{
	}
}
