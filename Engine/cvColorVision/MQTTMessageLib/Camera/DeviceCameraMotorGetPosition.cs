namespace MQTTMessageLib.Camera;

public class DeviceCameraMotorGetPosition : DeviceCVBaseNoParamRequest<CameraRequestType>, IDevCameraRequest, IDeviceRequest
{
	public DeviceCameraMotorGetPosition(string deviceCode, string serialNumber)
		: base(deviceCode, serialNumber, CameraRequestType.Motor_GetPosition)
	{
	}
}
