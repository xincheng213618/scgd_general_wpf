using System.Collections.Generic;

namespace MQTTMessageLib.Flow;

public class MQTTFlowGetCombinedResultResponse<T> : MQTTCVBaseResponse<List<T>>
{
	public MQTTFlowGetCombinedResultResponse(MQTTCVRequestHeader request, MQTTCVResponseStatus status, List<T> data)
		: base(request, status, data)
	{
	}

	public MQTTFlowGetCombinedResultResponse(MQTTCVRequestHeader request, DeviceGetCombinedResultResponse<T> response)
		: this(request, new MQTTCVResponseStatus(response), response.Results)
	{
	}
}
