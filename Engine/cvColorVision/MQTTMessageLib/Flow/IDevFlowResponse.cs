namespace MQTTMessageLib.Flow;

public interface IDevFlowResponse : IDeviceResponse
{
	FlowResultType ResultType { get; }

	long TotalTime { get; }

	int MasterId { get; set; }

	string Status { get; set; }
}
