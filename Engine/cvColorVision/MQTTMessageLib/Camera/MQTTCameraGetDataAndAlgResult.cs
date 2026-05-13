namespace MQTTMessageLib.Camera;

public class MQTTCameraGetDataAndAlgResult : MQTTCameraResult
{
	public MQTTCameraGetDataAndAlgResult(int masterId, int resultType, long totalTime)
		: base(masterId, resultType, totalTime)
	{
	}
}
