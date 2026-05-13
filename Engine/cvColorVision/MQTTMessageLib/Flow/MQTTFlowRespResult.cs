namespace MQTTMessageLib.Flow;

public class MQTTFlowRespResult
{
	public long TotalTime { get; set; }

	public MQTTFlowRespResult(IDevFlowResponse response)
	{
		TotalTime = response.TotalTime;
	}
}
