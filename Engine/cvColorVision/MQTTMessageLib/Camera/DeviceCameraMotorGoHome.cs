namespace MQTTMessageLib.Camera;

public class DeviceCameraMotorGoHome : DeviceCVBaseNoParamRequest<CameraRequestType>, IDevCameraRequest, IDeviceRequest
{
	public DeviceCameraMotorGoHome(string deviceCode, string serialNumber)
		: base(deviceCode, serialNumber, CameraRequestType.Motor_GoHome)
	{
	}
}
