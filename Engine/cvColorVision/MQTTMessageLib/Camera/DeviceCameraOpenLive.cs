namespace MQTTMessageLib.Camera;

public class DeviceCameraOpenLive : DeviceCVBaseRequest<CameraRequestType, OpenLiveParam>, IDevCameraRequest, IDeviceRequest
{
	public DeviceCameraOpenLive(string deviceCode, string serialNumber, OpenLiveParam param)
		: base(deviceCode, serialNumber, CameraRequestType.OpenLive, param)
	{
	}
}
