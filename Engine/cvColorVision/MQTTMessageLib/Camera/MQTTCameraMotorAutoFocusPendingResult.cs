namespace MQTTMessageLib.Camera;

public class MQTTCameraMotorAutoFocusPendingResult
{
	public int Position { get; set; }

	public string ImageTmpFile { get; set; }

	public int ResultZIndex { get; set; }

	public MQTTCameraMotorAutoFocusPendingResult(DeviceCameraMotorAutoFocusResponse response)
	{
		Position = response.Result.nPosition;
		ImageTmpFile = response.Result.ImageTmpFile;
		ResultZIndex = response.Result.ResultZIndex;
	}
}
