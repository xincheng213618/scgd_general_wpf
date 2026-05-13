namespace MQTTMessageLib.Camera;

public class DeviceCameraGetID : DeviceCVBaseNoParamRequest<CameraRequestType>, IDevCameraRequest, IDeviceRequest
{
	public DeviceCameraGetID(string deviceCode, string serialNumber)
		: base(deviceCode, serialNumber, CameraRequestType.GetID)
	{
		base.NeedAuth = false;
	}
}
