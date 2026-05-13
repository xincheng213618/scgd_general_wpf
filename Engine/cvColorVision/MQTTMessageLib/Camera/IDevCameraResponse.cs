namespace MQTTMessageLib.Camera;

public interface IDevCameraResponse : IDeviceResponse
{
	CameraResultType ResultType { get; }

	long TotalTime { get; set; }

	int MasterId { get; set; }
}
