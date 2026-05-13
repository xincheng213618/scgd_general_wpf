namespace MQTTMessageLib.Camera;

public class DeviceCameraGetAllID : DeviceCVBaseNoParamRequest<CameraRequestType>, IDevCameraRequest, IDeviceRequest
{
	public DeviceCameraGetAllID(string deviceCode, string serialNumber)
		: base(deviceCode, serialNumber, CameraRequestType.GetAllID)
	{
		base.NeedAuth = false;
	}
}
