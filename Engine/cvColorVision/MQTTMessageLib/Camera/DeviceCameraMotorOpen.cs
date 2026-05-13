namespace MQTTMessageLib.Camera;

public class DeviceCameraMotorOpen : DeviceCVBaseNoParamRequest<CameraRequestType>, IDevCameraRequest, IDeviceRequest
{
	public DeviceCameraMotorOpen(string deviceCode, string serialNumber)
		: base(deviceCode, serialNumber, CameraRequestType.Motor_Open)
	{
	}
}
