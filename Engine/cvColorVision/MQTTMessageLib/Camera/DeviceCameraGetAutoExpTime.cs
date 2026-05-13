namespace MQTTMessageLib.Camera;

public class DeviceCameraGetAutoExpTime : DeviceCVBaseRequest<CameraRequestType, GetAutoExpTimeParam>, IDevCameraRequest, IDeviceRequest
{
	public DeviceCameraGetAutoExpTime(string deviceCode, string serialNumber, GetAutoExpTimeParam param)
		: base(deviceCode, serialNumber, CameraRequestType.GetAutoExpTime, param)
	{
	}
}
