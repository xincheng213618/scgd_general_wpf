namespace MQTTMessageLib.Camera;

public class DeviceCameraClose : DeviceCVBaseNoParamRequest<CameraRequestType>, IDevCameraRequest, IDeviceRequest
{
	public DeviceCameraClose(string deviceCode, string serialNumber)
		: base(deviceCode, serialNumber, CameraRequestType.Close)
	{
	}
}
