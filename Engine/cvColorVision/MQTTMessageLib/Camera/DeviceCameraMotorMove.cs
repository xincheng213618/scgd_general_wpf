namespace MQTTMessageLib.Camera;

public class DeviceCameraMotorMove : DeviceCVBaseRequest<CameraRequestType, MotorMoveParam>, IDevCameraRequest, IDeviceRequest
{
	public DeviceCameraMotorMove(string deviceCode, string serialNumber, MotorMoveParam param)
		: base(deviceCode, serialNumber, CameraRequestType.Motor_Move, param)
	{
	}
}
