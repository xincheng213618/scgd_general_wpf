namespace MQTTMessageLib.Camera;

public class MQTTCameraFailedResponse : MQTTCVResponseHeader
{
	public int MasterId { get; set; }

	public long TotalTime { get; set; }

	public MQTTCameraFailedResponse(MQTTCVRequestHeader request, IDevCameraResponse response)
		: base(request, response.Code, response.Desc)
	{
		TotalTime = response.TotalTime;
		MasterId = response.MasterId;
	}

	public MQTTCameraFailedResponse(MQTTCVRequestHeader request, IDeviceResponse response)
		: base(request, response.Code, response.Desc)
	{
		TotalTime = -1L;
		MasterId = -1;
	}
}
