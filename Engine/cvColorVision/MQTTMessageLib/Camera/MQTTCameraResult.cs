namespace MQTTMessageLib.Camera;

public class MQTTCameraResult : MasterResponseResult
{
	public MQTTCameraResult(long totalTime)
		: base(totalTime)
	{
	}

	public MQTTCameraResult(int masterId, int masterResultType, long totalTime)
		: base(masterId, masterResultType, totalTime)
	{
	}
}
