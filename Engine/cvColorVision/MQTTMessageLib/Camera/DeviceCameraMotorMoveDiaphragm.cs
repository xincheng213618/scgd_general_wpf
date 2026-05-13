namespace MQTTMessageLib.Camera;

public class DeviceCameraMotorMoveDiaphragm : DeviceCVBaseRequest<CameraRequestType, MotorMoveDiaphragmParam>, IDevCameraRequest, IDeviceRequest
{
	public DeviceCameraMotorMoveDiaphragm(string deviceCode, string serialNumber, MotorMoveDiaphragmParam param)
		: base(deviceCode, serialNumber, CameraRequestType.Motor_MoveDiaphragm, param)
	{
	}
}
