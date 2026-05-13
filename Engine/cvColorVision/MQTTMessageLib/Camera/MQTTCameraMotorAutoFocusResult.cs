namespace MQTTMessageLib.Camera;

public class MQTTCameraMotorAutoFocusResult : MQTTCameraResult
{
	public int Position { get; set; }

	public double VidPosition { get; set; }

	public MQTTCameraMotorAutoFocusResult(DeviceCameraMotorAutoFocusResponse response)
		: this(response.MasterId, response.TotalTime)
	{
		Position = response.Result.nPosition;
		VidPosition = response.Result.VidPos;
	}

	public MQTTCameraMotorAutoFocusResult(int masterId, long totalTime)
		: base(masterId, 108, totalTime)
	{
	}
}
