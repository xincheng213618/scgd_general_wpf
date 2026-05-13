namespace MQTTMessageLib.Camera;

public class DeviceCameraReopen : DeviceCVBaseNoParamRequest<CameraRequestType>, IDevCameraRequest, IDeviceRequest
{
	public DeviceCameraReopen(string deviceCode, string serialNumber)
		: base(deviceCode, serialNumber, CameraRequestType.Reopen)
	{
	}
}
