namespace MQTTMessageLib.Camera;

public class DeviceCameraOpen : DeviceCVBaseNoParamRequest<CameraRequestType>, IDevCameraRequest, IDeviceRequest
{
	public DeviceCameraOpen(string deviceCode, string serialNumber)
		: base(deviceCode, serialNumber, CameraRequestType.Open)
	{
	}
}
