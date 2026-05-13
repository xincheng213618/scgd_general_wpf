namespace MQTTMessageLib.Camera;

public class DeviceCameraDeleteData : DeviceCVBaseNoParamRequest<CameraRequestType>, IDevCameraRequest, IDeviceRequest
{
	public DeviceCameraDeleteData(string deviceCode, string serialNumber)
		: base(deviceCode, serialNumber, CameraRequestType.DeleteData)
	{
	}
}
